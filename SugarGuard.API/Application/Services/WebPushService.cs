using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.DTOs;
using SugarGuard.API.Models;
using SugarGuard.Application.Repositories;
using WebPush;
using DomainPushSub = SugarGuard.Domain.Entities.PushSubscription;

namespace SugarGuard.API.Application.Services;

public sealed class WebPushService : IWebPushService
{
    /// <summary>
    /// Отправка Web Push-уведомлений
    /// </summary>
    private const int MaxPushParallelism = 4;

    private readonly IPushSubscriptionRepository _repository;
    private readonly IConfiguration _configuration;
    private readonly WebPushClient _client;
    private readonly ILogger<WebPushService> _logger;

    public WebPushService(
        IPushSubscriptionRepository repository,
        IConfiguration configuration,
        ILogger<WebPushService> logger)
    {
        _repository = repository;
        _configuration = configuration;
        _client = new WebPushClient();
        _logger = logger;
    }

    public async Task<NotificationResponse> SubscribeAsync(
        PushSubscriptionRequest request, Guid userId, CancellationToken ct = default)
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
        string endpoint, Guid userId, CancellationToken ct = default)
    {
        var sub = await _repository.GetByEndpointAsync(endpoint, ct);
        if (sub is null)
        {
            _logger.LogWarning(
                "UnsubscribeAsync: endpoint не найден. Endpoint: {Endpoint}",
                endpoint);
            return UnsubscribeResult.NotFound;
        }

        if (sub.UserId != userId)
        {
            _logger.LogWarning(
                "UnsubscribeAsync: попытка отписать чужой endpoint. " +
                "UserId={UserId}, Sub.UserId={SubUserId}, Endpoint={Endpoint}",
                userId, sub.UserId, endpoint);
            return UnsubscribeResult.Forbidden;
        }

        var removed = await _repository.RemoveByEndpointAsync(endpoint, ct);
        if (!removed)
        {
            _logger.LogInformation(
                "UnsubscribeAsync: endpoint удалён между Get и Remove. " +
                "UserId={UserId}, Endpoint={Endpoint}",
                userId, endpoint);
            return UnsubscribeResult.NotFound;
        }

        _logger.LogInformation(
            "UnsubscribeAsync: подписка удалена. UserId={UserId}, Endpoint={Endpoint}",
            userId, endpoint);
        return UnsubscribeResult.Removed;
    }

    public async Task SendNotificationAsync(
        Guid userId, string title, string body, string? url = null, CancellationToken ct = default)
    {
        var subs = await _repository.GetByUserIdAsync(userId, ct);

        if (subs.Count == 0)
        {
            _logger.LogWarning("Нет Push-подписок для пользователя. UserId: {UserId}", userId);
            return;
        }

        var vapidSubject = _configuration["Vapid:Subject"]
            ?? "mailto:sugarguard@example.com";
        var vapidPublicKey = _configuration["Vapid:PublicKey"]
            ?? throw new InvalidOperationException("Vapid:PublicKey не настроен.");
        var vapidPrivateKey = _configuration["Vapid:PrivateKey"]
            ?? throw new InvalidOperationException("Vapid:PrivateKey не настроен.");

        var vapidDetails = new VapidDetails(vapidSubject, vapidPublicKey, vapidPrivateKey);
        var payload = System.Text.Json.JsonSerializer.Serialize(new { title, body, url, icon = "/favicon.png" });

        _logger.LogDebug(
            "Отправка Push: UserId={UserId}, подписок={Count}, параллелизм={Max}",
            userId, subs.Count, MaxPushParallelism);

        await Parallel.ForEachAsync(
            subs,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = MaxPushParallelism,
                CancellationToken = ct
            },
            async (sub, innerCt) =>
            {
                var webPushSub = new PushSubscription(sub.Endpoint, sub.P256Dh, sub.Auth);
                try
                {
                    await _client.SendNotificationAsync(webPushSub, payload, vapidDetails);
                    _logger.LogDebug("Push отправлен. Endpoint: {Endpoint}", sub.Endpoint);
                }
                catch (WebPushException ex) when (ex.StatusCode is
                    System.Net.HttpStatusCode.Gone or
                    System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogInformation("Устаревшая подписка удалена. Endpoint: {Endpoint}", sub.Endpoint);
                    await _repository.RemoveByEndpointAsync(sub.Endpoint, innerCt);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка отправки Push. Endpoint: {Endpoint}", sub.Endpoint);
                }
            });
    }
}
