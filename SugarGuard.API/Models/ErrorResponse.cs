namespace SugarGuard.API.Models;

/// <summary>
/// Структура ответа об ошибке для клиентов API
/// </summary>
public class ErrorResponse
{
    public bool Success { get; set; } = false;
    public string ErrorCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public object? Details { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? TraceId { get; set; }
}
