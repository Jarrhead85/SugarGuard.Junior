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

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
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
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
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
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogError(ex, "Ошибка получения списка врачей для ребёнка {ChildId}", childId);
            return [];
        }
    }

    public Task<GenerateInviteCodeResponse> GenerateParentInviteCodeAsync(Guid childId, string? note = null) =>
        GenerateInviteCodeAsync(childId, UserRole.Parent, note);

    public Task<GenerateInviteCodeResponse> GenerateDoctorInviteCodeAsync(Guid childId, string? note = null) =>
        GenerateInviteCodeAsync(childId, UserRole.Doctor, note);

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
                    ? "Сервер вернул пустой ответ."
                    : "Код не удалось активировать."
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogError(ex, "Ошибка принятия кода приглашения");
            return new AcceptInviteCodeResponse
            {
                Success = false,
                ErrorMessage = "Ошибка подключения."
            };
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
                ErrorMessage = response.IsSuccessStatusCode ? null : "Не удалось отозвать код."
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "Ошибка отзыва кода приглашения {InviteCodeId}", inviteCodeId);
            return new RevokeInviteCodeResponse
            {
                Success = false,
                ErrorMessage = "Ошибка подключения."
            };
        }
    }

    public Task<List<InviteCodeSummaryDto>> GetIncomingRequestsAsync() =>
        Task.FromResult(new List<InviteCodeSummaryDto>());

    public Task<LinkOperationResponse> ApproveLinkRequestAsync(Guid inviteCodeId) =>
        Task.FromResult(new LinkOperationResponse
        {
            Success = false,
            ErrorMessage = "Входящие запросы не используются: родитель вводит код в веб-кабинете."
        });

    public Task<LinkOperationResponse> RejectLinkRequestAsync(Guid inviteCodeId) =>
        Task.FromResult(new LinkOperationResponse
        {
            Success = false,
            ErrorMessage = "Входящие запросы не используются: родитель вводит код в веб-кабинете."
        });

    public Task<LinkOperationResponse> RemoveParentLinkAsync(Guid childId, Guid linkId) =>
        RemoveLinkAsync(childId, "parent", linkId);

    public Task<LinkOperationResponse> RemoveDoctorLinkAsync(Guid childId, Guid linkId) =>
        RemoveLinkAsync(childId, "doctor", linkId);

    private async Task<GenerateInviteCodeResponse> GenerateInviteCodeAsync(
        Guid childId,
        UserRole targetRole,
        string? note)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                "api/invite-codes/generate",
                new { childId, targetRole = (int)targetRole, note },
                JsonOptions);

            if (!response.IsSuccessStatusCode)
            {
                var message = await response.Content.ReadAsStringAsync();
                _logger.LogWarning(
                    "Ошибка генерации кода: {Status} {Body}",
                    response.StatusCode,
                    message);

                return new GenerateInviteCodeResponse
                {
                    ErrorMessage = ExtractErrorMessage(message, response.StatusCode)
                };
            }

            var result = await response.Content.ReadFromJsonAsync<InviteCodeApiResponse>(JsonOptions);
            return MapInviteCodeResponse(result, targetRole.ToString());
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogError(ex, "Ошибка генерации кода приглашения для {Role}", targetRole);
            return new GenerateInviteCodeResponse
            {
                ErrorMessage = "Ошибка подключения. Проверьте интернет и попробуйте ещё раз."
            };
        }
    }

    private async Task<LinkOperationResponse> RemoveLinkAsync(Guid childId, string linkType, Guid linkId)
    {
        try
        {
            using var response = await _httpClient.DeleteAsync(
                $"api/invite-codes/{childId}/links/{linkType}/{linkId}");

            if (response.IsSuccessStatusCode)
            {
                return new LinkOperationResponse { Success = true };
            }

            var message = await response.Content.ReadAsStringAsync();
            _logger.LogWarning(
                "Не удалось удалить связь {LinkType}. ChildId={ChildId} LinkId={LinkId} Status={Status} Body={Body}",
                linkType,
                childId,
                linkId,
                response.StatusCode,
                message);

            return new LinkOperationResponse
            {
                Success = false,
                ErrorMessage = response.StatusCode == System.Net.HttpStatusCode.NotFound
                    ? "Связь уже удалена или не найдена."
                    : "Не удалось удалить связь."
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "Ошибка удаления связи {LinkType}. ChildId={ChildId} LinkId={LinkId}", linkType, childId, linkId);
            return new LinkOperationResponse
            {
                Success = false,
                ErrorMessage = "Ошибка подключения."
            };
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
            return new GenerateInviteCodeResponse { ErrorMessage = "Сервер вернул пустой ответ." };
        }

        return new GenerateInviteCodeResponse
        {
            InviteCodeId = result.InviteCodeId,
            DisplayCode = FormatInviteCode(result.Code),
            ExpiresAt = result.ExpiresAt,
            ActiveCodesCount = result.IsActive ? 1 : 0,
            TargetRole = GetTargetRoleName(result.TargetRole, targetRole)
        };
    }

    private static string FormatInviteCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return string.Empty;
        }

        var normalized = InviteCodeLimits.Normalize(code);
        return normalized.Length == InviteCodeLimits.CodeLength
            ? InviteCodeLimits.Format(normalized)
            : code.Trim();
    }

    private static string GetTargetRoleName(JsonElement? targetRole, string fallback)
    {
        if (targetRole is null)
        {
            return fallback;
        }

        return targetRole.Value.ValueKind switch
        {
            JsonValueKind.String => targetRole.Value.GetString() ?? fallback,
            JsonValueKind.Number when targetRole.Value.TryGetInt32(out var value)
                && Enum.IsDefined(typeof(UserRole), value) => ((UserRole)value).ToString(),
            _ => fallback
        };
    }

    private static string ExtractErrorMessage(string body, System.Net.HttpStatusCode statusCode)
    {
        if (statusCode == System.Net.HttpStatusCode.Forbidden)
        {
            return "Нет доступа к этому профилю ребёнка. Выйдите из приложения и войдите заново.";
        }

        if (statusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            return "Сессия истекла. Войдите в приложение ещё раз.";
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return "Не удалось сгенерировать код.";
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (root.TryGetProperty("message", out var message)
                && message.ValueKind == JsonValueKind.String)
            {
                return message.GetString() ?? "Не удалось сгенерировать код.";
            }

            if (root.TryGetProperty("errorMessage", out var errorMessage)
                && errorMessage.ValueKind == JsonValueKind.String)
            {
                return errorMessage.GetString() ?? "Не удалось сгенерировать код.";
            }

            if (root.TryGetProperty("title", out var title)
                && title.ValueKind == JsonValueKind.String)
            {
                return title.GetString() ?? "Не удалось сгенерировать код.";
            }
        }
        catch (JsonException)
        {
            // Сервер иногда может вернуть простой текст или HTML вместо JSON.
        }

        return "Не удалось сгенерировать код.";
    }

    private sealed class InviteCodeApiResponse
    {
        public Guid InviteCodeId { get; init; }
        public string Code { get; init; } = string.Empty;
        public JsonElement? TargetRole { get; init; }
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
