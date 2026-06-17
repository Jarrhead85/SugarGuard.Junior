// Глобальные константы приложения
namespace SugarGuard.Junior.Utilities;

public static class Constants
{
    // ========== ДИАПАЗОНЫ ГЛЮКОЗЫ И ФИЗИЧЕСКИЕ ПАРАМЕТРЫ ==========
    // ПРИМЕЧАНИЕ: Константы глюкозы, возраста, веса и роста находятся в:
    // - SugarGuard.Shared.Constants.GlucoseLevels
    // - SugarGuard.Shared.Constants.ChildProfileLimits
    // Используйте эти классы для централизованного доступа к константам

    // ========== ПАРОЛЬ ==========
    /// <summary>
    /// Минимальная длина пароля
    /// </summary>
    public const int PasswordMinLength = 8;

    /// <summary>
    /// Требование: хотя бы одна заглавная буква
    /// </summary>
    public const string PasswordRequiresUppercase = "A-Z";

    /// <summary>
    /// Требование: хотя бы одна цифра
    /// </summary>
    public const string PasswordRequiresDigit = "0-9";

    /// <summary>
    /// Требование: хотя бы один спецсимвол
    /// </summary>
    public const string PasswordRequiresSpecial = "!@#$%^&*";

    // ========== EMAIL ==========
    /// <summary>
    /// Regex для валидации email
    /// </summary>
    public const string EmailRegex = @"^[^\s@]+@[^\s@]+\.[^\s@]+$";

    // ========== ТЕЛЕФОН ==========
    /// <summary>
    /// Regex для валидации телефона России (+7 XXX XXX-XX-XX)
    /// </summary>
    public const string PhoneRegex = @"^\+7\s?\d{3}\s?\d{3}[-]?\d{2}[-]?\d{2}$";

    // ========== ВРЕМЯ ==========
    /// <summary>
    /// Время действия кода подтверждения email (минут)
    /// </summary>
    public const int EmailVerificationCodeExpirationMinutes = 15;

    /// <summary>
    /// Время действия кода активации Telegram (минут)
    /// </summary>
    public const int TelegramActivationCodeExpirationMinutes = 10;

    // ========== STORAGE KEYS ==========
    /// <summary>
    /// Ключ для хранения токена аутентификации
    /// </summary>
    public const string StorageKeyAuthToken = "auth_token";

    /// <summary>
    /// Ключ для хранения ID текущего пользователя
    /// </summary>
    public const string StorageKeyCurrentUserId = "current_user_id";

    /// <summary>
    /// Ключ для хранения ID текущего ребёнка
    /// </summary>
    public const string StorageKeyCurrentChildId = "current_child_id";

    /// <summary>
    /// Ключ для хранения Telegram ID
    /// </summary>
    public const string StorageKeyTelegramId = "telegram_id";

    /// <summary>
    /// Ключ для хранения номера телефона родителя
    /// </summary>
    public const string StorageKeyParentPhone = "parent_phone";

    /// <summary>
    /// Ключ для хранения последнего известного значения глюкозы (F1, инвариантная культура).
    /// Обновляется MainPageViewModel при загрузке данных главного экрана.
    /// Используется HelpAlertPageViewModel для передачи актуального значения в CriticalAlertRequest.
    /// </summary>
    public const string StorageKeyLastGlucoseValue = "last_glucose_value";

    // ========== API ==========
    /// <summary>
    /// Базовый URL для Gigachat API
    /// </summary>
    public const string GigachatApiBaseUrl = "https://gigachat.devices.sber.ru/api/v1";

    /// <summary>
    /// Базовый URL для SugarGuard API
    /// </summary>
    public const string SugarGuardApiBaseUrl = "https://api.sugar-guard.ru/";

    /// <summary>
    /// Timeout для API запросов (секунд)
    /// </summary>
    public const int ApiTimeoutSeconds = 10;

    /// <summary>
    /// Максимальное количество повторов при ошибке API
    /// </summary>
    public const int ApiMaxRetries = 3;

    public const string AuditActorUnknown = "unknown";

    // ========== UI / VIEWMODELS ==========

    /// <summary>
    /// Размер страницы истории измерений, запрашиваемой с API за один раз.
    /// </summary>
    /// <remarks>
    /// DEBT-12: раньше захардкожен как <c>30</c> в <c>HistoryPageViewModel</c>.
    /// Серверная защита остаётся в <c>MeasurementRepository.GetPagedAsync</c>
    /// (<c>Math.Clamp(pageSize, 1, 500)</c>) — UI-значение может быть
    /// меньше, но не должно превышать 500.
    /// </remarks>
    public const int HistoryPageSize = 30;
}
