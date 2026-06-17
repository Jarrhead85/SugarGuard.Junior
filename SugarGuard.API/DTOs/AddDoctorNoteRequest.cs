using System.ComponentModel.DataAnnotations;

namespace SugarGuard.API.DTOs;

/// <summary>
/// Запрос на добавление заметки врача
/// </summary>
public sealed class AddDoctorNoteRequest
{   
    public Guid? MeasurementId { get; init; } // ID привязанного замера

    // Текст заметки
    [Required]
    [MaxLength(2000, ErrorMessage = "Текст заметки не может превышать 2000 символов.")]
    public string Text { get; init; } = string.Empty;
   
    public bool IsImportant { get; init; } // Флаг — пометить заметку как важную
}
