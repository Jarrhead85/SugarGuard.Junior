// Универсальный интерфейс репозитория для любой сущности
// Содержит базовые CRUD операции (Create, Read, Update, Delete)
namespace SugarGuard.Junior.Repositories.Interfaces;

/// <summary>
/// Generic репозиторий для всех сущностей
/// 
/// Generic означает, что можно использовать с любым типом:
/// <c>IRepository&lt;Measurement&gt;</c>
/// <c>IRepository&lt;User&gt;</c>
/// <c>IRepository&lt;Child&gt;</c>
/// 
/// Это избегает дублирования кода для каждой сущности
/// </summary>
/// <typeparam name="T">Тип сущности.</typeparam>
public interface IRepository<T> where T : class
{
    /// <summary>
    /// Получает сущность по ID
    /// </summary>
    Task<T?> GetByIdAsync(string id);

    /// <summary>
    /// Получает все сущности
    /// </summary>
    Task<List<T>> GetAllAsync();

    /// <summary>
    /// Добавляет новую сущность
    /// </summary>
    Task<T> AddAsync(T entity);

    /// <summary>
    /// Добавляет множество сущностей
    /// </summary>
    Task<List<T>> AddRangeAsync(List<T> entities);

    /// <summary>
    /// Обновляет существующую сущность
    /// </summary>
    Task<T> UpdateAsync(T entity);

    /// <summary>
    /// Удаляет сущность по ID
    /// </summary>
    Task<bool> DeleteAsync(string id);

    /// <summary>
    /// Удаляет сущность
    /// </summary>
    Task<bool> DeleteAsync(T entity);

    /// <summary>
    /// Проверяет, существует ли сущность с таким ID
    /// </summary>
    Task<bool> ExistsAsync(string id);

    /// <summary>
    /// Получает количество сущностей
    /// </summary>
    Task<int> CountAsync();

}
