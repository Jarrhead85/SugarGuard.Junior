namespace SugarGuard.Application.Dashboard.Dto
{
    /// <summary>
    /// Агрегированная сводка для главного экрана родителя
    /// </summary>
    public sealed record ParentDashboardSummaryDto
    {       
        public required Guid ChildId { get; init; } // ID ребёнка
       
        public decimal? LatestGlucose { get; init; } // Последнее значение глюкозы
       
        public DateTime? LatestMeasurementTime { get; init; } // Время последнего измерения
       
        public string? LatestGlucoseStatus { get; init; } // Статус
       
        public string? LatestGlucoseUiState { get; init; } // UI-состояние для цветовой индикации

        public int? MinutesSinceLastMeasurement { get; init; } // Прошло минут с последнего измерения

        // KPI за последние 24 часа       
        public decimal? AverageGlucose24H { get; init; } // Среднее значение глюкозы за 24 ч
        
        public double TimeInRange24H { get; init; } // Time In Range 4.0–10.0 ммоль/л за 24 ч, в процентах
       
        public double TimeBelowRange24H { get; init; } // Время ниже нормы за 24 ч, в процентах
       
        public double TimeAboveRange24H { get; init; } // Время выше нормы 10.0 за 24 ч, в процентах
       
        public int HypoEpisodes24H { get; init; } // Количество гипо-эпизодов за 24 ч
       
        public int HyperEpisodes24H { get; init; } // Количество гипер-эпизодов за 24 ч
       
        public int CriticalEvents24H { get; init; } // Количество критических событий за 24 ч
       
        public int TotalMeasurements24H { get; init; } // Общее число измерений за 24 ч

        // Дельта

       
        public decimal? AverageGlucoseDelta { get; init; } // Изменение среднего глюкозы
       
        public double? TimeInRangeDelta { get; init; } // Изменение TIR

        // Хвостовые счётчики       
        public int PendingSyncConflicts { get; init; } // Количество неразрешённых конфликтов синхронизации
       
        public int PendingExportJobs { get; init; } // Количество заданий экспорта в очереди
       
        public int RecommendationsCount { get; init; } // Количество рекомендаций ИИ всего
    }
}
