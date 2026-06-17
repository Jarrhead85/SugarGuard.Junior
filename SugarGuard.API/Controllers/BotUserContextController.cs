using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.DTOs;
using SugarGuard.API.Extensions;
using SugarGuard.API.Services;
using SugarGuard.Application.Audit;

namespace SugarGuard.API.Controllers;

/// <summary>
/// Контроллер для управления контекстом пользователя бота
/// </summary>
[Authorize(Policy = "ParentOrDoctorOrAdmin")]
[ApiController]
[Route("api/bot-context")]
[Produces("application/json")]
public class BotUserContextController : ControllerBase
{
    private readonly IBotUserContextService _botContext;
    private readonly IChildAccessService _childAccess;
    private readonly IAuditService _audit;
    private readonly ILogger<BotUserContextController> _logger;

    public BotUserContextController(
        IBotUserContextService botContext,
        IChildAccessService childAccess,
        IAuditService audit,
        ILogger<BotUserContextController> logger)
    {
        _botContext = botContext;
        _childAccess = childAccess;
        _audit = audit;
        _logger = logger;
    }

    // GET /api/bot-context/{telegramUserId}
    /// <summary>
    /// Получить текущий ChildId для пользователя бота.
    /// </summary>
    [HttpGet("{telegramUserId:long}")]
    [ProducesResponseType(typeof(BotUserContextResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<BotUserContextResponse>> GetCurrentChildId(
        long telegramUserId,
        CancellationToken cancellationToken)
    {
        if (!await IsCurrentUserTelegramAsync(telegramUserId, cancellationToken))
            return Forbid();

        try
        {
            _logger.LogInformation(
                "Получение контекста для Telegram пользователя {TelegramUserId}.", telegramUserId);

            var botContext = await _botContext.GetAsync(
                telegramUserId, bumpLastActivity: true, cancellationToken);

            if (botContext is null)
            {
                _logger.LogInformation(
                    "Контекст не найден для Telegram пользователя {TelegramUserId}.", telegramUserId);
                return Ok(new BotUserContextResponse
                {
                    TelegramUserId = telegramUserId,
                    CurrentChildId = null,
                    HasContext = false
                });
            }

            _logger.LogInformation(
                "Контекст получен для Telegram пользователя {TelegramUserId}: ChildId={ChildId}.",
                telegramUserId, botContext.CurrentChildId);

            return Ok(new BotUserContextResponse
            {
                TelegramUserId = telegramUserId,
                CurrentChildId = botContext.CurrentChildId,
                HasContext = true,
                LastActivityAt = botContext.LastActivityAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Ошибка при получении контекста для Telegram пользователя {TelegramUserId}.", telegramUserId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "internal_error", message = "Внутренняя ошибка сервера." });
        }
    }

    // PUT /api/bot-context/{telegramUserId}
    /// <summary>
    /// Установить текущий ChildId для пользователя бота
    /// </summary>
    [HttpPut("{telegramUserId:long}")]
    [ProducesResponseType(typeof(BotUserContextResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<BotUserContextResponse>> SetCurrentChildId(
        long telegramUserId,
        [FromBody] SetBotUserContextRequest request,
        CancellationToken cancellationToken)
    {
        // Валидация модели
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            _logger.LogWarning(
                "Невалидные данные при установке контекста: {Errors}.",
                string.Join(", ", errors));
            return BadRequest(new { error = "validation_error", details = errors });
        }

        // IDOR-защита
        if (!await IsCurrentUserTelegramAsync(telegramUserId, cancellationToken))
        {
            _logger.LogWarning(
                "IDOR-заблокировано: текущий JWT-пользователь пытается изменить контекст " +
                "Telegram-пользователя {TelegramUserId}.",
                telegramUserId);
            return Forbid();
        }

        _logger.LogInformation(
            "Установка контекста для Telegram пользователя {TelegramUserId}: ChildId={ChildId}.",
            telegramUserId, request.ChildId);

        // Проверка доступа к ребёнку
        if (request.ChildId.HasValue)
        {
            if (!await _botContext.ChildExistsAsync(request.ChildId.Value, cancellationToken))
            {
                _logger.LogWarning(
                    "Попытка установить несуществующий ChildId {ChildId} для Telegram пользователя {TelegramUserId}.",
                    request.ChildId, telegramUserId);
                return this.ProblemWithCode(404, "Not Found",
                    "Ребёнок не найден", "child_not_found");
            }

            if (!await _childAccess.CanAccessChildAsync(request.ChildId.Value, cancellationToken))
                return Forbid();
        }

        // Upsert + audit
        try
        {
            var botContext = await _botContext.UpsertAsync(
                telegramUserId, request.ChildId, cancellationToken);

            var action = request.ChildId.HasValue
                ? "bot_context.set"
                : "bot_context.cleared";

            await _audit.WriteAsync(
                action: action,
                targetType: "BotUserContext",
                targetId: telegramUserId.ToString(),
                details: request.ChildId.HasValue
                    ? $"ChildId={request.ChildId.Value}"
                    : "ChildId=null (cleared)",
                cancellationToken: CancellationToken.None);

            return Ok(new BotUserContextResponse
            {
                TelegramUserId = telegramUserId,
                CurrentChildId = botContext.CurrentChildId,
                HasContext = true,
                LastActivityAt = botContext.LastActivityAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Ошибка при установке контекста для Telegram пользователя {TelegramUserId}.",
                telegramUserId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "internal_error", message = "Внутренняя ошибка сервера." });
        }
    }

    // GET /api/bot-context/{telegramUserId}/children
    /// <summary>
    /// Получить список привязанных детей для пользователя бота
    /// </summary>
    [HttpGet("{telegramUserId:long}/children")]
    [ProducesResponseType(typeof(LinkedChildrenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<LinkedChildrenResponse>> GetLinkedChildren(
        long telegramUserId,
        CancellationToken cancellationToken)
    {
        if (!await IsCurrentUserTelegramAsync(telegramUserId, cancellationToken))
            return Forbid();

        try
        {
            _logger.LogInformation(
                "Получение списка привязанных детей для Telegram пользователя {TelegramUserId}.",
                telegramUserId);

            // Находим пользователя по Telegram ID
            var user = await _botContext.FindUserByTelegramIdAsync(
                telegramUserId, cancellationToken);

            if (user is null)
            {
                _logger.LogInformation(
                    "Пользователь с Telegram ID {TelegramUserId} не найден.", telegramUserId);
                return Ok(new LinkedChildrenResponse
                {
                    TelegramUserId = telegramUserId,
                    Children = new List<ChildSummaryBotDto>(),
                    TotalChildren = 0
                });
            }

            var linkedChildren = await _botContext.GetLinkedChildrenAsync(
                user.UserId, cancellationToken);

            _logger.LogInformation(
                "Найдено {Count} привязанных детей для Telegram пользователя {TelegramUserId}.",
                linkedChildren.Count, telegramUserId);

            return Ok(new LinkedChildrenResponse
            {
                TelegramUserId = telegramUserId,
                Children = linkedChildren.ToList(),
                TotalChildren = linkedChildren.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Ошибка при получении списка привязанных детей для Telegram пользователя {TelegramUserId}.",
                telegramUserId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "internal_error", message = "Внутренняя ошибка сервера." });
        }
    }

    // Приватные хелперы
    /// <summary>
    /// Проверяет, что текущий пользователь — это пользователь с данным TelegramId
    /// </summary>
    private async Task<bool> IsCurrentUserTelegramAsync(
        long telegramUserId,
        CancellationToken cancellationToken)
    {
        var userId = _childAccess.GetCurrentUserId();
        if (!userId.HasValue) return false;
        return await _botContext.IsCurrentUserTelegramAsync(
            userId.Value, telegramUserId, cancellationToken);
    }
}
