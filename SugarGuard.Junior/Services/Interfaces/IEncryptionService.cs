// Интерфейс сервиса шифрования
using SugarGuard.Junior.Models.Core;

namespace SugarGuard.Junior.Services.Interfaces;

/// <summary>
/// Интерфейс сервиса для шифрования персональных данных
/// </summary>
public interface IEncryptionService
{
    /// <summary>
    /// Шифрует измерение перед сохранением
    /// </summary>
    Task<Measurement> EncryptMeasurementAsync(Measurement measurement, double glucoseValue, string? notes = null);

    /// <summary>
    /// Дешифрует измерение после загрузки
    /// </summary>
    Task<(double glucoseValue, string? notes)> DecryptMeasurementAsync(Measurement measurement);

    /// <summary>
    /// Шифрует данные ребёнка перед сохранением
    /// </summary>
    Task<Child> EncryptChildAsync(Child child, string firstName, string lastName);

    /// <summary>
    /// Дешифрует данные ребёнка после загрузки
    /// </summary>
    Task<(string firstName, string lastName)> DecryptChildAsync(Child child);

    /// <summary>
    /// Шифрует данные пользователя перед сохранением
    /// </summary>
    Task<User> EncryptUserAsync(User user, string firstName, string lastName, string email, string? phoneNumber = null);

    /// <summary>
    /// Дешифрует данные пользователя после загрузки
    /// </summary>
    Task<(string firstName, string lastName, string email, string? phoneNumber)> DecryptUserAsync(User user);

    /// <summary>
    /// Шифрует название перекуса
    /// </summary>
    Task<string> EncryptSnackNameAsync(string snackName);

    /// <summary>
    /// Дешифрует название перекуса
    /// </summary>
    Task<string> DecryptSnackNameAsync(string encryptedSnackName);
}