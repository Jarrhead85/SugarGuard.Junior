namespace SugarGuard.API.Application.Interfaces;

/// <summary>
/// Удаляет PHI из строки 
/// </summary>
public interface IAuditDetailsRedactor
{
    /// <summary>
    /// Преобразовать строку <paramref name="details"/>, удалив PHI
    /// </summary>
    string? Redact(string? details);
}
