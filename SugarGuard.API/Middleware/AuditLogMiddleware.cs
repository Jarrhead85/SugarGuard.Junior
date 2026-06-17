using System.Diagnostics;
using System.Security.Claims;
using SugarGuard.Application.Audit;

namespace SugarGuard.API.Middleware;

/// <summary>
/// Аудит HTTP-запросов
/// </summary>
public sealed class AuditLogMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuditLogMiddleware> _logger;

    /// <summary>
    /// HTTP-методы, которые изменяют состояние
    /// </summary>
    private static readonly HashSet<string> AuditedMethods =
        new(StringComparer.OrdinalIgnoreCase)
        {
            HttpMethods.Post,
            HttpMethods.Put,
            HttpMethods.Patch,
            HttpMethods.Delete
        };

    /// <summary>
    /// Пути, которые не нужно логировать в аудит
    /// </summary>
    private static readonly string[] ExcludedPathPrefixes =
    [
        "/health",
        "/metrics",
        "/hangfire",
        "/swagger",
        "/favicon"
    ];

    /// <summary>
    /// Инициализирует middleware аудита
    /// </summary>
    public AuditLogMiddleware(
        RequestDelegate next,
        ILogger<AuditLogMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Обрабатывает входящий HTTP-запрос
    /// </summary>

    public async Task InvokeAsync(HttpContext context)
    {
        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? string.Empty;

        if (!AuditedMethods.Contains(method) || IsExcludedPath(path))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();

        // Выполняем основной pipeline
        await _next(context);

        stopwatch.Stop();

        // Аудит пишем после ответа
        try
        {
            var auditService = context.RequestServices
                .GetRequiredService<IAuditService>();

            var actorId = ExtractActorId(context);

            var action = BuildActionName(method, path);
            var targetType = ExtractTargetType(path);
            var targetId = ExtractTargetId(path);

            var statusCode = context.Response.StatusCode;
            var details = BuildDetails(
                method, path, statusCode, stopwatch.ElapsedMilliseconds, context);

            await auditService.WriteAsync(
                action: action,
                targetType: targetType,
                targetId: targetId,
                details: details);

            _logger.LogDebug(
                "Аудит: Actor={ActorId} Action={Action} Status={Status} Elapsed={Elapsed}ms.",
                actorId ?? "anonymous",
                action,
                statusCode,
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            // Ошибка записи аудита не должна прерывать работу приложения
            _logger.LogError(ex,
                "AuditLogMiddleware: не удалось записать аудит-событие. " +
                "Method={Method} Path={Path}.",
                method, path);
        }
    }

    // Вспомогательные методы
    /// <summary>
    /// Проверяет, входит ли путь в список исключений аудита
    /// </summary>

    private static bool IsExcludedPath(string path)
    {
        foreach (var prefix in ExcludedPathPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Извлекает ID актора из текущего пользователя
    /// </summary>

    private static string? ExtractActorId(HttpContext context)
    {
        return context.User.FindFirstValue("UserId")
               ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    /// <summary>
    /// Формирует машиночитаемое имя действия из HTTP-метода и пути
    /// </summary>

    private static string BuildActionName(string method, string path)
    {
        // Нормализуем: убираем ведущий слэш, заменяем / и - на точки
        var normalizedPath = path
            .TrimStart('/')
            .Replace('/', '.')
            .Replace('-', '.')
            .ToLowerInvariant();

        var action = $"http.{method.ToLowerInvariant()}.{normalizedPath}";

        return action.Length > 128 ? action[..128] : action;
    }

    /// <summary>
    /// Определяет тип целевой сущности по пути запроса
    /// </summary>

    private static string? ExtractTargetType(string path)
    {
        // Извлекаем сегмент после /api/ как тип сущности
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length < 2 ||
            !segments[0].Equals("api", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var resource = segments[1];

        // Нормализация
        var targetType = resource switch
        {
            "measurements" => "Measurement",
            "backpack" => "BackpackItem",
            "auth" => "User",
            "children" => "Child",
            "recommendations" => "AIRecommendation",
            "export-jobs" => "ExportJob",
            "sync" => "SyncLog",
            "invite-codes" => "InviteCode",
            "doctor-notes" => "DoctorNote",
            "schedule" => "MeasurementSchedule",
            "telegram" => "TelegramWebhook",
            "admin" => "Admin",
            _ => resource
        };

        return targetType.Length > 128 ? targetType[..128] : targetType;
    }

    /// <summary>
    /// Извлекает ID целевой сущности из пути запроса
    /// </summary>
    private static string? ExtractTargetId(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        for (var i = 2; i < segments.Length; i++)
        {
            if (Guid.TryParse(segments[i], out _))
                return segments[i];
        }

        return null;
    }

    /// <summary>
    /// Формирует строку деталей аудит-записи
    /// </summary>
    private static string BuildDetails(
        string method,
        string path,
        int statusCode,
        long elapsedMs,
        HttpContext context)
    {
        var ip = context.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                     ?.Split(',')[0].Trim()
                 ?? context.Request.Headers["X-Real-IP"].FirstOrDefault()
                 ?? context.Connection.RemoteIpAddress?.ToString()
                 ?? "unknown";

        // Обрезаем User-Agent
        var userAgent = context.Request.Headers.UserAgent.ToString();
        if (userAgent.Length > 200)
            userAgent = userAgent[..200];

        var traceId = context.TraceIdentifier;

        var details = $"method={method} path={path} status={statusCode} " +
                      $"elapsed={elapsedMs}ms ip={ip} " +
                      $"ua={userAgent} traceId={traceId}";

        return details.Length > 4000 ? details[..4000] : details;
    }
}
