// Абстракция для запуска фоновых задач.
// В production: реальный Hangfire (PostgreSQL).

namespace SugarGuard.API.Application.Interfaces;

/// <summary>
/// Поставщик фонового выполнения для разовых задач
/// </summary>
public interface IBackgroundEnqueuer
{
    /// <summary>
    /// Ставит задачу формирования CSV-экспорта в очередь фонового исполнения
    /// </summary>
    void EnqueueExportJob(Guid exportJobId);
}
