namespace SugarGuard.Application.Dashboard.Dto
{
    /// <summary>
    /// Полная статистика за произвольный период
    /// </summary>
    public sealed record PeriodStatisticsDto
    {       
        public required Guid ChildId { get; init; } // ID ребёнка
       
        public required DateTime From { get; init; } // Начало периода
       
        public required DateTime To { get; init; } // Конец периода
       
        public int TotalMeasurements { get; init; } // Общее число измерений за период
       
        public double AverageGlucose { get; init; } // Среднее значение глюкозы
       
        public double MinGlucose { get; init; } // Минимальное значение глюкозы
        
        public double MaxGlucose { get; init; } // Максимальное значение глюкозы

        public double StandardDeviation { get; init; } // Стандартное отклонение       

        public double Gmi { get; init; } // Glucose Management Indicator GMI = 12.71 + 4.70587 × AverageGlucose
       
        public double TimeInTargetRange { get; init; } // Время в норме
       
        public double TimeBelowRange { get; init; } // Время ниже нормы
       
        public double TimeAboveRange { get; init; } // Время выше нормы
       
        public double TimeCriticallyLow { get; init; } // Время критически низкого сахара
       
        public double TimeCriticallyHigh { get; init; } // Время критически высокого сахара
       
        public int HypoEpisodes { get; init; } // Количество гипо-эпизодов
       
        public int HyperEpisodes { get; init; } // Количество гипер-эпизодов
       
        public int CriticalEpisodes { get; init; } // Количество критических эпизодов
       
        public DateTime GeneratedAt { get; init; } = DateTime.UtcNow; // Момент генерации
    }
}
