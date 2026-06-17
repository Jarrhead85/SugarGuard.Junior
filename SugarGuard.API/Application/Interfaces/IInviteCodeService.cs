using SugarGuard.API.DTOs;
using SugarGuard.Domain.Enums;

namespace SugarGuard.API.Services
{
    /// <summary>
    /// Сервис управления одноразовыми кодами приглашений
    /// </summary>
    public interface IInviteCodeService
    {
        /// <summary>
        /// Генерирует новый код приглашения для ребёнка
        /// </summary>
        Task<InviteCodeResponse> GenerateAsync(
            Guid childId,
            UserRole targetRole,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Принимает код приглашения от пользователя на веб-сайте
        /// </summary>
        Task<ClaimInviteCodeResult> ClaimAsync(
            string code,
            Guid claimedByUserId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Возвращает список активных кодов приглашений для ребёнка
        /// </summary>
        Task<IReadOnlyList<InviteCodeResponse>> GetActiveAsync(
            Guid childId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Отзывает код приглашения досрочно
        /// </summary>
        Task<bool> RevokeAsync(
            Guid inviteCodeId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Помечает истёкшие коды статусом Expired
        /// </summary>
        Task<int> PurgeExpiredAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Возвращает текущие связки ребёнка с родителями и врачами
        /// </summary>
        Task<ChildAccessLinksResponse> GetChildLinksAsync(
            Guid childId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Удаляет связку ребёнка с родителем или врачом
        /// </summary>
        Task<UnlinkResult> UnlinkAsync(
            Guid childId,
            string linkType,
            Guid linkId,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Результат операции удаления связки
    /// </summary>
    public enum UnlinkResult
    {
        Success,
        NotFound,
        InvalidLinkType
    }
}
