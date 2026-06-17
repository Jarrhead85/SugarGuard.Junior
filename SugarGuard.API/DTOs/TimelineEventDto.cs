namespace SugarGuard.Application.Dashboard.Dto
{
    /// <summary>
    /// Тип события в хронологической ленте
    /// </summary>
    public enum TimelineEventType
    {
        Measurement,
        SnackConsumed,
        CriticalAlert,
        DoctorNote
    }

    /// <summary>
    /// Одно событие в хронологической ленте родительского дашборда
    /// </summary>
    public sealed record TimelineEventDto
    {
        public required Guid EventId { get; init; } // Уникальный идентификатор события
      
        public required TimelineEventType EventType { get; init; } // Тип события

        public required DateTime OccurredAt { get; init; } // Время события 

        // Поля для Measurement      
        public decimal? GlucoseValue { get; init; } // Значение глюкозы
       
        public string? GlucoseUiState { get; init; } // UI-состояние глюкозы
       
        public string? DataSource { get; init; } // Источник данных

        // Поля для SnackConsumed       
        public string? SnackName { get; init; } // Название перекуса
       
        public decimal? BreadUnits { get; init; } // Хлебные единицы 

        // Поля для DoctorNote / CriticalAlert 
        public string? Notes { get; init; } // Текст заметки или описание алерта

        public bool IsImportant { get; init; } // Признак важности
    }
}
