using SugarGuard.API.DTOs;
using SugarGuard.Domain.Enums;

namespace SugarGuard.API.Application.Interfaces;

/// <summary>
/// Сервис админ операций: управление пользователями,
/// связями родитель-ребёнок, врач-ребёнок, инвайт-коды,
/// аудит-лог, воронка онбординга, системная статистика.
/// </summary>
public interface IAdminService
{
    // Пользователи
    /// <summary>
    /// Возвращает список пользователей с фильтрацией по роли
    /// </summary>
    Task<List<AdminUserResponse>> GetUsersAsync(
        UserRole? role,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает страницу пользователей для административного списка.
    /// </summary>
    Task<PagedResult<AdminUserResponse>> GetUsersPageAsync(
        UserRole? role,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает все профили детей для административного управления связями.
    /// </summary>
    Task<List<ChildResponse>> GetAllChildrenAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Изменяет роль пользователя и записывает событие в audit log
    /// </summary>
    Task<AdminUserResponse?> UpdateUserRoleAsync(
        Guid userId,
        string newRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Деактивирует пользователя (мягкое удаление: IsActive = false)
    /// </summary>
    Task<bool> DeactivateUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Массово изменяет статус учётных записей и пишет агрегированное событие в аудит.
    /// </summary>
    Task<int> SetUsersActivityAsync(
        IReadOnlyCollection<Guid> userIds,
        bool isActive,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    // Связи родитель-ребёнок
    /// <summary>
    /// Создаёт связь родитель-ребёнок.
    /// </summary>
    Task CreateParentChildLinkAsync(
        Guid parentUserId,
        Guid childId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Удаляет связь родитель-ребёнок.
    /// </summary>
    Task<bool> RemoveParentChildLinkAsync(
        Guid parentUserId,
        Guid childId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает список всех Parent–Child связей (для админ-страницы LinkManager).
    /// </summary>
    Task<List<AdminParentChildLinkResponse>> GetAllParentChildLinksAsync(
        CancellationToken cancellationToken = default);

    // Связи врач-ребёнок
    /// <summary>
    /// Создаёт связь врач-ребёнок.
    /// </summary>
    Task CreateDoctorChildLinkAsync(
        Guid doctorUserId,
        Guid childId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Удаляет связь врач-ребёнок.
    /// </summary>
    Task<bool> RemoveDoctorChildLinkAsync(
        Guid doctorUserId,
        Guid childId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает список всех Doctor–Child связей (для админ-страницы LinkManager).
    /// </summary>
    Task<List<AdminDoctorChildLinkResponse>> GetAllDoctorChildLinksAsync(
        CancellationToken cancellationToken = default);

    // Системная статистика
    /// <summary>
    /// Возвращает агрегированную статистику системы
    /// </summary>
    Task<AdminSystemStatsResponse> GetSystemStatsAsync(
        CancellationToken cancellationToken = default);

    // Инвайт-коды
    /// <summary>
    /// Возвращает список инвайт-кодов с опциональной фильтрацией по статусу
    /// </summary>
    Task<List<InviteCodeResponse>> GetInvitationsAsync(
        string? status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Создаёт новый инвайт-код для указанной роли (только Parent или Doctor)
    /// </summary>
    Task<InviteCodeResponse> CreateInvitationAsync(
        Guid childId,
        UserRole targetRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Отзывает инвайт-код. Отозвать можно только код со статусом Pending
    /// </summary>
    Task<bool?> RevokeInvitationAsync(
        Guid inviteCodeId,
        CancellationToken cancellationToken = default);

    // Аудит-лог
    /// <summary>
    /// Возвращает список записей аудит-лога с фильтрацией
    /// </summary>
    Task<List<AuditLogResponse>> GetAuditLogsAsync(
        Guid? actorUserId,
        string? action,
        DateTime? from,
        DateTime? to,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает страницу журнала аудита с фильтрацией на стороне базы данных.
    /// </summary>
    Task<PagedResult<AuditLogResponse>> GetAuditLogsPageAsync(
        Guid? actorUserId,
        string? action,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    // Онбординг
    /// <summary>
    /// Возвращает аналитику воронки онбординга
    /// </summary>

    Task<OnboardingFunnelResponse> GetOnboardingFunnelAsync(
        string? role,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default);
}
