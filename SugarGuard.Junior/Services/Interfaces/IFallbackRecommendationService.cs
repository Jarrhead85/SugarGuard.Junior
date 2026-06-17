// Интерфейс для локальных fallback рекомендаций
using SugarGuard.Junior.Models.Core;
using SugarGuard.Junior.Models.Enums;

namespace SugarGuard.Junior.Services.Interfaces;

/// <summary>
/// Интерфейс для генерации локальных fallback рекомендаций
/// Используется когда GigaChat недоступен
/// </summary>
public interface IFallbackRecommendationService
{
    /// <summary>
    /// Получает локальную рекомендацию на основе статуса глюкозы и профиля ребёнка
    /// </summary>
    /// <param name="status">Статус глюкозы</param>
    /// <param name="child">Профиль ребёнка</param>
    /// <param name="availableSnacks">Доступные перекусы</param>
    /// <returns>Текст рекомендации</returns>
    string GetRecommendation(GlucoseStatus status, Child child, List<string> availableSnacks);
}
