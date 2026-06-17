using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SugarGuard.Junior.Services.Interfaces;
using SugarGuard.Shared.Dto;

namespace SugarGuard.Junior.Services.Implementations;

/// <summary>
/// Реализация сервиса управления связками через HTTP-запросы к API.
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
            var response = await _httpClient.GetAsync($"api/links/{childId}/parents");
            response.EnsureSuccessStatusCode();

            var links = await response.Content.ReadFromJsonAsync<List<ParentChildLinkDto>>(JsonOptions);
            return links ?? new List<ParentChildLinkDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения списка родителей для ребёнка {ChildId}", childId);
            return new List<ParentChildLinkDto>();
        }
    }

    public async Task<List<DoctorChildLinkDto>> GetDoctorLinksAsync(Guid childId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/links/{childId}/doctors");
            response.EnsureSuccessStatusCode();

            var links = await response.Content.ReadFromJsonAsync<List<DoctorChildLinkDto>>(JsonOptions);
            return links ?? new List<DoctorChildLinkDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения списка врачей для ребёнка {ChildId}", childId);
            return new List<DoctorChildLinkDto>();
        }
    }

    public async Task<GenerateInviteCodeResponse> GenerateParentInviteCodeAsync(Guid childId, string? note = null)
    {
        try
        {
            var request = new GenerateInviteCodeRequest
            {
                ChildId = childId,
                TargetRole = "Parent",
                Note = note
            };

            var response = await _httpClient.PostAsJsonAsync("api/links/invite", request, JsonOptions);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<GenerateInviteCodeResponse>(JsonOptions);
            return result ?? new GenerateInviteCodeResponse { ErrorMessage = "Пустой ответ сервера" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка генерации кода приглашения для родителя");
            return new GenerateInviteCodeResponse { ErrorMessage = "Ошибка подключения" };
        }
    }

    public async Task<GenerateInviteCodeResponse> GenerateDoctorInviteCodeAsync(Guid childId, string? note = null)
    {
        try
        {
            var request = new GenerateInviteCodeRequest
            {
                ChildId = childId,
                TargetRole = "Doctor",
                Note = note
            };

            var response = await _httpClient.PostAsJsonAsync("api/links/invite", request, JsonOptions);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<GenerateInviteCodeResponse>(JsonOptions);
            return result ?? new GenerateInviteCodeResponse { ErrorMessage = "Пустой ответ сервера" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка генерации кода приглашения для врача");
            return new GenerateInviteCodeResponse { ErrorMessage = "Ошибка подключения" };
        }
    }

    public async Task<AcceptInviteCodeResponse> AcceptInviteCodeAsync(string code)
    {
        try
        {
            var request = new AcceptInviteCodeRequest { Code = code };

            var response = await _httpClient.PostAsJsonAsync("api/links/accept", request, JsonOptions);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<AcceptInviteCodeResponse>(JsonOptions);
            return result ?? new AcceptInviteCodeResponse { Success = false, ErrorMessage = "Пустой ответ сервера" };
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
            var request = new RevokeInviteCodeRequest { InviteCodeId = inviteCodeId };
            var requestMessage = new HttpRequestMessage(HttpMethod.Delete, "api/links/invite")
            {
                Content = JsonContent.Create(request, options: JsonOptions)
            };

            var response = await _httpClient.SendAsync(requestMessage);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<RevokeInviteCodeResponse>(JsonOptions);
            return result ?? new RevokeInviteCodeResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка отзыва кода приглашения");
            return new RevokeInviteCodeResponse { Success = false, ErrorMessage = "Ошибка подключения" };
        }
    }

    public async Task<List<InviteCodeSummaryDto>> GetIncomingRequestsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/links/incoming");
            response.EnsureSuccessStatusCode();

            var requests = await response.Content.ReadFromJsonAsync<List<InviteCodeSummaryDto>>(JsonOptions);
            return requests ?? new List<InviteCodeSummaryDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения входящих запросов на связку");
            return new List<InviteCodeSummaryDto>();
        }
    }

    public async Task<LinkOperationResponse> ApproveLinkRequestAsync(Guid inviteCodeId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"api/links/incoming/{inviteCodeId}/approve", null);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<LinkOperationResponse>(JsonOptions);
            return result ?? new LinkOperationResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка подтверждения запроса на связку");
            return new LinkOperationResponse { Success = false, ErrorMessage = "Ошибка подключения" };
        }
    }

    public async Task<LinkOperationResponse> RejectLinkRequestAsync(Guid inviteCodeId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"api/links/incoming/{inviteCodeId}/reject", null);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<LinkOperationResponse>(JsonOptions);
            return result ?? new LinkOperationResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка отклонения запроса на связку");
            return new LinkOperationResponse { Success = false, ErrorMessage = "Ошибка подключения" };
        }
    }

    public async Task<LinkOperationResponse> RemoveParentLinkAsync(Guid linkId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/links/parent/{linkId}");
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<LinkOperationResponse>(JsonOptions);
            return result ?? new LinkOperationResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка удаления связи с родителем");
            return new LinkOperationResponse { Success = false, ErrorMessage = "Ошибка подключения" };
        }
    }

    public async Task<LinkOperationResponse> RemoveDoctorLinkAsync(Guid linkId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/links/doctor/{linkId}");
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<LinkOperationResponse>(JsonOptions);
            return result ?? new LinkOperationResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка удаления связи с врачом");
            return new LinkOperationResponse { Success = false, ErrorMessage = "Ошибка подключения" };
        }
    }
}
