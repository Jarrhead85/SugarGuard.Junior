// SugarGuard.Web/ViewModels/DoctorCohortSummaryVm.cs
namespace SugarGuard.Web.ViewModels;

/// <summary>Сводная статистика по всем пациентам врача.</summary>
public sealed record DoctorCohortSummaryVm(
    int      TotalPatients,
    int      PatientsWithCriticalToday,
    double   AverageTimeInTargetRange,
    int      PatientsWithoutMeasurementsToday,
    DateTime GeneratedAt);
