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

    private readonly IPushSubscriptionRepository _repository;
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly WebPushClient _client;
    private readonly ILogger<WebPushService> _logger;

    public WebPushService(
        IPushSubscriptionRepository repository,
        AppDbContext db,
        IConfiguration configuration,
        ILogger<WebPushService> logger)
    {
        _repository = repository;
        _db = db;
        _configuration = configuration;
        _client = new WebPushClient();
        _logger = logger;
    }

    public async Task<NotificationResponse> SubscribeAsync(
        PushSubscriptionRequest request,
        Guid userId,
        CancellationToken ct = default)
    {
        var sub = new DomainPushSub
        {
            UserId = userId,
            Endpoint = request.Endpoint,
            P256Dh = request.P256Dh,
            Auth = request.Auth,
            UserAgent = request.UserAgent
        };

        await _repository.AddAsync(sub, ct);
        _logger.LogInformation("Web Push подписка сохранена. UserId: {UserId}", userId);

        return new NotificationResponse { Success = true, SentAt = DateTime.UtcNow };
    }

    public async Task<UnsubscribeResult> UnsubscribeAsync(
        string endpoint,
        Guid userId,
        CancellationToken ct = default)
    {
        var sub = await _repository.GetByEndpointAsync(endpoint, ct);
        if (sub is null)
        {
            return UnsubscribeResult.NotFound;
        }

        if (sub.UserId != userId)
        {
            _logger.LogWarning(
                "Попытка отписать чужой Web Push endpoint. UserId={UserId}, Endpoint={Endpoint}",
                userId,
                endpoint);
            return UnsubscribeResult.Forbidden;
        }

        return await _repository.RemoveByEndpointAsync(endpoint, ct)
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
                try
                {
                    var webPushSubscription = new PushSubscription(
                        subscription.Endpoint,
                        subscription.P256Dh,
                        subscription.Auth);
                    await _client.SendNotificationAsync(webPushSubscription, payload, vapidDetails);
                }
                catch (WebPushException ex) when (ex.StatusCode is
                    System.Net.HttpStatusCode.Gone or
                    System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogInformation("Удалена устаревшая Web Push подписка. Endpoint={Endpoint}", subscription.Endpoint);
                    await _repository.RemoveByEndpointAsync(subscription.Endpoint, innerCt);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка отправки Web Push. Endpoint={Endpoint}", subscription.Endpoint);
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
