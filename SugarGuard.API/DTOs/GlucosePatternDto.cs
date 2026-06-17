namespace SugarGuard.Application.Dashboard.Dto
{
    /// <summary>
    /// Тип обнаруженного паттерна
    /// </summary>
    public enum PatternType
    {
        RecurringHypo,
        RecurringHyper,
        NocturnalEpisode
    }

    /// <summary>
    /// Автоматически обнаруженный паттерн глюкозы
    /// </summary>
    public sealed record GlucosePatternDto
    {       
        public required PatternType PatternType { get; init; } // Тип паттерна

        public int PeakHour { get; init; } // Час суток (0–23), в который паттерн наиболее выражен

        public int OccurrenceDays { get; init; } // Число дней из последних 14, когда паттерн проявлялся
       
        public double AverageGlucoseInWindow { get; init; } // Среднее значение глюкозы в часовом окне
       
        public required string Description { get; init; } // Человекочитаемое описание паттерна на русском языке
    }
}
