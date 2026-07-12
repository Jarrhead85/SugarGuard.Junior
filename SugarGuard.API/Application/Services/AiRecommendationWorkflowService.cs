using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SugarGuard.API.Application.Ai;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Data;
using SugarGuard.API.DTOs;
using SugarGuard.API.Services;
using SugarGuard.Domain.Entities;
using SugarGuard.Domain.Enums;
using SugarGuard.Shared.Constants;

namespace SugarGuard.API.Application.Services;

/// <summary>
/// Оркестратор AI-рекомендации с долгосрочной памятью ребёнка.
/// </summary>
public sealed class AiRecommendationWorkflowService : IAiRecommendationWorkflowService
{
    private static readonly JsonSerializerOptions ContextJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IChildAccessService _childAccess;
    private readonly ICurrentUserContext _currentUser;
    private readonly IRecommendationService _recommendationService;
    private readonly IClinicalContextBuilder _contextBuilder;
    private readonly IAiRecommendationSafetyPolicy _safetyPolicy;
    private readonly IGigaChatService _gigaChatService;
    private readonly ILogger<AiRecommendationWorkflowService> _logger;

    /// <summary>
    /// Создаёт экземпляр workflow-сервиса AI-рекомендаций.
    /// </summary>
    public AiRecommendationWorkflowService(
        IDbContextFactory<AppDbContext> dbFactory,
        IChildAccessService childAccess,
        ICurrentUserContext currentUser,
        IRecommendationService recommendationService,
        IClinicalContextBuilder contextBuilder,
        IAiRecommendationSafetyPolicy safetyPolicy,
        IGigaChatService gigaChatService,
        ILogger<AiRecommendationWorkflowService> logger)
    {
        _dbFactory = dbFactory;
        _childAccess = childAccess;
        _currentUser = currentUser;
        _recommendationService = recommendationService;
        _contextBuilder = contextBuilder;
        _safetyPolicy = safetyPolicy;
        _gigaChatService = gigaChatService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<RecommendationResponse> CreateRecommendationAsync(
        CreateRecommendationRequest request,
        CancellationToken cancellationToken)
    {
        var child = await _recommendationService.GetChildWithSettingsAsync(request.ChildId, cancellationToken)
            ?? throw new KeyNotFoundException("Ребёнок не найден.");

        if (!await _childAccess.CanAccessChildAsync(request.ChildId, cancellationToken))
        {
            throw new UnauthorizedAccessException("Нет доступа к ребёнку.");
        }

        var canUseCache = request.GlucoseValue >= (decimal)GlucoseLevels.TargetRangeMin
            && request.GlucoseValue <= (decimal)GlucoseLevels.TargetRangeMax;

        if (!request.ForceNew && canUseCache && string.IsNullOrWhiteSpace(request.Question))
        {
            var cached = await _recommendationService.FindCachedRecommendationAsync(
                request.ChildId,
                request.GlucoseValue,
                cancellationToken);

            if (cached is not null)
            {
                return MapToResponse(cached, isFromCache: true);
            }
        }

        var conversation = await FindOrCreateConversationAsync(
            request.ChildId,
            request.ConversationId,
            cancellationToken);

        var question = string.IsNullOrWhiteSpace(request.Question)
            ? $"Подскажи безопасное действие при глюкозе {request.GlucoseValue:F1} ммоль/л."
            : request.Question.Trim();

        await SaveUserMessageAsync(conversation.ConversationId, request.MeasurementId, question, cancellationToken);

        var context = await _contextBuilder.BuildAsync(
            request.ChildId,
            conversation.ConversationId,
            request.MeasurementId,
            question,
            cancellationToken);

        EnsureRequestedMeasurement(context, request);

        var contextJson = JsonSerializer.Serialize(context, ContextJsonOptions);
        var preCheck = _safetyPolicy.EvaluateBeforeModel(context);
        GigaChatResponse gigaChatResponse;
        AiSafetyResult safetyResult;

        if (!preCheck.CanCallModel)
        {
            gigaChatResponse = new GigaChatResponse
            {
                RecommendationText = preCheck.SafeText ?? "Сообщи взрослому и действуй по утверждённому плану.",
                Urgency = preCheck.Urgency,
                ModelUsed = "SafetyRules",
                IsLocalFallback = true,
                IsSuccess = true
            };
            safetyResult = preCheck.Result;
        }
        else
        {
            var recentMeasurements = await _recommendationService.GetRecentMeasurementsAsync(request.ChildId, cancellationToken);
            var availableSnacks = request.AvailableSnacks
                ?? await _recommendationService.GetAvailableSnacksAsync(request.ChildId, cancellationToken);

            var gigaChatRequest = _recommendationService.BuildGigaChatRequest(
                child,
                request.GlucoseValue,
                recentMeasurements,
                availableSnacks);

            gigaChatRequest.Question = question;
            gigaChatRequest.StructuredContextJson = contextJson;

            gigaChatResponse = await _gigaChatService.GetRecommendationAsync(gigaChatRequest, cancellationToken);
            var postCheck = _safetyPolicy.EvaluateAfterModel(context, gigaChatResponse.RecommendationText);
            safetyResult = postCheck.Result;

            if (postCheck.Result == AiSafetyResult.BlockedUnsafeOutput)
            {
                gigaChatResponse.RecommendationText = postCheck.SafeText ?? "Покажи измерение взрослому и следуй утверждённому плану.";
                gigaChatResponse.Urgency = postCheck.Urgency;
                gigaChatResponse.IsLocalFallback = true;
                gigaChatResponse.ModelUsed = "SafetyPolicy";
            }
        }

        var saved = await _recommendationService.SaveRecommendationAsync(
            request.ChildId,
            request.MeasurementId,
            request.GlucoseValue,
            gigaChatResponse,
            cancellationToken);

        await SaveAiArtifactsAsync(
            conversation.ConversationId,
            request.ChildId,
            request.MeasurementId,
            saved.RecommendationId,
            context.FormatVersion,
            contextJson,
            gigaChatResponse,
            safetyResult,
            cancellationToken);

        _logger.LogInformation(
            "AI-рекомендация {RecommendationId} создана для ребёнка {ChildId}. Safety={SafetyResult}, Model={Model}.",
            saved.RecommendationId,
            request.ChildId,
            safetyResult,
            gigaChatResponse.ModelUsed);

        var response = MapToResponse(saved, isFromCache: false);
        response.ConversationId = conversation.ConversationId;
        response.IsLocalFallback = gigaChatResponse.IsLocalFallback;
        response.SafetyResult = safetyResult.ToString();
        response.InputTokens = gigaChatResponse.InputTokens;
        response.OutputTokens = gigaChatResponse.OutputTokens;
        response.TotalTokens = gigaChatResponse.TotalTokens;
        return response;
    }

    /// <inheritdoc/>
    public async Task<AiConversationHistoryResponse> GetConversationHistoryAsync(
        Guid childId,
        int limit,
        CancellationToken cancellationToken)
    {
        var safeLimit = Math.Clamp(limit, 1, 50);
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var conversation = await db.Set<AiConversation>()
            .AsNoTracking()
            .Where(item => item.ChildId == childId && item.Status == AiConversationStatus.Active)
            .OrderByDescending(item => item.LastMessageAt ?? item.CreatedAt)
            .Select(item => new
            {
                item.ConversationId,
                item.Summary
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (conversation is null)
        {
            return new AiConversationHistoryResponse();
        }

        var messages = await db.Set<AiConversationMessage>()
            .AsNoTracking()
            .Where(message => message.ConversationId == conversation.ConversationId)
            .Where(message => message.Role == AiMessageRole.User || message.Role == AiMessageRole.Assistant)
            .OrderByDescending(message => message.CreatedAt)
            .Take(safeLimit)
            .OrderBy(message => message.CreatedAt)
            .Select(message => new AiConversationMessageResponse
            {
                MessageId = message.MessageId,
                Role = message.Role.ToString(),
                Text = message.Text,
                CreatedAt = message.CreatedAt,
                MeasurementId = message.MeasurementId,
                RecommendationId = message.RecommendationId,
                SafetyResult = message.SafetyResult.ToString()
            })
            .ToListAsync(cancellationToken);

        return new AiConversationHistoryResponse
        {
            ConversationId = conversation.ConversationId,
            Summary = conversation.Summary,
            Messages = messages
        };
    }

    private async Task<AiConversation> FindOrCreateConversationAsync(
        Guid childId,
        Guid? requestedConversationId,
        CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Set<AiConversation>().Where(conversation => conversation.ChildId == childId);

        var conversation = requestedConversationId.HasValue
            ? await query.FirstOrDefaultAsync(item => item.ConversationId == requestedConversationId.Value, cancellationToken)
            : await query
                .Where(item => item.Status == AiConversationStatus.Active)
                .OrderByDescending(item => item.LastMessageAt ?? item.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

        if (conversation is not null)
        {
            return conversation;
        }

        conversation = new AiConversation
        {
            ChildId = childId,
            Status = AiConversationStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        db.Set<AiConversation>().Add(conversation);
        await db.SaveChangesAsync(cancellationToken);
        return conversation;
    }

    private async Task SaveUserMessageAsync(
        Guid conversationId,
        Guid? measurementId,
        string question,
        CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var persistedMeasurementId = await FindPersistedMeasurementIdAsync(db, measurementId, cancellationToken);

        db.Set<AiConversationMessage>().Add(new AiConversationMessage
        {
            ConversationId = conversationId,
            MeasurementId = persistedMeasurementId,
            Role = AiMessageRole.User,
            Text = Trim(question, 4000),
            AuthorUserId = _currentUser.GetUserId(),
            CreatedAt = DateTime.UtcNow
        });

        var conversation = await db.Set<AiConversation>()
            .FirstAsync(item => item.ConversationId == conversationId, cancellationToken);
        conversation.LastMessageAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task SaveAiArtifactsAsync(
        Guid conversationId,
        Guid childId,
        Guid? measurementId,
        Guid recommendationId,
        string formatVersion,
        string contextJson,
        GigaChatResponse response,
        AiSafetyResult safetyResult,
        CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        db.Set<AiContextSnapshot>().Add(new AiContextSnapshot
        {
            ChildId = childId,
            ConversationId = conversationId,
            MeasurementId = await FindPersistedMeasurementIdAsync(db, measurementId, cancellationToken),
            FormatVersion = formatVersion,
            ContextJson = contextJson,
            CreatedAt = DateTime.UtcNow
        });

        db.Set<AiConversationMessage>().Add(new AiConversationMessage
        {
            ConversationId = conversationId,
            MeasurementId = await FindPersistedMeasurementIdAsync(db, measurementId, cancellationToken),
            RecommendationId = recommendationId,
            Role = AiMessageRole.Assistant,
            Text = Trim(response.RecommendationText, 4000),
            CreatedAt = DateTime.UtcNow,
            Model = response.ModelUsed,
            InputTokens = response.InputTokens,
            OutputTokens = response.OutputTokens,
            SafetyResult = safetyResult
        });

        var conversation = await db.Set<AiConversation>()
            .FirstAsync(item => item.ConversationId == conversationId, cancellationToken);
        conversation.LastMessageAt = DateTime.UtcNow;
        conversation.Summary = BuildDeterministicSummary(conversation.Summary, response.RecommendationText);
        conversation.SummaryUpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }

    private static async Task<Guid?> FindPersistedMeasurementIdAsync(
        AppDbContext db,
        Guid? measurementId,
        CancellationToken cancellationToken)
    {
        if (!measurementId.HasValue)
        {
            return null;
        }

        return await db.Measurements
            .AsNoTracking()
            .Where(measurement => measurement.MeasurementId == measurementId.Value)
            .Select(measurement => (Guid?)measurement.MeasurementId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static void EnsureRequestedMeasurement(ClinicalContext context, CreateRecommendationRequest request)
    {
        if (context.Current.Measurement is null
            || (request.MeasurementId.HasValue && context.Current.Measurement.Value != request.GlucoseValue))
        {
            context.Current.Measurement = new GlucoseContext
            {
                MeasuredAt = DateTime.UtcNow,
                Value = request.GlucoseValue,
                Source = "request"
            };
        }
    }

    private static RecommendationResponse MapToResponse(AIRecommendation recommendation, bool isFromCache) => new()
    {
        RecommendationId = recommendation.RecommendationId,
        ChildId = recommendation.ChildId,
        MeasurementId = recommendation.MeasurementId,
        GlucoseValueAtRequest = recommendation.GlucoseValueAtRequest,
        RecommendationText = recommendation.RecommendationText,
        Urgency = recommendation.Urgency,
        ModelUsed = recommendation.ModelUsed,
        IsFromCache = isFromCache,
        LatencyMs = recommendation.LatencyMs,
        CreatedAt = recommendation.CreatedAt
    };

    private static string BuildDeterministicSummary(string previousSummary, string assistantText)
    {
        var combined = string.IsNullOrWhiteSpace(previousSummary)
            ? $"Последний безопасный совет: {assistantText}"
            : $"{previousSummary}\nПоследний безопасный совет: {assistantText}";
        return Trim(combined, 1200);
    }

    private static string Trim(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
