// Интерфейс репозитория для Child
using SugarGuard.Junior.Models.Core;

namespace SugarGuard.Junior.Repositories.Interfaces;

public interface IChildRepository : IRepository<Child>
{
    /// <summary>
    /// Получает всех детей родителя
    /// </summary>
    Task<List<Child>> GetByParentIdAsync(string parentUserId);

    /// <summary>
    /// Получает ребёнка по ID и проверяет принадлежность родителю
    /// </summary>
    Task<Child?> GetByIdAndParentAsync(string childId, string parentUserId);

    /// <summary>
    /// Получает расшифрованное полное имя ребёнка
    /// </summary>
    Task<string> GetFullNameAsync(Child child);

    /// <summary>
    /// Получает расшифрованное имя ребёнка
    /// </summary>
    Task<string> GetFirstNameAsync(Child child);

    /// <summary>
    /// Получает расшифрованную фамилию ребёнка
    /// </summary>
    Task<string> GetLastNameAsync(Child child);

    /// <summary>
    /// Добавляет ребёнка с шифрованием персональных данных
    /// </summary>
    Task<Child> AddChildWithEncryptionAsync(Child child);

    /// <summary>
    /// Обновляет ребёнка с шифрованием персональных данных
    /// </summary>
    Task<Child> UpdateChildWithEncryptionAsync(Child child);
}
