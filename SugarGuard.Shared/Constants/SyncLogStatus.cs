namespace SugarGuard.Shared.Constants;

/// <summary>
/// Допустимые значения статуса
/// </summary>
public static class SyncLogStatus
{   
    public const string Pending = "pending"; // Ожидает обработки
   
    public const string Conflict = "conflict"; // Конфликт версий
   
    public const string Resolved = "resolved"; // Конфликт разрешён
   
    public const string Failed = "failed"; // Финальный сбой после исчерпания retry-попыток
}
