using System.Text.Json.Serialization;

namespace SugarGuard.API.DTOs;

/// <summary>
/// Ответ на успешный логин 
/// </summary>
public sealed class LoginResponse
{   
    public bool Success { get; init; } // Успешность операции
   
    public Guid? UserId { get; init; } // ID пользователя в системе
   
    public string? Role { get; init; } // Роль пользователя
   
    public IReadOnlyCollection<string>? Permissions { get; init; } // Список разрешений, доступных данной роли

    public string? AccessToken { get; init; } // Краткосрочный access-токен

    public string? RefreshToken { get; init; } // Долгосрочный refresh-токен
   
    public DateTime? ExpiresAt { get; init; } // Время истечения access-токена

    public string? Message { get; init; } // Сообщение об успехе
   
    public string? ErrorMessage { get; init; } // Сообщение об ошибке
}

/// <summary>
/// Ответ на ротацию токенов
/// </summary>
public sealed class RefreshTokenResponse
{   
    public string AccessToken { get; init; } = string.Empty; // Новый access-токен
   
    public string RefreshToken { get; init; } = string.Empty; // Новый refresh-токен
   
    public DateTime AccessTokenExpiresAt { get; init; } // Время истечения нового access-токена
   
    public DateTime RefreshTokenExpiresAt { get; init; } // Время истечения нового refresh-токена
}

/// <summary>
/// Запрос на ротацию токенов
/// </summary>
public sealed class RefreshTokenRequest
{
    [JsonPropertyName("accessToken")]
    public string AccessToken { get; init; } = string.Empty; // Текущий access-токен
   
    [JsonPropertyName("refreshToken")]
    public string RefreshToken { get; init; } = string.Empty; // Действующий refresh-токен
}   
