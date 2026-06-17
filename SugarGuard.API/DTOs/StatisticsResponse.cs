namespace SugarGuard.API.DTOs;

/// <summary>
/// DTO для ответа со статистическими данными измерений
/// </summary>
public class StatisticsResponse
{
    public Guid ChildId { get; set; } // ID ребёнка

    public string Period { get; set; } = string.Empty; // Период статистики

    public DateTime FromDate { get; set; } // Начальная дата периода

    public DateTime ToDate { get; set; } // Конечная дата периода

    public int TotalMeasurements { get; set; } // Общее количество измерений

    public double AverageGlucose { get; set; } // Среднее значение глюкозы 

    public double MinGlucose { get; set; } // Минимальное значение глюкозы

    public double MaxGlucose { get; set; } // Максимальное значение глюкозы

    public double StandardDeviation { get; set; } // Стандартное отклонение

    public double TimeInTargetRange { get; set; } // Процент времени в целевом диапазоне (4.0-10.0)

    public int HypoEpisodes { get; set; } // Количество гипогликемических эпизодов

    public int HyperEpisodes { get; set; } // Количество гипергликемических эпизодов

    public int CriticalEpisodes { get; set; } // Количество критических эпизодов

    public List<MeasurementResponse> Measurements { get; set; } = new(); // Список измерений за период

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow; // Время генерации статистики
}
