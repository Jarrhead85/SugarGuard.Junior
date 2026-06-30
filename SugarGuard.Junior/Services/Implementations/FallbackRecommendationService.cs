// Реализация сервиса локальных fallback рекомендаций
using Microsoft.Extensions.Logging;
using SugarGuard.Junior.Models.Core;
using SugarGuard.Junior.Models.Enums;
using SugarGuard.Junior.Services.Interfaces;

namespace SugarGuard.Junior.Services.Implementations;

/// <summary>
/// Сервис для генерации локальных fallback рекомендаций
/// Используется когда GigaChat недоступен
/// </summary>
public class FallbackRecommendationService : IFallbackRecommendationService
{
    private readonly ILogger<FallbackRecommendationService> _logger;

    public FallbackRecommendationService(ILogger<FallbackRecommendationService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Получает локальную рекомендацию на основе статуса глюкозы и профиля ребёнка
    /// </summary>
    public string GetRecommendation(GlucoseStatus status, Child child, List<string> availableSnacks)
    {
        _logger.LogInformation(
            "Генерация локальной рекомендации: статус={Status}, возраст={Age}",
            status, child.AgeInYears);

        return status switch
        {
            GlucoseStatus.CriticallyLow => GetCriticallyLowRecommendation(availableSnacks),
            GlucoseStatus.Low => GetLowRecommendation(availableSnacks),
            GlucoseStatus.Normal => GetNormalRecommendation(),
            GlucoseStatus.High => GetHighRecommendation(child),
            GlucoseStatus.CriticallyHigh => GetCriticallyHighRecommendation(child),
            _ => "Проверьте уровень глюкозы и обратитесь к врачу."
        };
    }

    /// <summary>
    /// Рекомендация при критически низкой глюкозе
    /// </summary>
    private string GetCriticallyLowRecommendation(List<string> availableSnacks)
    {
        var fastCarbs = GetFastCarbsRecommendation(availableSnacks);
        
        return $" КРИТИЧЕСКАЯ ГИПОГЛИКЕМИЯ!\n\n" +
               $"НЕМЕДЛЕННО дайте быстрый углевод (1-2 ХЕ):\n" +
               $"{fastCarbs}\n\n" +
               $"Проверьте глюкозу через 10-15 минут.\n" +
               $"При необходимости повторите приём углеводов.";
    }

    /// <summary>
    /// Рекомендация при низкой глюкозе
    /// </summary>
    private string GetLowRecommendation(List<string> availableSnacks)
    {
        var fastCarbs = GetFastCarbsRecommendation(availableSnacks);
        
        return $" ГИПОГЛИКЕМИЯ!\n\n" +
               $"Дайте перекус (0.5-1 ХЕ) с быстрыми углеводами:\n" +
               $"{fastCarbs}\n\n" +
               $"Проверьте глюкозу через 15-20 минут.";
    }

    /// <summary>
    /// Рекомендация при нормальной глюкозе
    /// </summary>
    private string GetNormalRecommendation()
    {
        return $"✅ Отлично! Глюкоза в норме.\n\n" +
               $"Продолжайте обычный режим.\n" +
               $"Следите за регулярными измерениями.";
    }

    /// <summary>
    /// Рекомендация при высокой глюкозе
    /// </summary>
    private string GetHighRecommendation(Child child)
    {
        return "Глюкоза выше цели. Сообщи взрослому, пей воду и действуй по своему плану коррекции. " +
               "Не ешь дополнительные углеводы без взрослого.";
    }

    /// <summary>
    /// Рекомендация при критически высокой глюкозе
    /// </summary>
    private string GetCriticallyHighRecommendation(Child child)
    {
        return $" КРИТИЧЕСКАЯ ГИПЕРГЛИКЕМИЯ!\n\n" +
               $"СРОЧНЫЕ действия:\n" +
               $"• Проверьте дозу инсулина\n" +
               $"• Предложите пить воду\n" +
               $"• Проверьте кетоны (если возможно)\n" +
               $"• ОБРАТИТЕСЬ К ВРАЧУ!\n\n" +
               $"Не давайте дополнительных углеводов.\n" +
               $"Проверяйте глюкозу каждые 30 минут.";
    }

    /// <summary>
    /// Формирует рекомендацию для быстрых углеводов
    /// </summary>
    private string GetFastCarbsRecommendation(List<string> availableSnacks)
    {
        var fastCarbs = availableSnacks
            .Where(s => IsFastCarb(s))
            .Take(3)
            .ToList();

        if (fastCarbs.Count == 0)
        {
            return "• Сок (1.5 ХЕ / 200мл)\n" +
                   "• Сахар (1 ХЕ / 3-5 кусков)\n" +
                   "• Конфета (0.5 ХЕ)";
        }

        return string.Join("\n", fastCarbs.Select(s => $"• {s}"));
    }

    /// <summary>
    /// Проверяет, является ли перекус "быстрым углеводом"
    /// </summary>
    private bool IsFastCarb(string snackName)
    {
        var fastCarbKeywords = new[] 
        { 
            "сок", "сахар", "конфета", "печенье", 
            "мёд", "компот", "ягода", "фрукт" 
        };
        
        return fastCarbKeywords.Any(kw => 
            snackName.ToLower().Contains(kw));
    }
}
