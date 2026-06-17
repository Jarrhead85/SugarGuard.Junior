using System.ComponentModel.DataAnnotations;

namespace SugarGuard.API.DTOs
{
    /// <summary>
    /// Запрос на создание врачебной заметки
    /// </summary>
    public class CreateDoctorNoteRequest
    {       
        [Required]
        public Guid ChildId { get; set; } // ID ребёнка, к которому относится заметка
       
        public Guid? MeasurementId { get; set; } // ID измерения, к которому прикрепляется заметка
       
        [Required]
        [StringLength(4000, MinimumLength = 1,
            ErrorMessage = "Текст заметки должен содержать от 1 до 4000 символов.")]
        public string NoteText { get; set; } = string.Empty; // Текст заметки
       
        public bool IsImportant { get; set; } // Отметить заметку как важную
    }
}
