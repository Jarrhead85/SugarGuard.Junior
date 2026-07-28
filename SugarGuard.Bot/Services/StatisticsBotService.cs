using Microsoft.Extensions.Logging;
using SugarGuard.Bot.Keyboards;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace SugarGuard.Bot.Services;

/// <summary>
/// Сервис для работы со статистикой в Telegram-боте
/// Отвечает за отображение статистических данных и таблиц измерений
/// </summary>
public class StatisticsBotService
{
    private readonly ITelegramBotClient _botClient;
    private readonly ApiClient _apiClient;
    private readonly StatisticsKeyboard _statisticsKeyboard;
    private readonly ILogger<StatisticsBotService> _logger;

    public StatisticsBotService(
        ITelegramBotClient botClient,
        ApiClient apiClient,
        StatisticsKeyboard statisticsKeyboard,
        ILogger<StatisticsBotService> logger)
    {
        _botClient = botClient;
        _apiClient = apiClient;
        _statisticsKeyboard = statisticsKeyboard;
        _logger = logger;
    }

    /// <summary>
    /// Показывает меню выбора периода статистики
    /// </summary>
    public async Task ShowStatisticsMenuAsync(long chatId, long userId, CancellationToken cancellationToken)
    {
        try
        {
            var message = """
                📊 **Статистика измерений**
                
                Выберите период для просмотра статистики:
                
                📅 **День** - статистика за сегодня
                📊 **Неделя** - статистика за текущую неделю  
                📈 **Месяц** - статистика за текущий месяц
                📋 **Год** - статистика за текущий год
                """;

            await _botClient.SendTextMessageAsync(
                chatId: chatId,
                text: message,
                parseMode: ParseMode.Markdown,
                replyMarkup: _statisticsKeyboard.GetPeriodSelectionKeyboard(),
                cancellationToken: cancellationToken
            );

            _logger.LogInformation("Отображено меню статистики для пользователя {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при отображении меню статистики");
            await SendErrorMessageAsync(chatId, "Не удалось загрузить меню статистики", cancellationToken);
        }
    }

    /// <summary>
    /// Показывает статистику за выбранный период
    /// </summary>
    public async Task ShowPeriodStatisticsAsync(
        long chatId, 
        long userId, 
        Guid childId, 
        string period, 
        DateTime? date = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Загрузка статистики для ребёнка {ChildId}, период {Period}", childId, period);

            // Получаем статистику с API
            var statistics = await _apiClient.GetStatisticsAsync(userId, childId, period, date, cancellationToken);

            if (statistics == null)
            {
                await SendErrorMessageAsync(chatId, "Не удалось загрузить статистику", cancellationToken);
                return;
            }

            // Формируем сообщение со статистикой
            var message = FormatStatisticsMessage(statistics);

            await _botClient.SendTextMessageAsync(
                chatId: chatId,
                text: message,
                parseMode: ParseMode.Markdown,
                replyMarkup: _statisticsKeyboard.GetStatisticsActionsKeyboard(period),
                cancellationToken: cancellationToken
            );

            // Если есть измерения, отправляем таблицу отдельным сообщением
            if (statistics.Measurements.Any())
            {
                var tableMessage = FormatMeasurementsTable(statistics.Measurements, statistics.Period, statistics.TimeZoneId);
                
                await _botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: tableMessage,
                    parseMode: ParseMode.Markdown,
                    cancellationToken: cancellationToken
                );
            }

            _logger.LogInformation("Статистика отправлена пользователю {UserId}: {Count} измерений", 
                userId, statistics.TotalMeasurements);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при отображении статистики");
            await SendErrorMessageAsync(chatId, "Произошла ошибка при загрузке статистики", cancellationToken);
        }
    }

    /// <summary>
    /// Показывает последнее измерение и простую динамику относительно предыдущего.
    /// Это информационный экран: он не даёт медицинских рекомендаций.
    /// </summary>
    public async Task ShowLastMeasurementAsync(
        long chatId,
        long userId,
        Guid childId,
        CancellationToken cancellationToken)
    {
        try
        {
            // За сутки обычно достаточно данных. Если утренних измерений ещё не было,
            // запрашиваем неделю, чтобы не показывать ложное «данных нет».
            var statistics = await _apiClient.GetStatisticsAsync(userId, childId, "day", null, cancellationToken);
            if (statistics?.Measurements.Count == 0)
            {
                statistics = await _apiClient.GetStatisticsAsync(userId, childId, "week", null, cancellationToken);
            }

            var measurements = statistics?.Measurements
                .OrderByDescending(item => item.MeasurementTime)
                .Take(2)
                .ToList() ?? [];

            if (measurements.Count == 0)
            {
                await _botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: "🩸 Последних измерений пока нет. Когда приложение синхронизирует данные, они появятся здесь.",
                    replyMarkup: CreateLastMeasurementKeyboard(),
                    cancellationToken: cancellationToken);
                return;
            }

            var latest = measurements[0];
            var timeZoneId = statistics?.TimeZoneId ?? "Europe/Moscow";
            var trend = measurements.Count == 1
                ? "— недостаточно данных для динамики"
                : FormatTrend(latest.GlucoseValue - measurements[1].GlucoseValue);

            var message = new StringBuilder()
                .AppendLine("🩸 **Последнее измерение**")
                .AppendLine()
                .AppendLine($"Значение: **{latest.GlucoseValue:F1} ммоль/л**")
                .AppendLine($"Статус: {FormatStatus(latest.GlucoseStatus)}")
                .AppendLine($"Время: {ToLocalTime(latest.MeasurementTime, timeZoneId):dd.MM.yyyy HH:mm}")
                .AppendLine($"Динамика: {trend}")
                .AppendLine()
                .AppendLine("Данные предназначены для контроля. При плохом самочувствии ребёнка следуйте индивидуальному плану врача.")
                .ToString();

            await _botClient.SendTextMessageAsync(
                chatId: chatId,
                text: message,
                parseMode: ParseMode.Markdown,
                replyMarkup: CreateLastMeasurementKeyboard(),
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не удалось показать последнее измерение пользователю {UserId}", userId);
            await SendErrorMessageAsync(chatId, "Не удалось загрузить последнее измерение", cancellationToken);
        }
    }

    private static Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup CreateLastMeasurementKeyboard() =>
        new(new[]
        {
            new[] { Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("🔄 Обновить", "last_measurement") },
            new[] { Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("📊 Статистика", "statistics") },
            new[] { Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("🏠 Главное меню", "main_menu") }
        });

    private static string FormatTrend(decimal difference)
    {
        if (Math.Abs(difference) < 0.1m)
        {
            return "→ без заметного изменения";
        }

        return difference > 0
            ? $"↗ +{difference:F1} ммоль/л относительно предыдущего"
            : $"↘ {difference:F1} ммоль/л относительно предыдущего";
    }

    private static string FormatStatus(string status) => status switch
    {
        "Normal" => "🟢 в целевом диапазоне",
        "Low" => "🟡 ниже целевого диапазона",
        "High" => "🟠 выше целевого диапазона",
        "CriticallyLow" => "🔴 критически низкий уровень",
        "CriticallyHigh" => "🔴 критически высокий уровень",
        _ => "⚪ статус не указан"
    };

    /// <summary>
    /// Форматирует статистические показатели в текстовое сообщение
    /// </summary>
    private static string FormatStatisticsMessage(StatisticsResponse statistics)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine($"📊 **Статистика за {statistics.Period.ToLower()}**");
        sb.AppendLine($"📅 {statistics.FromDate:dd.MM.yyyy} - {statistics.ToDate:dd.MM.yyyy}");
        sb.AppendLine();

        if (statistics.TotalMeasurements == 0)
        {
            sb.AppendLine("📭 **Нет измерений за выбранный период**");
            sb.AppendLine();
            sb.AppendLine("Попробуйте выбрать другой период или проверьте, что ребёнок вводит измерения в приложение.");
            return sb.ToString();
        }

        // Основные показатели
        sb.AppendLine("📈 **Основные показатели:**");
        sb.AppendLine($"• Всего измерений: **{statistics.TotalMeasurements}**");
        sb.AppendLine($"• Среднее значение: **{statistics.AverageGlucose:F1} ммоль/л**");
        sb.AppendLine($"• Минимум: **{statistics.MinGlucose:F1} ммоль/л**");
        sb.AppendLine($"• Максимум: **{statistics.MaxGlucose:F1} ммоль/л**");
        sb.AppendLine($"• Вариабельность: **{statistics.StandardDeviation:F1}**");
        sb.AppendLine();

        // Время в диапазоне
        var rangeEmoji = statistics.TimeInTargetRange >= 70 ? "✅" : statistics.TimeInTargetRange >= 50 ? "⚠️" : "❌";
        sb.AppendLine("🎯 **Время в целевом диапазоне (4.0-10.0):**");
        sb.AppendLine($"{rangeEmoji} **{statistics.TimeInTargetRange:F1}%**");
        sb.AppendLine();

        // Эпизоды
        sb.AppendLine("⚡ **Эпизоды:**");
        sb.AppendLine($"🔻 Гипогликемия (<4.0): **{statistics.HypoEpisodes}**");
        sb.AppendLine($"🔺 Гипергликемия (>10.0): **{statistics.HyperEpisodes}**");
        
        if (statistics.CriticalEpisodes > 0)
        {
            sb.AppendLine($"🚨 Критические (<3.1 или >15.0): **{statistics.CriticalEpisodes}**");
        }

        sb.AppendLine();
        sb.AppendLine($"🕐 Обновлено: {ToLocalTime(statistics.GeneratedAt, statistics.TimeZoneId):HH:mm dd.MM.yyyy}");

        return sb.ToString();
    }

    /// <summary>
    /// Форматирует таблицу измерений
    /// </summary>
    private static string FormatMeasurementsTable(List<MeasurementResponseBot> measurements, string period, string timeZoneId)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine($"📋 **Таблица измерений за {period.ToLower()}**");
        sb.AppendLine();
        sb.AppendLine("```");
        sb.AppendLine("Время      Глюкоза  Статус");
        sb.AppendLine("─────────────────────────────");

        // Берём последние 20 измерений для отображения
        var displayMeasurements = measurements.Take(20).ToList();

        foreach (var measurement in displayMeasurements)
        {
            var timeStr = ToLocalTime(measurement.MeasurementTime, timeZoneId).ToString("dd.MM HH:mm");
            var glucoseStr = $"{measurement.GlucoseValue:F1}".PadLeft(6);
            var statusStr = GetStatusEmoji(measurement.GlucoseStatus);
            
            sb.AppendLine($"{timeStr}  {glucoseStr}  {statusStr}");
        }

        if (measurements.Count > 20)
        {
            sb.AppendLine($"... и ещё {measurements.Count - 20} измерений");
        }

        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("🔍 **Обозначения:**");
        sb.AppendLine("🟢 Норма (4.0-10.0) | 🟡 Низко (3.1-3.9) | 🔴 Высоко (10.1-15.0)");
        sb.AppendLine("🚨 Критически низко (<3.1) | ⚠️ Критически высоко (>15.0)");

        return sb.ToString();
    }

    private static DateTime ToLocalTime(DateTime value, string? timeZoneId)
    {
        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(string.IsNullOrWhiteSpace(timeZoneId) ? "Europe/Moscow" : timeZoneId);
            var utc = value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
            return TimeZoneInfo.ConvertTimeFromUtc(utc, zone);
        }
        catch (TimeZoneNotFoundException)
        {
            return value.ToLocalTime();
        }
    }

    /// <summary>
    /// Возвращает эмодзи для статуса глюкозы
    /// </summary>
    private static string GetStatusEmoji(string status)
    {
        return status switch
        {
            "Normal" => "🟢 Норма",
            "Low" => "🟡 Низко",
            "High" => "🔴 Высоко", 
            "CriticallyLow" => "🚨 Крит.низко",
            "CriticallyHigh" => "⚠️ Крит.высоко",
            _ => "❓ Неизв."
        };
    }

    /// <summary>
    /// Отправляет сообщение об ошибке
    /// </summary>
    private async Task SendErrorMessageAsync(long chatId, string errorText, CancellationToken cancellationToken)
    {
        try
        {
            var message = $"""
                ❌ **Ошибка**
                
                {errorText}
                
                Попробуйте позже или обратитесь в поддержку.
                """;

            await _botClient.SendTextMessageAsync(
                chatId: chatId,
                text: message,
                parseMode: ParseMode.Markdown,
                replyMarkup: _statisticsKeyboard.GetErrorKeyboard(),
                cancellationToken: cancellationToken
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при отправке сообщения об ошибке");
        }
    }

    /// <summary>
    /// Обновляет статистику (повторно загружает данные)
    /// </summary>
    public async Task RefreshStatisticsAsync(
        long chatId, 
        long userId, 
        Guid childId, 
        string period,
        CancellationToken cancellationToken)
    {
        try
        {
            // Отправляем сообщение о загрузке
            var loadingMessage = await _botClient.SendTextMessageAsync(
                chatId: chatId,
                text: "🔄 Обновление статистики...",
                cancellationToken: cancellationToken
            );

            // Загружаем обновлённую статистику
            await ShowPeriodStatisticsAsync(chatId, userId, childId, period, null, cancellationToken);

            // Удаляем сообщение о загрузке
            try
            {
                await _botClient.DeleteMessageAsync(chatId, loadingMessage.MessageId, cancellationToken);
            }
            catch
            {
                // Игнорируем ошибку удаления сообщения
            }

            _logger.LogInformation("Статистика обновлена для пользователя {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обновлении статистики");
            await SendErrorMessageAsync(chatId, "Не удалось обновить статистику", cancellationToken);
        }
    }

    /// <summary>
    /// Допустимые значения периода (whitelist — соответствует API <c>MeasurementsController</c>).
    /// </summary>
    private static readonly IReadOnlySet<string> AllowedPeriods = new HashSet<string>(StringComparer.Ordinal)
    {
        "day", "week", "month", "year"
    };

    /// <summary>
    /// Экспортирует статистику в PDF и отправляет файл пользователю
    /// </summary>
    public async Task ExportToPdfAsync(
        long chatId,
        long userId,
        Guid childId,
        string period,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(period) || !AllowedPeriods.Contains(period))
            {
                _logger.LogWarning("Недопустимый период '{Period}' для пользователя {UserId}", period, userId);
                await _botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: "❌ Не удалось сгенерировать PDF-отчёт: неподдерживаемый период.",
                    cancellationToken: cancellationToken
                );
                return;
            }

            _logger.LogInformation("Начинаем экспорт PDF для пользователя {UserId}, период {Period}", userId, period);

            // Отправляем сообщение о генерации PDF
            var loadingMessage = await _botClient.SendTextMessageAsync(
                chatId: chatId,
                text: "📄 Генерация PDF-отчёта...",
                cancellationToken: cancellationToken
            );

            // Получаем PDF от API
            var pdfBytes = await _apiClient.ExportStatisticsToPdfAsync(userId, childId, period, false, null, cancellationToken);

            if (pdfBytes == null || pdfBytes.Length == 0)
            {
                await _botClient.EditMessageTextAsync(
                    chatId: chatId,
                    messageId: loadingMessage.MessageId,
                    text: "❌ Не удалось сгенерировать PDF-отчёт. Попробуйте позже.",
                    cancellationToken: cancellationToken
                );
                return;
            }

            // Формируем имя файла
            var periodName = period switch
            {
                "day" => "День",
                "week" => "Неделя",
                "month" => "Месяц",
                "year" => "Год",
                _ => "Период"
            };

            var safeFileName = $"SugarGuard_Report_{periodName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.pdf";

            // Создаём временный файл. `Path.Combine` гарантирует корректную обработку trailing slash.
            var tempFilePath = Path.Combine(Path.GetTempPath(), safeFileName);
            await File.WriteAllBytesAsync(tempFilePath, pdfBytes, cancellationToken);

            try
            {
                // Отправляем PDF файл. `FileShare.Read` — предотвращает TOCTOU при concurrent
                // попытке чтения/удаления из другого процесса.
                using var fileStream = new FileStream(
                    tempFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);

                await _botClient.SendDocumentAsync(
                    chatId: chatId,
                    document: Telegram.Bot.Types.InputFile.FromStream(fileStream, safeFileName),
                    caption: $"📊 Отчёт по глюкозе за {periodName.ToLower()}\n🕐 Сгенерирован: {DateTime.Now:dd.MM.yyyy HH:mm}",
                    cancellationToken: cancellationToken
                );

                // Удаляем сообщение о загрузке
                try
                {
                    await _botClient.DeleteMessageAsync(chatId, loadingMessage.MessageId, cancellationToken);
                }
                catch
                {
                    // Игнорируем ошибку удаления сообщения
                }

                _logger.LogInformation("✓ PDF-отчёт отправлен пользователю {UserId}, размер: {Size} байт",
                    userId, pdfBytes.Length);
            }
            finally
            {
                // Удаляем временный файл
                try
                {
                    File.Delete(tempFilePath);
                }
                catch (Exception deleteEx)
                {
                    _logger.LogWarning(deleteEx, "Не удалось удалить временный файл {FilePath}", tempFilePath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при экспорте PDF для пользователя {UserId}", userId);

            await _botClient.SendTextMessageAsync(
                chatId: chatId,
                text: "❌ Произошла ошибка при генерации PDF-отчёта. Попробуйте позже.",
                cancellationToken: cancellationToken
            );
        }
    }
}
