using System.ComponentModel.DataAnnotations;
using SugarGuard.Shared.Constants;

namespace SugarGuard.Shared.Validation;

/// <summary>
/// Проверяет, что строка соответствует формату кода подключения
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class ConnectionCodeAttribute : ValidationAttribute
{
    public ConnectionCodeAttribute()
        : base("Код должен быть в формате ABCD-1234 (8 символов A–Z, 2–9).")
    { }

    /// <inheritdoc/>
    public override bool IsValid(object? value) =>
        ConnectionCodeFormat.IsValid(value as string, normalize: true);

    /// <inheritdoc/>
    public override string FormatErrorMessage(string name) =>
        $"Поле {name}: {ErrorMessageString}";
}
