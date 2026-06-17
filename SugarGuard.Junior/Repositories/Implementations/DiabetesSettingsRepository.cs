// Реализация репозитория настроек диабета
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SugarGuard.Junior.Database;
using SugarGuard.Junior.Models.Core;
using SugarGuard.Junior.Repositories.Interfaces;
using SugarGuard.Junior.Security;
using SugarGuard.Junior.Utilities;

namespace SugarGuard.Junior.Repositories.Implementations;

/// <summary>
/// Репозиторий для работы с настройками диабета
/// </summary>
public class DiabetesSettingsRepository : BaseRepository<DiabetesSettings>, IDiabetesSettingsRepository
{
    private readonly ICryptoService _cryptoService;

    public DiabetesSettingsRepository(
        IDbContextFactory<AppDbContext> factory,
        ILogger<DiabetesSettingsRepository> logger,
        ICryptoService cryptoService)
        : base(factory, logger)
    {
        _cryptoService = cryptoService;
    }

    /// <summary>
    /// Получает настройки диабета для ребёнка (read-only с AsNoTracking для оптимизации)
    /// </summary>
    public async Task<DiabetesSettings?> GetByChildIdAsync(string childId)
    {
        try
        {
            await using var ctx = await CreateDbContextAsync();
            var settings = await ctx.Set<DiabetesSettings>()
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.ChildId == childId);

            if (settings != null)
            {
                Logger.LogDebug(" Настройки диабета найдены для {ChildId}", childId);
            }
            else
            {
                Logger.LogWarning(" Настройки диабета не найдены для {ChildId}", childId);
            }

            return settings;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при получении настроек диабета: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Получает настройки диабета для ребёнка для обновления (tracked, без AsNoTracking)
    /// </summary>
    public async Task<DiabetesSettings?> GetByChildIdForUpdateAsync(string childId)
    {
        try
        {
            await using var ctx = await CreateDbContextAsync();
            return await ctx.Set<DiabetesSettings>().FirstOrDefaultAsync(s => s.ChildId == childId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при получении настроек для обновления: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Получает расшифрованный минимальный целевой диапазон
    /// </summary>
    public async Task<double> GetDecryptedTargetRangeMinAsync(DiabetesSettings settings)
    {
        try
        {
            if (string.IsNullOrEmpty(settings.EncryptedTargetRangeMin))
            {
                return 4.0; // Default value if not encrypted yet
            }

            var decrypted = await _cryptoService.DecryptAsync(settings.EncryptedTargetRangeMin);
            if (DoubleParser.TryParseDecrypted(decrypted, out var value))
            {
                return value;
            }
            return 4.0; // Default value
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при дешифровании минимального целевого диапазона: {Message}", ex.Message);
            return 4.0;
        }
    }

    /// <summary>
    /// Получает расшифрованный максимальный целевой диапазон
    /// </summary>
    public async Task<double> GetDecryptedTargetRangeMaxAsync(DiabetesSettings settings)
    {
        try
        {
            if (string.IsNullOrEmpty(settings.EncryptedTargetRangeMax))
            {
                return 10.0; // Default value if not encrypted yet
            }

            var decrypted = await _cryptoService.DecryptAsync(settings.EncryptedTargetRangeMax);
            if (DoubleParser.TryParseDecrypted(decrypted, out var value))
            {
                return value;
            }
            return 10.0; // Default value
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при дешифровании максимального целевого диапазона: {Message}", ex.Message);
            return 10.0;
        }
    }

    /// <summary>
    /// Получает расшифрованную чувствительность к инсулину
    /// </summary>
    public async Task<double> GetDecryptedInsulinSensitivityAsync(DiabetesSettings settings)
    {
        try
        {
            if (string.IsNullOrEmpty(settings.EncryptedInsulinSensitivity))
            {
                return 1.5; // Default value if not encrypted yet
            }

            var decrypted = await _cryptoService.DecryptAsync(settings.EncryptedInsulinSensitivity);
            if (DoubleParser.TryParseDecrypted(decrypted, out var value))
            {
                return value;
            }
            return 1.5; // Default value
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при дешифровании чувствительности к инсулину: {Message}", ex.Message);
            return 1.5;
        }
    }

    /// <summary>
    /// Получает расшифрованный коэффициент углеводов-инсулина
    /// </summary>
    public async Task<double> GetDecryptedCarbInsulinRatioAsync(DiabetesSettings settings)
    {
        try
        {
            if (string.IsNullOrEmpty(settings.EncryptedCarbInsulinRatio))
            {
                return 10.0; // Default value if not encrypted yet
            }

            var decrypted = await _cryptoService.DecryptAsync(settings.EncryptedCarbInsulinRatio);
            if (DoubleParser.TryParseDecrypted(decrypted, out var value))
            {
                return value;
            }
            return 10.0; // Default value
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при дешифровании коэффициента углеводов-инсулина: {Message}", ex.Message);
            return 10.0;
        }
    }

    /// <summary>
    /// Шифрует минимальный целевой диапазон
    /// </summary>
    public async Task<string> EncryptTargetRangeMinAsync(double value)
    {
        try
        {
            return await _cryptoService.EncryptAsync(value.ToString("F1"));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при шифровании минимального целевого диапазона: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Шифрует максимальный целевой диапазон
    /// </summary>
    public async Task<string> EncryptTargetRangeMaxAsync(double value)
    {
        try
        {
            return await _cryptoService.EncryptAsync(value.ToString("F1"));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при шифровании максимального целевого диапазона: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Шифрует чувствительность к инсулину
    /// </summary>
    public async Task<string> EncryptInsulinSensitivityAsync(double value)
    {
        try
        {
            return await _cryptoService.EncryptAsync(value.ToString("F2"));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при шифровании чувствительности к инсулину: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Шифрует коэффициент углеводов-инсулина
    /// </summary>
    public async Task<string> EncryptCarbInsulinRatioAsync(double value)
    {
        try
        {
            return await _cryptoService.EncryptAsync(value.ToString("F2"));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при шифровании коэффициента углеводов-инсулина: {Message}", ex.Message);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task UpdateEncryptedAsync(string childId, string encryptedTargetMin, string encryptedTargetMax, string encryptedSensitivity, string encryptedRatio, int longDuration, int shortDuration)
    {
        var existing = await GetByChildIdForUpdateAsync(childId);
        if (existing == null)
        {
            await AddEncryptedAsync(childId, encryptedTargetMin, encryptedTargetMax, encryptedSensitivity, encryptedRatio, longDuration, shortDuration);
            return;
        }
        existing.EncryptedTargetRangeMin = encryptedTargetMin;
        existing.EncryptedTargetRangeMax = encryptedTargetMax;
        existing.EncryptedInsulinSensitivity = encryptedSensitivity;
        existing.EncryptedCarbInsulinRatio = encryptedRatio;
        existing.LongActingDuration = longDuration;
        existing.ShortActingDuration = shortDuration;
        existing.UpdatedAt = DateTime.UtcNow;
        await UpdateAsync(existing);
    }

    /// <inheritdoc />
    public async Task AddEncryptedAsync(string childId, string encryptedTargetMin, string encryptedTargetMax, string encryptedSensitivity, string encryptedRatio, int longDuration, int shortDuration)
    {
        var settings = new DiabetesSettings
        {
            ChildId = childId,
            EncryptedTargetRangeMin = encryptedTargetMin,
            EncryptedTargetRangeMax = encryptedTargetMax,
            EncryptedInsulinSensitivity = encryptedSensitivity,
            EncryptedCarbInsulinRatio = encryptedRatio,
            LongActingDuration = longDuration,
            ShortActingDuration = shortDuration,
            UpdatedAt = DateTime.UtcNow
        };
        await AddAsync(settings);
    }
}
