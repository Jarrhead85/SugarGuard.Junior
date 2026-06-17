using System.ComponentModel.DataAnnotations;

namespace SugarGuard.Shared.Dto;

// Регистрация нового пользователя
/// <summary>
/// Запрос на регистрацию нового пользователя
/// </summary>
public sealed class RegisterRequest
{
    [Required(ErrorMessage = "Имя обязательно для заполнения.")]
    [MaxLength(100, ErrorMessage = "Имя не должно превышать 100 символов.")]
    public string FirstName { get; init; } = string.Empty;

    [Required(ErrorMessage = "Фамилия обязательна для заполнения.")]
    [MaxLength(100, ErrorMessage = "Фамилия не должна превышать 100 символов.")]
    public string LastName { get; init; } = string.Empty;

    [Required(ErrorMessage = "Email обязателен для заполнения.")]
    [EmailAddress(ErrorMessage = "Некорректный формат email.")]
    [MaxLength(256, ErrorMessage = "Email не должен превышать 256 символов.")]
    public string Email { get; init; } = string.Empty;

    [Phone(ErrorMessage = "Некорректный формат номера телефона.")]
    [MaxLength(20, ErrorMessage = "Номер телефона не должен превышать 20 символов.")]
    public string? PhoneNumber { get; init; }

    [Required(ErrorMessage = "Пароль обязателен для заполнения.")]
    [MinLength(8, ErrorMessage = "Пароль должен содержать не менее 8 символов.")]
    [MaxLength(128, ErrorMessage = "Пароль не должен превышать 128 символов.")]
    public string Password { get; init; } = string.Empty;

    [Required(ErrorMessage = "Роль обязательна для заполнения.")]
    [MaxLength(32)]
    public string Role { get; init; } = string.Empty;
   
    [Range(typeof(bool), "true", "true",
        ErrorMessage = "Необходимо принять условия использования.")]
    public bool AgreedToTerms { get; init; } // Признак согласия с условиями использования
}

/// <summary>
/// Ответ на запрос регистрации нового пользователя
/// </summary>
public sealed class RegisterResponse
{
    public bool Success { get; init; } // Признак успешного создания аккаунта

    public Guid? UserId { get; init; }

    public string? Role { get; init; }

    public string? NextStep { get; init; }

    public string? MaskedEmail { get; init; }

    public string? ErrorMessage { get; init; }
}

// Создание профиля ребёнка

/// <summary>
/// Запрос на создание профиля ребёнка
/// </summary>
public sealed class CreateChildOnboardingRequest
{
    [Required(ErrorMessage = "Имя ребёнка обязательно для заполнения.")]
    [MaxLength(255, ErrorMessage = "Имя ребёнка не должно превышать 255 символов.")]
    public string FirstName { get; init; } = string.Empty;

    [Required(ErrorMessage = "Фамилия ребёнка обязательна для заполнения.")]
    [MaxLength(255, ErrorMessage = "Фамилия ребёнка не должна превышать 255 символов.")]
    public string LastName { get; init; } = string.Empty;

    [Required(ErrorMessage = "Дата рождения обязательна для заполнения.")]
    public DateOnly DateOfBirth { get; init; }

    [Required(ErrorMessage = "Тип диабета обязателен для заполнения.")]
    [MaxLength(32)]
    public string DiabetesType { get; init; } = string.Empty;

    public DateOnly? DiagnosisDate { get; init; } // Дата постановки диагноза

    [Range(5.0, 200.0, ErrorMessage = "Вес должен быть от 5 до 200 кг.")]
    public decimal? Weight { get; init; }

    [Range(50.0, 250.0, ErrorMessage = "Рост должен быть от 50 до 250 см.")]
    public decimal? Height { get; init; }

    [MaxLength(100)]
    public string TimeZoneId { get; init; } = "UTC";
   
    [MaxLength(500)]
    public string? InsulinScheme { get; init; } // Описание схемы инсулинотерапии в произвольной форме
}

/// <summary>
/// Ответ на создание профиля ребёнка
/// </summary>
public sealed class CreateChildOnboardingResponse
{
    public bool Success { get; init; }

    public Guid? ChildId { get; init; }

    public Guid? LinkId { get; init; }

    public string? NextStep { get; init; }

    public string? ErrorMessage { get; init; }
}

// Статус онбординга
/// <summary>
/// Текущий статус прохождения онбординга пользователем
/// </summary>
public sealed class OnboardingStatusResponse
{
    public bool IsCompleted { get; init; }

    public string CurrentStep { get; init; } = OnboardingStep.EmailVerification;

    public string? Role { get; init; }

    public bool IsEmailVerified { get; init; }

    public bool HasChild { get; init; }

    public bool HasDiabetesSettings { get; init; }

    public bool IsApprovedByAdmin { get; init; } // Для роли Doctor — признак верификации администратором

    public Guid? ChildId { get; init; }

    public int ProgressPercent { get; init; }
}

// Завершение онбординга
/// <summary>
/// Запрос на явное завершение онбординга пользователем
/// </summary>
public sealed class CompleteOnboardingRequest
{
    public Guid? SelectedChildId { get; init; }

    public bool WantsToConnectTelegram { get; init; } // Признак того, что пользователь хочет подключить Telegram-бота
}

/// <summary>
/// Ответ на завершение онбординга
/// </summary>
public sealed class CompleteOnboardingResponse
{
    public bool Success { get; init; }

    public string? NextAction { get; init; }

    public string? ErrorMessage { get; init; }
}

// Шаги онбординга
/// <summary>
/// Строковые константы шагов онбординга
/// </summary>
public static class OnboardingStep
{   
    public const string EmailVerification = "EmailVerification"; // Подтверждение email

    public const string CreateChild = "CreateChild"; // Создание профиля ребёнка

    public const string DiabetesSettings = "DiabetesSettings"; // Заполнение настроек диабета

    public const string AwaitAdminApproval = "AwaitAdminApproval"; // Ожидание верификации администратором

    public const string ConnectTelegram = "ConnectTelegram"; // Подключение Telegram-бота

    public const string Done = "Done";
}

// Типы диабета
/// <summary>
/// Строковые константы типов сахарного диабета
/// </summary>
public static class DiabetesType
{
    public const string Type1 = "Type1";

    public const string Type2 = "Type2";
   
    public const string Gestational = "Gestational"; // Гестационный диабет

    /// <summary>Другой / неуточнённый тип.</summary>
    public const string Other = "Other";

    /// <summary>
    /// Все допустимые значения типов диабета
    /// </summary>
    public static readonly IReadOnlyList<string> All =
    [
        Type1,
        Type2,
        Gestational,
        Other
    ];
}
