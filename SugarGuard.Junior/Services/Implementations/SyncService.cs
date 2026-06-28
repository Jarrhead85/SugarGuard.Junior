// Реализация сервиса синхронизации
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SugarGuard.Junior.Database;
using SugarGuard.Junior.Models.Api;
using SugarGuard.Junior.Models.Core;
using SugarGuard.Junior.Repositories.Interfaces;
using SugarGuard.Junior.Services.Interfaces;
using SugarGuard.Shared.Constants;

namespace SugarGuard.Junior.Services.Implementations;

/// <summary>
/// Сервис синхронизации
/// 
/// Как это работает:
/// 1. Мониторит соединение с интернетом
/// 2. Когда интернет появляется → начинает синхронизировать
/// 3. Для каждой операции пытается отправить на сервер
/// 4. Если ошибка → добавляет в очередь с exponential backoff
/// 5. Максимум 10 попыток, потом сдаётся
/// </summary>
public class SyncService : ISyncService
{
    private readonly ILogger<SyncService> _logger;
    private readonly IApiClient _apiClient;
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IMeasurementRepository _measurementRepository;
    private readonly IBackpackRepository _backpackRepository;
    private readonly ISyncConflictResolver _conflictResolver;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _isInitialized;
    private bool _lastConnectivityStatus;

    /// <summary>
    /// Единый флаг синхронизации (int для Interlocked.CompareExchange / Volatile.Read).
    /// 0 = idle, 1 = syncing.
    /// </summary>
    private int _isSyncingFlag;

    // Параметры retry
    private const int MaxRetries = 10;
    private const int InitialDelaySeconds = 10;

    // События
    public event EventHandler<SyncStartedEventArgs>? SyncStarted;
    public event EventHandler<SyncCompletedEventArgs>? SyncCompleted;
    public event EventHandler<Interfaces.ConnectivityChangedEventArgs>? ConnectivityChanged;

    public SyncService(
        ILogger<SyncService> logger,
        IApiClient apiClient,
        IDbContextFactory<AppDbContext> factory,
        IMeasurementRepository measurementRepository,
        IBackpackRepository backpackRepository,
        ISyncConflictResolver conflictResolver)
    {
        _logger = logger;
        _apiClient = apiClient;
        _factory = factory;
        _measurementRepository = measurementRepository;
        _backpackRepository = backpackRepository;
        _conflictResolver = conflictResolver;
        _lastConnectivityStatus = false;
    }

    /// <summary>
    /// Инициализирует сервис синхронизации
    /// Запускает фоновую задачу для проверки соединения
    /// </summary>
    public Task InitializeAsync()
    {
        if (_isInitialized)
        {
            _logger.LogDebug("SyncService already initialized, skipping");
            return Task.CompletedTask;
        }

        try
        {
            _logger.LogInformation(" Инициализация SyncService...");

            _cancellationTokenSource = new CancellationTokenSource();

            // Запускаем фоновую задачу для мониторинга соединения (fire-and-forget)
            _ = MonitorConnectivityAsync(_cancellationTokenSource.Token);

            _isInitialized = true;
            _logger.LogInformation(" SyncService инициализирован");
            
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, " Ошибка при инициализации SyncService: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Проверяет наличие интернета (только локальный Connectivity API,
    /// без HTTP health-check — не убиваем батарею Android)
    /// </summary>
    public Task<bool> IsConnectedAsync()
    {
        var isConnected = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
        return Task.FromResult(isConnected);
    }

    /// <summary>
    /// Запускает синхронизацию вручную
    /// </summary>
    public async Task<bool> SyncNowAsync()
    {
        try
        {
            if (!_isInitialized)
            {
                _logger.LogWarning("SyncService не инициализирован");
                return false;
            }

            _logger.LogInformation("Запуск синхронизации...");

            if (Interlocked.CompareExchange(ref _isSyncingFlag, 1, 0) != 0)
            {
                _logger.LogInformation("Синхронизация уже выполняется, пропускаем повторный запуск.");
                return true;
            }

            var status = await GetStatusAsync();

            if (status.PendingItemsCount == 0)
            {
                _logger.LogInformation("Нет записей для синхронизации");
                Interlocked.Exchange(ref _isSyncingFlag, 0);
                return true;
            }

            if (!status.IsConnected)
            {
                _logger.LogWarning("Нет соединения с интернетом");
                Interlocked.Exchange(ref _isSyncingFlag, 0);
                return false;
            }

            // Поднимаем событие начала синхронизации
            SyncStarted?.Invoke(this, new SyncStartedEventArgs
            {
                ItemsCount = status.PendingItemsCount,
                StartedAt = DateTime.UtcNow
            });

            // Получаем очередь синхронизации
            await using var ctx = await _factory.CreateDbContextAsync();
            var syncQueue = await ctx.Set<SyncQueueItem>()
                .Where(q => !q.IsSynced && q.RetryCount < MaxRetries)
                .OrderBy(q => q.CreatedAt)
                .ToListAsync();

            int successCount = 0;
            int errorCount = 0;
            var syncedItems = new List<SyncQueueItem>(syncQueue.Count);

            foreach (var item in syncQueue)
            {
                try
                {
                    var result = await SyncItemAsync(item);
                    if (result)
                    {
                        successCount++;
                        syncedItems.Add(item);
                        _logger.LogInformation(" Синхронизирована запись: {EntityId}", item.EntityId);
                    }
                    else
                    {
                        errorCount++;
                        item.RetryCount++;
                        item.LastRetryAt = DateTime.UtcNow;
                        ctx.Set<SyncQueueItem>().Update(item);
                        _logger.LogWarning(" Ошибка синхронизации: {EntityId} (попытка {RetryCount})", item.EntityId, item.RetryCount);
                    }
                }
                catch (Exception ex)
                {
                    errorCount++;
                    item.RetryCount++;
                    item.LastRetryAt = DateTime.UtcNow;
                    ctx.Set<SyncQueueItem>().Update(item);
                    _logger.LogError(ex, " Исключение при синхронизации {EntityId}", item.EntityId);
                }
            }

            var isSuccessful = errorCount == 0;

            // Удаляем успешно синхронизированные записи (одна транзакция, без лишнего SQL)
            if (syncedItems.Count > 0)
            {
                ctx.Set<SyncQueueItem>().RemoveRange(syncedItems);
            }
            await ctx.SaveChangesAsync();

            if (successCount > 0)
            {
                Preferences.Set("last_successful_sync_utc", DateTime.UtcNow.ToString("O"));
            }

            // Поднимаем событие завершения
            SyncCompleted?.Invoke(this, new SyncCompletedEventArgs
            {
                SuccessCount = successCount,
                ErrorCount = errorCount,
                CompletedAt = DateTime.UtcNow,
                IsSuccessful = isSuccessful
            });

            _logger.LogInformation(" Синхронизация завершена: {SuccessCount} успешно, {ErrorCount} ошибок", successCount, errorCount);
            Interlocked.Exchange(ref _isSyncingFlag, 0);
            return isSuccessful;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, " Ошибка при синхронизации: {Message}", ex.Message);
            Interlocked.Exchange(ref _isSyncingFlag, 0);
            return false;
        }
    }

    /// <summary>
    /// Синхронизирует одну запись с обработкой конфликтов
    /// </summary>
    private async Task<bool> SyncItemAsync(SyncQueueItem item)
    {
        try
        {
            switch (item.EntityType)
            {
                case "Measurement":
                    return await SyncMeasurementAsync(item);

                case "BackpackItem":
                    return await SyncBackpackItemAsync(item);
                case "SnackConsumption":
                    return await SyncSnackConsumptionAsync(item);

                case "MeasurementSchedule":
                    return await SyncScheduleItemAsync(item);

                default:
                    _logger.LogWarning("Неизвестный тип сущности: {EntityType}", item.EntityType);
                    return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при синхронизации элемента {EntityId}", item.EntityId);
            return false;
        }
    }

    /// <summary>
    /// Синхронизирует измерение с обработкой конфликтов (M-2)
    /// </summary>
    private async Task<bool> SyncMeasurementAsync(SyncQueueItem item)
    {
        try
        {
            var measurementRequest = JsonConvert.DeserializeObject<SendMeasurementRequest>(item.Payload);
            if (measurementRequest == null)
                return false;

            // Прикрепляем время последнего изменения для обнаружения конфликтов
            measurementRequest.LastModifiedAt = item.LastModifiedAt;

            // Пытаемся получить серверную версию (если это обновление существующей записи)
            SyncConflictInfo? serverConflict = null;
            try
            {
                var serverMeasurement = await _apiClient.GetMeasurementByIdAsync(item.EntityId);
                if (serverMeasurement != null && serverMeasurement.ServerModifiedAt.HasValue)
                {
                    // Серверная версия новее локальной → конфликт
                    if (serverMeasurement.ServerModifiedAt > item.LastModifiedAt)
                    {
                        serverConflict = new SyncConflictInfo
                        {
                            EntityId = item.EntityId,
                            EntityType = item.EntityType,
                            ServerModifiedAt = serverMeasurement.ServerModifiedAt.Value,
                            LocalModifiedAt = item.LastModifiedAt ?? DateTime.MinValue,
                            ServerVersion = serverMeasurement.ServerVersion ?? "{}",
                            ResolutionStrategy = SyncResolutionStrategy.FirstWriteWins
                        };
                    }
                }
            }
            catch (Exception fetchEx)
            {
                // Сервер недоступен для проверки — пробуем отправить, сервер сам решит
                _logger.LogDebug(fetchEx, "Не удалось проверить серверную версию для {EntityId}, отправляем напрямую", item.EntityId);
            }

            if (serverConflict != null)
            {
                _logger.LogInformation(" Обнаружен конфликт для {EntityType} {EntityId}: сервер новее (сервер: {ServerModifiedAt:O}, локально: {LocalModifiedAt:O})",
                    item.EntityType, item.EntityId, serverConflict.ServerModifiedAt, serverConflict.LocalModifiedAt);

                var resolution = await _conflictResolver.ResolveConflictAsync(serverConflict, item.Payload);

                if (resolution.WinningVersion == "Local")
                {
                    // Наша версия победила — отправляем на сервер
                    _logger.LogInformation("Локальная версия победила, отправляем на сервер");
                    var response = await _apiClient.SendMeasurementAsync(measurementRequest);
                    return response.Success;
                }
                else
                {
                    // Серверная версия победила — обновляем локально
                    _logger.LogInformation("Серверная версия победила, обновляем локальные данные");
                    await UpdateLocalEntityAsync(item.EntityId, item.EntityType, resolution.ResolvedData);
                    return true;
                }
            }

            // Нет конфликта — отправляем как есть
            var sendResponse = await _apiClient.SendMeasurementAsync(measurementRequest);

            // Проверяем конфликт в ответе сервера (если сервер сам его обнаружил)
            if (sendResponse.Success && sendResponse.HasConflict && sendResponse.ServerVersion != null)
            {
                var responseConflict = new SyncConflictInfo
                {
                    EntityId = item.EntityId,
                    EntityType = item.EntityType,
                    ServerModifiedAt = sendResponse.ServerModifiedAt ?? DateTime.UtcNow,
                    LocalModifiedAt = item.LastModifiedAt ?? DateTime.UtcNow,
                    ServerVersion = sendResponse.ServerVersion,
                    ResolutionStrategy = SyncResolutionStrategy.FirstWriteWins
                };

                var responseResolution = await _conflictResolver.ResolveConflictAsync(responseConflict, item.Payload);
                if (responseResolution.WinningVersion == "Server")
                {
                    await UpdateLocalEntityAsync(item.EntityId, item.EntityType, responseResolution.ResolvedData);
                }
            }

            return sendResponse.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при синхронизации измерения {EntityId}", item.EntityId);
            return false;
        }
    }

    /// <summary>
    /// Синхронизирует элемент рюкзака (M-2: с LastModifiedAt для будущих конфликтов)
    /// </summary>
    private async Task<bool> SyncBackpackItemAsync(SyncQueueItem item)
    {
        try
        {
            return item.OperationType switch
            {
                SyncOperationType.Delete => await SyncBackpackDeleteAsync(item),
                _ => await SyncBackpackAddOrUpdateAsync(item)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при синхронизации элемента рюкзака {EntityId}", item.EntityId);
            return false;
        }
    }

    private async Task<bool> SyncBackpackAddOrUpdateAsync(SyncQueueItem item)
    {
        var snackRequest = JsonConvert.DeserializeObject<AddSnackRequest>(item.Payload);
        if (snackRequest == null)
            return false;

        snackRequest.LastModifiedAt = item.LastModifiedAt;
        if (string.IsNullOrWhiteSpace(snackRequest.BackpackItemId))
            snackRequest.BackpackItemId = item.EntityId;

        var synced = await _apiClient.AddSnackAsync(snackRequest);
        if (synced)
            await _backpackRepository.MarkAsSyncedAsync(item.EntityId);

        return synced;
    }

    private async Task<bool> SyncBackpackDeleteAsync(SyncQueueItem item)
    {
        var removeRequest = JsonConvert.DeserializeObject<RemoveSnackRequest>(item.Payload);
        if (removeRequest == null)
            return false;

        return await _apiClient.RemoveSnackAsync(removeRequest);
    }

    private async Task<bool> SyncSnackConsumptionAsync(SyncQueueItem item)
    {
        var request = JsonConvert.DeserializeObject<ConsumeBackpackSnackRequest>(item.Payload);
        if (request is null || !await _apiClient.ConsumeSnackAsync(request))
            return false;

        await _backpackRepository.RemoveOrphanedUnsyncedDuplicateAsync(
            request.ChildId,
            request.SnackName,
            request.BreadUnits);
        return true;
    }

    /// <summary>
    /// Синхронизирует запись расписания измерений
    /// </summary>
    private Task<bool> SyncScheduleItemAsync(SyncQueueItem item)
    {
        // API-методы для расписания ещё не реализованы — оставляем в очереди
        _logger.LogWarning("API для синхронизации расписания не реализован: {EntityId}", item.EntityId);
        return Task.FromResult(false);
    }

    /// <summary>
    /// Обновляет локальную сущность после разрешения конфликта
    /// Requirements: 14.1, 14.2, 14.3
    /// </summary>
    private async Task UpdateLocalEntityAsync(string entityId, string entityType, string resolvedData)
    {
        try
        {
            _logger.LogInformation(
                " Обновление локальной сущности {EntityType} {EntityId} после разрешения конфликта",
                entityType, entityId);

            await using var ctx = await _factory.CreateDbContextAsync();
            switch (entityType)
            {
                case "Measurement":
                    // Обновляем локальное измерение
                    var measurement = JsonConvert.DeserializeObject<MeasurementEntity>(resolvedData);
                    if (measurement != null)
                    {
                        var existingMeasurement = await ctx.Set<MeasurementEntity>().FirstOrDefaultAsync(m => m.MeasurementId == entityId);
                        if (existingMeasurement != null)
                        {
                            // Обновляем зашифрованные поля из разрешённой версии
                            existingMeasurement.EncryptedGlucoseValue = measurement.EncryptedGlucoseValue;
                            existingMeasurement.MeasurementTime = measurement.MeasurementTime;
                            existingMeasurement.EncryptedChildState = measurement.EncryptedChildState;
                            existingMeasurement.EncryptedNotes = measurement.EncryptedNotes;
                            existingMeasurement.IsSynced = true;
                            
                            ctx.Set<MeasurementEntity>().Update(existingMeasurement);
                            await ctx.SaveChangesAsync();
                            
                            _logger.LogInformation(
                                "Локальное измерение {EntityId} обновлено после разрешения конфликта (MeasurementTime: {Time})",
                                entityId, measurement.MeasurementTime);
                        }
                        else
                        {
                            _logger.LogWarning("Измерение {EntityId} не найдено в локальной БД", entityId);
                        }
                    }
                    break;

                case "BackpackItem":
                    // Обновляем локальный элемент рюкзака
                    var backpackItem = JsonConvert.DeserializeObject<BackpackItem>(resolvedData);
                    if (backpackItem != null)
                    {
                        var existingItem = await ctx.Set<BackpackItem>().FirstOrDefaultAsync(b => b.BackpackItemId == entityId);
                        if (existingItem != null)
                        {
                            existingItem.EncryptedSnackName = backpackItem.EncryptedSnackName;
                            existingItem.EncryptedBreadUnits = backpackItem.EncryptedBreadUnits;
                            existingItem.IsSynced = true;
                            
                            ctx.Set<BackpackItem>().Update(existingItem);
                            await ctx.SaveChangesAsync();
                            
                            _logger.LogInformation(
                                "Локальный элемент рюкзака {EntityId} обновлён после разрешения конфликта",
                                entityId);
                        }
                        else
                        {
                            _logger.LogWarning("Элемент рюкзака {EntityId} не найден в локальной БД", entityId);
                        }
                    }
                    break;

                case "DiabetesSettings":
                    // Обновляем настройки диабета
                    var settings = JsonConvert.DeserializeObject<DiabetesSettings>(resolvedData);
                    if (settings != null)
                    {
                        var existingSettings = await ctx.Set<DiabetesSettings>().FirstOrDefaultAsync(d => d.ChildId == entityId);
                        if (existingSettings != null)
                        {
                            existingSettings.EncryptedTargetRangeMin = settings.EncryptedTargetRangeMin;
                            existingSettings.EncryptedTargetRangeMax = settings.EncryptedTargetRangeMax;
                            existingSettings.EncryptedInsulinSensitivity = settings.EncryptedInsulinSensitivity;
                            existingSettings.EncryptedCarbInsulinRatio = settings.EncryptedCarbInsulinRatio;
                            existingSettings.UpdatedAt = DateTime.UtcNow;
                            
                            ctx.Set<DiabetesSettings>().Update(existingSettings);
                            await ctx.SaveChangesAsync();
                            
                            _logger.LogInformation(
                                "Настройки диабета для ребёнка {EntityId} обновлены после разрешения конфликта",
                                entityId);
                        }
                        else
                        {
                            _logger.LogWarning("Настройки диабета для ребёнка {EntityId} не найдены в локальной БД", entityId);
                        }
                    }
                    break;

                default:
                    _logger.LogWarning("Неизвестный тип сущности для обновления: {EntityType}", entityType);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обновлении локальной сущности {EntityId}", entityId);
        }
    }

    /// <summary>
    /// Добавляет запись в очередь синхронизации
    /// </summary>
    public async Task<bool> QueueItemAsync(string entityId, string entityType, string operationType, string payload)
    {
        try
        {
            var queueItem = new SyncQueueItem
            {
                QueueId = Guid.NewGuid().ToString(),
                EntityId = entityId,
                EntityType = entityType,
                OperationType = Enum.Parse<SyncOperationType>(operationType),
                Payload = payload,
                IsSynced = false,
                RetryCount = 0,
                CreatedAt = DateTime.UtcNow,
                LastModifiedAt = DateTime.UtcNow // Устанавливаем время изменения для обнаружения конфликтов
            };

            await using var ctx = await _factory.CreateDbContextAsync();
            ctx.Set<SyncQueueItem>().Add(queueItem);
            await ctx.SaveChangesAsync();

            _logger.LogInformation(" Запись добавлена в очередь: {EntityId}", entityId);
            ScheduleImmediateSyncIfPossible();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, " Ошибка при добавлении в очередь: {EntityId}", entityId);
            return false;
        }
    }

    private void ScheduleImmediateSyncIfPossible()
    {
        if (!_isInitialized || Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                await SyncNowAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Не удалось выполнить немедленную синхронизацию очереди.");
            }
        });
    }

    /// <summary>
    /// Получает статус синхронизации
    /// </summary>
    public async Task<SyncStatus> GetStatusAsync()
    {
        try
        {
            var isConnected = await IsConnectedAsync();
            await using var ctx = await _factory.CreateDbContextAsync();
            var pendingCount = await ctx.Set<SyncQueueItem>()
                .CountAsync(q => !q.IsSynced && q.RetryCount < MaxRetries);

            DateTime? lastSyncUtc = null;
            var saved = Preferences.Get("last_successful_sync_utc", string.Empty);
            if (!string.IsNullOrEmpty(saved) && DateTime.TryParse(saved, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed))
            {
                lastSyncUtc = parsed;
            }

            return new SyncStatus
            {
                IsConnected = isConnected,
                PendingItemsCount = pendingCount,
                LastSuccessfulSync = lastSyncUtc,
                IsSyncing = Volatile.Read(ref _isSyncingFlag) == 1,
                ProgressPercent = 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, " Ошибка при получении статуса: {Message}", ex.Message);
            return new SyncStatus { IsConnected = false };
        }
    }

    /// <summary>
    /// Мониторинг соединения через событийную модель Connectivity API.
    /// Заменяет старый timer-based polling (C-3).
    /// </summary>
    private Task MonitorConnectivityAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(" Мониторинг соединения запущен (событийная модель)");

        // Первоначальная проверка
        _lastConnectivityStatus = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

        // Подписываемся на события Connectivity API
        Connectivity.Current.ConnectivityChanged += OnPlatformConnectivityChanged;

        // Регистрируем отписку при отмене
        cancellationToken.Register(() =>
        {
            Connectivity.Current.ConnectivityChanged -= OnPlatformConnectivityChanged;
            _logger.LogInformation("Мониторинг соединения остановлен");
        });

        return Task.CompletedTask;
    }

    /// <summary>
    /// Обработчик изменения сетевого подключения от MAUI Connectivity API.
    /// </summary>
    private async void OnPlatformConnectivityChanged(object? sender, Microsoft.Maui.Networking.ConnectivityChangedEventArgs e)
    {
        try
        {
            var isConnected = e.NetworkAccess == NetworkAccess.Internet;

            if (isConnected == _lastConnectivityStatus)
                return;

            _lastConnectivityStatus = isConnected;

            _logger.LogInformation(
                isConnected
                    ? " Соединение восстановлено"
                    : " Соединение потеряно");

            ConnectivityChanged?.Invoke(this, new Interfaces.ConnectivityChangedEventArgs
            {
                IsConnected = isConnected,
                ChangedAt = DateTime.UtcNow
            });

            if (isConnected)
            {
                _logger.LogInformation(" Автоматическая синхронизация...");
                await SyncNowAsync();
                await _conflictResolver.CleanupOldConflictHistoryAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка в обработчике ConnectivityChanged");
        }
    }

    /// <summary>
    /// Демонстрационный метод для тестирования разрешения конфликтов
    /// Создаёт искусственный конфликт и показывает как он разрешается
    /// </summary>
    public async Task<bool> TestConflictResolutionAsync()
    {
        try
        {
            _logger.LogInformation("Тестирование разрешения конфликтов...");

            // Создаём искусственный конфликт
            var conflictInfo = new Models.Api.SyncConflictInfo
            {
                EntityId = "test_measurement_001",
                EntityType = "Measurement",
                ServerModifiedAt = DateTime.UtcNow.AddMinutes(-5), // Серверная версия старше
                LocalModifiedAt = DateTime.UtcNow, // Локальная версия новее
                ServerVersion = JsonConvert.SerializeObject(new { 
                    GlucoseValue = 7.5, 
                    MeasurementTime = DateTime.UtcNow.AddMinutes(-5),
                    Notes = "Серверная версия"
                }),
                ResolutionStrategy = "LastWriteWins"
            };

            var localData = JsonConvert.SerializeObject(new { 
                GlucoseValue = 8.2, 
                MeasurementTime = DateTime.UtcNow,
                Notes = "Локальная версия (новее)"
            });

            // Разрешаем конфликт
            var result = await _conflictResolver.ResolveConflictAsync(conflictInfo, localData);

            _logger.LogInformation("Тест конфликта завершён: {WinningVersion} версия победила", 
                result.WinningVersion);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при тестировании конфликтов");
            return false;
        }
    }

    /// <summary>
    /// Получает статистику конфликтов
    /// </summary>
    public async Task<ConflictStatistics> GetConflictStatisticsAsync()
    {
        try
        {
            await using var ctx = await _factory.CreateDbContextAsync();
            var totalConflicts = await ctx.Set<SyncConflictHistory>().CountAsync();
            var recentConflicts = await ctx.Set<SyncConflictHistory>()
                .Where(c => c.ResolvedAt >= DateTime.UtcNow.AddDays(-7))
                .CountAsync();
            
            var serverWins = await ctx.Set<SyncConflictHistory>()
                .Where(c => c.WinningVersion == "Server")
                .CountAsync();
            
            var localWins = await ctx.Set<SyncConflictHistory>()
                .Where(c => c.WinningVersion == "Local")
                .CountAsync();

            var lastConflict = await ctx.Set<SyncConflictHistory>()
                .OrderByDescending(c => c.ResolvedAt)
                .FirstOrDefaultAsync();

            return new ConflictStatistics
            {
                TotalConflicts = totalConflicts,
                RecentConflicts = recentConflicts,
                ServerWins = serverWins,
                LocalWins = localWins,
                LastConflictAt = lastConflict?.ResolvedAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, " Ошибка при получении статистики конфликтов");
            return new ConflictStatistics();
        }
    }

    /// <summary>
    /// Очищает ресурсы при завершении
    /// </summary>
    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
    }
}


