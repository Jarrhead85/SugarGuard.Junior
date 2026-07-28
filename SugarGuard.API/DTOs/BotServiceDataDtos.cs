using System.ComponentModel.DataAnnotations;

namespace SugarGuard.API.DTOs;

/// <summary>
/// Данные для добавления перекуса из Telegram-бота.
/// Идентификатор ребёнка берётся только из маршрута защищённого endpoint.
/// </summary>
public sealed class BotBackpackCreateRequest
{
    [Required]
    [StringLength(500, MinimumLength = 1)]
    public string SnackName { get; init; } = string.Empty;

    [Range(typeof(decimal), "0.01", "50")]
    public decimal BreadUnits { get; init; }
}
