using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.DTOs;
using SugarGuard.API.Extensions;
using SugarGuard.API.Models;
using SugarGuard.API.Services;

namespace SugarGuard.API.Controllers;

[ApiController]
[Authorize]
[Route("api/push")]
public class PushSubscriptionController : ControllerBase
{
    private readonly IWebPushService _webPush;
    private readonly ICurrentUserContext _currentUser;
    private readonly ILogger<PushSubscriptionController> _logger;

    public PushSubscriptionController(
        IWebPushService webPush,
        ICurrentUserContext currentUser,
        ILogger<PushSubscriptionController> logger)
    {
        _webPush = webPush;
        _currentUser = currentUser;
        _logger = logger;
    }

    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe(
        [FromBody] PushSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = _currentUser.GetUserId();
        if (userId is not Guid uid)
            return Unauthorized();

        var result = await _webPush.SubscribeAsync(request, uid, cancellationToken);
        _logger.LogInformation("Web Push subscription request completed. Result={Result}", result);

        return result switch
        {
            PushSubscribeResult.Created or PushSubscribeResult.Updated => Ok(new NotificationResponse
            {
                Success = true,
                SentAt = DateTime.UtcNow
            }),
            PushSubscribeResult.InvalidEndpoint => this.ProblemWithCode(400, "Invalid Push Endpoint",
                "Указан неподдерживаемый адрес push-сервиса", "invalid_push_endpoint"),
            PushSubscribeResult.EndpointOwnedByAnotherUser => Conflict(new
            {
                error = "push_endpoint_conflict",
                message = "Эта push-подписка уже зарегистрирована."
            }),
            PushSubscribeResult.LimitExceeded => StatusCode(StatusCodes.Status429TooManyRequests, new
            {
                error = "push_subscription_limit",
                message = "Достигнут лимит push-подписок."
            }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    /// <summary>
    /// Отменяет подписку на Push-уведомления
    /// </summary>
    [HttpDelete("unsubscribe")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unsubscribe(
        [FromBody] UnsubscribePushRequest request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetUserId();
        if (userId is not Guid uid)
            return Unauthorized();

        var result = await _webPush.UnsubscribeAsync(request.Endpoint, uid, cancellationToken);

        return result switch
        {
            UnsubscribeResult.Removed => NoContent(),
            UnsubscribeResult.NotFound => this.ProblemWithCode(404, "Subscription Not Found",
                "Подписка с таким endpoint не найдена", "subscription_not_found"),
            UnsubscribeResult.Forbidden => Forbid(),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}
