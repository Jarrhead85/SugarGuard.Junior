using SugarGuard.Web.Services;

namespace SugarGuard.Web.ViewModels;

/// <summary>
/// View-model списка пациентов для страницы врача.
/// </summary>
public sealed record PatientListVm
{
    /// <summary>
    /// Список пациентов, доступных врачу.
    /// </summary>
    public IReadOnlyList<DoctorPatientSummaryVm> Patients { get; init; } = Array.Empty<DoctorPatientSummaryVm>();

    /// <summary>
    /// Суммарные показатели по когорте пациентов.
    /// </summary>
    public DoctorCohortSummaryVm? CohortSummary { get; init; }

    /// <summary>
    /// Текст поискового запроса для клиентской фильтрации.
    /// </summary>
    public string SearchText { get; init; } = string.Empty;

    /// <summary>
    /// Текущий способ сортировки списка.
    /// </summary>
    public string SortBy { get; init; } = "lastMeasurement";

    /// <summary>
    /// Признак загрузки данных.
    /// </summary>
    public bool IsLoading { get; init; }

    /// <summary>
    /// Сообщение об ошибке загрузки или обработки.
    /// </summary>
    public string? ErrorMessage { get; init; }
}
