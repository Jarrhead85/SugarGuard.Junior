// Реализация специализированного репозитория для Measurement
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SugarGuard.Shared.Constants;
using SugarGuard.Junior.Database;
using SugarGuard.Junior.Models.Core;
using SugarGuard.Junior.Models.Enums;
using SugarGuard.Junior.Repositories.Interfaces;
using SugarGuard.Junior.Security;
using SugarGuard.Junior.Utilities;

namespace SugarGuard.Junior.Repositories.Implementations;

/// <summary>
/// Репозиторий для работы с измерениями
/// Специфичные методы для глюкозы
/// </summary>
public class MeasurementRepository : BaseRepository<Measurement>, IMeasurementRepository
{
    private readonly ICryptoService _cryptoService;

    // ✅ ПРАВИЛЬНО: Один конструктор со всеми параметрами!
    public MeasurementRepository(
        IDbContextFactory<AppDbContext> factory,
        ILogger<MeasurementRepository> logger,
        ICryptoService cryptoService)
        : base(factory, logger)
    {
        _cryptoService = cryptoService;
    }

    /// <summary>
    /// Добавляет новое измерение с шифрованием всех PHI полей
    /// </summary>
    public new async Task<Measurement> AddAsync(Measurement measurement)
    {
        try
        {
            // Создаём entity с зашифрованными данными
            var entity = new MeasurementEntity
            {
                MeasurementId = measurement.MeasurementId,
                ChildId = measurement.ChildId,
                MeasurementTime = measurement.MeasurementTime,
                DataSource = measurement.DataSource,
                IsSynced = measurement.IsSynced,
                CreatedAt = measurement.CreatedAt,
                RecommendationId = !string.IsNullOrEmpty(measurement.RecommendationId) ? measurement.RecommendationId : null
            };

            // Шифруем GlucoseValue
            var glucoseValue = await GetDecryptedGlucoseValueAsync(measurement);
            if (glucoseValue.HasValue)
            {
                entity.EncryptedGlucoseValue = await _cryptoService.EncryptAsync(glucoseValue.Value.ToString());
            }
            else
            {
                // Если уже зашифровано, используем как есть
                entity.EncryptedGlucoseValue = measurement.EncryptedGlucoseValue;
            }

            // Шифруем ChildState
            if (!string.IsNullOrEmpty(measurement.EncryptedChildState))
            {
                entity.EncryptedChildState = measurement.EncryptedChildState;
            }
            else
            {
                // Если нужно зашифровать из незашифрованного значения
                var childState = await GetDecryptedChildStateAsync(measurement);
                if (childState.HasValue)
                {
                    entity.EncryptedChildState = await _cryptoService.EncryptAsync(childState.Value.ToString());
                }
            }

            // Шифруем Notes
            if (!string.IsNullOrEmpty(measurement.EncryptedNotes))
            {
                entity.EncryptedNotes = measurement.EncryptedNotes;
            }
            else
            {
                // Если есть незашифрованные заметки, шифруем их
                var notes = await GetDecryptedNotesAsync(measurement);
                if (!string.IsNullOrEmpty(notes))
                {
                    entity.EncryptedNotes = await _cryptoService.EncryptAsync(notes);
                }
            }

            await using var ctx = await CreateDbContextAsync();
            await ctx.Set<MeasurementEntity>().AddAsync(entity);
            await ctx.SaveChangesAsync();
            
            Logger.LogInformation(" Измерение добавлено с полным шифрованием PHI: {MeasurementId}", measurement.MeasurementId);
            return measurement;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при добавлении измерения: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Получает измерение по ID с расшифровкой всех PHI полей
    /// </summary>
    public new async Task<Measurement?> GetByIdAsync(string measurementId)
    {
        return await GetByIdAsync(measurementId, includeRecommendation: false);
    }

    /// <summary>
    /// Получает измерение по ID с опциональной загрузкой связанных данных
    /// </summary>
    public async Task<Measurement?> GetByIdAsync(string measurementId, bool includeRecommendation = false)
    {
        try
        {
            await using var ctx = await CreateDbContextAsync();
            var query = ctx.Set<MeasurementEntity>().AsQueryable();

            // Include navigation properties if requested
            // Note: Navigation properties need to be configured in AppDbContext for Include to work
            // This is prepared for future when navigation properties are added
            if (includeRecommendation)
            {
                // query = query.Include(m => m.Recommendation);
                // Currently commented out as navigation properties are not yet configured
            }

            var entity = await query
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.MeasurementId == measurementId);

            if (entity == null)
            {
                Logger.LogDebug(" Измерение не найдено: {MeasurementId}", measurementId);
                return null;
            }

            // Создаём модель с расшифрованными данными
            var measurement = new Measurement
            {
                MeasurementId = entity.MeasurementId,
                ChildId = entity.ChildId,
                MeasurementTime = entity.MeasurementTime,
                DataSource = entity.DataSource,
                IsSynced = entity.IsSynced,
                CreatedAt = entity.CreatedAt,
                EncryptedGlucoseValue = entity.EncryptedGlucoseValue,
                EncryptedChildState = entity.EncryptedChildState,
                EncryptedNotes = entity.EncryptedNotes
            };

            Logger.LogDebug(" Измерение найдено и расшифровано: {MeasurementId}", measurementId);
            return measurement;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при получении измерения: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Получает последнее измерение ребёнка
    /// </summary>
    public async Task<Measurement?> GetLatestByChildIdAsync(string childId)
    {
        return await GetLatestByChildIdAsync(childId, includeRecommendation: false, includeChild: false);
    }

    /// <summary>
    /// Получает последнее измерение ребёнка с опциональной загрузкой связанных данных
    /// </summary>
    public async Task<Measurement?> GetLatestByChildIdAsync(string childId, bool includeRecommendation = false, bool includeChild = false)
    {
        try
        {
            await using var ctx = await CreateDbContextAsync();
            var query = ctx.Set<MeasurementEntity>()
                .Where(m => m.ChildId == childId);

            // Include navigation properties if requested
            // Note: Navigation properties need to be configured in AppDbContext for Include to work
            // This is prepared for future when navigation properties are added
            if (includeRecommendation)
            {
                // query = query.Include(m => m.Recommendation);
                // Currently commented out as navigation properties are not yet configured
            }

            if (includeChild)
            {
                // query = query.Include(m => m.Child);
                // Currently commented out as navigation properties are not yet configured
            }

            var entity = await query
                .OrderByDescending(m => m.MeasurementTime)
                .AsNoTracking()
                .FirstOrDefaultAsync();

            if (entity == null)
            {
                return null;
            }

            var measurement = new Measurement
            {
                MeasurementId = entity.MeasurementId,
                ChildId = entity.ChildId,
                EncryptedGlucoseValue = entity.EncryptedGlucoseValue,
                MeasurementTime = entity.MeasurementTime,
                EncryptedChildState = entity.EncryptedChildState,
                EncryptedNotes = entity.EncryptedNotes,
                DataSource = entity.DataSource,
                IsSynced = entity.IsSynced,
                CreatedAt = entity.CreatedAt
            };

            Logger.LogDebug(" Последнее измерение найдено для {ChildId}", childId);
            return measurement;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при получении последнего измерения: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Получает все измерения за дату с опциональной загрузкой связанных данных
    /// </summary>
    public async Task<List<Measurement>> GetByDateAsync(string childId, DateTime date, bool includeRecommendation = false, bool includeChild = false)
    {
        try
        {
            await using var ctx = await CreateDbContextAsync();
            var query = ctx.Set<MeasurementEntity>()
                .Where(m => m.ChildId == childId &&
                       m.MeasurementTime.Date == date.Date);

            // Include navigation properties if requested
            // Note: Navigation properties need to be configured in AppDbContext for Include to work
            // This is prepared for future when navigation properties are added
            if (includeRecommendation)
            {
                // query = query.Include(m => m.Recommendation);
                // Currently commented out as navigation properties are not yet configured
            }

            if (includeChild)
            {
                // query = query.Include(m => m.Child);
                // Currently commented out as navigation properties are not yet configured
            }

            var entities = await query
                .OrderBy(m => m.MeasurementTime)
                .AsNoTracking()
                .ToListAsync();

            // Преобразуем entities в Measurement модели
            var measurements = entities.Select(e => new Measurement
            {
                MeasurementId = e.MeasurementId,
                ChildId = e.ChildId,
                EncryptedGlucoseValue = e.EncryptedGlucoseValue,
                MeasurementTime = e.MeasurementTime,
                EncryptedChildState = e.EncryptedChildState,
                EncryptedNotes = e.EncryptedNotes,
                DataSource = e.DataSource,
                IsSynced = e.IsSynced,
                CreatedAt = e.CreatedAt
            }).ToList();

            Logger.LogDebug(" Получено {Count} измерений за {Date:yyyy-MM-dd}", measurements.Count, date);
            return measurements;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при получении измерений за дату: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Возвращает измерения за диапазон дат с постраничной загрузкой.
    /// </summary>
    public async Task<List<Measurement>> GetByDateRangeAsync(string childId, DateTime startDate, DateTime endDate, bool includeRecommendation = false, bool includeChild = false, int page = 1, int pageSize = 100)
    {
        try
        {
            var safePageSize = Math.Clamp(pageSize, 1, 500);
            var safeOffset = Math.Max(0, (page - 1) * safePageSize);

            await using var ctx = await CreateDbContextAsync();
            var query = ctx.Set<MeasurementEntity>()
                .Where(m => m.ChildId == childId &&
                       m.MeasurementTime.Date >= startDate.Date &&
                       m.MeasurementTime.Date <= endDate.Date);

            // Include navigation properties if requested
            // Note: Navigation properties need to be configured in AppDbContext for Include to work
            // This is prepared for future when navigation properties are added
            if (includeRecommendation)
            {
                // query = query.Include(m => m.Recommendation);
                // Currently commented out as navigation properties are not yet configured
            }

            if (includeChild)
            {
                // query = query.Include(m => m.Child);
                // Currently commented out as navigation properties are not yet configured
            }

            var entities = await query
                .OrderByDescending(m => m.MeasurementTime)
                .Skip(safeOffset)
                .Take(safePageSize)
                .AsNoTracking()
                .ToListAsync();

            // Преобразуем entities в Measurement модели
            var measurements = entities.Select(e => new Measurement
            {
                MeasurementId = e.MeasurementId,
                ChildId = e.ChildId,
                EncryptedGlucoseValue = e.EncryptedGlucoseValue,
                MeasurementTime = e.MeasurementTime,
                EncryptedChildState = e.EncryptedChildState,
                EncryptedNotes = e.EncryptedNotes,
                DataSource = e.DataSource,
                IsSynced = e.IsSynced,
                CreatedAt = e.CreatedAt
            }).ToList();

            Logger.LogDebug(" Получено {Count} измерений за {StartDate:yyyy-MM-dd} - {EndDate:yyyy-MM-dd}", 
                measurements.Count, startDate, endDate);
            return measurements;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при получении измерений за диапазон: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Получает не синхронизированные измерения
    /// </summary>
    public async Task<List<Measurement>> GetUnsyncedAsync()
    {
        try
        {
            await using var ctx = await CreateDbContextAsync();
            var entities = await ctx.Set<MeasurementEntity>()
                .Where(m => !m.IsSynced)
                .OrderBy(m => m.CreatedAt)
                .AsNoTracking()
                .ToListAsync();

            var measurements = entities.Select(e => new Measurement
            {
                MeasurementId = e.MeasurementId,
                ChildId = e.ChildId,
                EncryptedGlucoseValue = e.EncryptedGlucoseValue,
                MeasurementTime = e.MeasurementTime,
                EncryptedChildState = e.EncryptedChildState,
                EncryptedNotes = e.EncryptedNotes,
                DataSource = e.DataSource,
                IsSynced = e.IsSynced,
                CreatedAt = e.CreatedAt
            }).ToList();

            Logger.LogDebug(" Получено {Count} не синхронизированных измерений", measurements.Count);
            return measurements;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при получении не синхронизированных: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Отмечает измерение как синхронизированное
    /// </summary>
    public async Task<bool> MarkAsSyncedAsync(string measurementId)
    {
        try
        {
            await using var ctx = await CreateDbContextAsync();
            var entity = await ctx.Set<MeasurementEntity>().FirstOrDefaultAsync(m => m.MeasurementId == measurementId);
            if (entity == null)
            {
                Logger.LogWarning(" Измерение не найдено: {MeasurementId}", measurementId);
                return false;
            }

            entity.IsSynced = true;
            ctx.Set<MeasurementEntity>().Update(entity);
            await ctx.SaveChangesAsync();
            
            Logger.LogInformation(" Измерение отмечено как синхронизированное: {MeasurementId}", measurementId);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при отметке синхронизации: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Удаляет все измерения ребёнка
    /// </summary>
    public async Task<int> DeleteAllByChildIdAsync(string childId)
    {
        try
        {
            await using var ctx = await CreateDbContextAsync();
            var measurements = await ctx.Set<MeasurementEntity>()
                .Where(m => m.ChildId == childId)
                .ToListAsync();

            ctx.Set<MeasurementEntity>().RemoveRange(measurements);
            await ctx.SaveChangesAsync();

            Logger.LogWarning(" Удалено {MeasurementsCount} измерений для {ChildId}", measurements.Count, childId);
            return measurements.Count;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при удалении всех измерений: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Получает статистику за день
    /// </summary>
    public async Task<MeasurementStatistics> GetDailyStatisticsAsync(string childId, DateTime date)
    {
        try
        {
            var measurements = await GetByDateAsync(childId, date);

            if (measurements.Count == 0)
            {
                return new MeasurementStatistics();
            }

            // Параллельная расшифровка (Task.WhenAll): N измерений × RTT Keychain = 1×RTT.
            var glucoseValues = await DecryptGlucoseValuesAsync(measurements);

            if (glucoseValues.Count == 0)
            {
                return new MeasurementStatistics { MeasurementCount = measurements.Count };
            }

            // Рассчитываем статистику
            var average = glucoseValues.Average();
            var min = glucoseValues.Min();
            var max = glucoseValues.Max();
            var variance = glucoseValues.Sum(x => Math.Pow(x - average, 2)) / glucoseValues.Count;
            var standardDeviation = Math.Sqrt(variance);

            // Целевой диапазон: используем централизованные константы
            var inRange = glucoseValues.Count(g => g >= GlucoseLevels.TargetRangeMin && g <= GlucoseLevels.TargetRangeMax);
            var timeInRange = (inRange / (double)glucoseValues.Count) * 100;

            // Гипо- и гипергликемические эпизоды
            var hypoEpisodes = glucoseValues.Count(g => g < GlucoseLevels.TargetRangeMin);
            var hyperEpisodes = glucoseValues.Count(g => g > GlucoseLevels.TargetRangeMax);

            var stats = new MeasurementStatistics
            {
                AverageGlucose = Math.Round(average, 2),
                MinGlucose = Math.Round(min, 2),
                MaxGlucose = Math.Round(max, 2),
                StandardDeviation = Math.Round(standardDeviation, 2),
                MeasurementCount = glucoseValues.Count,
                TimeInTargetRange = Math.Round(timeInRange, 2),
                HypoEpisodes = hypoEpisodes,
                HyperEpisodes = hyperEpisodes
            };

            Logger.LogInformation(" Статистика за {Date:yyyy-MM-dd}: {MeasurementCount} измерений, среднее {AverageGlucose}, в норме {TimeInTargetRange}%", 
                date, stats.MeasurementCount, stats.AverageGlucose, stats.TimeInTargetRange);

            return stats;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при расчёте статистики: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Получает количество гипогликемических эпизодов
    /// </summary>
    public async Task<int> GetHypoEpisodesAsync(string childId, DateTime startDate, DateTime endDate)
    {
        try
        {
            var measurements = await GetByDateRangeAsync(childId, startDate, endDate);
            if (measurements.Count == 0)
                return 0;

            var values = await DecryptGlucoseValuesAsync(measurements);
            return values.Count(v => v < GlucoseLevels.TargetRangeMin);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при подсчёте гипо: {Message}", ex.Message);
            return 0;
        }
    }

    /// <summary>
    /// Получает количество гипергликемических эпизодов
    /// </summary>
    public async Task<int> GetHyperEpisodesAsync(string childId, DateTime startDate, DateTime endDate)
    {
        try
        {
            var measurements = await GetByDateRangeAsync(childId, startDate, endDate);
            if (measurements.Count == 0)
                return 0;

            var values = await DecryptGlucoseValuesAsync(measurements);
            return values.Count(v => v > 10.0);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при подсчёте гипер: {Message}", ex.Message);
            return 0;
        }
    }

    /// <summary>
    /// Получает среднее значение глюкозы
    /// </summary>
    public async Task<double> GetAverageGlucoseAsync(string childId, DateTime startDate, DateTime endDate)
    {
        try
        {
            var measurements = await GetByDateRangeAsync(childId, startDate, endDate);
            if (measurements.Count == 0)
                return 0;

            var values = await DecryptGlucoseValuesAsync(measurements);
            return values.Count > 0 ? Math.Round(values.Average(), 2) : 0;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при расчёте среднего: {Message}", ex.Message);
            return 0;
        }
    }

    /// <summary>
    /// Получает процент времени в целевом диапазоне
    /// </summary>
    public async Task<double> GetTimeInTargetRangeAsync(string childId, DateTime startDate, DateTime endDate, double minTarget = 4.0, double maxTarget = 10.0)
    {
        try
        {
            var measurements = await GetByDateRangeAsync(childId, startDate, endDate);
            if (measurements.Count == 0)
                return 0;

            var values = await DecryptGlucoseValuesAsync(measurements);
            if (values.Count == 0)
                return 0;

            var inRange = values.Count(v => v >= minTarget && v <= maxTarget);
            return Math.Round((inRange / (double)values.Count) * 100, 2);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при расчёте времени в диапазоне: {Message}", ex.Message);
            return 0;
        }
    }

    /// <summary>
    /// Параллельно расшифровывает значения глюкозы из списка измерений.
    /// Неудачные расшифровки пропускаются (битый ciphertext не ломает весь батч).
    /// </summary>
    private async Task<List<double>> DecryptGlucoseValuesAsync(IReadOnlyList<Measurement> measurements)
    {
        if (measurements.Count == 0)
            return new List<double>();

        var decryptTasks = measurements
            .Select(async m =>
            {
                try
                {
                    var decrypted = await _cryptoService.DecryptAsync(m.EncryptedGlucoseValue);
                    if (DoubleParser.TryParseDecrypted(decrypted, out var value))
                        return (double?)value;
                }
                catch
                {
                    // Битый ciphertext — пропускаем
                }
                return null;
            })
            .ToList();

        var results = await Task.WhenAll(decryptTasks);
        return results.Where(v => v.HasValue).Select(v => v!.Value).ToList();
    }

    /// <summary>
    /// Получает расшифрованные заметки измерения
    /// </summary>
    public async Task<string?> GetDecryptedNotesAsync(Measurement measurement)
    {
        try
        {
            if (string.IsNullOrEmpty(measurement.EncryptedNotes))
                return null;
                
            return await _cryptoService.DecryptAsync(measurement.EncryptedNotes);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при дешифровании заметок измерения: {Message}", ex.Message);
            return "*** ОШИБКА ДЕШИФРОВАНИЯ ***";
        }
    }

    /// <summary>
    /// Получает расшифрованное значение глюкозы
    /// </summary>
    public async Task<double?> GetDecryptedGlucoseValueAsync(Measurement measurement)
    {
        try
        {
            var decrypted = await _cryptoService.DecryptAsync(measurement.EncryptedGlucoseValue);
            if (DoubleParser.TryParseDecrypted(decrypted, out var value))
            {
                return value;
            }
            return null;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при дешифровании значения глюкозы: {Message}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Получает расшифрованное состояние ребёнка
    /// </summary>
    public async Task<ChildState?> GetDecryptedChildStateAsync(Measurement measurement)
    {
        try
        {
            if (string.IsNullOrEmpty(measurement.EncryptedChildState))
            {
                return null;
            }

            var decrypted = await _cryptoService.DecryptAsync(measurement.EncryptedChildState);
            if (Enum.TryParse<ChildState>(decrypted, out var state))
            {
                return state;
            }
            return null;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при дешифровании состояния ребёнка: {Message}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Шифрует состояние ребёнка перед сохранением
    /// </summary>
    public async Task<string> EncryptChildStateAsync(ChildState childState)
    {
        try
        {
            return await _cryptoService.EncryptAsync(childState.ToString());
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при шифровании состояния ребёнка: {Message}", ex.Message);
            throw;
        }
    }
}
