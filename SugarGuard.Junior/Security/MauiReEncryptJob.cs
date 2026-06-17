using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SugarGuard.Junior.Core.Security;
using SugarGuard.Junior.Database;
using SugarGuard.Junior.Security;

namespace SugarGuard.Junior.Security;

/// <summary>
/// Фоновая задача миграции ciphertext из legacy AES-CBC в AES-GCM.
/// <para>
/// после введения <see cref="MauiEncryptionService"/>
/// с префиксом версии все НОВЫЕ записи пишутся в GCM (<c>"2:..."</c>).
/// Однако существующие данные в БД по-прежнему зашифрованы legacy CBC
/// (либо <c>base64(IV+ciphertext)</c> без префикса, либо <c>"1:..."</c>).
/// </para>
/// <para>
/// Эта задача запускается при старте приложения (<see cref="App.OnStart"/>)
/// и поэтапно перешифровывает существующие записи в GCM. До завершения
/// миграции данные читаются через legacy-CBC path, запись — уже в GCM.
/// </para>
/// <para>
/// <b>Алгоритм:</b>
/// <list type="number">
///   <item><description>Загрузить батч (100 записей) из таблицы c <c>EncryptionVersion == LegacyCbc</c>.</description></item>
///   <item><description>Для каждой записи: Decrypt через <see cref="ICryptoService.DecryptAsync"/>
///     (роутинг по префиксу), encrypt заново (получит <c>"2:"</c>),
///     установить <c>EncryptionVersion = AesGcm</c>.</description></item>
///   <item><description>Сохранить батч одним <c>SaveChangesAsync</c>.</description></item>
///   <item><description>Повторять, пока всё не мигрировано или не превышен лимит батчей.</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Concurrency:</b> запускается из <c>App.OnStart</c> через
/// <c>Task.Run</c> — fire-and-forget. Не блокирует UI.
/// </para>
/// </summary>
public sealed class MauiReEncryptJob
{
    private const int BatchSize = 100;
    private const int MaxBatchesPerRun = 50;

    private readonly ICryptoService _cryptoService;
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly ILogger<MauiReEncryptJob> _logger;

    public MauiReEncryptJob(
        ICryptoService cryptoService,
        IDbContextFactory<AppDbContext> dbContextFactory,
        ILogger<MauiReEncryptJob> logger)
    {
        _cryptoService = cryptoService;
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("MauiReEncryptJob: запуск миграции ciphertext CBC → GCM");

            int totalMigrated = 0;
            totalMigrated += await MigrateMeasurementsAsync(cancellationToken);
            totalMigrated += await MigrateChildrenAsync(cancellationToken);
            totalMigrated += await MigrateUsersAsync(cancellationToken);
            totalMigrated += await MigrateBackpackAsync(cancellationToken);

            _logger.LogInformation("MauiReEncryptJob: миграция завершена, перешифровано {Count} записей", totalMigrated);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("MauiReEncryptJob: миграция отменена");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MauiReEncryptJob: ошибка при миграции ciphertext");
        }
    }

    private async Task<int> MigrateMeasurementsAsync(CancellationToken ct)
    {
        int migrated = 0;
        for (int batch = 0; batch < MaxBatchesPerRun; batch++)
        {
            ct.ThrowIfCancellationRequested();
            await using var ctx = await _dbContextFactory.CreateDbContextAsync(ct);
            var batchItems = await ctx.Measurements
                .Where(m => m.EncryptionVersion == EncryptionVersion.LegacyCbc)
                .Take(BatchSize)
                .ToListAsync(ct);
            if (batchItems.Count == 0) break;

            foreach (var m in batchItems)
            {
                m.EncryptedGlucoseValue = await ReEncryptAsync(m.EncryptedGlucoseValue);
                if (m.EncryptedNotes != null)
                    m.EncryptedNotes = await ReEncryptAsync(m.EncryptedNotes);
                m.EncryptionVersion = EncryptionVersion.AesGcm;
                migrated++;
            }
            await ctx.SaveChangesAsync(ct);
        }
        return migrated;
    }

    private async Task<int> MigrateChildrenAsync(CancellationToken ct)
    {
        int migrated = 0;
        for (int batch = 0; batch < MaxBatchesPerRun; batch++)
        {
            ct.ThrowIfCancellationRequested();
            await using var ctx = await _dbContextFactory.CreateDbContextAsync(ct);
            var batchItems = await ctx.Children
                .Where(c => c.EncryptionVersion == EncryptionVersion.LegacyCbc)
                .Take(BatchSize)
                .ToListAsync(ct);
            if (batchItems.Count == 0) break;

            foreach (var c in batchItems)
            {
                c.EncryptedFirstName = await ReEncryptAsync(c.EncryptedFirstName);
                c.EncryptedLastName = await ReEncryptAsync(c.EncryptedLastName);
                c.EncryptionVersion = EncryptionVersion.AesGcm;
                migrated++;
            }
            await ctx.SaveChangesAsync(ct);
        }
        return migrated;
    }

    private async Task<int> MigrateUsersAsync(CancellationToken ct)
    {
        int migrated = 0;
        for (int batch = 0; batch < MaxBatchesPerRun; batch++)
        {
            ct.ThrowIfCancellationRequested();
            await using var ctx = await _dbContextFactory.CreateDbContextAsync(ct);
            var batchItems = await ctx.Users
                .Where(u => u.EncryptionVersion == EncryptionVersion.LegacyCbc)
                .Take(BatchSize)
                .ToListAsync(ct);
            if (batchItems.Count == 0) break;

            foreach (var u in batchItems)
            {
                u.EncryptedFirstName = await ReEncryptAsync(u.EncryptedFirstName);
                u.EncryptedLastName = await ReEncryptAsync(u.EncryptedLastName);
                u.EncryptedEmail = await ReEncryptAsync(u.EncryptedEmail);
                u.EncryptionVersion = EncryptionVersion.AesGcm;
                migrated++;
            }
            await ctx.SaveChangesAsync(ct);
        }
        return migrated;
    }

    private async Task<int> MigrateBackpackAsync(CancellationToken ct)
    {
        int migrated = 0;
        for (int batch = 0; batch < MaxBatchesPerRun; batch++)
        {
            ct.ThrowIfCancellationRequested();
            await using var ctx = await _dbContextFactory.CreateDbContextAsync(ct);
            var batchItems = await ctx.BackpackItems
                .Where(i => i.EncryptionVersion == EncryptionVersion.LegacyCbc)
                .Take(BatchSize)
                .ToListAsync(ct);
            if (batchItems.Count == 0) break;

            foreach (var item in batchItems)
            {
                item.EncryptedSnackName = await ReEncryptAsync(item.EncryptedSnackName);
                item.EncryptionVersion = EncryptionVersion.AesGcm;
                migrated++;
            }
            await ctx.SaveChangesAsync(ct);
        }
        return migrated;
    }

    private async Task<string> ReEncryptAsync(string ciphertext)
    {
        var plain = await _cryptoService.DecryptAsync(ciphertext);
        return await _cryptoService.EncryptAsync(plain);
    }
}

