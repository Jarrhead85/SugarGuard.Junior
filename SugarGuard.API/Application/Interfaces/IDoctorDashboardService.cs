using SugarGuard.API.DTOs;

namespace SugarGuard.API.Application.Interfaces;

/// <summary>
/// Сервис дашборда врача
/// </summary>
public interface IDoctorDashboardService
{
    /// <summary>
    /// Возвращает список пациентов врача с ключевыми метриками
    /// </summary>
    Task<IReadOnlyList<DoctorPatientSummaryDto>> GetPatientsAsync(
        Guid doctorUserId,
        string? sortBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает список заметок врача по конкретному пациенту
    /// </summary>
    Task<IReadOnlyList<DoctorNoteDto>> GetNotesAsync(
        Guid doctorUserId,
        Guid childId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Добавляет заметку врача к пациенту, опционально привязывая к замеру
    /// </summary>
    Task<DoctorNoteDto> AddNoteAsync(
        Guid doctorUserId,
        Guid childId,
        AddDoctorNoteRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает агрегированную сводку по всей когорте пациентов врача
    /// </summary>
    Task<DoctorCohortSummaryDto> GetCohortSummaryAsync(
        Guid doctorUserId,
        CancellationToken cancellationToken = default);
}
