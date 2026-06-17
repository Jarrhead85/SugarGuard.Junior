using SugarGuard.API.DTOs;
using SugarGuard.Application.Dashboard.Dto;

namespace SugarGuard.Application.Dashboard
{
    /// <summary>
    /// Сервис агрегации данных для дашборда родителя
    /// </summary>
    public interface IParentDashboardService
    {
        /// <summary>
        /// Возвращает агрегированную сводку KPI для дашборда родителя
        /// </summary>
        Task<ParentDashboardSummaryDto> GetSummaryAsync(
            Guid childId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Возвращает статистику за произвольный период
        /// </summary>
        Task<PeriodStatisticsDto> GetPeriodStatisticsAsync(
            Guid childId,
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Возвращает сравнение двух периодов одинаковой длины
        /// </summary>
        Task<PeriodComparisonDto> GetPeriodComparisonAsync(
            Guid childId,
            DateTime currentFrom,
            DateTime currentTo,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Возвращает единую хронологическую ленту событий за период
        /// </summary>
        Task<IReadOnlyList<TimelineEventDto>> GetTimelineAsync(
            Guid childId,
            DateTime from,
            DateTime to,
            int limit = 100,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Автоматически обнаруживает паттерны гипо/гипер-эпизодов
        /// </summary>
        Task<IReadOnlyList<GlucosePatternDto>> DetectPatternsAsync(
            Guid childId,
            CancellationToken cancellationToken = default);
    }
}
