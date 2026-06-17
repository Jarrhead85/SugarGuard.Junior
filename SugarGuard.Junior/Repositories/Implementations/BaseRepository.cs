// Базовая реализация Generic репозитория
// Содержит все общие CRUD операции
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SugarGuard.Junior.Database;
using SugarGuard.Junior.Repositories.Interfaces;

namespace SugarGuard.Junior.Repositories.Implementations;

/// <summary>
/// Базовая реализация репозитория
/// Каждый метод создаёт короткоживущий DbContext через IDbContextFactory.
/// </summary>
/// <typeparam name="T">Тип сущности</typeparam>
public class BaseRepository<T> : IRepository<T> where T : class
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    protected readonly ILogger<BaseRepository<T>> Logger;

    public BaseRepository(IDbContextFactory<AppDbContext> factory, ILogger<BaseRepository<T>> logger)
    {
        _factory = factory;
        Logger = logger;
    }

    protected async Task<AppDbContext> CreateDbContextAsync()
    {
        return await _factory.CreateDbContextAsync();
    }

    /// <summary>
    /// Получает сущность по ID
    /// </summary>
    public virtual async Task<T?> GetByIdAsync(string id)
    {
        try
        {
            await using var ctx = await _factory.CreateDbContextAsync();
            var entity = await ctx.Set<T>().FindAsync(id);
            if (entity != null)
                Logger.LogDebug(" Сущность найдена по ID: {Id}", id);
            else
                Logger.LogDebug(" Сущность не найдена по ID: {Id}", id);
            return entity;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при получении по ID: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Получает все сущности (read-only)
    /// </summary>
    public async Task<List<T>> GetAllAsync()
    {
        try
        {
            await using var ctx = await _factory.CreateDbContextAsync();
            var entities = await ctx.Set<T>().AsNoTracking().ToListAsync();
            Logger.LogDebug(" Получено {EntitiesCount} сущностей", entities.Count);
            return entities;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при получении всех сущностей: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Добавляет новую сущность
    /// </summary>
    public async Task<T> AddAsync(T entity)
    {
        try
        {
            await using var ctx = await _factory.CreateDbContextAsync();
            var entry = await ctx.Set<T>().AddAsync(entity);
            await ctx.SaveChangesAsync();
            Logger.LogInformation(" Сущность добавлена");
            return entry.Entity;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при добавлении сущности: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Добавляет множество сущностей
    /// </summary>
    public async Task<List<T>> AddRangeAsync(List<T> entities)
    {
        try
        {
            await using var ctx = await _factory.CreateDbContextAsync();
            ctx.Set<T>().AddRange(entities);
            await ctx.SaveChangesAsync();
            Logger.LogInformation(" Добавлено {EntitiesCount} сущностей", entities.Count);
            return entities;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при добавлении множества: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Обновляет существующую сущность
    /// </summary>
    public async Task<T> UpdateAsync(T entity)
    {
        try
        {
            await using var ctx = await _factory.CreateDbContextAsync();
            ctx.Set<T>().Update(entity);
            await ctx.SaveChangesAsync();
            Logger.LogInformation(" Сущность обновлена");
            return entity;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при обновлении сущности: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Удаляет сущность по ID
    /// </summary>
    public async Task<bool> DeleteAsync(string id)
    {
        try
        {
            await using var ctx = await _factory.CreateDbContextAsync();
            var entity = await ctx.Set<T>().FindAsync(id);
            if (entity == null)
            {
                Logger.LogWarning(" Сущность не найдена для удаления: {Id}", id);
                return false;
            }
            ctx.Set<T>().Remove(entity);
            await ctx.SaveChangesAsync();
            Logger.LogInformation(" Сущность удалена: {Id}", id);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при удалении сущности: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Удаляет сущность
    /// </summary>
    public async Task<bool> DeleteAsync(T entity)
    {
        try
        {
            await using var ctx = await _factory.CreateDbContextAsync();
            ctx.Set<T>().Remove(entity);
            await ctx.SaveChangesAsync();
            Logger.LogInformation(" Сущность удалена");
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при удалении сущности: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Проверяет, существует ли сущность
    /// </summary>
    public async Task<bool> ExistsAsync(string id)
    {
        try
        {
            await using var ctx = await _factory.CreateDbContextAsync();
            return await ctx.Set<T>().AsNoTracking().AnyAsync();
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Получает количество сущностей
    /// </summary>
    public async Task<int> CountAsync()
    {
        try
        {
            await using var ctx = await _factory.CreateDbContextAsync();
            return await ctx.Set<T>().AsNoTracking().CountAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при подсчёте: {Message}", ex.Message);
            throw;
        }
    }
}
