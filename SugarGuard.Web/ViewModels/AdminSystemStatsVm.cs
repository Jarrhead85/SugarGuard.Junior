using SugarGuard.Web.Services;

namespace SugarGuard.Web.ViewModels;

/// <summary>
/// ViewModel статистики системы для страницы Admin/SystemStats.razor.
/// </summary>
public sealed class AdminSystemStatsVm
{
    /// <summary>Количество активных Hangfire-задач.</summary>
    public int HangfireActiveJobs { get; init; }

    /// <summary>Общее количество пользователей.</summary>
    public int TotalUsers { get; init; }

    /// <summary>Общее количество детей.</summary>
    public int TotalChildren { get; init; }

    /// <summary>Общее количество измерений.</summary>
    public long TotalMeasurements { get; init; }

    /// <summary>Элементов, ожидающих синхронизации.</summary>
    public int PendingSyncItems { get; init; }

    /// <summary>Неразрешённых конфликтов синхронизации.</summary>
    public int UnresolvedConflicts { get; init; }

    /// <summary>Задач экспорта в очереди.</summary>
    public int PendingExportJobs { get; init; }

    /// <summary>Завершённых задач экспорта за сегодня.</summary>
    public int CompletedExportJobsToday { get; init; }

    /// <summary>Время сервера (UTC).</summary>
    public DateTime ServerUtcTime { get; init; }

    /// <summary>
    /// Создаёт VM из DTO.
    /// </summary>
    /// <param name="dto">DTO из API.</param>
    /// <returns>Готовый <see cref="AdminSystemStatsVm"/>.</returns>
    internal static AdminSystemStatsVm FromDto(AdminSystemStatsDto dto) => new()
    {
        HangfireActiveJobs = dto.HangfireActiveJobs,
        TotalUsers = dto.TotalUsers,
        TotalChildren = dto.TotalChildren,
        TotalMeasurements = dto.TotalMeasurements,
        PendingSyncItems = dto.PendingSyncItems,
        UnresolvedConflicts = dto.UnresolvedConflicts,
        PendingExportJobs = dto.PendingExportJobs,
        CompletedExportJobsToday = dto.CompletedExportJobsToday,
        ServerUtcTime = dto.ServerUtcTime
    };
}
