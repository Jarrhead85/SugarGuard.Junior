using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Globalization;
using SugarGuard.Shared.Constants;
using SugarGuard.Junior.Database;
using SugarGuard.Junior.Models.Api;
using SugarGuard.Junior.Models.Core;
using SugarGuard.Junior.Models.Enums;
using SugarGuard.Junior.Security;
using SugarGuard.Junior.Services.Interfaces;
using SugarGuard.Junior.Utilities;

namespace SugarGuard.Junior.Services.Implementations;

/// <summary>
/// Сервис для работы с измерениями
/// Отвечает за:
/// - Сохранение измерений в БД
/// - Получение истории
/// - Интеграция с GigaChat для получения рекомендаций
/// - Логика определения статуса и рекомендаций
/// </summary>
public class MeasurementService : IMeasurementService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly ILogger<MeasurementService> _logger;
    private readonly IAIRecommendationService _aiRecommendationService;
    private readonly IRecommendationCacheService _cacheService;
    private readonly IBackpackService _backpackService;
    private readonly ILocationService _locationService;
    private readonly INotificationService _notificationService;
    private readonly ICryptoService _cryptoService;
    private readonly ISyncService _syncService;

    public MeasurementService(
        IDbContextFactory<AppDbContext> factory,
        ILogger<MeasurementService> logger,
        IAIRecommendationService aiRecommendationService,
        IRecommendationCacheService cacheService,
        IBackpackService backpackService,
        ILocationService locationService,
        INotificationService notificationService,
        ICryptoService cryptoService,
        ISyncService syncService)
    {
        _factory = factory;
        _logger = logger;
        _aiRecommendationService = aiRecommendationService;
        _cacheService = cacheService;
        _backpackService = backpackService;
        _locationService = locationService;
        _notificationService = notificationService;
        _cryptoService = cryptoService;
        _syncService = syncService;
    }

    /// <summary>
    /// Получает количество измерений за сегодня для отображения статистики на главной
    /// </summary>
    public async Task<int> GetMeasurementCountTodayAsync(string childId)
    {
        try
        {
            var todayStart = DateTime.UtcNow.Date;
            await using var ctx = await _factory.CreateDbContextAsync();
            return await ctx.Set<MeasurementEntity>()
                .CountAsync(m => m.ChildId == childId && m.MeasurementTime >= todayStart);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при подсчёте измерений за сегодня");
            return 0;
        }
    }

    /// <summary>
    /// Получает последние два измерения для отображения тренда на главной
    /// Примечание: Возвращает entities с зашифрованными данными.
    /// Вызывающий код должен использовать _cryptoService для расшифровки при необходимости.
    /// </summary>
    public async Task<List<MeasurementEntity>> GetLastTwoMeasurementsAsync(string childId)
    {
        try
        {
            await using var ctx = await _factory.CreateDbContextAsync();
            var entities = await ctx.Set<MeasurementEntity>()
                .AsNoTracking()
                .Where(m => m.ChildId == childId)
                .OrderByDescending(m => m.MeasurementTime)
                .Take(2)
                .ToListAsync();

            _logger.LogInformation(" Получено {Count} измерений для {ChildId}", entities.Count, childId);
            return entities;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, " Ошибка при получении измерений");
            return new List<MeasurementEntity>();
        }
    }

    /// <summary>
    /// Основной метод: сохраняет измерение и возвращает рекомендацию от GigaChat
    /// Вызывается с главной страницы когда пользователь нажимает "Отправить"
    /// </summary>
    public async Task<RecommendationResponse?> ProcessMeasurementWithRecommendationAsync(
        string childId,
        double glucoseValue,
        string childState = "normal",
        DateTime? measurementTimeUtc = null)
    {
        try
        {
            _logger.LogInformation("Обработка измерения: {GlucoseValue} ммоль/л для {ChildId}", glucoseValue, childId);

            // 1⃣ ВАЛИДАЦИЯ
            if (!GlucoseLevels.IsValidInput(glucoseValue))
            {
                _logger.LogWarning("Значение вне диапазона: {GlucoseValue}", glucoseValue);
                return null;
            }

            if (string.IsNullOrWhiteSpace(childId))
            {
                _logger.LogWarning("ChildId не указан");
                return null;
            }

            // 2⃣ ОПРЕДЕЛЯЕМ СТАТУС
            var status = GlucoseClassifier.Classify(glucoseValue);
            var state = status switch
            {
                GlucoseStatus.CriticallyLow => ChildState.Hypoglycemia,
                GlucoseStatus.Low => ChildState.Hypoglycemia,
                GlucoseStatus.High => ChildState.Hyperglycemia,
                GlucoseStatus.CriticallyHigh => ChildState.Hyperglycemia,
                _ => ChildState.Normal
            };

            // 2.5⃣ ОБРАБОТКА КРИТИЧЕСКОГО УРОВНЯ С ГЕОЛОКАЦИЕЙ
            if (GlucoseLevels.IsCritical(glucoseValue))
            {
                _logger.LogWarning("КРИТИЧЕСКИЙ УРОВЕНЬ ГЛЮКОЗЫ: {GlucoseValue} ммоль/л", glucoseValue);

                // Дожидаемся результата — это критический путь (M-1)
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    var location = await _locationService.GetCurrentLocationAsync(TimeSpan.FromSeconds(5));
                    if (location != null)
                    {
                        var locationSent = await _locationService.SendLocationToParentsAsync(childId, glucoseValue, location);
                        if (locationSent)
                        {
                            _logger.LogInformation("Геолокация отправлена родителям при критическом уровне");
                        }
                        else
                        {
                            _logger.LogError("Не удалось отправить геолокацию родителям");
                            // Fallback: локальный алерт
                            await _notificationService.SendCriticalAlertAsync(
                                "Критический уровень глюкозы!",
                                $"Глюкоза: {glucoseValue:F1} ммоль/л. Невозможно отправить геолокацию.",
                                glucoseValue);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Не удалось получить геолокацию — отправляем критический алерт без координат");
                        await _notificationService.SendCriticalAlertAsync(
                            "Критический уровень глюкозы!",
                            $"Глюкоза: {glucoseValue:F1} ммоль/л. Срочно проверьте ребёнка!",
                            glucoseValue);
                    }
                }
                catch (Exception locationEx)
                {
                    _logger.LogError(locationEx, "Ошибка при обработке геолокации для критического уровня");
                    // Fallback: локальный алерт
                    await _notificationService.SendCriticalAlertAsync(
                        "Критический уровень глюкозы!",
                        $"Глюкоза: {glucoseValue:F1} ммоль/л. Срочно проверьте ребёнка!",
                        glucoseValue);
                }
            }

            var measurementId = Guid.NewGuid().ToString();
            var measurementTime = NormalizeMeasurementTime(measurementTimeUtc);

            // 3⃣ ПОЛУЧАЕМ РЕКОМЕНДАЦИЮ ОТ ИИ (сначала пытаемся из кэша, потом от GigaChat)
            var aiRecommendation = await GetAIRecommendationAsync(
                childId,
                glucoseValue,
                childState,
                measurementId);
            
            // 4⃣ СОЗДАЁМ СУЩНОСТЬ ИЗМЕРЕНИЯ (все PHI данные зашифрованы)
            var measurement = new MeasurementEntity
            {
                MeasurementId = measurementId,
                ChildId = childId,
                EncryptedGlucoseValue = await _cryptoService.EncryptAsync(glucoseValue.ToString("F1", CultureInfo.InvariantCulture)),
                MeasurementTime = measurementTime,
                EncryptedChildState = await _cryptoService.EncryptAsync(state.ToString()),
                DataSource = DataSource.ManualInput,
                IsSynced = false,
                RecommendationId = aiRecommendation?.RecommendationId
            };

            // 5⃣ СОХРАНЯЕМ В БД
            await using var ctx = await _factory.CreateDbContextAsync();
            await ctx.Set<MeasurementEntity>().AddAsync(measurement);
            await ctx.SaveChangesAsync();

            _logger.LogInformation("Измерение сохранено: {MeasurementId}", measurement.MeasurementId);

            var payload = new SendMeasurementRequest
            {
                MeasurementId = measurement.MeasurementId,
                ChildId = childId,
                GlucoseValue = glucoseValue,
                MeasurementTime = measurementTime,
                ChildState = state.ToString(),
                DataSource = DataSource.ManualInput.ToString(),
                RequestRecommendation = true,
                LastModifiedAt = DateTime.UtcNow
            };
            try
            {
                await _syncService.QueueItemAsync(
                    measurement.MeasurementId,
                    "Measurement",
                    "Insert",
                    Newtonsoft.Json.JsonConvert.SerializeObject(payload));
            }
            catch (Exception syncEx)
            {
                _logger.LogWarning(
                    syncEx,
                    "Измерение {MeasurementId} сохранено локально, но не поставлено в очередь синхронизации",
                    measurement.MeasurementId);
            }

            // 6⃣ ОТМЕЧАЕМ ИЗМЕРЕНИЕ КАК ВЫПОЛНЕННОЕ (останавливает повторные напоминания)
            try
            {
                await _notificationService.MarkMeasurementCompletedAsync(childId);
            }
            catch (Exception notificationEx)
            {
                _logger.LogError(notificationEx, "Ошибка при отметке измерения как выполненного");
            }

            // 7⃣ КОНВЕРТИРУЕМ В ОТВЕТ
            var recommendation = ConvertAIRecommendationToResponse(aiRecommendation, glucoseValue, status);

            return recommendation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обработке измерения");
            return null;
        }
    }

    /// <summary>
    /// Определяет статус глюкозы по значению
    /// </summary>
    public GlucoseStatus GetStatus(double glucoseValue)
    {
        return GlucoseClassifier.Classify(glucoseValue);
    }

    private static DateTime NormalizeMeasurementTime(DateTime? measurementTimeUtc)
    {
        if (!measurementTimeUtc.HasValue)
        {
            return DateTime.UtcNow;
        }

        return measurementTimeUtc.Value.Kind switch
        {
            DateTimeKind.Utc => measurementTimeUtc.Value,
            DateTimeKind.Local => measurementTimeUtc.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(measurementTimeUtc.Value, DateTimeKind.Local).ToUniversalTime()
        };
    }

    /// <summary>
    /// Проверяет, критично ли значение глюкозы
    /// </summary>
    /// <param name="glucoseValue">Значение глюкозы в ммоль/л</param>
    /// <returns>true если уровень критический, иначе false</returns>
    public bool IsCritical(double glucoseValue)
    {
        return GlucoseLevels.IsCritical(glucoseValue);
    }

    /// <summary>
    /// Логирует съеденный перекус и удаляет его из рюкзака по BackpackItemId
    /// </summary>
    public async Task<bool> LogSnackConsumedAsync(SnackConsumedRequest request)
    {
        try
        {
            _logger.LogInformation("Логирование перекуса: {SnackName}", request.SnackName);

            // 1⃣ Логируем в таблицу истории потребления
            var encryptedSnackName = await _cryptoService.EncryptAsync(request.SnackName);
            var encryptedBreadUnits = await _cryptoService.EncryptAsync(request.BreadUnits.ToString("F2"));

            var log = new SnackConsumptionLog
            {
                LogId = Guid.NewGuid().ToString(),
                ChildId = request.ChildId,
                EncryptedSnackName = encryptedSnackName,
                EncryptedBreadUnits = encryptedBreadUnits,
                RecommendationId = request.RecommendationId,
                ConsumedAt = request.ConsumedAt,
                CreatedAt = DateTime.UtcNow
            };

            await using var ctx = await _factory.CreateDbContextAsync();
            ctx.Set<SnackConsumptionLog>().Add(log);

            // 2⃣ Удаляем перекус из рюкзака по BackpackItemId (надёжно, без сравнения шифротекста)
            if (!string.IsNullOrEmpty(request.BackpackItemId))
            {
                var backpackItem = await ctx.Set<BackpackItem>()
                    .FirstOrDefaultAsync(b =>
                        b.BackpackItemId == request.BackpackItemId &&
                        b.ChildId == request.ChildId);

                if (backpackItem != null)
                {
                    var history = new BackpackHistory
                    {
                        HistoryId = Guid.NewGuid().ToString(),
                        ChildId = request.ChildId,
                        EncryptedSnackName = backpackItem.EncryptedSnackName,
                        EncryptedBreadUnits = backpackItem.EncryptedBreadUnits,
                        AddedAt = backpackItem.CreatedAt,
                        DeletedAt = DateTime.UtcNow,
                        DeletedBy = "child",
                        CreatedAt = DateTime.UtcNow
                    };

                    ctx.Set<BackpackHistory>().Add(history);
                    ctx.Set<BackpackItem>().Remove(backpackItem);
                    _logger.LogInformation(" Перекус удалён из рюкзака: {BackpackItemId}", request.BackpackItemId);
                }
            }

            await ctx.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, " Ошибка при логировании перекуса");
            return false;
        }
    }

    /// <summary>
    /// Логирует пропущенную рекомендацию
    /// </summary>
    public async Task<bool> LogSkippedRecommendationAsync(SkippedRecommendationRequest request)
    {
        try
        {
            _logger.LogInformation("Рекомендация пропущена: {RecommendationId}", request.RecommendationId);

            var log = new SkippedRecommendationLog
            {
                LogId = Guid.NewGuid().ToString(),
                ChildId = request.ChildId,
                RecommendationId = request.RecommendationId,
                Reason = request.Reason,
                SkippedAt = request.SkippedAt,
                CreatedAt = DateTime.UtcNow
            };

            await using var ctx = await _factory.CreateDbContextAsync();
            ctx.Set<SkippedRecommendationLog>().Add(log);
            await ctx.SaveChangesAsync();

            _logger.LogInformation("Логирование завершено");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при логировании");
            return false;
        }
    }

    /// <summary>
    /// ====== МЕТОДЫ ДЛЯ РАБОТЫ С ИИ РЕКОМЕНДАЦИЯМИ ======
    /// </summary>

    /// <summary>
    /// Получает рекомендацию от ИИ через API-прокси.
    /// </summary>
    private async Task<AIRecommendation?> GetAIRecommendationAsync(
        string childId,
        double glucoseValue,
        string childState,
        string measurementId)
    {
        try
        {
            var recentGlucoseValues = await GetRecentGlucoseValuesAsync(childId);
            var availableSnacks = await GetAvailableSnacksAsync(childId);

            return await _aiRecommendationService.GetRecommendationAsync(
                childId,
                glucoseValue,
                recentGlucoseValues,
                childState,
                availableSnacks,
                measurementId,
                forceNew: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении рекомендации");
            return await GenerateLocalRecommendation(childId, glucoseValue, childState);
        }
    }

    /// <summary>
    /// Получает профиль ребёнка
    /// </summary>
    private async Task<Child?> GetChildProfileAsync(string childId)
    {
        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.Set<Child>().AsNoTracking().FirstOrDefaultAsync(c => c.ChildId == childId);
    }

    /// <summary>
    /// Получает настройки диабета ребёнка
    /// </summary>
    private async Task<DiabetesSettings?> GetDiabetesSettingsAsync(string childId)
    {
        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.Set<DiabetesSettings>().FirstOrDefaultAsync(d => d.ChildId == childId);
    }

    /// <summary>
    /// Получает последние значения глюкозы за 3 часа для определения тренда.
    /// Расшифровка параллельная (<see cref="Task.WhenAll{TResult}(IEnumerable{Task{TResult}})"/>):
    /// N измерений × ~50 мс Keychain = 1×RTT вместо N×RTT.
    /// </summary>
    private async Task<List<double>> GetRecentGlucoseValuesAsync(string childId)
    {
        var threeHoursAgo = DateTime.UtcNow.AddHours(-3);

        await using var ctx = await _factory.CreateDbContextAsync();
        var measurements = await ctx.Set<MeasurementEntity>()
            .Where(m => m.ChildId == childId && m.MeasurementTime >= threeHoursAgo)
            .OrderBy(m => m.MeasurementTime)
            .ToListAsync();

        if (measurements.Count == 0)
            return new List<double>();

        // Параллельная расшифровка. Неудачи обрабатываются per-item через SafeDecryptAsync.
        var decryptedValues = await Task.WhenAll(
            measurements.Select(m => SafeDecryptGlucoseAsync(m)));

        var values = new List<double>(measurements.Count);
        foreach (var decrypted in decryptedValues)
        {
            if (string.IsNullOrEmpty(decrypted))
                continue;
            if (DoubleParser.TryParseDecrypted(decrypted, out double value))
                values.Add(value);
        }
        return values;
    }

    /// <summary>
    /// Безопасная расшифровка значения глюкозы с fallback на plaintext
    /// (для обратной совместимости со старыми записями без шифрования).
    /// </summary>
    private async Task<string?> SafeDecryptGlucoseAsync(MeasurementEntity measurement)
    {
        try
        {
            return await _cryptoService.DecryptAsync(measurement.EncryptedGlucoseValue);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось дешифровать значение глюкозы для {EntityId}", measurement.MeasurementId);
            // Fallback для обратной совместимости
            if (DoubleParser.TryParseDecrypted(measurement.EncryptedGlucoseValue, out _))
                return measurement.EncryptedGlucoseValue;
            return null;
        }
    }

    /// <summary>
    /// Получает список доступных перекусов из рюкзака.
    /// Расшифровка параллельная (имя + ХЕ для каждого перекуса).
    /// </summary>
    private async Task<List<string>> GetAvailableSnacksAsync(string childId)
    {
        try
        {
            var backpackItems = await _backpackService.GetBackpackAsync(childId);
            if (backpackItems.Count == 0)
                return new List<string>();

            // Для каждого item параллельно расшифровываем 2 поля.
            var decrypted = await Task.WhenAll(
                backpackItems.Select(item => SafeDecryptSnackAsync(item)));

            return decrypted
                .Where(snack => snack.HasValue && !string.IsNullOrWhiteSpace(snack.Value.Name))
                .Select(snack => snack!.Value)
                .GroupBy(
                    snack => $"{NormalizeSnackName(snack.Name)}|{snack.BreadUnits:F3}",
                    StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    var snack = group.First();
                    var quantity = group.Count();
                    return quantity == 1
                        ? $"{snack.Name} ({snack.BreadUnits:F1} ХЕ)"
                        : $"{snack.Name}: {quantity} шт. по {snack.BreadUnits:F1} ХЕ";
                })
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось получить рюкзак, используем пустой список");
            return [];
        }
    }

    private static string NormalizeSnackName(string value) =>
        string.Join(' ', value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToUpperInvariant();

    /// <summary>
    /// Безопасная расшифровка перекуса (имя + ХЕ). Возвращает null при неудаче.
    /// </summary>
    private async Task<(string Name, double BreadUnits)?> SafeDecryptSnackAsync(BackpackItem item)
    {
        try
        {
            var nameTask = _cryptoService.DecryptAsync(item.EncryptedSnackName);
            var buTask = _cryptoService.DecryptAsync(item.EncryptedBreadUnits ?? string.Empty);
            await Task.WhenAll(nameTask, buTask);

            var bu = DoubleParser.TryParseDecrypted(buTask.Result, out var parsed) ? parsed : 0.0;
            return (nameTask.Result, bu);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось дешифровать перекус {EntityId}", item.BackpackItemId);
            // Fallback: пробуем прочитать поля как plaintext (обратная совместимость)
            var bu = DoubleParser.TryParseDecrypted(item.EncryptedBreadUnits, out var parsed) ? parsed : 0.0;
            return (item.EncryptedSnackName, bu);
        }
    }

    /// <summary>
    /// Вычисляет тренд глюкозы на основе последних значений
    /// </summary>
    private static string CalculateTrend(List<double> recentValues)
    {
        if (recentValues.Count < 2)
            return "неизвестен";

        var lastValue = recentValues[^1];
        var prevValue = recentValues[^2];

        return (lastValue - prevValue) switch
        {
            > 0.5 => "вверх",
            < -0.5 => "вниз",
            _ => "стабильно"
        };
    }

    /// <summary>
    /// Конвертирует статус глюкозы в текст для GigaChat
    /// </summary>
    private static string GetGlucoseStatusText(GlucoseStatus status)
    {
        return status switch
        {
            GlucoseStatus.CriticallyLow => "КРИТИЧЕСКИ НИЗКО",
            GlucoseStatus.Low => "НИЗКО",
            GlucoseStatus.Normal => "НОРМА",
            GlucoseStatus.High => "ВЫСОКО",
            GlucoseStatus.CriticallyHigh => "КРИТИЧЕСКИ ВЫСОКО",
            _ => "НЕИЗВЕСТНО"
        };
    }

    /// <summary>
    /// Генерирует локальную рекомендацию (fallback когда GigaChat недоступен)
    /// </summary>
    private Task<AIRecommendation> GenerateLocalRecommendation(string childId, double glucoseValue, string childState)
    {
        var status = GetStatus(glucoseValue);
        var (urgency, text) = GenerateLocalRecommendationText(glucoseValue, status);

        return Task.FromResult(new AIRecommendation
        {
            RecommendationId = Guid.NewGuid().ToString(),
            ChildId = childId,
            GlucoseValueAtRequest = glucoseValue,
            RecommendationText = text,
            Urgency = urgency,
            ModelUsed = "Local",
            IsFromCache = false,
            LatencyMs = 0,
            CreatedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Конвертирует AIRecommendation в RecommendationResponse для API
    /// </summary>
    private static RecommendationResponse ConvertAIRecommendationToResponse(AIRecommendation? aiRecommendation, double glucoseValue, GlucoseStatus status)
    {
        if (aiRecommendation == null)
        {
            // Fallback если не удалось получить рекомендацию
            var (urgency, text) = GenerateLocalRecommendationText(glucoseValue, status);
            return new RecommendationResponse
            {
                RecommendationId = Guid.NewGuid().ToString(),
                RecommendationText = text,
                Urgency = urgency.ToString().ToLower(),
                GlucoseValueAtRequest = glucoseValue,
                ModelUsed = "Local",
                IsFromCache = false,
                Success = true,
                CreatedAt = DateTime.UtcNow
            };
        }

        return new RecommendationResponse
        {
            RecommendationId = aiRecommendation.RecommendationId,
            RecommendationText = aiRecommendation.RecommendationText,
            Urgency = aiRecommendation.Urgency.ToString().ToLower(),
            GlucoseValueAtRequest = aiRecommendation.GlucoseValueAtRequest,
            ModelUsed = aiRecommendation.ModelUsed,
            IsFromCache = aiRecommendation.IsFromCache,
            LatencyMs = aiRecommendation.LatencyMs,
            Success = true,
            CreatedAt = aiRecommendation.CreatedAt
        };
    }

    /// <summary>
    /// ====== ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ======
    /// </summary>

    /// <summary>
    /// Генерирует локальную рекомендацию на основе статуса (fallback для GigaChat)
    /// </summary>
    private static (RecommendationUrgency urgency, string text) GenerateLocalRecommendationText(double glucoseValue, GlucoseStatus status)
    {
        return status switch
        {
            GlucoseStatus.CriticallyLow =>
                (RecommendationUrgency.Critical, " КРИТИЧЕСКИ НИЗКИЙ САХАР! Срочно съешьте быстрые углеводы (сок, сахар, глюкоза) на 1-2 ХЕ!"),

            GlucoseStatus.Low =>
                (RecommendationUrgency.Warning, " Сахар низкий. Рекомендуется съесть перекус на 1-1.5 ХЕ."),

            GlucoseStatus.Normal =>
                (RecommendationUrgency.Normal, "✅ Сахар в норме. Молодец! Продолжай так держать!"),

            GlucoseStatus.High =>
                (RecommendationUrgency.Warning, glucoseValue >= 14.0
                    ? $"Глюкоза очень высокая: {glucoseValue:0.0} ммоль/л. Сразу сообщи взрослому, пей воду и проверь кетоны по своему плану."
                    : $"Глюкоза повышена: {glucoseValue:0.0} ммоль/л. Сообщи взрослому, пей воду и следуй своему плану коррекции."),

            GlucoseStatus.CriticallyHigh =>
                (RecommendationUrgency.Critical, "Критически высокий сахар. Срочно позови взрослого, пей воду и проверь кетоны по своему плану."),

            _ => (RecommendationUrgency.Normal, "Неизвестный статус")
        };
    }
}
