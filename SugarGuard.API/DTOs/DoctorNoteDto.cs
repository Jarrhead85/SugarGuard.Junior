namespace SugarGuard.API.DTOs
{
    /// <summary>
    /// Врачебная заметка
    /// </summary>
    public class DoctorNoteDto
    {
        public Guid NoteId { get; init; } // ID заметки

        public Guid DoctorUserId { get; init; } // ID врача-автора

        public string DoctorName { get; init; } = string.Empty; // Отображаемое имя врача

        public Guid ChildId { get; init; } // ID ребёнка

        public Guid? MeasurementId { get; init; } // ID измерения, к которому прикреплена заметка

        public string NoteText { get; init; } = string.Empty; // Текст заметки

        public bool IsImportant { get; init; } // Флаг важности заметки

        public DateTime CreatedAt { get; init; } // UTC-время создания

        public DateTime? UpdatedAt { get; init; } // UTC-время последнего редактирования
    }
}
