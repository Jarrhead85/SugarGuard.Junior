// Контекст базы данных Entity Framework Core
// Это "мост" между C# объектами и БД таблицами
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SugarGuard.Junior.Core.Security;
using SugarGuard.Junior.Models.Core;
using SugarGuard.Junior.Models.Enums;
using SugarGuard.Junior.Services.Interfaces;

namespace SugarGuard.Junior.Database;

/// <summary>
/// Логирование съеденного перекуса
/// </summary>
public class SnackConsumptionLog
{
    public string LogId { get; set; } = Guid.NewGuid().ToString();
    public string ChildId { get; set; } = string.Empty;

    /// <summary>
    /// Зашифрованное название перекуса (PHI)
    /// </summary>
    public string EncryptedSnackName { get; set; } = string.Empty;

    /// <summary>
    /// Зашифрованные хлебные единицы (PHI)
    /// </summary>
    public string? EncryptedBreadUnits { get; set; }

    public string RecommendationId { get; set; } = string.Empty;
    public DateTime ConsumedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Версия шифрования (см. <see cref="EncryptionVersion"/>).
    /// Используется <see cref="VersionedEncryptionService"/> для dual-decrypt.
    /// </summary>
    public EncryptionVersion EncryptionVersion { get; set; } = EncryptionVersion.AesGcm;
}

/// <summary>
/// Логирование пропущенной рекомендации
/// </summary>
public class SkippedRecommendationLog
{
    public string LogId { get; set; } = Guid.NewGuid().ToString();
    public string ChildId { get; set; } = string.Empty;
    public string RecommendationId { get; set; } = string.Empty;
    public string Reason { get; set; } = "user_skipped";
    public DateTime SkippedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}



/// <summary>
/// Контекст приложения для работы с SQLite БД
/// </summary>
public class AppDbContext : DbContext
{
    private readonly ILogger<AppDbContext> _logger;

    /// <summary>
    /// Таблица пользователей (родителей)
    /// </summary>
    public DbSet<User> Users { get; set; } = null!;

    /// <summary>
    /// Таблица профилей детей
    /// </summary>
    public DbSet<Child> Children { get; set; } = null!;

    /// <summary>
    /// Таблица измерений глюкозы
    /// </summary>
    public DbSet<MeasurementEntity> Measurements { get; set; } = null!;

    /// <summary>
    /// Таблица настроек диабета
    /// </summary>
    public DbSet<DiabetesSettings> DiabetesSettings { get; set; } = null!;

    /// <summary>
    /// Таблица рекомендаций от ИИ
    /// </summary>
    public DbSet<AIRecommendation> AIRecommendations { get; set; } = null!;

    /// <summary>
    /// Таблица активных перекусов в рюкзаке
    /// </summary>
    public DbSet<BackpackItem> BackpackItems { get; set; } = null!;

    /// <summary>
    /// Таблица истории перекусов (для аналитики, архивируется)
    /// </summary>
    public DbSet<BackpackHistory> BackpackHistory { get; set; } = null!;

    /// <summary>
    /// Таблица очереди синхронизации (для offline-first)
    /// </summary>
    public DbSet<SyncQueueItem> SyncQueue { get; set; } = null!;

    /// <summary>
    /// Таблица истории конфликтов синхронизации
    /// </summary>
    public DbSet<SyncConflictHistory> SyncConflictHistory { get; set; } = null!;

    public AppDbContext(DbContextOptions<AppDbContext> options, ILogger<AppDbContext> logger)
        : base(options)
    {
        _logger = logger;
    }

    /// <summary>
    /// Логирование съеденных перекусов
    /// </summary>
    public DbSet<SnackConsumptionLog> SnackConsumptionLogs { get; set; } = null!;

    /// <summary>
    /// Логирование пропущенных рекомендаций
    /// </summary>
    public DbSet<SkippedRecommendationLog> SkippedRecommendationLogs { get; set; } = null!;

    /// <summary>
    /// Расписание измерений глюкозы
    /// </summary>
    public DbSet<MeasurementSchedule> MeasurementSchedules { get; set; } = null!;


    /// <summary>
    /// Конфигурация моделей и связей между ними
    /// Вызывается один раз при создании контекста
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ========== ТАБЛИЦА USERS ==========
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId);

            // Индекс по email для быстрого поиска
            entity.HasIndex(e => e.EncryptedEmail)
                .IsUnique()
                .HasDatabaseName("IX_Users_Email");

            // Partial index по legacy-версии для MauiReEncryptJob
            entity.HasIndex(e => e.EncryptionVersion)
                .HasFilter("\"EncryptionVersion\" = 1")
                .HasDatabaseName("IX_Users_LegacyCbc");

            entity.Property(e => e.UserId)
                .HasMaxLength(36)
                .IsRequired();

            entity.Property(e => e.EncryptedEmail)
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(e => e.EncryptedFirstName)
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(e => e.EncryptedLastName)
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(e => e.EncryptedPhoneNumber)
                .HasMaxLength(500);

            entity.Property(e => e.PasswordHash)
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(e => e.PasswordSalt)
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(e => e.IsEmailVerified)
                .HasDefaultValue(false);

            entity.Property(e => e.IsTelegramConnected)
                .HasDefaultValue(false);

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.EncryptionVersion)
                .HasConversion<byte>()
                .HasDefaultValue(EncryptionVersion.AesGcm)
                .IsRequired();
        });

        // ========== ТАБЛИЦА CHILDREN ==========
        modelBuilder.Entity<Child>(entity =>
        {
            entity.HasKey(e => e.ChildId);

            // Индекс по parent_id для быстрого поиска всех детей родителя
            entity.HasIndex(e => e.ParentUserId)
                .HasDatabaseName("IX_Children_ParentUserId");

            entity.Property(e => e.ChildId)
                .HasMaxLength(36)
                .IsRequired();

            entity.Property(e => e.ParentUserId)
                .HasMaxLength(36)
                .IsRequired();

            entity.Property(e => e.EncryptedFirstName)
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(e => e.EncryptedLastName)
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(e => e.EncryptedWeight)
                .HasMaxLength(500);

            entity.Property(e => e.EncryptedHeight)
                .HasMaxLength(500);

            entity.Property(e => e.EncryptedDiabetesType)
                .HasMaxLength(500);

            entity.Property(e => e.EncryptedDateOfBirth)
                .HasMaxLength(500);

            entity.Property(e => e.EncryptedDiagnosisDate)
                .HasMaxLength(500);

            entity.Property(e => e.InsulinScheme)
                .HasMaxLength(500);

            entity.Property(e => e.CurrentInsulins)
                .HasMaxLength(2000)
                .HasDefaultValue("[]");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.EncryptionVersion)
                .HasConversion<byte>()
                .HasDefaultValue(EncryptionVersion.AesGcm)
                .IsRequired();

            // Partial index по legacy-версии для MauiReEncryptJob
            entity.HasIndex(e => e.EncryptionVersion)
                .HasFilter("\"EncryptionVersion\" = 1")
                .HasDatabaseName("IX_Children_LegacyCbc");
        });

        // ========== ТАБЛИЦА MEASUREMENTS ==========
        modelBuilder.Entity<MeasurementEntity>(entity =>
        {
            entity.HasKey(e => e.MeasurementId);

            // Индекс по child_id для быстрого поиска всех измерений ребёнка
            entity.HasIndex(e => e.ChildId)
                .HasDatabaseName("IX_Measurements_ChildId");

            // Индекс по времени для сортировки
            entity.HasIndex(e => e.MeasurementTime)
                .HasDatabaseName("IX_Measurements_Time");

            // Составной индекс для поиска по ребёнку и дате
            entity.HasIndex(e => new { e.ChildId, e.MeasurementTime })
                .HasDatabaseName("IX_Measurements_ChildTime");

            entity.Property(e => e.MeasurementId)
                .HasMaxLength(36)
                .IsRequired();

            entity.Property(e => e.ChildId)
                .HasMaxLength(36)
                .IsRequired();

            entity.Property(e => e.EncryptedGlucoseValue)
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(e => e.EncryptedChildState)
                .HasMaxLength(500);

            entity.Property(e => e.EncryptedNotes)
                .HasMaxLength(2000);

            entity.Property(e => e.DataSource)
                .HasConversion<string>()
                .HasMaxLength(50);

            entity.Property(e => e.IsSynced)
                .HasDefaultValue(false);

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.EncryptionVersion)
                .HasConversion<byte>()
                .HasDefaultValue(EncryptionVersion.AesGcm)
                .IsRequired();

            // Partial index по legacy-версии для MauiReEncryptJob
            entity.HasIndex(e => e.EncryptionVersion)
                .HasFilter("\"EncryptionVersion\" = 1")
                .HasDatabaseName("IX_Measurements_LegacyCbc");
        });

        // ========== ТАБЛИЦА DIABETES_SETTINGS ==========
        modelBuilder.Entity<DiabetesSettings>(entity =>
        {
            entity.HasKey(e => e.ChildId);

            entity.Property(e => e.ChildId)
                .HasMaxLength(36)
                .IsRequired();

            entity.Property(e => e.EncryptedTargetRangeMin)
                .HasMaxLength(500);

            entity.Property(e => e.EncryptedTargetRangeMax)
                .HasMaxLength(500);

            entity.Property(e => e.EncryptedInsulinSensitivity)
                .HasMaxLength(500);

            entity.Property(e => e.EncryptedCarbInsulinRatio)
                .HasMaxLength(500);

            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.EncryptionVersion)
                .HasConversion<byte>()
                .HasDefaultValue(EncryptionVersion.AesGcm)
                .IsRequired();

            // Partial index по legacy-версии для MauiReEncryptJob
            entity.HasIndex(e => e.EncryptionVersion)
                .HasFilter("\"EncryptionVersion\" = 1")
                .HasDatabaseName("IX_DiabetesSettings_LegacyCbc");
        });

        // ========== ТАБЛИЦА AI_RECOMMENDATIONS ==========
        modelBuilder.Entity<AIRecommendation>(entity =>
        {
            entity.HasKey(e => e.RecommendationId);

            // Индекс по child_id для быстрого поиска рекомендаций
            entity.HasIndex(e => e.ChildId)
                .HasDatabaseName("IX_AIRecommendations_ChildId");

            // Индекс по значению глюкозы для кэша
            entity.HasIndex(e => e.GlucoseValueAtRequest)
                .HasDatabaseName("IX_AIRecommendations_GlucoseValue");

            entity.Property(e => e.RecommendationId)
                .HasMaxLength(36)
                .IsRequired();

            entity.Property(e => e.MeasurementId)
                .HasMaxLength(36);

            entity.Property(e => e.ChildId)
                .HasMaxLength(36)
                .IsRequired();

            entity.Property(e => e.RecommendationText)
                .HasMaxLength(5000)
                .IsRequired();

            entity.Property(e => e.ModelUsed)
                .HasMaxLength(100);

            entity.Property(e => e.Urgency)
                .HasConversion<string>()
                .HasMaxLength(50);

            entity.Property(e => e.IsFromCache)
                .HasDefaultValue(false);

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.EncryptionVersion)
                .HasConversion<byte>()
                .HasDefaultValue(EncryptionVersion.AesGcm)
                .IsRequired();

            // Partial index по legacy-версии для MauiReEncryptJob
            entity.HasIndex(e => e.EncryptionVersion)
                .HasFilter("\"EncryptionVersion\" = 1")
                .HasDatabaseName("IX_AIRecommendations_LegacyCbc");
        });

        // ========== ТАБЛИЦА BACKPACK_ITEMS (активные) ==========
        modelBuilder.Entity<BackpackItem>(entity =>
        {
            entity.HasKey(e => e.BackpackItemId);

            // Индекс по child_id для быстрого получения рюкзака
            entity.HasIndex(e => e.ChildId)
                .HasDatabaseName("IX_BackpackItems_ChildId");

            entity.Property(e => e.BackpackItemId)
                .HasMaxLength(36)
                .IsRequired();

            entity.Property(e => e.ChildId)
                .HasMaxLength(36)
                .IsRequired();

            entity.Property(e => e.EncryptedSnackName)
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(e => e.EncryptedBreadUnits)
                .HasMaxLength(500);

            entity.Property(e => e.IsSynced)
                .HasDefaultValue(false);

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.EncryptionVersion)
                .HasConversion<byte>()
                .HasDefaultValue(EncryptionVersion.AesGcm)
                .IsRequired();

            // Partial index по legacy-версии для MauiReEncryptJob
            entity.HasIndex(e => e.EncryptionVersion)
                .HasFilter("\"EncryptionVersion\" = 1")
                .HasDatabaseName("IX_BackpackItems_LegacyCbc");
        });

        // ========== ТАБЛИЦА BACKPACK_HISTORY (архив) ==========
        modelBuilder.Entity<BackpackHistory>(entity =>
        {
            entity.HasKey(e => e.HistoryId);

            // Индекс по child_id
            entity.HasIndex(e => e.ChildId)
                .HasDatabaseName("IX_BackpackHistory_ChildId");

            // Индекс по дате удаления (для архивации)
            entity.HasIndex(e => e.DeletedAt)
                .HasDatabaseName("IX_BackpackHistory_DeletedAt");

            entity.Property(e => e.HistoryId)
                .HasMaxLength(36)
                .IsRequired();

            entity.Property(e => e.ChildId)
                .HasMaxLength(36)
                .IsRequired();

            entity.Property(e => e.EncryptedSnackName)
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(e => e.EncryptedBreadUnits)
                .HasMaxLength(500);

            entity.Property(e => e.DeletedBy)
                .HasMaxLength(50);

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.EncryptionVersion)
                .HasConversion<byte>()
                .HasDefaultValue(EncryptionVersion.AesGcm)
                .IsRequired();

            // Partial index по legacy-версии для MauiReEncryptJob
            entity.HasIndex(e => e.EncryptionVersion)
                .HasFilter("\"EncryptionVersion\" = 1")
                .HasDatabaseName("IX_BackpackHistory_LegacyCbc");
        });

        // ========== ТАБЛИЦА SYNC_QUEUE (очередь синхронизации) ==========
        modelBuilder.Entity<SyncQueueItem>(entity =>
        {
            entity.HasKey(e => e.QueueId);

            // Индекс по статусу для поиска не синхронизированных
            entity.HasIndex(e => e.IsSynced)
                .HasDatabaseName("IX_SyncQueue_IsSynced");

            // Индекс по child_id для синхронизации по ребёнку
            entity.HasIndex(e => e.EntityId)
                .HasDatabaseName("IX_SyncQueue_EntityId");

            entity.Property(e => e.QueueId)
                .HasMaxLength(36)
                .IsRequired();

            entity.Property(e => e.EntityId)
                .HasMaxLength(36)
                .IsRequired();

            entity.Property(e => e.EntityType)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.OperationType)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.Payload)
                .HasMaxLength(10000)
                .IsRequired();

            entity.Property(e => e.IsSynced)
                .HasDefaultValue(false);

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.LastRetryAt);

            entity.Property(e => e.LastModifiedAt);
        });

        // ========== ТАБЛИЦА SYNC_CONFLICT_HISTORY ==========
        modelBuilder.Entity<SyncConflictHistory>(entity =>
        {
            entity.HasKey(e => e.ConflictId);

            // Индекс по entity_id для поиска конфликтов по сущности
            entity.HasIndex(e => e.EntityId)
                .HasDatabaseName("IX_SyncConflictHistory_EntityId");

            // Индекс по времени разрешения для очистки старых записей
            entity.HasIndex(e => e.ResolvedAt)
                .HasDatabaseName("IX_SyncConflictHistory_ResolvedAt");

            // Составной индекс по entity_id и entity_type
            entity.HasIndex(e => new { e.EntityId, e.EntityType })
                .HasDatabaseName("IX_SyncConflictHistory_EntityIdType");

            entity.Property(e => e.ConflictId)
                .HasMaxLength(36)
                .IsRequired();

            entity.Property(e => e.EntityId)
                .HasMaxLength(36)
                .IsRequired();

            entity.Property(e => e.EntityType)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.LocalVersion)
                .HasMaxLength(10000)
                .IsRequired();

            entity.Property(e => e.ServerVersion)
                .HasMaxLength(10000)
                .IsRequired();

            entity.Property(e => e.ResolutionStrategy)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.WinningVersion)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.ResolutionReason)
                .HasMaxLength(1000);

            entity.Property(e => e.ResolvedBy)
                .HasMaxLength(100)
                .IsRequired()
                .HasDefaultValue("SyncConflictResolver");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // ========== ТАБЛИЦА SKIPPED_RECOMMENDATION_LOGS ==========
        modelBuilder.Entity<SkippedRecommendationLog>(entity =>
        {
            entity.HasKey(s => s.LogId);

            entity.Property(e => e.LogId)
                .HasMaxLength(36)
                .IsRequired();

            entity.Property(e => e.ChildId)
                .HasMaxLength(36)
                .IsRequired();
        });

        // ========== ТАБЛИЦА SNACK_CONSUMPTION_LOGS ==========
        modelBuilder.Entity<SnackConsumptionLog>(entity =>
        {
            entity.HasKey(s => s.LogId);

            entity.Property(e => e.LogId)
                .HasMaxLength(36)
                .IsRequired();

            entity.Property(e => e.ChildId)
                .HasMaxLength(36)
                .IsRequired();

            entity.Property(e => e.EncryptedSnackName)
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(e => e.EncryptedBreadUnits)
                .HasMaxLength(500);

            entity.Property(e => e.RecommendationId)
                .HasMaxLength(36)
                .IsRequired();

            entity.Property(e => e.EncryptionVersion)
                .HasConversion<byte>()
                .HasDefaultValue(EncryptionVersion.AesGcm)
                .IsRequired();

            // Partial index по legacy-версии для MauiReEncryptJob
            entity.HasIndex(e => e.EncryptionVersion)
                .HasFilter("\"EncryptionVersion\" = 1")
                .HasDatabaseName("IX_SnackConsumptionLogs_LegacyCbc");
        });

        // ========== ТАБЛИЦА MEASUREMENT_SCHEDULES ==========
        modelBuilder.Entity<MeasurementSchedule>(entity =>
        {
            entity.HasKey(e => e.ScheduleId);

            // Индекс по child_id для быстрого поиска расписания
            entity.HasIndex(e => e.ChildId)
                .HasDatabaseName("IX_MeasurementSchedules_ChildId");

            // Уникальный индекс по child_id + scheduled_time (нет дубликатов времени)
            entity.HasIndex(e => new { e.ChildId, e.ScheduledTime })
                .IsUnique()
                .HasDatabaseName("IX_MeasurementSchedules_ChildTime");

            entity.Property(e => e.ScheduleId)
                .HasMaxLength(36)
                .IsRequired();

            entity.Property(e => e.ChildId)
                .HasMaxLength(36)
                .IsRequired();

            entity.Property(e => e.ScheduledTime)
                .IsRequired();

            entity.Property(e => e.IsActive)
                .HasDefaultValue(true);

            entity.Property(e => e.IsSynced)
                .HasDefaultValue(false);

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

    }

    /// <summary>
    /// Конфигурация подключения к БД
    /// </summary>
    /// <remarks>
    /// <para>
    /// Если options уже сконфигурированы извне (например, через
    /// <c>AppDbContextDesignTimeFactory</c> для <c>dotnet ef</c>),
    /// <c>OnConfiguring</c> НЕ переопределяет их. Это стандартный паттерн EF Core.
    /// </para>
    /// </remarks>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Если опции уже заданы (design-time factory, runtime DI) — выходим.
        if (optionsBuilder.IsConfigured) return;

        base.OnConfiguring(optionsBuilder);

        // Путь к файлу БД в локальном хранилище приложения
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "sugarguard.db");

        // Используем SQLite
        optionsBuilder.UseSqlite($"Data Source={dbPath}");

#if DEBUG
        // В режиме отладки логируем SQL запросы
        optionsBuilder.LogTo(Console.WriteLine, LogLevel.Information);
#endif

        _logger.LogInformation("БД файл: {DbPath}", dbPath);
    }
}

/// <summary>
/// Перечисление для типов операций в SyncQueue
/// </summary>
public enum SyncOperationType
{
    Insert,
    Update,
    Delete
}

/// <summary>
/// Элемент в очереди синхронизации
/// Отслеживает что нужно отправить на сервер
/// </summary>
public class SyncQueueItem
{
    public string QueueId { get; set; } = Guid.NewGuid().ToString();
    public string EntityId { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty; // "Measurement", "BackpackItem"
    public SyncOperationType OperationType { get; set; }
    public string Payload { get; set; } = string.Empty; // JSON данные
    public bool IsSynced { get; set; } = false;
    public int RetryCount { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastRetryAt { get; set; }
    public DateTime? LastModifiedAt { get; set; } // Время последнего изменения для обнаружения конфликтов
}

/// <summary>
/// История конфликтов синхронизации
/// Сохраняет обе версии данных при конфликте
/// </summary>
public class SyncConflictHistory
{
    public string ConflictId { get; set; } = Guid.NewGuid().ToString();
    public string EntityId { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string LocalVersion { get; set; } = string.Empty; // JSON локальной версии
    public string ServerVersion { get; set; } = string.Empty; // JSON серверной версии
    public DateTime LocalModifiedAt { get; set; }
    public DateTime ServerModifiedAt { get; set; }
    public string ResolutionStrategy { get; set; } = "LastWriteWins"; // Стратегия разрешения
    public string WinningVersion { get; set; } = string.Empty; // "Local" или "Server"
    public string? ResolutionReason { get; set; } // Причина выбора победившей версии
    public string ResolvedBy { get; set; } = "SyncConflictResolver"; // Кто разрешил конфликт
    public DateTime ResolvedAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Активный элемент рюкзака
/// </summary>
public class BackpackItem
{
    public string BackpackItemId { get; set; } = Guid.NewGuid().ToString();
    public string ChildId { get; set; } = string.Empty;

    /// <summary>
    /// Зашифрованное название перекуса (PHI)
    /// </summary>
    public string EncryptedSnackName { get; set; } = string.Empty;

    /// <summary>
    /// Зашифрованные хлебные единицы (PHI)
    /// </summary>
    public string? EncryptedBreadUnits { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsSynced { get; set; } = false;

    /// <summary>
    /// Версия шифрования (см. <see cref="EncryptionVersion"/>).
    /// </summary>
    public EncryptionVersion EncryptionVersion { get; set; } = EncryptionVersion.AesGcm;
}

/// <summary>
/// История перекусов (архив, удаляется через 90 дней)
/// </summary>
public class BackpackHistory
{
    public string HistoryId { get; set; } = Guid.NewGuid().ToString();
    public string ChildId { get; set; } = string.Empty;

    /// <summary>
    /// Зашифрованное название перекуса (PHI)
    /// </summary>
    public string EncryptedSnackName { get; set; } = string.Empty;

    /// <summary>
    /// Зашифрованные хлебные единицы (PHI)
    /// </summary>
    public string? EncryptedBreadUnits { get; set; }

    public DateTime AddedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; } // "child" или "parent"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Версия шифрования (см. <see cref="EncryptionVersion"/>).
    /// </summary>
    public EncryptionVersion EncryptionVersion { get; set; } = EncryptionVersion.AesGcm;
}
/// <summary>
/// Измерение глюкозы ребёнка
/// Все PHI данные хранятся в зашифрованном виде
/// </summary>
public class MeasurementEntity
{
    public string MeasurementId { get; set; } = Guid.NewGuid().ToString();
    public string ChildId { get; set; } = string.Empty;

    /// <summary>
    /// Зашифрованное значение глюкозы (PHI)
    /// </summary>
    public string EncryptedGlucoseValue { get; set; } = string.Empty;

    public DateTime MeasurementTime { get; set; }

    /// <summary>
    /// Зашифрованное состояние ребёнка (PHI)
    /// </summary>
    public string? EncryptedChildState { get; set; }

    /// <summary>
    /// Зашифрованные заметки (PHI)
    /// </summary>
    public string? EncryptedNotes { get; set; }

    public DataSource DataSource { get; set; }
    public bool IsSynced { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// ID рекомендации от ИИ (если она была запрошена)
    /// </summary>
    public string? RecommendationId { get; set; }

    /// <summary>
    /// Версия шифрования (см. <see cref="EncryptionVersion"/>).
    /// Используется <see cref="VersionedEncryptionService"/> для dual-decrypt CBC/GCM.
    /// </summary>
    public EncryptionVersion EncryptionVersion { get; set; } = EncryptionVersion.AesGcm;
}
