// Сервис для шифрования/дешифрования данных на уровне сервисов
using Microsoft.Extensions.Logging;
using SugarGuard.Junior.Models.Core;
using SugarGuard.Junior.Security;
using SugarGuard.Junior.Services.Interfaces;
using SugarGuard.Junior.Utilities;

namespace SugarGuard.Junior.Services.Implementations;

/// <summary>
/// Сервис для шифрования персональных данных
/// Обеспечивает единообразное шифрование на уровне сервисов
/// </summary>
public class EncryptionService : IEncryptionService
{
    private readonly ICryptoService _cryptoService;
    private readonly ILogger<EncryptionService> _logger;

    public EncryptionService(ICryptoService cryptoService, ILogger<EncryptionService> logger)
    {
        _cryptoService = cryptoService;
        _logger = logger;
    }

    /// <summary>
    /// Шифрует измерение перед сохранением
    /// </summary>
    public async Task<Measurement> EncryptMeasurementAsync(Measurement measurement, double glucoseValue, string? notes = null)
    {
        try
        {
            // Шифруем значение глюкозы
            measurement.EncryptedGlucoseValue = await _cryptoService.EncryptAsync(glucoseValue.ToString());

            // Шифруем заметки, если они есть
            if (!string.IsNullOrEmpty(notes))
            {
                measurement.EncryptedNotes = await _cryptoService.EncryptAsync(notes);
            }

            _logger.LogDebug(" Измерение зашифровано");
            return measurement;
        }
        catch (Exception ex)
        {
            _logger.LogError(" Ошибка при шифровании измерения: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Дешифрует измерение после загрузки
    /// </summary>
    public async Task<(double glucoseValue, string? notes)> DecryptMeasurementAsync(Measurement measurement)
    {
        try
        {
            // Дешифруем значение глюкозы
            var glucoseStr = await _cryptoService.DecryptAsync(measurement.EncryptedGlucoseValue);
            if (!DoubleParser.TryParseDecrypted(glucoseStr, out var glucoseValue))
            {
                throw new InvalidOperationException($"Не удалось преобразовать значение глюкозы: {glucoseStr}");
            }

            // Дешифруем заметки, если они есть
            string? notes = null;
            if (!string.IsNullOrEmpty(measurement.EncryptedNotes))
            {
                notes = await _cryptoService.DecryptAsync(measurement.EncryptedNotes);
            }

            _logger.LogDebug(" Измерение дешифровано");
            return (glucoseValue, notes);
        }
        catch (Exception ex)
        {
            _logger.LogError(" Ошибка при дешифровании измерения: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Шифрует данные ребёнка перед сохранением
    /// </summary>
    public async Task<Child> EncryptChildAsync(Child child, string firstName, string lastName)
    {
        try
        {
            if (!CipherFormat.IsEncrypted(child.EncryptedFirstName))
            {
                child.EncryptedFirstName = await _cryptoService.EncryptAsync(firstName);
            }

            if (!CipherFormat.IsEncrypted(child.EncryptedLastName))
            {
                child.EncryptedLastName = await _cryptoService.EncryptAsync(lastName);
            }

            _logger.LogDebug(" Данные ребёнка зашифрованы");
            return child;
        }
        catch (Exception ex)
        {
            _logger.LogError(" Ошибка при шифровании данных ребёнка: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Дешифрует данные ребёнка после загрузки
    /// </summary>
    public async Task<(string firstName, string lastName)> DecryptChildAsync(Child child)
    {
        try
        {
            var firstName = await _cryptoService.DecryptAsync(child.EncryptedFirstName);
            var lastName = await _cryptoService.DecryptAsync(child.EncryptedLastName);

            _logger.LogDebug(" Данные ребёнка дешифрованы");
            return (firstName, lastName);
        }
        catch (Exception ex)
        {
            _logger.LogError(" Ошибка при дешифровании данных ребёнка: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Шифрует данные пользователя перед сохранением
    /// </summary>
    public async Task<User> EncryptUserAsync(User user, string firstName, string lastName, string email, string? phoneNumber = null)
    {
        try
        {
            if (!CipherFormat.IsEncrypted(user.EncryptedFirstName))
            {
                user.EncryptedFirstName = await _cryptoService.EncryptAsync(firstName);
            }

            if (!CipherFormat.IsEncrypted(user.EncryptedLastName))
            {
                user.EncryptedLastName = await _cryptoService.EncryptAsync(lastName);
            }

            if (!CipherFormat.IsEncrypted(user.EncryptedEmail))
            {
                user.EncryptedEmail = await _cryptoService.EncryptAsync(email);
            }

            if (!string.IsNullOrEmpty(phoneNumber) && !CipherFormat.IsEncrypted(user.EncryptedPhoneNumber))
            {
                user.EncryptedPhoneNumber = await _cryptoService.EncryptAsync(phoneNumber);
            }

            _logger.LogDebug(" Данные пользователя зашифрованы");
            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(" Ошибка при шифровании данных пользователя: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Дешифрует данные пользователя после загрузки
    /// </summary>
    public async Task<(string firstName, string lastName, string email, string? phoneNumber)> DecryptUserAsync(User user)
    {
        try
        {
            var firstName = await _cryptoService.DecryptAsync(user.EncryptedFirstName);
            var lastName = await _cryptoService.DecryptAsync(user.EncryptedLastName);
            var email = await _cryptoService.DecryptAsync(user.EncryptedEmail);

            string? phoneNumber = null;
            if (!string.IsNullOrEmpty(user.EncryptedPhoneNumber))
            {
                phoneNumber = await _cryptoService.DecryptAsync(user.EncryptedPhoneNumber);
            }

            _logger.LogDebug(" Данные пользователя дешифрованы");
            return (firstName, lastName, email, phoneNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(" Ошибка при дешифровании данных пользователя: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Шифрует название перекуса
    /// </summary>
    public async Task<string> EncryptSnackNameAsync(string snackName)
    {
        try
        {
            var encrypted = await _cryptoService.EncryptAsync(snackName);
            _logger.LogDebug(" Название перекуса зашифровано");
            return encrypted;
        }
        catch (Exception ex)
        {
            _logger.LogError(" Ошибка при шифровании названия перекуса: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Дешифрует название перекуса
    /// </summary>
    public async Task<string> DecryptSnackNameAsync(string encryptedSnackName)
    {
        try
        {
            var decrypted = await _cryptoService.DecryptAsync(encryptedSnackName);
            _logger.LogDebug(" Название перекуса дешифровано");
            return decrypted;
        }
        catch (Exception ex)
        {
            _logger.LogError(" Ошибка при дешифровании названия перекуса: {Message}", ex.Message);
            return "*** ОШИБКА ДЕШИФРОВАНИЯ ***";
        }
    }
}
