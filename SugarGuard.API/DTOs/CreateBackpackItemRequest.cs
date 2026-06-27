using System.ComponentModel.DataAnnotations;

namespace SugarGuard.API.DTOs;

/// <summary>
/// DTO для создания нового перекуса в рюкзаке
/// </summary>
public class CreateBackpackItemRequest
{
    /// <summary>
    /// Optional client-generated identifier. Mobile uses this to keep local and server records aligned.
    /// </summary>
    public Guid? BackpackItemId { get; set; }

    [Required]
    public Guid ChildId { get; set; }

    [Required]
    [MaxLength(500, ErrorMessage = "Название перекуса не должно превышать 500 символов")]
    [MinLength(1, ErrorMessage = "Название перекуса не может быть пустым")]
    public string SnackName { get; set; } = string.Empty;

    [Required]
    [Range(0, 99.99, ErrorMessage = "Хлебные единицы должны быть в диапазоне 0-99.99")]
    public decimal BreadUnits { get; set; }

    [MaxLength(50)]
    public string? AddedBy { get; set; } = "parent";
}
