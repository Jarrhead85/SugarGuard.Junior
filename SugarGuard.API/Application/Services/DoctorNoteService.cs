using Microsoft.EntityFrameworkCore;
using SugarGuard.API.Data;
using SugarGuard.API.DTOs;
using SugarGuard.Domain.Entities;

namespace SugarGuard.API.Services
{
    /// <summary>
    /// Реализация сервиса заметок
    /// </summary>
    public class DoctorNoteService : IDoctorNoteService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<DoctorNoteService> _logger;

        public DoctorNoteService(AppDbContext db, ILogger<DoctorNoteService> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<PagedResult<DoctorNoteDto>> GetByChildAsync(
            Guid childId,
            int page,
            int pageSize,
            bool onlyImportant,
            CancellationToken cancellationToken = default)
        {
            // Защита от некорректных параметров пагинации
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = _db.DoctorNotes
                .AsNoTracking()
                .Include(n => n.DoctorUser)
                .Where(n => n.ChildId == childId);

            if (onlyImportant)
                query = query.Where(n => n.IsImportant);

            var totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .OrderByDescending(n => n.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(n => MapToDto(n))
                .ToListAsync(cancellationToken);

            _logger.LogDebug(
                "GetByChild: ChildId={ChildId} Page={Page} PageSize={PageSize} Total={Total}",
                childId, page, pageSize, totalCount);

            return new PagedResult<DoctorNoteDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<DoctorNoteDto>> GetByMeasurementAsync(
            Guid measurementId,
            CancellationToken cancellationToken = default)
        {
            var notes = await _db.DoctorNotes
                .AsNoTracking()
                .Include(n => n.DoctorUser)
                .Where(n => n.MeasurementId == measurementId)
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => MapToDto(n))
                .ToListAsync(cancellationToken);

            return notes;
        }

        /// <inheritdoc/>
        public async Task<DoctorNoteDto?> GetByIdAsync(
            Guid noteId,
            CancellationToken cancellationToken = default)
        {
            var note = await _db.DoctorNotes
                .AsNoTracking()
                .Include(n => n.DoctorUser)
                .FirstOrDefaultAsync(n => n.NoteId == noteId, cancellationToken);

            return note is null ? null : MapToDto(note);
        }

        /// <inheritdoc/>
        public async Task<DoctorNoteDto> CreateAsync(
            Guid doctorUserId,
            CreateDoctorNoteRequest request,
            CancellationToken cancellationToken = default)
        {
            // Проверяем что врач имеет доступ к этому ребёнку.
            var hasAccess = await _db.DoctorChildLinks
                .AnyAsync(
                    l => l.DoctorUserId == doctorUserId
                      && l.ChildId == request.ChildId
                      && l.IsActive,
                    cancellationToken);

            if (!hasAccess)
            {
                _logger.LogWarning(
                    "Врач {DoctorUserId} попытался создать заметку без связи с ребёнком {ChildId}.",
                    doctorUserId, request.ChildId);
                throw new UnauthorizedAccessException(
                    "Врач не связан с этим ребёнком. Доступ запрещён.");
            }

            // Если указано MeasurementId, то убеждаемся что измерение принадлежит этому ребёнку
            if (request.MeasurementId.HasValue)
            {
                var measurementExists = await _db.Measurements
                    .AnyAsync(
                        m => m.MeasurementId == request.MeasurementId.Value
                             && m.ChildId == request.ChildId,
                        cancellationToken);

                if (!measurementExists)
                    throw new KeyNotFoundException(
                        $"Измерение {request.MeasurementId} не найдено для ребёнка {request.ChildId}.");
            }

            var note = new DoctorNote
            {
                NoteId = Guid.NewGuid(),
                DoctorUserId = doctorUserId,
                ChildId = request.ChildId,
                MeasurementId = request.MeasurementId,
                NoteText = request.NoteText.Trim(),
                IsImportant = request.IsImportant,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = null
            };

            _db.DoctorNotes.Add(note);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Создана врачебная заметка. NoteId={NoteId} DoctorId={DoctorId} ChildId={ChildId}",
                note.NoteId, doctorUserId, request.ChildId);

            var created = await _db.DoctorNotes
                .AsNoTracking()
                .Include(n => n.DoctorUser)
                .FirstAsync(n => n.NoteId == note.NoteId, cancellationToken);

            return MapToDto(created);
        }

        /// <inheritdoc/>
        public async Task<DoctorNoteDto?> UpdateAsync(
            Guid noteId,
            Guid doctorUserId,
            UpdateDoctorNoteRequest request,
            CancellationToken cancellationToken = default)
        {
            var note = await _db.DoctorNotes
                .Include(n => n.DoctorUser)
                .FirstOrDefaultAsync(n => n.NoteId == noteId, cancellationToken);

            if (note is null)
                return null;

            // Редактировать может только автор
            if (note.DoctorUserId != doctorUserId)
            {
                _logger.LogWarning(
                    "Попытка редактирования чужой заметки. NoteId={NoteId} RequestBy={By} Author={Author}",
                    noteId, doctorUserId, note.DoctorUserId);
                throw new UnauthorizedAccessException(
                    "Редактировать можно только собственные заметки.");
            }

            note.NoteText = request.NoteText.Trim();
            note.IsImportant = request.IsImportant;
            note.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Обновлена врачебная заметка. NoteId={NoteId} DoctorId={DoctorId}",
                noteId, doctorUserId);

            return MapToDto(note);
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteAsync(
            Guid noteId,
            Guid doctorUserId,
            bool isAdmin,
            CancellationToken cancellationToken = default)
        {
            var note = await _db.DoctorNotes
                .FirstOrDefaultAsync(n => n.NoteId == noteId, cancellationToken);

            if (note is null)
                return false;

            // Удалять может автор или администратор
            if (!isAdmin && note.DoctorUserId != doctorUserId)
            {
                _logger.LogWarning(
                    "Попытка удаления чужой заметки. NoteId={NoteId} RequestBy={By} Author={Author}",
                    noteId, doctorUserId, note.DoctorUserId);
                throw new UnauthorizedAccessException(
                    "Удалять можно только собственные заметки.");
            }

            _db.DoctorNotes.Remove(note);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Удалена врачебная заметка. NoteId={NoteId} By={By} IsAdmin={IsAdmin}",
                noteId, doctorUserId, isAdmin);

            return true;
        }

        /// <summary>
        /// Преобразует <see cref="DoctorNote"/> в <see cref="DoctorNoteDto"/>.
        /// </summary>
        private static DoctorNoteDto MapToDto(DoctorNote note) => new()
        {
            NoteId = note.NoteId,
            DoctorUserId = note.DoctorUserId,
            DoctorName = $"{note.DoctorUser.EncryptedFirstName} {note.DoctorUser.EncryptedLastName}".Trim(),
            ChildId = note.ChildId,
            MeasurementId = note.MeasurementId,
            NoteText = note.NoteText,
            IsImportant = note.IsImportant,
            CreatedAt = note.CreatedAt,
            UpdatedAt = note.UpdatedAt
        };
    }
}
