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
    /// <summary>
    /// Имя типизированного клиента
    /// </summary>
    public const string HttpClientName = "TelegramBotApi";

    private readonly AppDbContext _dbContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TelegramNotificationService> _logger;
    private readonly IConfiguration _configuration;

    private string? _botToken;
    private string BotToken => _botToken ??= _configuration["Telegram:BotToken"]
        ?? throw new InvalidOperationException("Telegram Bot Token не настроен в конфигурации");

    public TelegramNotificationService(
        AppDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        ILogger<TelegramNotificationService> logger,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _configuration = configuration;
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

            // Получаем имя ребёнка для уведомления
            var childName = await GetChildNameAsync(request.ChildId);

            // Формируем сообщение
            var message = FormatMeasurementMessage(childName, request);

            // Отправляем уведомления всем родителям через единый метод
            return await SendNotificationBatchAsync(parentTelegramIds, async (telegramId) =>
            {
                await SendTelegramMessageAsync(telegramId, message);
                _logger.LogDebug("Уведомление отправлено родителю {TelegramId}", telegramId);
            });
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

            // Получаем имя ребёнка для уведомления
            var childName = await GetChildNameAsync(request.ChildId);

            // Формируем сообщение
            var message = FormatSnackConsumedMessage(childName, request);

            // Отправляем уведомления всем родителям через единый метод
            return await SendNotificationBatchAsync(parentTelegramIds, async (telegramId) =>
            {
                await SendTelegramMessageAsync(telegramId, message);
                _logger.LogDebug("Уведомление о перекусе отправлено родителю {TelegramId}", telegramId);
            });
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

            // Получаем имя ребёнка для уведомления
            var childName = await GetChildNameAsync(request.ChildId);

            // Формируем критическое сообщение
            var message = FormatCriticalAlertMessage(childName, request);

            // Отправляем критические уведомления всем родителям через единый метод
            return await SendNotificationBatchAsync(parentTelegramIds, async (telegramId) =>
            {
                // Отправляем текстовое сообщение
                await SendTelegramMessageAsync(telegramId, message);

                // Если есть координаты, отправляем геолокацию
                if (request.Latitude.HasValue && request.Longitude.HasValue)
                {
                    await SendTelegramLocationAsync(telegramId, request.Latitude.Value, request.Longitude.Value);
                }

                _logger.LogInformation("Критическое уведомление отправлено родителю {TelegramId}", telegramId);
            });
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

    /// <summary>
    /// Единый метод пакетной отправки уведомлений всем родителям
    /// </summary>
    private async Task<NotificationResponse> SendNotificationBatchAsync(
        IEnumerable<long> parentTelegramIds,
        Func<long, Task> sendAction)
    {
        int successCount = 0;
        var errors = new List<string>();

        foreach (var telegramId in parentTelegramIds)
        {
            try
            {
                await sendAction(telegramId);
                successCount++;
            }
            catch (Exception ex)
            {
                var error = $"Ошибка отправки родителю {telegramId}: {ex.Message}";
                errors.Add(error);
                _logger.LogError(ex, "✗ {Error}", error);
            }
        }

        return new NotificationResponse
        {
            Success = successCount > 0,
            ParentsNotified = successCount,
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
    /// Получает имя ребёнка для отображения в уведомлениях
    /// </summary>
    private async Task<string> GetChildNameAsync(string childId)
    {
        if (!Guid.TryParse(childId, out var childGuid))
            return "Ребёнок";
        var child = await _dbContext.Children
            .Where(c => c.ChildId == childGuid)
            .Select(c => new { c.FirstName, c.LastName })
            .FirstOrDefaultAsync();

        if (child == null)
            return "Ребёнок";

        return $"{child.FirstName} {child.LastName}".Trim();
    }

    /// <summary>
    /// Формирует сообщение об измерении глюкозы
    /// </summary>
    private static string FormatMeasurementMessage(string childName, MeasurementNotificationRequest request)
    {
        var statusEmoji = GetStatusEmoji(request.Status);
        var timeStr = request.MeasurementTime.ToString("HH:mm");

        var message = new StringBuilder();
        message.AppendLine($"{statusEmoji} Измерение глюкозы");
        message.AppendLine($"👤 Ребёнок: {childName}");
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
    private static string FormatSnackConsumedMessage(string childName, SnackConsumedNotificationRequest request)
    {
        var timeStr = request.ConsumedAt.ToString("HH:mm");

        var message = new StringBuilder();
        message.AppendLine("🍴 Перекус съеден");
        message.AppendLine($"👤 Ребёнок: {childName}");
        message.AppendLine($"🥪 Перекус: {request.SnackName}");
        message.AppendLine($"🍞 Хлебные единицы: {request.BreadUnits:F1} ХЕ");
        message.AppendLine($"📊 Текущая глюкоза: {request.CurrentGlucose:F1} ммоль/л");
        message.AppendLine($"🕐 Время: {timeStr}");

        return message.ToString();
    }

    /// <summary>
    /// Формирует критическое сообщение с геолокацией
    /// </summary>
    private static string FormatCriticalAlertMessage(string childName, CriticalAlertRequest request)
    {
        var timeStr = request.MeasurementTime.ToString("HH:mm");
        var criticalType = request.CriticalGlucose < 3.3 ? "КРИТИЧЕСКИ НИЗКИЙ" : "КРИТИЧЕСКИ ВЫСОКИЙ";

        var message = new StringBuilder();
        message.AppendLine("🚨 КРИТИЧЕСКОЕ СОСТОЯНИЕ!");
        message.AppendLine($"👤 Ребёнок: {childName}");
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

    /// <summary>
    /// Отправляет текстовое сообщение через Telegram Bot API
    /// </summary>
    private async Task SendTelegramMessageAsync(long chatId, string message)
    {
        // Валидация chatId
        if (chatId <= 0)
        {
            _logger.LogWarning("Невалидный Telegram chat ID: {ChatId}", chatId);
            throw new ArgumentException($"Невалидный chat ID: {chatId}. Chat ID должен быть положительным числом.", nameof(chatId));
        }

        // Валидация message
        if (string.IsNullOrWhiteSpace(message))
        {
            _logger.LogWarning("Попытка отправить пустое сообщение в chat {ChatId}", chatId);
            throw new ArgumentException("Сообщение не может быть пустым", nameof(message));
        }

        var url = $"https://api.telegram.org/bot{BotToken}/sendMessage";

        var payload = new
        {
            chat_id = chatId,
            text = message,
            parse_mode = "HTML"
        };

        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var client = _httpClientFactory.CreateClient(HttpClientName);
        var response = await client.PostAsync(url, content);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Telegram API error: {response.StatusCode}, {errorContent}");
        }
    }

    /// <summary>
    /// Отправляет геолокацию через Telegram Bot API
    /// </summary>
    private async Task SendTelegramLocationAsync(long chatId, double latitude, double longitude)
    {
        // Валидация chatId
        if (chatId <= 0)
        {
            _logger.LogWarning("Невалидный Telegram chat ID для геолокации: {ChatId}", chatId);
            throw new ArgumentException($"Невалидный chat ID: {chatId}. Chat ID должен быть положительным числом.", nameof(chatId));
        }

        var url = $"https://api.telegram.org/bot{BotToken}/sendLocation";

        var payload = new
        {
            chat_id = chatId,
            latitude = latitude,
            longitude = longitude
        };

        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var client = _httpClientFactory.CreateClient(HttpClientName);
        var response = await client.PostAsync(url, content);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Telegram API error for location: {response.StatusCode}, {errorContent}");
        }
    }
}
