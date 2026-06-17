namespace SugarGuard.Web.Models.Analytics;

/// <summary>
/// ViewModel статистики за период
/// </summary>
public sealed class StatisticsVm
{   
    public double AverageGlucose { get; init; } // Среднее значение глюкозы
   
    public double MinGlucose { get; init; } // Минимальное значение глюкозы за период
   
    public double MaxGlucose { get; init; } // Максимальное значение глюкозы за период
   
    public double StandardDeviation { get; init; } // Стандартное отклонение 
   
    public double Gmi { get; init; } // Индекс управления глюкозой
   
    public double TimeInTargetRange { get; init; } // Время в целевом диапазоне
   
    public double TimeBelowRange { get; init; } // Время ниже целевого диапазона
   
    public double TimeAboveRange { get; init; } // Время выше целевого диапазона

    public double TimeCriticallyLow { get; init; } // Время в критически низкой зоне
   
    public double TimeCriticallyHigh { get; init; } // Время в критически высокой зоне
   
    public int HypoEpisodes { get; init; } // Количество гипогликемических эпизодов за период
   
    public int HyperEpisodes { get; init; } // Количество гипергликемических эпизодов за период
   
    public int TotalMeasurements { get; init; } // Общее количество измерений за период
}

/// <summary>
/// ViewModel сравнения текущего периода с предыдущим аналогичным
/// </summary>
public sealed class PeriodComparisonVm
{   
    public double AverageGlucoseDelta { get; init; } // Дельта среднего значения глюкозы
   
    public double TimeInRangeDelta { get; init; } // Дельта TIR 
   
    public int HypoEpisodesDelta { get; init; } // Дельта числа гипо-эпизодов 
   
    public int HyperEpisodesDelta { get; init; } // Дельта числа гипер-эпизодов 
   
    public bool IsReliable { get; init; } // Признак надёжности сравнения
   
    public string? UnreliableReason { get; init; } // Причина ненадёжности сравнения 
}

/// <summary>
/// ViewModel выявленного паттерна глюкозы за 14 дней
/// </summary>
public sealed class GlucosePatternVm
{
    public string PatternType { get; init; } = string.Empty;// Тип паттерна

    public int PeakHour { get; init; } // Час пика паттерна
   
    public int OccurrenceDays { get; init; } // Количество дней из 14, в которых паттерн наблюдался
   
    public double AverageGlucoseInWindow { get; init; } // Среднее значение глюкозы в окне паттерна
   
    public string? Description { get; init; } // Человекочитаемое описание паттерна
}

/// <summary>
/// DTO ответа API для сравнения периодов
/// </summary>
internal sealed class PeriodComparisonDto
{
    public decimal AverageGlucoseDelta { get; init; }
    public decimal TimeInRangeDelta { get; init; }
    public int HypoEpisodesDelta { get; init; }
    public int HyperEpisodesDelta { get; init; }
    public bool IsReliable { get; init; }
    public string? UnreliableReason { get; init; }
}

/// <summary>
/// DTO ответа API для одного паттерна глюкозы
/// </summary>
internal sealed class GlucosePatternDto
{
    public string PatternType { get; init; } = string.Empty;
    public int PeakHour { get; init; }
    public int OccurrenceDays { get; init; }
    public decimal AverageGlucoseInWindow { get; init; }
    public string? Description { get; init; }
}
