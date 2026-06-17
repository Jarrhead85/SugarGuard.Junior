namespace SugarGuard.Web.Services.Models;

/// <summary>
/// Минимальное представление ProblemDetails-ответа от API
/// </summary>
internal sealed class ApiErrorResponse
{
    public string? Message { get; init; }
    public string? Detail { get; init; }
    public string? Error { get; init; }
}
