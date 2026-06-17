using SugarGuard.Application.Dashboard.Dto;

namespace SugarGuard.API.DTOs
{
    /// <summary>
    /// Сравнение статистики двух периодов равной длины
    /// </summary>
    public sealed record PeriodComparisonDto
    {       
        public required Guid ChildId { get; init; } // ID ребёнка
       
        public required PeriodStatisticsDto Current { get; init; } // Статистика текущего периода
       
        public required PeriodStatisticsDto Previous { get; init; } // Статистика предыдущего аналогичного периода

        public double AverageGlucoseDelta { get; init; } // Дельта среднего глюкозы
       
        public double TimeInRangeDelta { get; init; } // Дельта TIR в процентных пунктах
       
        public int HypoEpisodesDelta { get; init; } // Дельта числа гипо-эпизодов
       
        public int HyperEpisodesDelta { get; init; } // Дельта числа гипер-эпизодов
       
        public bool IsReliable { get; init; } // Достаточно ли данных в обоих периодах для надёжного сравнения       

        public string? UnreliableReason { get; init; } // Причина ненадёжности
    }
}
