namespace SugarGuard.API.DTOs;

/// <summary>
/// Запрос на логаут
/// </summary>
public sealed class LogoutRequest
{
    public string? RefreshToken { get; init; } // Refresh-токен для отзыва
}
