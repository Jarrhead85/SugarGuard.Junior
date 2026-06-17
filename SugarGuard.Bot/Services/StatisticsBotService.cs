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
            var statistics = await _apiClient.GetStatisticsAsync(childId, period, date, cancellationToken);

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
                var tableMessage = FormatMeasurementsTable(statistics.Measurements, statistics.Period);
                
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
        sb.AppendLine($"🕐 Обновлено: {statistics.GeneratedAt:HH:mm dd.MM.yyyy}");

        return sb.ToString();
    }

    /// <summary>
    /// Форматирует таблицу измерений
    /// </summary>
    private static string FormatMeasurementsTable(List<MeasurementResponseBot> measurements, string period)
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
            var timeStr = measurement.MeasurementTime.ToString("dd.MM HH:mm");
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
            var pdfBytes = await _apiClient.ExportStatisticsToPdfAsync(childId, period, false, null, cancellationToken);

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
