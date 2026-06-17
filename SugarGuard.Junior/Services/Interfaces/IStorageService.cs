// Интерфейс для сохранения и загрузки данных
// Позволяет нам легко переключаться между разными реализациями (БД, облако и т.д.)
namespace SugarGuard.Junior.Services.Interfaces;

public interface IStorageService
{
    /// <summary>
    /// Сохраняет строковое значение с ключом
    /// </summary>
    Task<bool> SaveAsync(string key, string value);

    /// <summary>
    /// Получает строковое значение по ключу
    /// Возвращает null если ключ не найден
    /// </summary>
    Task<string?> GetAsync(string key);

    /// <summary>
    /// Удаляет значение по ключу
    /// </summary>
    Task<bool> DeleteAsync(string key);

    /// <summary>
    /// Проверяет, существует ли ключ в хранилище
    /// </summary>
    Task<bool> ContainsKeyAsync(string key);

    /// <summary>
    /// Очищает всё хранилище (используется при выходе из аккаунта)
    /// </summary>
    Task<bool> ClearAsync();
}
