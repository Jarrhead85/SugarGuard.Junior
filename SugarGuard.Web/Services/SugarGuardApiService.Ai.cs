using SugarGuard.Web.ViewModels;

namespace SugarGuard.Web.Services;

public sealed partial class SugarGuardApiService
{
    /// <summary>
    /// Возвращает последние пользовательские сообщения AI-диалога ребёнка.
    /// </summary>
    public async Task<AiConversationHistoryVm> GetAiConversationHistoryAsync(
        Guid childId,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var client = await CreateAuthorizedClientAsync(cancellationToken);
        var safeLimit = Math.Clamp(limit, 1, 50);
        var dto = await GetRequiredAsync<AiConversationHistoryApiDto>(
            client,
            $"api/recommendations/{childId:D}/ai-dialog?limit={safeLimit}",
            cancellationToken);

        return new AiConversationHistoryVm
        {
            ConversationId = dto.ConversationId,
            Summary = dto.Summary,
            Messages = dto.Messages.Select(message => new AiConversationMessageVm
            {
                MessageId = message.MessageId,
                Role = message.Role,
                Text = message.Text,
                CreatedAt = message.CreatedAt,
                MeasurementId = message.MeasurementId,
                RecommendationId = message.RecommendationId,
                SafetyResult = message.SafetyResult
            }).ToList()
        };
    }

    private sealed class AiConversationHistoryApiDto
    {
        public Guid? ConversationId { get; init; }
        public string? Summary { get; init; }
        public List<AiConversationMessageApiDto> Messages { get; init; } = [];
    }

    private sealed class AiConversationMessageApiDto
    {
        public Guid MessageId { get; init; }
        public string Role { get; init; } = string.Empty;
        public string Text { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; }
        public Guid? MeasurementId { get; init; }
        public Guid? RecommendationId { get; init; }
        public string? SafetyResult { get; init; }
    }
}
