using System.Net.Http.Headers;
using System.Net.Http.Json;
using SugarGuard.Domain.Enums;
using SugarGuard.Web.ViewModels;

namespace SugarGuard.Web.Services;

public sealed partial class SugarGuardApiService
{
    public async Task<IReadOnlyList<SupportConversationVm>> GetSupportConversationsAsync(
        CancellationToken cancellationToken = default)
    {
        var client = await CreateAuthorizedClientAsync(cancellationToken);
        return await client.GetFromJsonAsync<List<SupportConversationVm>>(
            "api/support/conversations",
            _jsonOptions,
            cancellationToken) ?? [];
    }

    public async Task<SupportConversationDetailsVm> GetSupportConversationAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        var client = await CreateAuthorizedClientAsync(cancellationToken);
        return await client.GetFromJsonAsync<SupportConversationDetailsVm>(
            $"api/support/conversations/{conversationId}",
            _jsonOptions,
            cancellationToken) ?? throw new InvalidOperationException("API вернул пустое обращение.");
    }

    public async Task<SupportConversationDetailsVm> CreateSupportConversationAsync(
        CreateSupportConversationVm request,
        CancellationToken cancellationToken = default)
    {
        var client = await CreateAuthorizedClientAsync(cancellationToken);
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(request.Subject), "Subject");
        form.Add(new StringContent(request.Message), "Message");
        if (!string.IsNullOrWhiteSpace(request.ClientLogs))
        {
            form.Add(new StringContent(request.ClientLogs), "ClientLogs");
        }

        if (request.Attachment is { Content.Length: > 0 } attachment)
        {
            var contentType = string.IsNullOrWhiteSpace(attachment.ContentType)
                ? "application/octet-stream"
                : attachment.ContentType;
            var fileContent = new ByteArrayContent(attachment.Content);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
            form.Add(fileContent, "Attachment", attachment.FileName);
        }

        using var response = await client.PostAsync("api/support/requests", form, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SupportConversationDetailsVm>(_jsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("API вернул пустое обращение.");
    }

    public async Task<SupportMessageVm> AddSupportMessageAsync(
        Guid conversationId,
        string message,
        CancellationToken cancellationToken = default)
    {
        var client = await CreateAuthorizedClientAsync(cancellationToken);
        using var response = await client.PostAsJsonAsync(
            $"api/support/conversations/{conversationId}/messages",
            new { message },
            _jsonOptions,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SupportMessageVm>(_jsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("API вернул пустое сообщение.");
    }

    public async Task MarkSupportConversationReadAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        var client = await CreateAuthorizedClientAsync(cancellationToken);
        using var response = await client.PostAsync(
            $"api/support/conversations/{conversationId}/read",
            null,
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateSupportConversationStatusAsync(
        Guid conversationId,
        SupportConversationStatus status,
        CancellationToken cancellationToken = default)
    {
        var client = await CreateAuthorizedClientAsync(cancellationToken);
        using var response = await client.PutAsJsonAsync(
            $"api/support/conversations/{conversationId}/status",
            new { status },
            _jsonOptions,
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
