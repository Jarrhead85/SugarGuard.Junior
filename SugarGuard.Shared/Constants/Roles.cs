
namespace SugarGuard.Shared.Constants;

/// <summary>
/// Строковые константы ролей для использования
/// </summary>
public static class Roles
{
    public const string Parent = "Parent";

    public const string Doctor = "Doctor";

    public const string Admin = "Admin";

    public const string SupportAdmin = "SupportAdmin"; // Вспомогательный администратор. // Не может изменять роли других Admin/SupportAdmin.

    public const string ChildDevice = "ChildDevice"; // Устройство ребёнка (мобильное приложение)

    public const string ServiceAccount = "ServiceAccount";// Сервисный аккаунт — используется Telegram-ботом и другими внутренними сервисами для межсервисного взаимодействия

    /// <summary>
    /// Роли, которым разрешён доступ к данным ребёнка (чтение)
    /// </summary>
    public const string AnyDataReader =
        $"{Parent},{Doctor},{Admin},{SupportAdmin},{ServiceAccount}";

    /// <summary>
    /// Роли с правами администрирования системы
    /// </summary>
    public const string AnyAdmin =
        $"{Admin},{SupportAdmin}";

    /// <summary>
    /// Роли, которым разрешено просматривать кабинет врача
    /// </summary>
    public const string DoctorOrAdmin =
        $"{Doctor},{Admin},{SupportAdmin}";

    // Вспомогательные методы
    /// <summary>
    /// Проверяет, является ли переданная строка известной ролью системы
    /// </summary>
    public static bool IsValid(string? role) => role switch
    {
        Parent => true,
        Doctor => true,
        Admin => true,
        SupportAdmin => true,
        ChildDevice => true,
        ServiceAccount => true,
        _ => false
    };

    /// <summary>
    /// Возвращает все роли, которые может назначить администратор
    /// </summary>
    public static IReadOnlyList<string> AssignableByAdmin =>
    [
        Parent,
        Doctor,
        Admin,
        SupportAdmin
    ];
}
