// Интерфейс для API клиента
// Одинаков для Mock и реального API
using SugarGuard.Junior.Models.Api;
using SugarGuard.Shared.Dto;

namespace SugarGuard.Junior.Services.Interfaces;

/// <summary>
/// API клиент для коммуникации с сервером
/// 
/// Это интерфейс, который:
/// 1. Mock реализует с фальшивыми данными (для разработки)
/// 2. Реальный API реализует с настоящими HTTP запросами
/// 
/// Для приложения разницы нет - использует одинаково!
/// </summary>
public interface IApiClient
{
    // ========== АУТЕНТИФИКАЦИЯ ==========
    /// <summary>
    /// Вход в приложение
    /// </summary>
    Task<LoginResponse> LoginAsync(string email, string password);

    /// <summary>
    /// Регистрация нового пользователя
    /// </summary>
    Task<RegistrationResponse> RegisterAsync(RegistrationRequest request);

    /// <summary>
    /// Подтверждение email с кодом
    /// </summary>
    Task<VerifyCodeResponse> VerifyEmailAsync(string email, string code);

    /// <summary>
    /// Отправка кода подтверждения на email
    /// </summary>
    Task<bool> SendEmailVerificationCodeAsync(string email);

    /// <summary>
    /// Обновление токена доступа (refresh token)
    /// </summary>
    Task<LoginResponse> RefreshTokenAsync(string refreshToken);

    Task LogoutAsync(string refreshToken);

    Task<CreateChildOnboardingResponse> CreateChildOnboardingAsync(CreateChildOnboardingRequest request);

    // ========== ИЗМЕРЕНИЯ ==========
    /// <summary>
    /// Отправляет одно измерение на сервер
    /// </summary>
    Task<MeasurementResponse> SendMeasurementAsync(SendMeasurementRequest request);

    /// <summary>
    /// Отправляет множество измерений (для синхронизации)
    /// </summary>
    Task<SyncResponse> SyncMeasurementsAsync(SyncRequest request);

    /// <summary>
    /// Получает последнее измерение ребёнка
    /// </summary>
    Task<MeasurementResponse> GetLatestMeasurementAsync(string childId);

    /// <summary>
    /// Получает измерение по ID (для детекции конфликтов синхронизации)
    /// </summary>
    Task<MeasurementResponse> GetMeasurementByIdAsync(string measurementId);

    // ========== ИИ РЕКОМЕНДАЦИИ ==========
    /// <summary>
    /// Получает рекомендацию от Gigachat
    /// </summary>
    Task<RecommendationResponse?> GetRecommendationAsync(RecommendationRequest request);

    // ========== TELEGRAM ИНТЕГРАЦИЯ ==========
    /// <summary>
    /// Генерирует код подключения Telegram
    /// Возвращает ID кода (сам код хранится на сервере)
    /// </summary>
    Task<TelegramConnectResponse> GenerateTelegramCodeAsync(string childId);

    /// <summary>
    /// Подтверждает подключение Telegram по коду
    /// </summary>
    Task<bool> VerifyTelegramConnectionAsync(VerifyTelegramCodeRequest request);

    // ========== РЮКЗАК ==========
    /// <summary>
    /// Добавляет перекус в рюкзак
    /// </summary>
    Task<bool> AddSnackAsync(AddSnackRequest request);

    /// <summary>
    /// Удаляет перекус из рюкзака
    /// </summary>
    Task<bool> RemoveSnackAsync(RemoveSnackRequest request);

    /// <summary>
    /// Фиксирует употребление перекуса и удаляет его из серверного рюкзака.
    /// </summary>
    Task<bool> ConsumeSnackAsync(ConsumeBackpackSnackRequest request);

    /// <summary>
    /// Получает содержимое рюкзака
    /// </summary>
    Task<List<string>> GetBackpackAsync(string childId);

    /// <summary>
    /// Получает содержимое рюкзака с идентификаторами и ХЕ для локального merge.
    /// </summary>
    Task<List<BackpackApiItemResponse>> GetBackpackItemsAsync(string childId);

    // ========== ПРИВЯЗКА РОДИТЕЛЕЙ ==========
    /// <summary>
    /// Сохраняет хеш кода привязки на сервере
    /// </summary>
    Task<SaveConnectionCodeResponse> SaveConnectionCodeAsync(SaveConnectionCodeRequest request);

    /// <summary>
    /// Проверяет код привязки от Telegram-бота
    /// </summary>
    Task<VerifyConnectionCodeResponse> VerifyConnectionCodeAsync(VerifyConnectionCodeRequest request);

    // ========== УВЕДОМЛЕНИЯ РОДИТЕЛЯМ ==========
    /// <summary>
    /// Отправляет уведомление родителям об измерении
    /// </summary>
    Task<bool> SendMeasurementNotificationAsync(MeasurementNotificationRequest request);

    /// <summary>
    /// Отправляет уведомление родителям о съеденном перекусе
    /// </summary>
    Task<bool> SendSnackConsumedNotificationAsync(SnackConsumedNotificationRequest request);

    /// <summary>
    /// Отправляет критическое уведомление с геолокацией
    /// </summary>
    Task<bool> SendCriticalAlertAsync(CriticalAlertRequest request);

    /// <summary>
    /// Отправляет уведомление о пропущенном измерении
    /// </summary>
    Task<bool> SendMissedMeasurementNotificationAsync(MissedMeasurementNotificationRequest request);

    // ========== СТАТИСТИКА И ЭКСПОРТ ==========
    /// <summary>
    /// Экспортирует статистику в PDF
    /// </summary>
    /// <param name="childId">ID ребёнка</param>
    /// <param name="period">Период: day, week, month, year</param>
    /// <param name="detailed">Подробный отчёт с полной таблицей</param>
    /// <returns>PDF-файл в виде массива байт</returns>
    Task<byte[]> ExportStatisticsToPdfAsync(string childId, string period = "day", bool detailed = false);

    // ========== ПРОВЕРКА СОЕДИНЕНИЯ ==========
    /// <summary>
    /// Проверяет доступность сервера
    /// </summary>
    Task<bool> HealthCheckAsync();

    // ========== ФОТО РЕБЁНКА (TODO-3) ==========
    /// <summary>
    /// Загружает фото ребёнка на сервер (multipart/form-data, поле <c>photo</c>).
    /// Возвращает абсолютный или относительный PhotoUrl при успехе, иначе null.
    /// </summary>
    /// <param name="childId">ID ребёнка.</param>
    /// <param name="photoStream">Поток с содержимым изображения.</param>
    /// <param name="fileName">Имя файла (для имени и расширения).</param>
    /// <param name="contentType">MIME-тип (image/jpeg, image/png и т.п.).</param>
    /// <param name="ct">Токен отмены.</param>
    Task<string?> UploadChildPhotoAsync(
        string childId,
        Stream photoStream,
        string fileName,
        string contentType,
        CancellationToken ct = default);

    /// <summary>
    /// Удаляет фото ребёнка на сервере.
    /// Возвращает true при успехе или если фото уже отсутствовало.
    /// </summary>
    Task<bool> DeleteChildPhotoAsync(string childId, CancellationToken ct = default);
}
