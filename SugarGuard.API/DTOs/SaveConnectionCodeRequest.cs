using SugarGuard.Shared.Constants;
using SugarGuard.Shared.Validation;
using System.ComponentModel.DataAnnotations;

namespace SugarGuard.API.DTOs;

/// <summary>
/// Запрос на сохранение кода привязки
/// </summary>
public class SaveConnectionCodeRequest
{
    [Required]
    public Guid ChildId { get; set; } // ID ребёнка

    [Required]
    [StringLength(9, MinimumLength = 8)]
    [ConnectionCode]
    public string Code { get; set; } = string.Empty; // Сырой 8-символьный код привязки
}
