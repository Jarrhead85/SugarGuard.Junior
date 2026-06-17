namespace SugarGuard.Shared.Constants;

/// <summary>
/// Маркеры для удаленных перекусов
/// </summary>

public static class BackpackHistoryActor
{   
    public const string UserPrefix = "userId:"; // Префикс для удаления перекуса конкретным пользователем
   
    public const string ConsumedPrefix = "consumed:userId:"; // Префикс для потребления перекуса конкретным пользователем
   
    public const string MidnightCleanup = "midnight_cleanup"; // Системная очистка рюкзаков в полночь
   
    public static string RemovedByUser(Guid userId) => $"{UserPrefix}{userId}"; // Маркер «удалено пользователем» с подставленным userId
   
    public static string ConsumedByUser(Guid userId) => $"{ConsumedPrefix}{userId}"; // Маркер «потреблено пользователем» с подставленным userId
}
