using System.ComponentModel.DataAnnotations;

namespace SugarGuard.API.DTOs;

/// <summary>
/// Запрос на отписку от Push-уведомлений
/// </summary>
public sealed record UnsubscribePushRequest
{   
    [Required]
    public string Endpoint { get; init; } = string.Empty; // Endpoint браузерной подписки
}
