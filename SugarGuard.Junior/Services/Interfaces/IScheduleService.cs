// Интерфейс для управления расписанием измерений
using SugarGuard.Junior.Models.Core;

namespace SugarGuard.Junior.Services.Interfaces;

/// <summary>
/// Сервис для управления расписанием измерений глюкозы
/// Позволяет добавлять, удалять и получать времена измерений
/// </summary>
public interface IScheduleService
{
    /// <summary>
    /// Получает все активные времена измерений для ребёнка
    /// </summary>
    /// <param name="childId">ID ребёнка</param>
    /// <returns>Список времён измерений, отсортированный по времени</returns>
    Task<List<MeasurementSchedule>> GetScheduleAsync(string childId);

    /// <summary>
    /// Добавляет новое время измерения в расписание
    /// </summary>
    /// <param name="childId">ID ребёнка</param>
    /// <param name="time">Время измерения (HH:mm)</param>
    /// <returns>true если добавлено успешно, false если время уже существует</returns>
    Task<bool> AddScheduleItemAsync(string childId, TimeOnly time);

    /// <summary>
    /// Удаляет время измерения из расписания
    /// </summary>
    /// <param name="childId">ID ребёнка</param>
    /// <param name="time">Время для удаления</param>
    /// <returns>true если удалено успешно, false если время не найдено</returns>
    Task<bool> RemoveScheduleItemAsync(string childId, TimeOnly time);

    /// <summary>
    /// Получает следующее запланированное измерение
    /// </summary>
    /// <param name="childId">ID ребёнка</param>
    /// <returns>Ближайшее время измерения или null если расписание пустое</returns>
    Task<MeasurementSchedule?> GetNextScheduledMeasurementAsync(string childId);

    /// <summary>
    /// Активирует или деактивирует время измерения
    /// </summary>
    /// <param name="scheduleId">ID элемента расписания</param>
    /// <param name="isActive">Новое состояние</param>
    /// <returns>true если обновлено успешно</returns>
    Task<bool> SetScheduleItemActiveAsync(string scheduleId, bool isActive);

    /// <summary>
    /// Проверяет валидность времени (формат HH:MM, нет дубликатов)
    /// </summary>
    /// <param name="childId">ID ребёнка</param>
    /// <param name="time">Время для проверки</param>
    /// <returns>true если время валидно</returns>
    Task<bool> IsValidScheduleTimeAsync(string childId, TimeOnly time);
}