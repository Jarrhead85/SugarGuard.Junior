using SugarGuard.API.DTOs;

namespace SugarGuard.API.Application.Interfaces;

public interface INutritionTrackerService
{
    Task<IReadOnlyList<NutritionEntryResponse>> GetEntriesAsync(Guid childId, DateTime from, DateTime to, CancellationToken cancellationToken);
    Task<NutritionEntryResponse> CreateEntryAsync(Guid childId, Guid actorId, SaveNutritionEntryRequest request, CancellationToken cancellationToken);
    Task<NutritionEntryResponse?> UpdateEntryAsync(Guid childId, Guid entryId, SaveNutritionEntryRequest request, CancellationToken cancellationToken);
    Task<bool> DeleteEntryAsync(Guid childId, Guid entryId, CancellationToken cancellationToken);
    Task<IReadOnlyList<MealScheduleResponse>> GetSchedulesAsync(Guid childId, CancellationToken cancellationToken);
    Task<MealScheduleResponse> CreateScheduleAsync(Guid childId, SaveMealScheduleRequest request, CancellationToken cancellationToken);
    Task<MealScheduleResponse?> UpdateScheduleAsync(Guid childId, Guid scheduleId, SaveMealScheduleRequest request, CancellationToken cancellationToken);
    Task<bool> DeleteScheduleAsync(Guid childId, Guid scheduleId, CancellationToken cancellationToken);
    Task<NutritionSummaryResponse> GetSummaryAsync(Guid childId, DateTime from, DateTime to, CancellationToken cancellationToken);
    Task<IReadOnlyList<AchievementResponse>> GetAchievementsAsync(Guid childId, CancellationToken cancellationToken);
    Task<byte[]> ExportCsvAsync(Guid childId, DateTime from, DateTime to, CancellationToken cancellationToken);
    Task<byte[]> ExportPdfAsync(Guid childId, DateTime from, DateTime to, CancellationToken cancellationToken);
}
