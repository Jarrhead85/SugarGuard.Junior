// Модели ответов от API
namespace SugarGuard.Junior.Models.Api;

/// <summary>
/// Постраничный ответ API.
/// </summary>
public sealed class PagedApiResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
    public bool HasNextPage { get; init; }
    public bool HasPreviousPage { get; init; }
}

/// <summary>
/// Краткий профиль ребёнка, доступного текущему аккаунту на сервере.
/// </summary>
public sealed class ChildSummaryApiModel
{
    public Guid ChildId { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public DateOnly DateOfBirth { get; init; }
    public string? DiabetesType { get; init; }
    public DateOnly? DiagnosisDate { get; init; }
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// Ответ после входа
/// </summary>
public class LoginResponse
{
    /// <summary>
    /// Успешно ли выполнен вход
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// JWT токен для авторизации
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// Access токен
    /// </summary>
    public string? AccessToken { get; set; }

    /// <summary>
    /// Refresh токен для обновления сессии
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Данные пользователя
    /// </summary>
    public UserDto? User { get; set; }

    /// <summary>
    /// Сообщение (сообщение об ошибке или успеха)
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Сообщение об ошибке (альтернативное имя)
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// DTO пользователя (для ответа)
/// </summary>
public class UserDto
{
    /// <summary>
    /// ID пользователя
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Имя
    /// </summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// Фамилия
    /// </summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Email
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Email верифицирован?
    /// </summary>
    public bool IsEmailVerified { get; set; }

    /// <summary>
    /// Роль пользователя
    /// </summary>
    public string Role { get; set; } = "parent";
}

/// <summary>
/// Ответ после регистрации
/// </summary>
public class RegistrationResponse
{
    /// <summary>
    /// Успешно ли прошла регистрация
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// ID пользователя
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Email пользователя
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// JWT токен
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// Требуется ли верификация email?
    /// </summary>
    public bool RequiresEmailVerification { get; set; }

    /// <summary>
    /// Сообщение (успеха или ошибки)
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Сообщение об ошибке
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Ответ после отправки измерения
/// </summary>
public class MeasurementResponse
{
    /// <summary>
    /// Успешно ли обработано
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// ID созданного измерения на сервере
    /// </summary>
    public string? MeasurementId { get; set; }

    public string? ChildId { get; set; }
    public decimal GlucoseValue { get; set; }
    public DateTime MeasurementTime { get; set; }
    public string? ChildState { get; set; }
    public string? Notes { get; set; }
    public string? DataSource { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Является ли это критическим измерением?
    /// </summary>
    public bool IsCritical { get; set; }

    /// <summary>
    /// Сообщение об ошибке
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Рекомендация от ИИ (если запрашивали)
    /// </summary>
    public RecommendationResponse? Recommendation { get; set; }

    /// <summary>
    /// Есть ли конфликт версий (M-2)
    /// </summary>
    public bool HasConflict { get; set; }

    /// <summary>
    /// Серверная версия данных (JSON) при конфликте
    /// </summary>
    public string? ServerVersion { get; set; }

    /// <summary>
    /// Время последнего изменения на сервере
    /// </summary>
    public DateTime? ServerModifiedAt { get; set; }
}

/// <summary>
/// Ответ с рекомендацией от ИИ
/// </summary>
public class RecommendationResponse
{
    /// <summary>
    /// ID рекомендации
    /// </summary>
    public string RecommendationId { get; set; } = string.Empty;

    /// <summary>
    /// Текст рекомендации
    /// </summary>
    public string RecommendationText { get; set; } = string.Empty;

    /// <summary>
    /// Альтернативное имя для текста
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// Текст действия (что нужно сделать)
    /// </summary>
    public string ActionText { get; set; } = string.Empty;

    /// <summary>
    /// Рекомендуемое количество углеводов (ХЕ)
    /// </summary>
    public double RecommendedCarbs { get; set; }

    /// <summary>
    /// Уровень срочности: "Normal", "Warning", "Critical"
    /// </summary>
    public string Urgency { get; set; } = "Normal";

    /// <summary>
    /// Успешно ли получена рекомендация
    /// </summary>
    public bool Success { get; set; } = true;

    /// <summary>
    /// Какая модель использовалась
    /// </summary>
    public string? ModelUsed { get; set; }

    /// <summary>
    /// Альтернативное имя для модели
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Время обработки (мс)
    /// </summary>
    public long LatencyMs { get; set; }

    /// <summary>
    /// Значение глюкозы при запросе
    /// </summary>
    public double GlucoseValueAtRequest { get; set; }

    /// <summary>
    /// Это рекомендация из кэша?
    /// </summary>
    public bool IsFromCache { get; set; }

    /// <summary>
    /// Время создания
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

}

/// <summary>
/// Ответ синхронизации
/// </summary>
public class SyncResponse
{
    /// <summary>
    /// Успешно ли прошла синхронизация
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Количество синхронизированных записей
    /// </summary>
    public int SyncedCount { get; set; }

    /// <summary>
    /// Количество успешно синхронизированных
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// Количество ошибок
    /// </summary>
    public int ErrorCount { get; set; }

    /// <summary>
    /// Сообщение об ошибке
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Информация о конфликтах синхронизации
    /// </summary>
    public List<SyncConflictInfo>? Conflicts { get; set; }
}

/// <summary>
/// Информация о конфликте синхронизации
/// </summary>
public class SyncConflictInfo
{
    /// <summary>
    /// ID сущности с конфликтом
    /// </summary>
    public string EntityId { get; set; } = string.Empty;

    /// <summary>
    /// Тип сущности
    /// </summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// Время последнего изменения на сервере
    /// </summary>
    public DateTime ServerModifiedAt { get; set; }

    /// <summary>
    /// Время последнего изменения локально
    /// </summary>
    public DateTime LocalModifiedAt { get; set; }

    /// <summary>
    /// Серверная версия данных (JSON)
    /// </summary>
    public string ServerVersion { get; set; } = string.Empty;

    /// <summary>
    /// Какая версия победила ("Server" или "Local")
    /// </summary>
    public string WinningVersion { get; set; } = "Server";

    /// <summary>
    /// Стратегия разрешения конфликта
    /// </summary>
    public string ResolutionStrategy { get; set; } = "LastWriteWins";
}

/// <summary>
/// Серверный элемент рюкзака с полными данными для merge в локальную БД.
/// </summary>
public sealed class BackpackApiItemResponse
{
    public string BackpackItemId { get; set; } = string.Empty;
    public string ChildId { get; set; } = string.Empty;
    public string SnackName { get; set; } = string.Empty;
    public double BreadUnits { get; set; }
    public string? AddedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Ответ при подключении к Telegram
/// </summary>
public class TelegramConnectResponse
{
    /// <summary>
    /// Успешно ли прошло подключение
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Код подключения (для вывода пользователю)
    /// </summary>
    public string? ConnectionCode { get; set; }

    /// <summary>
    /// ID кода подключения
    /// </summary>
    public string? ConnectionCodeId { get; set; }

    /// <summary>
    /// Ссылка на Telegram бота
    /// </summary>
    public string? BotLink { get; set; }

    /// <summary>
    /// Время истечения кода (в секундах)
    /// </summary>
    public int ExpiresIn { get; set; } = 600;  // 10 минут

    /// <summary>
    /// Сообщение об ошибке
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Ответ при подтверждении кода
/// </summary>
public class VerifyCodeResponse
{
    /// <summary>
    /// Успешно ли верифицирован код
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Является ли код валидным?
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// ID пользователя после email-подтверждения.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Роль пользователя.
    /// </summary>
    public string? Role { get; set; }

    /// <summary>
    /// JWT access-токен.
    /// </summary>
    public string? AccessToken { get; set; }

    /// <summary>
    /// Альтернативное имя access-токена для совместимости.
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// Refresh-токен.
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Время истечения access-токена.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Сообщение (успеха или ошибки)
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Сообщение об ошибке
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Telegram ID (если успешно)
    /// </summary>
    public long? TelegramUserId { get; set; }
}

/// <summary>
/// Ответ на сохранение хеша кода привязки
/// </summary>
public class SaveConnectionCodeResponse
{
    /// <summary>
    /// Успешно ли сохранён хеш
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// ID созданной записи кода
    /// </summary>
    public string? CodeId { get; set; }

    /// <summary>
    /// Время истечения кода (UTC)
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Сообщение об ошибке
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Ответ на проверку кода привязки
/// </summary>
public class VerifyConnectionCodeResponse
{
    /// <summary>
    /// Успешно ли прошла проверка
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Валиден ли код
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// ID ребёнка (если код валиден)
    /// </summary>
    public string? ChildId { get; set; }

    /// <summary>
    /// ID созданной связи родитель-ребёнок
    /// </summary>
    public string? LinkId { get; set; }

    /// <summary>
    /// Сообщение (успеха или ошибки)
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Сообщение об ошибке
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Общий ответ от API
/// </summary>
public class ApiResponse<T>
{
    /// <summary>
    /// Успешно ли выполнен запрос
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Данные ответа
    /// </summary>
    public T? Data { get; set; }

    /// <summary>
    /// Сообщение об ошибке
    /// </summary>
    public string? Message { get; set; }
}
