using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.DTOs;
using SugarGuard.API.Filters;
using SugarGuard.Application.Audit;

namespace SugarGuard.API.Controllers;

/// <summary>
/// Контроллер для service-to-service запросов от бота
/// </summary>
[BotServiceApiKey]
[AllowAnonymous]
[ApiController]
[Route("api/bot-service/context")]
[Produces("application/json")]
public class BotServiceContextController : ControllerBase
{
    private readonly IBotUserContextService _botContext;
    private readonly IAuditService _audit;
    private readonly ILogger<BotServiceContextController> _logger;

    public BotServiceContextController(
        IBotUserContextService botContext,
        IAuditService audit,
        ILogger<BotServiceContextController> logger)
    {
        _botContext = botContext;
        _audit = audit;
        _logger = logger;
    }

    /// <summary>
    /// Получить текущий ChildId для пользователя бота
    /// </summary>
    [HttpGet("{telegramUserId:long}")]
    [ProducesResponseType(typeof(BotUserContextResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<BotUserContextResponse>> GetCurrentChildId(
        long telegramUserId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "BotService: получение контекста для Telegram {TelegramUserId}.", telegramUserId);

        try
        {
            var botContext = await _botContext.GetAsync(
                telegramUserId, bumpLastActivity: true, cancellationToken);

            return Ok(new BotUserContextResponse
            {
                TelegramUserId = telegramUserId,
                CurrentChildId = botContext?.CurrentChildId,
                HasContext = botContext is not null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BotService: ошибка при получении контекста для {TelegramUserId}.",
                telegramUserId);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Установить текущий ChildId для пользователя бота
    /// </summary>
    [HttpPut("{telegramUserId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SetCurrentChildId(
        long telegramUserId,
        [FromBody] SetBotUserContextRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "BotService: установка ChildId={ChildId} для Telegram {TelegramUserId}.",
            request.ChildId, telegramUserId);

        var result = await _botContext.UpsertAsync(
            telegramUserId, request.ChildId, cancellationToken);

        return result is not null ? NoContent() : StatusCode(StatusCodes.Status500InternalServerError);
    }

    /// <summary>
    /// Получить список привязанных детей для пользователя бота
    /// </summary>
    [HttpGet("{telegramUserId:long}/children")]
    [ProducesResponseType(typeof(LinkedChildrenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<LinkedChildrenResponse>> GetLinkedChildren(
        long telegramUserId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "BotService: получение списка детей для Telegram {TelegramUserId}.", telegramUserId);

        try
        {
            var user = await _botContext.FindUserByTelegramIdAsync(telegramUserId, cancellationToken);
            if (user is null)
            {
                return Ok(new LinkedChildrenResponse
                {
                    TelegramUserId = telegramUserId,
                    Children = new List<ChildSummaryBotDto>(),
                    TotalChildren = 0
                });
            }

            var children = await _botContext.GetLinkedChildrenAsync(user.UserId, cancellationToken);

            return Ok(new LinkedChildrenResponse
            {
                TelegramUserId = telegramUserId,
                Children = children.ToList(),
                TotalChildren = children.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BotService: ошибка при получении детей для {TelegramUserId}.",
                telegramUserId);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }
}

