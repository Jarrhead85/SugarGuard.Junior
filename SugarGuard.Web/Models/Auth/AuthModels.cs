namespace SugarGuard.Web.Models.Auth;

/// <summary>
/// Результат попытки входа
/// </summary>
public sealed record LoginResult
{   
    public bool Success { get; init; } // Вход выполнен успешно
   
    public string? AccessToken { get; init; } // Access JWT-токен
   
    public string? RefreshToken { get; init; } // Refresh-токен
   
    public DateTime? ExpiresAt { get; init; } // Время истечения access-токена
   
    public string? Role { get; init; } // Роль пользователя
   
    public string? ErrorMessage { get; init; } // Сообщение об ошибке

    /// <summary>
    /// Фабричный метод для успешного результата
    /// </summary>
    public static LoginResult Ok(string accessToken, string? refreshToken,
        DateTime? expiresAt, string? role) =>
        new()
        {
            Success = true,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt,
            Role = role
        };

    /// <summary>
    /// Фабричный метод для ошибки
    /// </summary>
    public static LoginResult Fail(string message) =>
        new() { Success = false, ErrorMessage = message };
}

/// <summary>
/// Результат регистрации нового аккаунта
/// </summary>
public sealed record RegisterResult
{   
    public bool Success { get; init; } // Регистрация прошла успешно, письмо отправлено
       
    public string? Email { get; init; } // Email зарегистрированного пользователя
   
    public bool RequiresEmailVerification { get; init; } // Требуется ли подтверждение email
   
    public string? ErrorMessage { get; init; } // Сообщение об ошибке 

    /// <inheritdoc/>
    public static RegisterResult Ok(string email, bool requiresEmailVerification = true) =>
        new() { Success = true, Email = email, RequiresEmailVerification = requiresEmailVerification };

    /// <inheritdoc/>
    public static RegisterResult Fail(string message) =>
        new() { Success = false, ErrorMessage = message };
}

/// <summary>
/// Результат верификации email-кода
/// </summary>
public sealed record VerifyEmailResult
{   
    public bool IsValid { get; init; } // Код корректен и email подтверждён
   
    public string? Message { get; init; } // Дополнительное сообщение

    /// <inheritdoc/>
    public static VerifyEmailResult Ok() => new() { IsValid = true };

    /// <inheritdoc/>
    public static VerifyEmailResult Fail(string message) =>
        new() { IsValid = false, Message = message };
}
