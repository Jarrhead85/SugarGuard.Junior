using SugarGuard.API.DTOs;
using SugarGuard.Domain.Enums;

namespace SugarGuard.API.Application.Interfaces;

/// <summary>
/// CRUD профилей детей и привязка к родителю при создании
/// </summary>
public interface IChildrenService
{
    /// <summary>
    /// Страница списка детей, доступных текущему пользователю
    /// </summary>
    Task<PagedResult<ChildSummaryResponse>> GetAccessibleAsync(
        Guid userId,
        UserRole role,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Профиль ребёнка по ID или null, если не найден
    /// </summary>
    Task<ChildResponse?> GetByIdAsync(
        Guid childId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Создаёт ребёнка
    /// </summary>
    Task<CreateChildResult> CreateAsync(
        Guid userId,
        UserRole role,
        CreateChildRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Обновляет профиль ребёнка
    /// </summary>
    Task<ChildResponse?> UpdateAsync(
        Guid childId,
        UpdateChildRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Удаляет ребёнка и все связанные записи
    /// </summary>
    Task<bool> DeleteChildAsync(Guid childId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Загружает фото профиля ребёнка
    /// </summary>
    Task<string?> UploadPhotoAsync(
        Guid childId,
        Microsoft.AspNetCore.Http.IFormFile file,
        string uploadRoot,
        string publicBaseUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Удаляет фото профиля ребёнка
    /// </summary>
    Task<bool> DeletePhotoAsync(
        Guid childId,
        string uploadRoot,
        CancellationToken cancellationToken = default);
}
