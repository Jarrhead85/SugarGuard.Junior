using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.DTOs;
using SugarGuard.API.Extensions;
using SugarGuard.API.Services;
using SugarGuard.Domain.Entities;

namespace SugarGuard.API.Controllers;

/// <summary>
/// Контроллер для управления ИИ-рекомендациями
/// </summary>
[Authorize(Policy = "ParentOrDoctorOrAdmin")]
[ApiController]
[Route("api/recommendations")]
[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("recommendations")]
public class RecommendationsController : ControllerBase
{
    private readonly IGigaChatService _gigaChatService;
    private readonly ILogger<RecommendationsController> _logger;
    private readonly IChildAccessService _childAccess;
    private readonly IRecommendationService _recommendationService;

    public RecommendationsController(
        IGigaChatService gigaChatService,
        ILogger<RecommendationsController> logger,
        IChildAccessService childAccess,
        IRecommendationService recommendationService)
    {
        _gigaChatService = gigaChatService;
        _logger = logger;
        _childAccess = childAccess;
        _recommendationService = recommendationService;
    }

    /// <summary>
    /// Получить рекомендацию от GigaChat для ребёнка
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(RecommendationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(RecommendationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<RecommendationResponse>> GetRecommendation(
        [FromBody] CreateRecommendationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var child = await _recommendationService.GetChildWithSettingsAsync(
                request.ChildId, cancellationToken);

            if (child is null)
            {
                return this.ProblemWithCode(404, "Child Not Found",
                    "Ребёнок не найден", "child_not_found");
            }

            if (!await _childAccess.CanAccessChildAsync(request.ChildId, cancellationToken))
            {
                return Forbid();
            }

            if (!request.ForceNew)
            {
                var cached = await _recommendationService.FindCachedRecommendationAsync(
                    request.ChildId, request.GlucoseValue, cancellationToken);
                if (cached is not null)
                {
                    _logger.LogInformation(
                        "Найдена кэшированная рекомендация для ребёнка {ChildId}.",
                        request.ChildId);
                    return Ok(MapToResponse(cached, isFromCache: true));
                }
            }

            var recentMeasurements = await _recommendationService.GetRecentMeasurementsAsync(
                request.ChildId, cancellationToken);
            var availableSnacks = request.AvailableSnacks
                ?? await _recommendationService.GetAvailableSnacksAsync(
                    request.ChildId, cancellationToken);

            var gigaChatRequest = _recommendationService.BuildGigaChatRequest(
                child, request.GlucoseValue, recentMeasurements, availableSnacks);

            var gigaChatResponse = await _gigaChatService.GetRecommendationAsync(gigaChatRequest);

            var saved = await _recommendationService.SaveRecommendationAsync(
                childId: request.ChildId,
                measurementId: request.MeasurementId,
                glucoseValue: request.GlucoseValue,
                gigaChatResponse: gigaChatResponse,
                cancellationToken: cancellationToken);

            var response = MapToResponse(saved, isFromCache: false);
            return CreatedAtAction(nameof(GetRecommendation),
                new { id = saved.RecommendationId }, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Ошибка при создании рекомендации для ребёнка {ChildId}.",
                request.ChildId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "internal_error", message = "Внутренняя ошибка сервера." });
        }
    }

    /// <summary>
    /// Получить историю рекомендаций для ребёнка
    /// </summary>
    [HttpGet("{childId:guid}/history")]
    [ProducesResponseType(typeof(RecommendationHistoryPage), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<RecommendationHistoryPage>> GetRecommendationHistory(
        Guid childId,
        [FromQuery] int limit = 50,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] DateTime? cursor = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var child = await _recommendationService.GetChildWithSettingsAsync(
                childId, cancellationToken);
            if (child is null)
            {
                return this.ProblemWithCode(404, "Child Not Found",
                    "Ребёнок не найден", "child_not_found");
            }

            if (!await _childAccess.CanAccessChildAsync(childId, cancellationToken))
            {
                return Forbid();
            }

            var safeLimit = Math.Clamp(limit, 1, 200);
            var page = await _recommendationService.GetHistoryAsync(
                childId, safeLimit, from, to, cursor, cancellationToken);

            return Ok(page);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Ошибка при получении истории рекомендаций для ребёнка {ChildId}.", childId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "internal_error", message = "Внутренняя ошибка сервера." });
        }
    }

    /// <summary>
    /// Получить конкретную рекомендацию по ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(RecommendationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<RecommendationResponse>> GetRecommendation(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var recommendation = await _recommendationService.GetByIdAsync(id, cancellationToken);
            if (recommendation is null)
            {
                return this.ProblemWithCode(404, "Recommendation Not Found",
                    "Рекомендация не найдена", "recommendation_not_found");
            }

            if (!await _childAccess.CanAccessChildAsync(recommendation.ChildId, cancellationToken))
            {
                return Forbid();
            }

            return Ok(MapToResponse(recommendation, isFromCache: false));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Ошибка при получении рекомендации {RecommendationId}.", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "internal_error", message = "Внутренняя ошибка сервера." });
        }
    }

    private static RecommendationResponse MapToResponse(AIRecommendation recommendation, bool isFromCache)
    {
        return new RecommendationResponse
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
    }
}
