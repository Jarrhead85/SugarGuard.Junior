using SugarGuard.Shared.Dto;

namespace SugarGuard.Junior.Services.Interfaces;

/// <summary>
/// Сервис управления связками ребёнка с родителями и врачами.
/// </summary>
public interface ILinkService
{
    /// <summary>Возвращает список привязанных родителей.</summary>
    Task<List<ParentChildLinkDto>> GetParentLinksAsync(Guid childId);

    /// <summary>Возвращает список привязанных врачей.</summary>
    Task<List<DoctorChildLinkDto>> GetDoctorLinksAsync(Guid childId);

    /// <summary>Генерирует код приглашения для родителя.</summary>
    Task<GenerateInviteCodeResponse> GenerateParentInviteCodeAsync(Guid childId, string? note = null);

    /// <summary>Генерирует код приглашения для врача.</summary>
    Task<GenerateInviteCodeResponse> GenerateDoctorInviteCodeAsync(Guid childId, string? note = null);

    /// <summary>Принимает код приглашения.</summary>
    Task<AcceptInviteCodeResponse> AcceptInviteCodeAsync(string code);

    /// <summary>Отзывает код приглашения.</summary>
    Task<RevokeInviteCodeResponse> RevokeInviteCodeAsync(Guid inviteCodeId);

    /// <summary>Получает входящие запросы на связку.</summary>
    Task<List<InviteCodeSummaryDto>> GetIncomingRequestsAsync();

    /// <summary>Подтверждает входящий запрос на связку.</summary>
    Task<LinkOperationResponse> ApproveLinkRequestAsync(Guid inviteCodeId);

    /// <summary>Отклоняет входящий запрос на связку.</summary>
    Task<LinkOperationResponse> RejectLinkRequestAsync(Guid inviteCodeId);

    /// <summary>Разрывает связь с родителем.</summary>
    Task<LinkOperationResponse> RemoveParentLinkAsync(Guid childId, Guid linkId);

    /// <summary>Разрывает связь с врачом.</summary>
    Task<LinkOperationResponse> RemoveDoctorLinkAsync(Guid childId, Guid linkId);
}
