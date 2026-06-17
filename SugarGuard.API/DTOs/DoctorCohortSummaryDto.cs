namespace SugarGuard.API.DTOs;

/// <summary>
/// Агрегированная сводка по группе пациентов врача.
/// </summary>
public sealed class DoctorCohortSummaryDto
{   
    public int TotalPatients { get; init; } // Общее количество прикреплённых пациентов
       
    public int PatientsWithCriticalToday { get; init; } // Количество пациентов с критическими событиями за последние 24 часа
       
    public double AverageTimeInTargetRange { get; init; } // Среднее TIR по всей когорте за последние 7 дней
       
    public int PatientsWithoutMeasurementsToday { get; init; } // Количество пациентов без замеров за последние 24 часа
   
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow; // Дата и время генерации сводки UTC
}
