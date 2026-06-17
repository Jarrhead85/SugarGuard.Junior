namespace SugarGuard.Shared.Constants;

/// <summary>
/// Ограничения и параметры кодов приглашений для привязки родителей и врачей к ребёнку
/// </summary>
public static class InviteCodeLimits
{
    // Срок жизни кода
    public static readonly TimeSpan CodeTtl = TimeSpan.FromHours(48); // Время жизни кода приглашения: 48 часов

    public const int CodeTtlHours = 48; // Срок жизни в часах

    // Формат кода
    public const int CodeLength = 8; // Длина генерируемой случайной части кода

    public const int GroupSize = 4; // Размер группы символов для форматирования с дефисом

    public const char GroupSeparator = '-'; // Разделитель групп символов в отображаемом коде

    public const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // Алфавит для генерации кода

    // Попытки ввода
    public const int MaxAttempts = 5; // Максимальное число неверных попыток ввода кода до его блокировки

    public const int MaxActiveCodesPerChild = 3; // Максимальное число активных

    /// <summary>
    /// Роли, для которых разрешена генерация кода приглашения
    /// </summary>
    public static readonly IReadOnlyList<string> AllowedRoles =
    [
        Roles.Parent,
        Roles.Doctor
    ];


    // Параметры таймера в клиентском UI
    public const int TimerTickSeconds = 1; // Интервал обновления таймера обратного отсчёта в UI   

    public const int ShowMinutesThresholdHours = 3; // Порог в часах, при котором UI переключается с отображения «осталось X ч» на «осталось X мин» для большей точности

    // Вспомогательные методы
    /// <summary>
    /// Форматирует код в отображаемый вид
    /// </summary>
    public static string Format(string rawCode)
    {
        if (rawCode.Length != CodeLength)
            throw new ArgumentException(
                $"Код должен содержать ровно {CodeLength} символов, " +
                $"передано: {rawCode.Length}.",
                nameof(rawCode));

        // ABCD1234 - ABCD-1234
        return string.Join(
            GroupSeparator,
            Enumerable.Range(0, CodeLength / GroupSize)
                      .Select(i => rawCode.Substring(i * GroupSize, GroupSize)));
    }

    /// <summary>
    /// Убирает разделители из отображаемого кода и приводит к верхнему регистру
    /// </summary>
    public static string Normalize(string displayCode) =>
        displayCode
            .Replace(GroupSeparator.ToString(), string.Empty)
            .Trim()
            .ToUpperInvariant();

    /// <summary>
    /// Проверяет, является ли переданная роль допустимой для кода приглашения
    /// </summary>
    public static bool IsRoleAllowed(string? role) =>
        role is not null && AllowedRoles.Contains(role);
}
