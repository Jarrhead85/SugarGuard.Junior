namespace SugarGuard.API.DTOs;

/// <summary>
/// Краткая карточка пациента для списка врача
/// </summary>
public sealed class DoctorPatientSummaryDto
{
    public Guid LinkId { get; init; } // ID связи врач-ребёнок

    public Guid ChildId { get; init; } // ID пациента

    public string FirstName { get; init; } = string.Empty; // Имя пациента

    public string LastName { get; init; } = string.Empty; // Фамилия пациента

    public string DiabetesType { get; init; } = string.Empty; // Тип диабета

    public DateOnly DateOfBirth { get; init; } // Дата рождения

    public decimal? LatestGlucose { get; init; } // Последнее значение глюкозы

    public DateTime? LatestMeasurementTime { get; init; } // Время последнего замера

    public string? LatestGlucoseUiState { get; init; } // UI-состояние последнего замера

    public double TimeInTargetRange { get; init; } // Время нахождения в целевом диапазоне за последние 7 дней

    public int CriticalEventsLast7Days { get; init; } // Количество критических событий за последние 7 дней

    public int MeasurementsLast7Days { get; init; } // Общее число замеров за последние 7 дней
}
