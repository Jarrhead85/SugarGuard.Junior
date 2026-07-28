using Microsoft.EntityFrameworkCore;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Data;
using SugarGuard.API.DTOs;
using System.Text;

namespace SugarGuard.API.Application.Services;

/// <summary>
/// Сервис для отправки уведомлений родителям через Telegram Bot
/// </summary>
public class TelegramNotificationService : ITelegramNotificationService
{
    private readonly AppDbContext _dbContext;
    private readonly ITelegramOutboxService _outbox;
    private readonly ILogger<TelegramNotificationService> _logger;

    public TelegramNotificationService(
        AppDbContext dbContext,
        ITelegramOutboxService outbox,
        ILogger<TelegramNotificationService> logger)
    {
        _dbContext = dbContext;
        _outbox = outbox;
        _logger = logger;
    }

    /// <summary>
    /// Отправляет уведомление об измерении глюкозы всем родителям ребёнка
    /// </summary>
    public async Task<NotificationResponse> SendMeasurementNotificationAsync(MeasurementNotificationRequest request)
    {
        try
        {
            _logger.LogInformation("Отправка уведомления об измерении: {GlucoseValue} ммоль/л для {ChildId}", 
                request.GlucoseValue, request.ChildId);

            // Получаем всех родителей ребёнка
            var parentTelegramIds = await GetParentTelegramIdsAsync(request.ChildId);
            if (!parentTelegramIds.Any())
            {
                _logger.LogWarning("Не найдено родителей для ребёнка {ChildId}", request.ChildId);
                return new NotificationResponse
                {
                    Success = false,
                    ParentsNotified = 0,
                    ErrorMessage = "Не найдено привязанных родителей"
                };
            }

            var child = await GetChildContextAsync(request.ChildId);

            // Формируем сообщение
            var message = FormatMeasurementMessage(child, request);

            // Отправляем уведомления всем родителям через единый метод
            return await QueueNotificationBatchAsync(parentTelegramIds, "measurement", message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при отправке уведомления об измерении");
            return new NotificationResponse
            {
                Success = false,
                ParentsNotified = 0,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Отправляет уведомление о съеденном перекусе всем родителям ребёнка
    /// </summary>
    public async Task<NotificationResponse> SendSnackConsumedNotificationAsync(SnackConsumedNotificationRequest request)
    {
        try
        {
            _logger.LogInformation("Отправка уведомления о перекусе: {SnackName} для {ChildId}", 
                request.SnackName, request.ChildId);

            // Получаем всех родителей ребёнка
            var parentTelegramIds = await GetParentTelegramIdsAsync(request.ChildId);
            if (!parentTelegramIds.Any())
            {
                _logger.LogWarning("Не найдено родителей для ребёнка {ChildId}", request.ChildId);
                return new NotificationResponse
                {
                    Success = false,
                    ParentsNotified = 0,
                    ErrorMessage = "Не найдено привязанных родителей"
                };
            }

            var child = await GetChildContextAsync(request.ChildId);

            // Формируем сообщение
            var message = FormatSnackConsumedMessage(child, request);

            // Отправляем уведомления всем родителям через единый метод
            return await QueueNotificationBatchAsync(parentTelegramIds, "snack", message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при отправке уведомления о перекусе");
            return new NotificationResponse
            {
                Success = false,
                ParentsNotified = 0,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Отправляет критическое уведомление с геолокацией всем родителям ребёнка
    /// </summary>
    public async Task<NotificationResponse> SendCriticalAlertAsync(CriticalAlertRequest request)
    {
        try
        {
            _logger.LogWarning("Отправка критического уведомления: {CriticalGlucose} ммоль/л для {ChildId}", 
                request.CriticalGlucose, request.ChildId);

            // Получаем всех родителей ребёнка
            var parentTelegramIds = await GetParentTelegramIdsAsync(request.ChildId);
            if (!parentTelegramIds.Any())
            {
                _logger.LogError("КРИТИЧЕСКАЯ ОШИБКА: Не найдено родителей для ребёнка {ChildId} при критическом уровне глюкозы!", 
                    request.ChildId);
                return new NotificationResponse
                {
                    Success = false,
                    ParentsNotified = 0,
                    ErrorMessage = "Не найдено привязанных родителей для критического уведомления"
                };
            }

            var child = await GetChildContextAsync(request.ChildId);

            // Формируем критическое сообщение
            var message = FormatCriticalAlertMessage(child, request);

            // Отправляем критические уведомления всем родителям через единый метод
            return await QueueNotificationBatchAsync(
                parentTelegramIds,
                "critical",
                message,
                request.Latitude,
                request.Longitude,
                requiresAcknowledgement: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Критическая ошибка при отправке критического уведомления");
            return new NotificationResponse
            {
                Success = false,
                ParentsNotified = 0,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task SendDailySummaryAsync(
        long telegramId,
        string message,
        CancellationToken cancellationToken = default)
    {
        await _outbox.QueueAsync(telegramId, "daily-summary", message, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Единый метод постановки уведомлений в очередь всем родителям.
    /// </summary>
    private async Task<NotificationResponse> QueueNotificationBatchAsync(
        IEnumerable<long> parentTelegramIds,
        string messageType,
        string message,
        double? latitude = null,
        double? longitude = null,
        bool requiresAcknowledgement = false)
    {
        var queuedCount = 0;
        var errors = new List<string>();

        foreach (var telegramId in parentTelegramIds.Distinct())
        {
            try
            {
                await _outbox.QueueAsync(telegramId, messageType, message, latitude, longitude, requiresAcknowledgement);
                queuedCount++;
            }
            catch (Exception ex)
            {
                var error = $"Ошибка постановки уведомления для родителя {telegramId}: {ex.Message}";
                errors.Add(error);
                _logger.LogError(ex, "✗ {Error}", error);
            }
        }

        return new NotificationResponse
        {
            Success = queuedCount > 0,
            ParentsNotified = queuedCount,
            ErrorMessage = errors.Any() ? string.Join("; ", errors) : null
        };
    }

    /// <summary>
    /// Получает Telegram ID всех родителей ребёнка
    /// </summary>
    private async Task<List<long>> GetParentTelegramIdsAsync(string childId)
    {
        if (!Guid.TryParse(childId, out var childGuid))
            return new List<long>();
        return await _dbContext.ParentChildLinks
            .Where(pcl => pcl.ChildId == childGuid)
            .Join(_dbContext.Users, pcl => pcl.ParentUserId, u => u.UserId, (pcl, u) => u.TelegramId)
            .Where(telegramId => telegramId.HasValue)
            .Select(telegramId => telegramId!.Value)
            .ToListAsync();
    }

    /// <summary>
    /// Получает контекст ребёнка для отображения времени в уведомлениях.
    /// В ранних записях часовой пояс мог остаться значением UTC по умолчанию,
    /// поэтому для российских пользователей используем Москву как безопасный fallback.
    /// </summary>
    private async Task<ChildNotificationContext> GetChildContextAsync(string childId)
    {
        if (!Guid.TryParse(childId, out var childGuid))
        {
            return ChildNotificationContext.Default;
        }

        var child = await _dbContext.Children
            .Where(c => c.ChildId == childGuid)
            .Select(c => new { c.FirstName, c.LastName, c.TimeZoneId })
            .FirstOrDefaultAsync();

        if (child is null)
        {
            return ChildNotificationContext.Default;
        }

        var name = $"{child.FirstName} {child.LastName}".Trim();
        return string.IsNullOrWhiteSpace(name)
            ? ChildNotificationContext.Default
            : new ChildNotificationContext(name, child.TimeZoneId);
    }

    private static string FormatLocalTime(DateTime value, string? timeZoneId)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

        return TimeZoneInfo.ConvertTimeFromUtc(utc, ResolveTimeZone(timeZoneId)).ToString("HH:mm");
    }

    private static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        if (!string.IsNullOrWhiteSpace(timeZoneId) &&
            !string.Equals(timeZoneId, "UTC", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        foreach (var timeZoneIdCandidate in new[] { "Europe/Moscow", "Russian Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneIdCandidate);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Utc;
    }

    /// <summary>
    /// Формирует сообщение об измерении глюкозы
    /// </summary>
    private static string FormatMeasurementMessage(ChildNotificationContext child, MeasurementNotificationRequest request)
    {
        var statusEmoji = GetStatusEmoji(request.Status);
        var timeStr = FormatLocalTime(request.MeasurementTime, child.TimeZoneId);

        var message = new StringBuilder();
        message.AppendLine($"{statusEmoji} Измерение глюкозы");
        message.AppendLine($"👤 Ребёнок: {child.Name}");
        message.AppendLine($"📊 Уровень: {request.GlucoseValue:F1} ммоль/л");
        message.AppendLine($"📈 Статус: {request.Status}");
        message.AppendLine($"🕐 Время: {timeStr}");

        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            message.AppendLine($"📝 Заметки: {request.Notes}");
        }

        return message.ToString();
    }

    /// <summary>
    /// Формирует сообщение о съеденном перекусе
    /// </summary>
    private static string FormatSnackConsumedMessage(ChildNotificationContext child, SnackConsumedNotificationRequest request)
    {
        var timeStr = FormatLocalTime(request.ConsumedAt, child.TimeZoneId);

        var message = new StringBuilder();
        message.AppendLine("🍴 Перекус съеден");
        message.AppendLine($"👤 Ребёнок: {child.Name}");
        message.AppendLine($"🥪 Перекус: {request.SnackName}");
        message.AppendLine($"🍞 Хлебные единицы: {request.BreadUnits:F1} ХЕ");
        message.AppendLine($"📊 Текущая глюкоза: {request.CurrentGlucose:F1} ммоль/л");
        message.AppendLine($"🕐 Время: {timeStr}");

        return message.ToString();
    }

    /// <summary>
    /// Формирует критическое сообщение с геолокацией
    /// </summary>
    private static string FormatCriticalAlertMessage(ChildNotificationContext child, CriticalAlertRequest request)
    {
        var timeStr = FormatLocalTime(request.MeasurementTime, child.TimeZoneId);
        var criticalType = request.CriticalGlucose < 3.3 ? "КРИТИЧЕСКИ НИЗКИЙ" : "КРИТИЧЕСКИ ВЫСОКИЙ";

        var message = new StringBuilder();
        message.AppendLine("🚨 КРИТИЧЕСКОЕ СОСТОЯНИЕ!");
        message.AppendLine($"👤 Ребёнок: {child.Name}");
        message.AppendLine($"📊 Уровень: {request.CriticalGlucose:F1} ммоль/л");
        message.AppendLine($"⚠️ Статус: {criticalType}");
        message.AppendLine($"🕐 Время: {timeStr}");
        message.AppendLine();
        message.AppendLine("🆘 Требуется немедленная помощь!");

        if (!string.IsNullOrWhiteSpace(request.Address))
        {
            message.AppendLine($"📍 Адрес: {request.Address}");
        }

        return message.ToString();
    }

    /// <summary>
    /// Получает эмодзи для статуса глюкозы
    /// </summary>
    private static string GetStatusEmoji(string status)
    {
        return status.ToLower() switch
        {
            "критически низкий" or "критически высокий" => "🚨",
            "низкий" or "высокий" => "⚠️",
            "норма" => "✅",
            _ => "📊"
        };
    }

    private sealed record ChildNotificationContext(string Name, string? TimeZoneId)
    {
        public static ChildNotificationContext Default { get; } = new("Ребёнок", "Europe/Moscow");
    }

}
