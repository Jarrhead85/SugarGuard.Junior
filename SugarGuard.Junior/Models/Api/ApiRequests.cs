// Модели запросов к API
// Отправляем эти данные на сервер
namespace SugarGuard.Junior.Models.Api;

/// <summary>
/// Запрос на вход
/// </summary>
public class LoginRequest
{
    /// <summary>
    /// Email пользователя
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Пароль (передаём только по TLS!)
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Информация об устройстве (для логов)
    /// </summary>
    public string? DeviceInfo { get; set; }
}

/// <summary>
/// Запрос на регистрацию
/// </summary>
public class RegistrationRequest
{
    /// <summary>
    /// Имя родителя
    /// </summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// Фамилия родителя
    /// </summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Email
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Телефон
    /// </summary>
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>
    /// Пароль (только по TLS!)
    /// </summary>
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Запрос на отправку измерения
/// </summary>
public class SendMeasurementRequest
{
    /// <summary>
    /// ID ребёнка
    /// </summary>
    public string ChildId { get; set; } = string.Empty;

    /// <summary>
    /// Значение глюкозы
    /// </summary>
    public double GlucoseValue { get; set; }

    /// <summary>
    /// Время измерения
    /// </summary>
    public DateTime MeasurementTime { get; set; }

    /// <summary>
    /// Состояние ребёнка
    /// </summary>
    public string ChildState { get; set; } = string.Empty;

    /// <summary>
    /// Заметки
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Источник данных
    /// </summary>
    public string DataSource { get; set; } = string.Empty;

    /// <summary>
    /// Запросить ли рекомендацию ИИ
    /// </summary>
    public bool RequestRecommendation { get; set; }

    /// <summary>
    /// Время последнего изменения (для обнаружения конфликтов, M-2)
    /// </summary>
    public DateTime? LastModifiedAt { get; set; }
}

/// <summary>
/// Запрос на получение рекомендации
/// </summary>
public class RecommendationRequest
{
    /// <summary>
    /// ID ребёнка
    /// </summary>
    public string ChildId { get; set; } = string.Empty;

    /// <summary>
    /// Текущая глюкоза
    /// </summary>
    public double CurrentGlucose { get; set; }

    /// <summary>
    /// История за последние N часов (для тренда)
    /// </summary>
    public List<double> RecentGlucoseValues { get; set; } = new();

    /// <summary>
    /// Состояние ребёнка
    /// </summary>
    public string ChildState { get; set; } = string.Empty;

    /// <summary>
    /// Содержимое рюкзака (что есть в наличии)
    /// Формат: "Яблоко (1 ХЕ)", "Сок (1.5 ХЕ)", и т.д.
    /// </summary>
    public List<string> AvailableSnacks { get; set; } = new();
}

/// <summary>
/// Запрос на синхронизацию (отправляем много измерений)
/// </summary>
public class SyncRequest
{
    /// <summary>
    /// Измерения для синхронизации
    /// </summary>
    public List<SendMeasurementRequest> Measurements { get; set; } = new();

    /// <summary>
    /// Версия приложения (для совместимости)
    /// </summary>
    public string? AppVersion { get; set; }

    /// <summary>
    /// Время последней синхронизации
    /// </summary>
    public DateTime? LastSyncTime { get; set; }
}

/// <summary>
/// Запрос на генерирование кода подключения Telegram
/// </summary>
public class GenerateTelegramCodeRequest
{
    /// <summary>
    /// ID ребёнка
    /// </summary>
    public string ChildId { get; set; } = string.Empty;
}

/// <summary>
/// Запрос на подтверждение кода (родитель вводит в боте)
/// </summary>
public class VerifyTelegramCodeRequest
{
    /// <summary>
    /// ID кода подключения
    /// </summary>
    public string ConnectionCodeId { get; set; } = string.Empty;

    /// <summary>
    /// Telegram ID родителя
    /// </summary>
    public long TelegramUserId { get; set; }

    /// <summary>
    /// Юзернейм в Telegram (для верификации)
    /// </summary>
    public string? TelegramUsername { get; set; }
}

/// <summary>
/// Запрос на добавление перекуса в рюкзак
/// </summary>
public class AddSnackRequest
{
    /// <summary>
    /// ID ребёнка
    /// </summary>
    public string ChildId { get; set; } = string.Empty;

    /// <summary>
    /// Название перекуса
    /// </summary>
    public string SnackName { get; set; } = string.Empty;

    /// <summary>
    /// Количество хлебных единиц
    /// 1 ХЕ = 10-12 г углеводов
    /// </summary>
    public double Carbs { get; set; }

    /// <summary>
    /// Кто добавил: "child" или "parent"
    /// </summary>
    public string AddedBy { get; set; } = "child";

    /// <summary>
    /// Время последнего изменения (для обнаружения конфликтов, M-2)
    /// </summary>
    public DateTime? LastModifiedAt { get; set; }
}

/// <summary>
/// Запрос на удаление перекуса
/// </summary>
public class RemoveSnackRequest
{
    /// <summary>
    /// ID перекуса
    /// </summary>
    public string SnackId { get; set; } = string.Empty;

    /// <summary>
    /// ID ребёнка
    /// </summary>
    public string ChildId { get; set; } = string.Empty;

    /// <summary>
    /// Кто удалил
    /// </summary>
    public string RemovedBy { get; set; } = "child";
}
/// <summary>
/// Запрос на отправку измерения и получение рекомендации
/// </summary>
public class SendMeasurementWithRecommendationRequest
{
    public string ChildId { get; set; } = string.Empty;
    public double GlucoseValue { get; set; }
    public string ChildState { get; set; } = string.Empty;
    public List<double> RecentGlucoseValues { get; set; } = new();
    public List<string> AvailableSnacks { get; set; } = new();
}

/// <summary>
/// Запрос на логирование съеденного перекуса
/// </summary>
public class SnackConsumedRequest
{
    public string ChildId { get; set; } = string.Empty;
    /// <summary>ID элемента рюкзака — для однозначного удаления из рюкзака при учёте съеденного.</summary>
    public string BackpackItemId { get; set; } = string.Empty;
    public string SnackName { get; set; } = string.Empty;
    public double BreadUnits { get; set; }
    public string RecommendationId { get; set; } = string.Empty;  // Связь с рекомендацией
    public DateTime ConsumedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Запрос на логирование пропущенной рекомендации
/// </summary>
public class SkippedRecommendationRequest
{
    public string ChildId { get; set; } = string.Empty;
    public string RecommendationId { get; set; } = string.Empty;
    public string Reason { get; set; } = "user_skipped";  // или другая причина
    public DateTime SkippedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Запрос на отправку уведомления родителю
/// </summary>
public class NotifyParentRequest
{
    public string ParentTelegramId { get; set; } = string.Empty;
    public string ChildId { get; set; } = string.Empty;
    public string NotificationType { get; set; } = string.Empty;  // "measurement", "snack_consumed", "skipped_recommendation"
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, object>? Data { get; set; }
}

/// <summary>
/// Запрос на сохранение кода привязки.
/// </summary>
/// <remarks>
/// SEC-2: клиент передаёт <b>сырой</b> код, а не его хеш.
/// Хеширование HMAC-SHA256 с серверным ключом выполняется в
/// <c>ParentLinkService.SaveConnectionCodeAsync</c>. Раньше клиент
/// хешировал код unsalted SHA-256, и злоумышленник с дампом БД
/// мог за часы подобрать прообраз.
/// </remarks>
public class SaveConnectionCodeRequest
{
    /// <summary>
    /// ID ребёнка
    /// </summary>
    public string ChildId { get; set; } = string.Empty;

    /// <summary>
    /// Сырой 8-символьный код привязки (формат <c>ABCD-1234</c> или <c>ABCD1234</c>).
    /// Сервер хеширует его сам через HMAC-SHA256.
    /// </summary>
    public string Code { get; set; } = string.Empty;
}

/// <summary>
/// Запрос на проверку кода привязки от Telegram-бота
/// </summary>
public class VerifyConnectionCodeRequest
{
    /// <summary>
    /// Код привязки в формате "ABCD-1234"
    /// </summary>
    public string ConnectionCode { get; set; } = string.Empty;

    /// <summary>
    /// Telegram ID родителя
    /// </summary>
    public long TelegramUserId { get; set; }

    /// <summary>
    /// Имя пользователя в Telegram (опционально)
    /// </summary>
    public string? TelegramUsername { get; set; }
}

/// <summary>
/// Запрос на отправку уведомления родителям об измерении
/// </summary>
public class MeasurementNotificationRequest
{
    /// <summary>
    /// ID ребёнка
    /// </summary>
    public string ChildId { get; set; } = string.Empty;

    /// <summary>
    /// Значение глюкозы в ммоль/л
    /// </summary>
    public double GlucoseValue { get; set; }

    /// <summary>
    /// Статус глюкозы (норма/низкий/высокий/критический)
    /// </summary>
    public string GlucoseStatus { get; set; } = string.Empty;

    /// <summary>
    /// Время измерения
    /// </summary>
    public DateTime MeasurementTime { get; set; }

    /// <summary>
    /// Дополнительные заметки
    /// </summary>
    public string? Notes { get; set; }
}

/// <summary>
/// Запрос на отправку уведомления родителям о съеденном перекусе
/// </summary>
public class SnackConsumedNotificationRequest
{
    /// <summary>
    /// ID ребёнка
    /// </summary>
    public string ChildId { get; set; } = string.Empty;

    /// <summary>
    /// Название перекуса
    /// </summary>
    public string SnackName { get; set; } = string.Empty;

    /// <summary>
    /// Количество хлебных единиц
    /// </summary>
    public double BreadUnits { get; set; }

    /// <summary>
    /// Текущий уровень глюкозы
    /// </summary>
    public double CurrentGlucose { get; set; }

    /// <summary>
    /// Время употребления
    /// </summary>
    public DateTime ConsumedAt { get; set; }
}

/// <summary>
/// Запрос на отправку критического уведомления с геолокацией
/// </summary>
public class CriticalAlertRequest
{
    /// <summary>
    /// ID ребёнка
    /// </summary>
    public string ChildId { get; set; } = string.Empty;

    /// <summary>
    /// Критическое значение глюкозы
    /// </summary>
    public double GlucoseValue { get; set; }

    /// <summary>
    /// Время измерения
    /// </summary>
    public DateTime MeasurementTime { get; set; }

    /// <summary>
    /// Широта (если доступна геолокация)
    /// </summary>
    public double? Latitude { get; set; }

    /// <summary>
    /// Долгота (если доступна геолокация)
    /// </summary>
    public double? Longitude { get; set; }

    /// <summary>
    /// Точность геолокации в метрах
    /// </summary>
    public double? Accuracy { get; set; }

    /// <summary>
    /// Адрес (если удалось определить)
    /// </summary>
    public string? Address { get; set; }
}


