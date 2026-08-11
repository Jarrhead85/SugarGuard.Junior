using Microsoft.EntityFrameworkCore;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Data;
using SugarGuard.API.DTOs;
using SugarGuard.API.Models;
using SugarGuard.Application.Repositories;
using WebPush;
using DomainPushSub = SugarGuard.Domain.Entities.PushSubscription;

namespace SugarGuard.API.Application.Services;

public sealed class WebPushService : IWebPushService
{
    private const int MaxPushParallelism = 4;
    private const int MaxSubscriptionsPerUser = 10;

    private readonly IPushSubscriptionRepository _repository;
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly IWebPushEndpointValidator _endpointValidator;
    private readonly WebPushClient _client;
    private readonly ILogger<WebPushService> _logger;

    public WebPushService(
        IPushSubscriptionRepository repository,
        AppDbContext db,
        IConfiguration configuration,
        IWebPushEndpointValidator endpointValidator,
        HttpClient httpClient,
        ILogger<WebPushService> logger)
    {
        _repository = repository;
        _db = db;
        _configuration = configuration;
        _endpointValidator = endpointValidator;
        _client = new WebPushClient(httpClient);
        _logger = logger;
    }

    public async Task<PushSubscribeResult> SubscribeAsync(
        PushSubscriptionRequest request,
        Guid userId,
        CancellationToken ct = default)
    {
        if (!_endpointValidator.TryValidateAndNormalize(request.Endpoint, out var endpoint))
        {
            return PushSubscribeResult.InvalidEndpoint;
        }

        var sub = new DomainPushSub
        {
            UserId = userId,
            Endpoint = endpoint,
            P256Dh = request.P256Dh,
            Auth = request.Auth,
            UserAgent = request.UserAgent
        };

        var result = await _repository.UpsertForUserAsync(sub, MaxSubscriptionsPerUser, ct);
        _logger.LogInformation("Web Push подписка сохранена. UserId: {UserId}", userId);

        return result switch
        {
            PushSubscriptionUpsertResult.Created => PushSubscribeResult.Created,
            PushSubscriptionUpsertResult.Updated => PushSubscribeResult.Updated,
            PushSubscriptionUpsertResult.EndpointOwnedByAnotherUser => PushSubscribeResult.EndpointOwnedByAnotherUser,
            PushSubscriptionUpsertResult.LimitExceeded => PushSubscribeResult.LimitExceeded,
            _ => throw new InvalidOperationException("Unknown Web Push upsert result.")
        };
    }

    public async Task<UnsubscribeResult> UnsubscribeAsync(
        string endpoint,
        Guid userId,
        CancellationToken ct = default)
    {
        if (!_endpointValidator.TryValidateAndNormalize(endpoint, out var normalizedEndpoint))
        {
            return UnsubscribeResult.NotFound;
        }

        var sub = await _repository.GetByEndpointAsync(normalizedEndpoint, ct);
        if (sub is null)
        {
            return UnsubscribeResult.NotFound;
        }

        if (sub.UserId != userId)
        {
            _logger.LogWarning("Attempt to unsubscribe a Web Push endpoint owned by another user.");
            return UnsubscribeResult.NotFound;
        }

        return await _repository.RemoveByEndpointAsync(normalizedEndpoint, userId, ct)
            ? UnsubscribeResult.Removed
            : UnsubscribeResult.NotFound;
    }

    public async Task SendNotificationAsync(
        Guid userId,
        string title,
        string body,
        string? url = null,
        bool requireInteraction = false,
        CancellationToken ct = default)
    {
        var subscriptions = await _repository.GetByUserIdAsync(userId, ct);
        if (subscriptions.Count == 0)
        {
            return;
        }

        var vapidDetails = CreateVapidDetails();
        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            title,
            body,
            url,
            icon = "/images/sugarguard-icon.png",
            badge = "/images/sugarguard-icon.png",
            requireInteraction
        });

        await Parallel.ForEachAsync(
            subscriptions,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = MaxPushParallelism,
                CancellationToken = ct
            },
            async (subscription, innerCt) =>
            {
                if (!_endpointValidator.TryValidateAndNormalize(subscription.Endpoint, out var endpoint))
                {
                    _logger.LogWarning(
                        "Rejected an invalid persisted Web Push endpoint. SubscriptionId={SubscriptionId}",
                        subscription.SubscriptionId);
                    await _repository.RemoveByEndpointAsync(
                        subscription.Endpoint,
                        subscription.UserId,
                        innerCt);
                    return;
                }

                try
                {
                    var webPushSubscription = new PushSubscription(
                        endpoint,
                        subscription.P256Dh,
                        subscription.Auth);
                    await _client.SendNotificationAsync(
                        webPushSubscription,
                        payload,
                        vapidDetails,
                        innerCt);
                }
                catch (WebPushException ex) when (ex.StatusCode is
                    System.Net.HttpStatusCode.Gone or
                    System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogInformation(
                        "Removed an expired Web Push subscription. SubscriptionId={SubscriptionId}",
                        subscription.SubscriptionId);
                    await _repository.RemoveByEndpointAsync(
                        subscription.Endpoint,
                        subscription.UserId,
                        innerCt);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Web Push delivery failed. SubscriptionId={SubscriptionId}",
                        subscription.SubscriptionId);
                }
            });
    }

    public async Task SendForChildAsync(
        Guid childId,
        string title,
        string body,
        string? url = null,
        bool requireInteraction = false,
        CancellationToken ct = default)
    {
        var parentUserIds = await _db.ParentChildLinks
            .AsNoTracking()
            .Where(link => link.ChildId == childId)
            .Select(link => link.ParentUserId)
            .Distinct()
            .ToListAsync(ct);

        await Task.WhenAll(parentUserIds.Select(parentUserId =>
            SendNotificationAsync(parentUserId, title, body, url, requireInteraction, ct)));
    }

    private VapidDetails CreateVapidDetails()
    {
        var subject = _configuration["Vapid:Subject"] ?? "mailto:support@sugar-guard.ru";
        var publicKey = _configuration["Vapid:PublicKey"]
            ?? throw new InvalidOperationException("Vapid:PublicKey не настроен.");
        var privateKey = _configuration["Vapid:PrivateKey"]
            ?? throw new InvalidOperationException("Vapid:PrivateKey не настроен.");

        return new VapidDetails(subject, publicKey, privateKey);
    }
}
