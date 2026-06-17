using System.ComponentModel.DataAnnotations;
using SugarGuard.Shared.Validation;

namespace SugarGuard.API.DTOs;

/// <summary>
/// Запрос на проверку кода привязки от Telegram-бота
/// </summary>
public class VerifyConnectionCodeRequest
{
    [Required]
    [ConnectionCode]
    public string ConnectionCode { get; set; } = string.Empty; // Код привязки 

    [Required]
    public long TelegramUserId { get; set; } // Telegram ID родителя

    public string? TelegramUsername { get; set; } // Имя пользователя в Telegram
}
