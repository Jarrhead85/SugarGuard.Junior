using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SugarGuard.Domain.Enums;
using SugarGuard.Junior.Services.Interfaces;
using SugarGuard.Shared.Constants;
using SugarGuard.Shared.Dto;

namespace SugarGuard.Junior.Services.Implementations;

/// <summary>
/// HTTP-сервис управления связками ребёнка с родителями и врачами.
/// </summary>
public class LinkService : ILinkService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LinkService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public LinkService(HttpClient httpClient, ILogger<LinkService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<ParentChildLinkDto>> GetParentLinksAsync(Guid childId)
    {
        try
        {
            var links = await GetChildLinksAsync(childId);
            return links.ParentLinks.Select(x => new ParentChildLinkDto
            {
                LinkId = x.LinkId,
                ParentUserId = x.UserId,
                ParentEmail = x.EmailForLogin,
                ParentTelegramUsername = x.TelegramId?.ToString(),
                ChildId = links.ChildId,
                CreatedAt = x.LinkedAt
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения списка родителей для ребёнка {ChildId}", childId);
            return [];
        }
    }

    public async Task<List<DoctorChildLinkDto>> GetDoctorLinksAsync(Guid childId)
    {
        try
        {
            var links = await GetChildLinksAsync(childId);
            return links.DoctorLinks.Select(x => new DoctorChildLinkDto
            {
                LinkId = x.LinkId,
                DoctorUserId = x.UserId,
                DoctorEmail = x.EmailForLogin,
                ChildId = links.ChildId,
                CreatedAt = x.LinkedAt
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения списка врачей для ребёнка {ChildId}", childId);
            return [];
        }
    }

    public Task<GenerateInviteCodeResponse> GenerateParentInviteCodeAsync(Guid childId, string? note = null) =>
        GenerateInviteCodeAsync(childId, UserRole.Parent);

    public Task<GenerateInviteCodeResponse> GenerateDoctorInviteCodeAsync(Guid childId, string? note = null) =>
        GenerateInviteCodeAsync(childId, UserRole.Doctor);

    public async Task<AcceptInviteCodeResponse> AcceptInviteCodeAsync(string code)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                "api/invite-codes/claim",
                new { code },
                JsonOptions);

            var result = await response.Content.ReadFromJsonAsync<AcceptInviteCodeResponse>(JsonOptions);
            if (result is not null)
            {
                return result;
            }

            return new AcceptInviteCodeResponse
            {
                Success = false,
                ErrorMessage = response.IsSuccessStatusCode
                    ? "Пустой ответ сервера"
                    : "Код не удалось активировать"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка принятия кода приглашения");
            return new AcceptInviteCodeResponse { Success = false, ErrorMessage = "Ошибка подключения" };
        }
    }

    public async Task<RevokeInviteCodeResponse> RevokeInviteCodeAsync(Guid inviteCodeId)
    {
        try
        {
            using var response = await _httpClient.DeleteAsync($"api/invite-codes/{inviteCodeId}");
            return new RevokeInviteCodeResponse
            {
                Success = response.IsSuccessStatusCode,
                ErrorMessage = response.IsSuccessStatusCode ? null : "Не удалось отозвать код"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка отзыва кода приглашения");
            return new RevokeInviteCodeResponse { Success = false, ErrorMessage = "Ошибка подключения" };
        }
    }

    public Task<List<InviteCodeSummaryDto>> GetIncomingRequestsAsync() =>
        Task.FromResult(new List<InviteCodeSummaryDto>());

    public Task<LinkOperationResponse> ApproveLinkRequestAsync(Guid inviteCodeId) =>
        Task.FromResult(new LinkOperationResponse
        {
            Success = false,
            ErrorMessage = "Входящие запросы не используются в текущем сценарии."
        });

    public Task<LinkOperationResponse> RejectLinkRequestAsync(Guid inviteCodeId) =>
        Task.FromResult(new LinkOperationResponse
        {
            Success = false,
            ErrorMessage = "Входящие запросы не используются в текущем сценарии."
        });

    public Task<LinkOperationResponse> RemoveParentLinkAsync(Guid linkId) =>
        Task.FromResult(new LinkOperationResponse
        {
            Success = false,
            ErrorMessage = "Отвязка родителя доступна в веб-кабинете."
        });

    public Task<LinkOperationResponse> RemoveDoctorLinkAsync(Guid linkId) =>
        Task.FromResult(new LinkOperationResponse
        {
            Success = false,
            ErrorMessage = "Отвязка врача доступна в веб-кабинете."
        });

    private async Task<GenerateInviteCodeResponse> GenerateInviteCodeAsync(Guid childId, UserRole targetRole)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                "api/invite-codes/generate",
                new { childId, targetRole = targetRole.ToString() },
                JsonOptions);

            if (!response.IsSuccessStatusCode)
            {
                var message = await response.Content.ReadAsStringAsync();
                _logger.LogWarning(
                    "Ошибка генерации кода: {Status} {Body}",
                    response.StatusCode,
                    message);
                return new GenerateInviteCodeResponse { ErrorMessage = "Не удалось сгенерировать код" };
            }

            var result = await response.Content.ReadFromJsonAsync<InviteCodeApiResponse>(JsonOptions);
            return MapInviteCodeResponse(result, targetRole.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка генерации кода приглашения для {Role}", targetRole);
            return new GenerateInviteCodeResponse { ErrorMessage = "Ошибка подключения" };
        }
    }

    private async Task<ChildAccessLinksApiResponse> GetChildLinksAsync(Guid childId)
    {
        using var response = await _httpClient.GetAsync($"api/invite-codes/{childId}/links");
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ChildAccessLinksApiResponse>(JsonOptions)
            ?? new ChildAccessLinksApiResponse { ChildId = childId };
    }

    private static GenerateInviteCodeResponse MapInviteCodeResponse(InviteCodeApiResponse? result, string targetRole)
    {
        if (result is null)
        {
            return new GenerateInviteCodeResponse { ErrorMessage = "Пустой ответ сервера" };
        }

        return new GenerateInviteCodeResponse
        {
            InviteCodeId = result.InviteCodeId,
            DisplayCode = InviteCodeLimits.Format(InviteCodeLimits.Normalize(result.Code)),
            ExpiresAt = result.ExpiresAt,
            ActiveCodesCount = result.IsActive ? 1 : 0,
            TargetRole = result.TargetRole ?? targetRole
        };
    }

    private sealed class InviteCodeApiResponse
    {
        public Guid InviteCodeId { get; init; }
        public string Code { get; init; } = string.Empty;
        public string? TargetRole { get; init; }
        public DateTime ExpiresAt { get; init; }
        public bool IsActive { get; init; }
    }

    private sealed class ChildAccessLinksApiResponse
    {
        public Guid ChildId { get; init; }
        public List<LinkedAccessUserApiResponse> ParentLinks { get; init; } = [];
        public List<LinkedAccessUserApiResponse> DoctorLinks { get; init; } = [];
    }

    private sealed class LinkedAccessUserApiResponse
    {
        public Guid LinkId { get; init; }
        public Guid UserId { get; init; }
        public string? EmailForLogin { get; init; }
        public long? TelegramId { get; init; }
        public DateTime LinkedAt { get; init; }
    }
}
