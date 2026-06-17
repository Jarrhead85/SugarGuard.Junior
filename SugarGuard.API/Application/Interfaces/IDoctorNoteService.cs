using SugarGuard.API.DTOs;

namespace SugarGuard.API.Services
{
    /// <summary>
    /// Сервис управления врачебными заметками
    /// </summary>
    public interface IDoctorNoteService
    {
        /// <summary>
        /// Возвращает страницу заметок для указанного ребёнка
        /// </summary>
        Task<PagedResult<DoctorNoteDto>> GetByChildAsync(
            Guid childId,
            int page,
            int pageSize,
            bool onlyImportant,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Возвращает заметки, прикреплённые к конкретному измерению
        /// </summary>
        Task<IReadOnlyList<DoctorNoteDto>> GetByMeasurementAsync(
            Guid measurementId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Возвращает одну заметку по ID
        /// </summary>
        Task<DoctorNoteDto?> GetByIdAsync(
            Guid noteId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Создаёт новую врачебную заметку
        /// </summary>
        Task<DoctorNoteDto> CreateAsync(
            Guid doctorUserId,
            CreateDoctorNoteRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Редактирует существующую заметку
        /// </summary>
        Task<DoctorNoteDto?> UpdateAsync(
            Guid noteId,
            Guid doctorUserId,
            UpdateDoctorNoteRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Удаляет заметку.
        /// </summary> 
        Task<bool> DeleteAsync(
            Guid noteId,
            Guid doctorUserId,
            bool isAdmin,
            CancellationToken cancellationToken = default);
    }
}
