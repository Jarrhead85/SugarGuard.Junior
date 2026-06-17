namespace SugarGuard.Domain.Entities;

/// <summary>
/// Refresh-токен для обновления JWT без повторного ввода пароля
/// </summary>
public sealed class RefreshToken
{
    public long Id { get; set; } // Суррогатный первичный ключ 

    public string Token { get; set; } = string.Empty; // хэш токена

    public Guid UserId { get; set; } // Идентификатор пользователя

    public DateTime CreatedAt { get; set; } // Момент создания токена

    public DateTime ExpiresAt { get; set; } // Срок действия токена

    public bool IsRevoked { get; set; } = false; // Токен был явно отозван 

    public DateTime? RevokedAt { get; set; } // Момент отзыва токена

    public string? RevokedReason { get; set; } // Причина отзыва

    public string? ReplacedByToken { get; set; } // хэш токена
       
    public string? CreatedByIp { get; set; } // IP-адрес клиента в момент создания токена

    public string? CreatedByUserAgent { get; set; } // User-Agent клиента в момент создания токена

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt; // Токен истёк по времени

    public bool IsActive => !IsRevoked && !IsExpired; // Токен активен

    public User User { get; set; } = null!; // Пользователь — владелец токена
}
