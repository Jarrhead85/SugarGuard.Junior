using System.ComponentModel.DataAnnotations;

namespace SugarGuard.API.DTOs;

/// <summary>
/// DTO для обновления позиции рюкзака ребёнка.
/// </summary>
public sealed class UpdateBackpackItemRequest
{
    [Required]
    [MaxLength(500, ErrorMessage = "Название позиции не должно превышать 500 символов")]
    [MinLength(1, ErrorMessage = "Название позиции не может быть пустым")]
    public string SnackName { get; init; } = string.Empty;

    [Required]
    [Range(0, 99.99, ErrorMessage = "Хлебные единицы должны быть в диапазоне 0-99.99")]
    public decimal BreadUnits { get; init; }
}
