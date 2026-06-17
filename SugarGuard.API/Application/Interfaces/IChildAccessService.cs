using SugarGuard.Domain.Enums;

namespace SugarGuard.API.Services
{
    /// <summary>
    /// Сервис проверки доступа к данным ребёнка
    /// </summary>
    public interface IChildAccessService
    {
        Guid? GetCurrentUserId(); // Возвращает UserId текущего пользователя из JWT-токена

        UserRole? GetCurrentUserRole(); // Возвращает роль текущего пользователя из JWT-токена

        /// <summary>
        /// Проверяет, имеет ли текущий пользователь доступ к данным ребёнка
        /// </summary>

        Task<bool> CanAccessChildAsync(Guid childId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Возвращает список ID всех детей, доступных текущему пользователю
        /// </summary>
        Task<IReadOnlyList<Guid>> GetAccessibleChildIdsAsync(CancellationToken cancellationToken = default);
    }
}
