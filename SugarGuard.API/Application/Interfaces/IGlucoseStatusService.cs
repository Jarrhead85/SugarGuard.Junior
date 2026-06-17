namespace SugarGuard.API.Application.Interfaces;

/// <summary>
/// Сервис для определения статуса уровня глюкозы
/// </summary>
public interface IGlucoseStatusService
{
    string GetGlucoseStatus(decimal glucoseValue); // Определяет статус уровня глюкозы

    bool IsCritical(decimal glucoseValue); // Проверяет, является ли уровень глюкозы критическим

    string GetStatusDescription(string status); // Получает локализованное описание статуса
}
