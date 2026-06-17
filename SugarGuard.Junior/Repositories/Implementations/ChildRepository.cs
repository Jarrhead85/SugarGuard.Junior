// Реализация репозитория для Child
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SugarGuard.Junior.Database;
using SugarGuard.Junior.Models.Core;
using SugarGuard.Junior.Repositories.Interfaces;
using SugarGuard.Junior.Security;

namespace SugarGuard.Junior.Repositories.Implementations;

public class ChildRepository : BaseRepository<Child>, IChildRepository
{
    private readonly ICryptoService _cryptoService;

    public ChildRepository(IDbContextFactory<AppDbContext> factory, ILogger<ChildRepository> logger, ICryptoService cryptoService)
        : base(factory, logger)
    {
        _cryptoService = cryptoService;
    }

    /// <summary>
    /// Получает всех детей родителя (read-only с AsNoTracking).
    /// Сортировка по расшифрованному имени в памяти (H-8).
    /// </summary>
    public async Task<List<Child>> GetByParentIdAsync(string parentUserId)
    {
        try
        {
            await using var ctx = await CreateDbContextAsync();
            var children = await ctx.Set<Child>()
                .Where(c => c.ParentUserId == parentUserId)
                .AsNoTracking()
                .ToListAsync();

            // Сортируем по расшифрованному имени (в БД — шифротекст, сортировка некорректна)
            if (children.Count > 1)
            {
                var decryptedNames = await Task.WhenAll(
                    children.Select(async c => new
                    {
                        Child = c,
                        FirstName = await _cryptoService.DecryptAsync(c.EncryptedFirstName)
                    }));

                children = decryptedNames
                    .OrderBy(x => x.FirstName, StringComparer.CurrentCulture)
                    .Select(x => x.Child)
                    .ToList();
            }

            await Task.WhenAll(children.Select(c => DecryptChildDataAsync(c)));

            Logger.LogDebug(" Получено {ChildrenCount} детей для родителя {ParentUserId}", children.Count, parentUserId);
            return children;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при получении детей родителя: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Получает ребёнка по ID и проверяет принадлежность (read-only с AsNoTracking)
    /// Это безопасный метод - проверяет что ребёнок действительно принадлежит родителю
    /// </summary>
    public async Task<Child?> GetByIdAndParentAsync(string childId, string parentUserId)
    {
        try
        {
            await using var ctx = await CreateDbContextAsync();
            var child = await ctx.Set<Child>()
                .Where(c => c.ChildId == childId && c.ParentUserId == parentUserId)
                .AsNoTracking()
                .FirstOrDefaultAsync();

            if (child != null)
            {
                await DecryptChildDataAsync(child);
                Logger.LogDebug(" Ребёнок найден и принадлежит родителю");
            }
            else
            {
                Logger.LogWarning(" Ребёнок не найден или не принадлежит родителю");
            }

            return child;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при получении ребёнка: {Message}", ex.Message);
            throw;
        }
    }

    public override async Task<Child?> GetByIdAsync(string id)
    {
        var child = await base.GetByIdAsync(id);
        if (child != null)
            await DecryptChildDataAsync(child);
        return child;
    }

    /// <summary>
    /// Префиксы версий шифрования (должны совпадать с MauiEncryptionService)
    /// </summary>
    private const string LegacyCbcPrefix = "1:";
    private const string AesGcmPrefix = "2:";

    /// <summary>
    /// Проверяет, зашифрованы ли данные (по префиксу версии шифрования).
    /// Заменяет старую ненадёжную проверку через Contains("=").
    /// </summary>
    private static bool IsAlreadyEncrypted(string? value)
    {
        return !string.IsNullOrEmpty(value) &&
               (value.StartsWith(LegacyCbcPrefix, StringComparison.Ordinal) ||
                value.StartsWith(AesGcmPrefix, StringComparison.Ordinal));
    }

    /// <summary>
    /// Шифрует персональные данные ребёнка перед сохранением
    /// </summary>
    private async Task EncryptChildDataAsync(Child child)
    {
        try
        {
            if (!IsAlreadyEncrypted(child.EncryptedFirstName))
            {
                child.EncryptedFirstName = await _cryptoService.EncryptAsync(child.EncryptedFirstName);
            }

            if (!IsAlreadyEncrypted(child.EncryptedLastName))
            {
                child.EncryptedLastName = await _cryptoService.EncryptAsync(child.EncryptedLastName);
            }

            if (!IsAlreadyEncrypted(child.EncryptedDateOfBirth))
            {
                child.EncryptedDateOfBirth = await _cryptoService.EncryptAsync(child.DateOfBirth.ToString("O"));
            }

            if (!IsAlreadyEncrypted(child.EncryptedWeight))
            {
                child.EncryptedWeight = await _cryptoService.EncryptAsync(child.Weight.ToString("F2"));
            }

            if (!IsAlreadyEncrypted(child.EncryptedHeight))
            {
                child.EncryptedHeight = await _cryptoService.EncryptAsync(child.Height.ToString("F2"));
            }

            if (!IsAlreadyEncrypted(child.EncryptedDiabetesType))
            {
                child.EncryptedDiabetesType = await _cryptoService.EncryptAsync(child.DiabetesType.ToString());
            }

            if (!IsAlreadyEncrypted(child.EncryptedDiagnosisDate))
            {
                child.EncryptedDiagnosisDate = await _cryptoService.EncryptAsync(child.DiagnosisDate.ToString("O"));
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при шифровании данных ребёнка: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Дешифрует персональные данные ребёнка после загрузки
    /// </summary>
    private async Task DecryptChildDataAsync(Child child)
    {
        try
        {
            if (!string.IsNullOrEmpty(child.EncryptedDateOfBirth))
            {
                var dob = await _cryptoService.DecryptAsync(child.EncryptedDateOfBirth);
                if (DateTime.TryParse(dob, out var parsed)) child.DateOfBirth = parsed;
            }

            if (!string.IsNullOrEmpty(child.EncryptedWeight))
            {
                var w = await _cryptoService.DecryptAsync(child.EncryptedWeight);
                if (double.TryParse(w, out var parsed)) child.Weight = parsed;
            }

            if (!string.IsNullOrEmpty(child.EncryptedHeight))
            {
                var h = await _cryptoService.DecryptAsync(child.EncryptedHeight);
                if (double.TryParse(h, out var parsed)) child.Height = parsed;
            }

            if (!string.IsNullOrEmpty(child.EncryptedDiabetesType))
            {
                var dt = await _cryptoService.DecryptAsync(child.EncryptedDiabetesType);
                if (Enum.TryParse<Models.Enums.DiabetesType>(dt, ignoreCase: true, out var parsed))
                    child.DiabetesType = parsed;
            }

            if (!string.IsNullOrEmpty(child.EncryptedDiagnosisDate))
            {
                var dd = await _cryptoService.DecryptAsync(child.EncryptedDiagnosisDate);
                if (DateTime.TryParse(dd, out var parsed)) child.DiagnosisDate = parsed;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при дешифровании данных ребёнка: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// Получает расшифрованное полное имя ребёнка
    /// </summary>
    public async Task<string> GetFullNameAsync(Child child)
    {
        if (child == null)
            return string.Empty;
        var first = string.IsNullOrWhiteSpace(child.EncryptedFirstName)
            ? string.Empty
            : await GetFirstNameAsync(child);
        var last = string.IsNullOrWhiteSpace(child.EncryptedLastName)
            ? string.Empty
            : await GetLastNameAsync(child);
        return $"{first} {last}".Trim();
    }

    /// <summary>
    /// Получает расшифрованное имя ребёнка
    /// </summary>
    public async Task<string> GetFirstNameAsync(Child child)
    {
        if (child == null || string.IsNullOrWhiteSpace(child.EncryptedFirstName))
            return string.Empty;
        try
        {
            return await _cryptoService.DecryptAsync(child.EncryptedFirstName);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при получении имени: {Message}", ex.Message);
            return "*** ОШИБКА ***";
        }
    }

    /// <summary>
    /// Получает расшифрованную фамилию ребёнка
    /// </summary>
    public async Task<string> GetLastNameAsync(Child child)
    {
        if (child == null || string.IsNullOrWhiteSpace(child.EncryptedLastName))
            return string.Empty;
        try
        {
            return await _cryptoService.DecryptAsync(child.EncryptedLastName);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, " Ошибка при получении фамилии: {Message}", ex.Message);
            return "*** ОШИБКА ***";
        }
    }

    /// <summary>
    /// Добавляет ребёнка с шифрованием персональных данных
    /// </summary>
    public async Task<Child> AddChildWithEncryptionAsync(Child child)
    {
        await EncryptChildDataAsync(child);
        return await base.AddAsync(child);
    }

    /// <summary>
    /// Обновляет ребёнка с шифрованием персональных данных.
    /// Обновляет уже отслеживаемую сущность, чтобы избежать ошибки "another instance with the same key is already being tracked".
    /// </summary>
    public async Task<Child> UpdateChildWithEncryptionAsync(Child child)
    {
        await using var ctx = await CreateDbContextAsync();
        var existing = await ctx.Set<Child>().FindAsync(child.ChildId);
        if (existing == null)
        {
            Logger.LogWarning("Ребёнок с ID {ChildId} не найден для обновления", child.ChildId);
            throw new InvalidOperationException($"Child {child.ChildId} not found.");
        }

        await EncryptChildDataAsync(child);

        existing.EncryptedFirstName = child.EncryptedFirstName;
        existing.EncryptedLastName = child.EncryptedLastName;
        existing.EncryptedDateOfBirth = child.EncryptedDateOfBirth;
        existing.EncryptedWeight = child.EncryptedWeight;
        existing.EncryptedHeight = child.EncryptedHeight;
        existing.EncryptedDiabetesType = child.EncryptedDiabetesType;
        existing.EncryptedDiagnosisDate = child.EncryptedDiagnosisDate;
        existing.InsulinScheme = child.InsulinScheme;
        existing.CurrentInsulins = child.CurrentInsulins;
        existing.PhotoUrl = child.PhotoUrl;
        existing.UpdatedAt = DateTime.UtcNow;

        await ctx.SaveChangesAsync();
        Logger.LogInformation(" Профиль ребёнка обновлён: {ChildId}", child.ChildId);
        return existing;
    }
}