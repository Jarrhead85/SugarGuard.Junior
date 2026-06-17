using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SugarGuard.Domain.Entities
{
    /// <summary>
    /// Врачебная заметка, привязанная к ребёнку
    /// </summary>
    [Table("doctornotes")]
    public class DoctorNote
    {
        [Key]
        [Column("noteid")]
        public Guid NoteId { get; set; } // Уникальный идентификатор заметки

        [Column("doctoruserid")]
        public Guid DoctorUserId { get; set; } // ID врача-автора

        [Column("childid")]
        public Guid ChildId { get; set; } // ID ребёнка, к которому относится заметка

        [Column("measurementid")]
        public Guid? MeasurementId { get; set; } // ID конкретного измерения глюкозы, к которому прикреплена заметка

        [Required]
        [MaxLength(4000)]
        [Column("notetext")]
        public string NoteText { get; set; } = string.Empty; // Текст заметки

        [Column("isimportant")]
        public bool IsImportant { get; set; } // Флаг важности заметки

        [Column("createdat")]
        public DateTime CreatedAt { get; set; } // время создания заметки

        [Column("updatedat")]
        public DateTime? UpdatedAt { get; set; } // время последнего редактирования

        // Навигационные свойства
         [ForeignKey(nameof(DoctorUserId))]
        public User DoctorUser { get; set; } = null!; // Врач-автор

        [ForeignKey(nameof(ChildId))]
        public Child Child { get; set; } = null!; // Ребёнок, к которому относится заметка

        [ForeignKey(nameof(MeasurementId))]
        public Measurement? Measurement { get; set; } // Измерение, к которому прикреплена заметка
    }
}
