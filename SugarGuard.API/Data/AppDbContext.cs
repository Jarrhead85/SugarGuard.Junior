using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SugarGuard.Domain.Entities;
using SugarGuard.Domain.Enums;

namespace SugarGuard.API.Data
{
    /// <summary>
    /// Основной контекст БД SugarGuard API
    /// </summary>
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // Пользователи и дети 
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Child> Children { get; set; } = null!;
        public DbSet<DiabetesSettings> DiabetesSettings { get; set; } = null!;

        // Связки 
        public DbSet<ParentChildLink> ParentChildLinks { get; set; } = null!;
        public DbSet<DoctorChildLink> DoctorChildLinks { get; set; } = null!;

        public DbSet<InviteCode> InviteCodes { get; set; } = null!;// Коды приглашений для связки ребёнок - родитель/врач

        public DbSet<DoctorNote> DoctorNotes => Set<DoctorNote>();// Врачебные заметки с привязкой к измерению

        // Измерения и рюкзак
        public DbSet<Measurement> Measurements { get; set; } = null!;
        public DbSet<BackpackItem> BackpackItems { get; set; } = null!;
        public DbSet<BackpackHistory> BackpackHistory { get; set; } = null!;

        // ИИ и расписание
        public DbSet<AIRecommendation> AIRecommendations { get; set; } = null!;
        public DbSet<MeasurementSchedule> MeasurementSchedules { get; set; } = null!;

        // Бот и уведомления
        public DbSet<ConnectionCode> ConnectionCodes { get; set; } = null!;
        public DbSet<SnackConsumptionLog> SnackConsumptionLogs { get; set; } = null!;
        public DbSet<BotUserContext> BotUserContexts { get; set; } = null!;

        // Инфраструктура
        public DbSet<AuditLog> AuditLogs { get; set; } = null!;
        public DbSet<FaqArticle> FaqArticles { get; set; } = null!;
        public DbSet<ExportJob> ExportJobs { get; set; } = null!;
        public DbSet<SyncLog> SyncLogs { get; set; } = null!;

        // Безопасность: refresh-токены
        public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;

        public DbSet<OnboardingEvent> OnboardingEvents => Set<OnboardingEvent>(); // События онбординга пользователей.

        public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>(); // Web Push-подписки пользователей

        public DbSet<UserNotification> UserNotifications => Set<UserNotification>();
        public DbSet<NutritionEntry> NutritionEntries => Set<NutritionEntry>();
        public DbSet<MealSchedule> MealSchedules => Set<MealSchedule>();
        public DbSet<ChildAchievement> ChildAchievements => Set<ChildAchievement>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Users
            modelBuilder.Entity<User>()
                .HasIndex(u => u.TelegramId)
                .IsUnique()
                .HasFilter("\"telegramid\" IS NOT NULL");

            modelBuilder.Entity<User>()
                .HasIndex(u => u.EmailForLogin)
                .IsUnique()
                .HasFilter("\"emailforlogin\" IS NOT NULL")
                .HasDatabaseName("ix_users_emailforlogin");

            modelBuilder.Entity<User>()
                .Property(u => u.Role)
                .HasConversion<string>()
                .HasMaxLength(32)
                .HasDefaultValue(UserRole.Parent);

            modelBuilder.Entity<User>()
                .Property(u => u.CreatedAt)
                .HasDefaultValueSql("NOW()");

            // Children 
            modelBuilder.Entity<Child>()
                .Property(c => c.CreatedAt)
                .HasDefaultValueSql("NOW()");

            modelBuilder.Entity<Child>()
                .Property(c => c.UpdatedAt)
                .HasDefaultValueSql("NOW()");

            // DiabetesSettings 
            modelBuilder.Entity<DiabetesSettings>()
                .HasOne(ds => ds.Child)
                .WithOne(c => c.DiabetesSettings)
                .HasForeignKey<DiabetesSettings>(ds => ds.ChildId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DiabetesSettings>()
                .Property(ds => ds.UpdatedAt)
                .HasDefaultValueSql("NOW()");

            // ParentChildLinks 
            modelBuilder.Entity<ParentChildLink>()
                .HasKey(pcl => pcl.LinkId);

            modelBuilder.Entity<ParentChildLink>()
                .HasIndex(pcl => new { pcl.ParentUserId, pcl.ChildId })
                .IsUnique();

            modelBuilder.Entity<ParentChildLink>()
                .HasOne(pcl => pcl.ParentUser)
                .WithMany(u => u.ParentChildLinks)
                .HasForeignKey(pcl => pcl.ParentUserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ParentChildLink>()
                .HasOne(pcl => pcl.Child)
                .WithMany(c => c.ParentChildLinks)
                .HasForeignKey(pcl => pcl.ChildId)
                .OnDelete(DeleteBehavior.Cascade);

            // DoctorChildLinks 
            modelBuilder.Entity<DoctorChildLink>()
                .HasKey(dcl => dcl.LinkId);

            modelBuilder.Entity<DoctorChildLink>()
                .HasIndex(dcl => new { dcl.DoctorUserId, dcl.ChildId })
                .IsUnique();

            modelBuilder.Entity<DoctorChildLink>()
                .HasOne(dcl => dcl.DoctorUser)
                .WithMany()
                .HasForeignKey(dcl => dcl.DoctorUserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DoctorChildLink>()
                .HasOne(dcl => dcl.Child)
                .WithMany(c => c.DoctorChildLinks)
                .HasForeignKey(dcl => dcl.ChildId)
                .OnDelete(DeleteBehavior.Cascade);

            // PushSubscriptions
            modelBuilder.Entity<PushSubscription>(b =>
            {
                b.ToTable("pushsubscriptions");
                b.HasKey(s => s.SubscriptionId);
                b.Property(s => s.SubscriptionId).HasDefaultValueSql("gen_random_uuid()");
                b.Property(s => s.Endpoint).HasMaxLength(2048).IsRequired();
                b.Property(s => s.P256Dh).HasMaxLength(512).IsRequired();
                b.Property(s => s.Auth).HasMaxLength(512).IsRequired();
                b.Property(s => s.UserAgent).HasMaxLength(512);
                b.Property(s => s.CreatedAt).HasDefaultValueSql("NOW()");
                b.HasIndex(s => s.Endpoint).IsUnique();
                b.HasIndex(s => s.UserId);
                b.HasOne(s => s.User)
                 .WithMany()
                 .HasForeignKey(s => s.UserId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<UserNotification>(entity =>
            {
                entity.HasKey(notification => notification.NotificationId);
                entity.Property(notification => notification.CreatedAt)
                    .HasDefaultValueSql("NOW()");
                entity.Property(notification => notification.IsRead)
                    .HasDefaultValue(false);
                entity.HasIndex(notification => new
                    {
                        notification.RecipientUserId,
                        notification.IsRead,
                        notification.CreatedAt
                    })
                    .HasDatabaseName("ix_user_notifications_recipient_unread");
                entity.HasIndex(notification => new
                    {
                        notification.RecipientUserId,
                        notification.SourceType,
                        notification.SourceId
                    })
                    .IsUnique()
                    .HasDatabaseName("ux_user_notifications_recipient_source");
                entity.HasOne(notification => notification.RecipientUser)
                    .WithMany()
                    .HasForeignKey(notification => notification.RecipientUserId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(notification => notification.Child)
                    .WithMany()
                    .HasForeignKey(notification => notification.ChildId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<NutritionEntry>(entity =>
            {
                entity.Property(entry => entry.MealType).HasConversion<string>().HasMaxLength(24);
                entity.Property(entry => entry.Source).HasConversion<string>().HasMaxLength(24);
                entity.HasIndex(entry => new { entry.ChildId, entry.RecordedAt })
                    .HasDatabaseName("ix_nutrition_entries_child_recorded");
                entity.HasOne(entry => entry.Child).WithMany(child => child.NutritionEntries)
                    .HasForeignKey(entry => entry.ChildId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<MealSchedule>(entity =>
            {
                entity.Property(schedule => schedule.MealType).HasConversion<string>().HasMaxLength(24);
                entity.HasIndex(schedule => new { schedule.ChildId, schedule.ScheduledTime, schedule.Title })
                    .IsUnique().HasDatabaseName("ux_meal_schedules_child_time_title");
                entity.HasOne(schedule => schedule.Child).WithMany(child => child.MealSchedules)
                    .HasForeignKey(schedule => schedule.ChildId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ChildAchievement>(entity =>
            {
                entity.HasIndex(achievement => new { achievement.ChildId, achievement.AchievementCode })
                    .IsUnique().HasDatabaseName("ux_child_achievements_child_code");
                entity.HasOne(achievement => achievement.Child).WithMany(child => child.Achievements)
                    .HasForeignKey(achievement => achievement.ChildId).OnDelete(DeleteBehavior.Cascade);
            });

            // InviteCodes
            modelBuilder.Entity<InviteCode>(entity =>
            {
                // Уникальный индекс по коду — исключает коллизии при генерации
                entity.HasIndex(ic => ic.Code)
                    .IsUnique()
                    .HasDatabaseName("ix_invitecodes_code");

                // Составной индекс для быстрого поиска активных кодов ребёнка
                entity.HasIndex(ic => new { ic.ChildId, ic.ExpiresAt })
                    .HasDatabaseName("ix_invitecodes_child_expires");

                entity.Property(ic => ic.Code)
                    .IsRequired()
                    .HasMaxLength(8);

                entity.Property(ic => ic.TargetRole)
                    .HasConversion<string>()
                    .HasMaxLength(32);

                entity.Property(ic => ic.Status)
                    .HasConversion<string>()
                    .HasMaxLength(16)
                    .HasDefaultValue("Pending");

                entity.Property(ic => ic.CreatedAt)
                    .HasDefaultValueSql("NOW()");

                entity.HasOne(ic => ic.Child)
                    .WithMany()
                    .HasForeignKey(ic => ic.ChildId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // DoctorNotes
            modelBuilder.Entity<DoctorNote>(entity =>
            {
                entity.HasKey(e => e.NoteId);

                entity.Property(e => e.NoteId)
                    .HasColumnType("uuid")
                    .HasColumnName("noteid");

                entity.Property(n => n.IsImportant).HasDefaultValue(false);

                entity.Property(e => e.DoctorUserId)
                    .HasColumnType("uuid")
                    .HasColumnName("doctoruserid")
                    .IsRequired();

                entity.Property(e => e.ChildId)
                    .HasColumnType("uuid")
                    .HasColumnName("childid")
                    .IsRequired();

                entity.Property(e => e.MeasurementId)
                    .HasColumnType("uuid")
                    .HasColumnName("measurementid");

                entity.Property(e => e.NoteText)
                    .IsRequired()
                    .HasMaxLength(4000)
                    .HasColumnType("character varying(4000)")
                    .HasColumnName("notetext");

                entity.Property(e => e.IsImportant)
                    .HasColumnType("boolean")
                    .HasColumnName("isimportant");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("createdat")
                    .HasDefaultValueSql("NOW()");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("updatedat");

                // Индекс для выборки всех заметок врача по ребёнку
                entity.HasIndex(e => new { e.ChildId, e.CreatedAt })
                    .HasDatabaseName("IXdoctornoteschildidcreatedat");

                // Индекс для выборки всех заметок конкретного врача
                entity.HasIndex(e => new { e.DoctorUserId, e.ChildId })
                    .HasDatabaseName("IXdoctornotesdoctoruseridchildid");

                // Опциональный индекс по измерению
                entity.HasIndex(e => e.MeasurementId)
                    .HasDatabaseName("IXdoctornotesmeasurementid")
                    .HasFilter("measurementid IS NOT NULL");

                // Связи
                entity.HasOne(e => e.DoctorUser)
                    .WithMany()
                    .HasForeignKey(e => e.DoctorUserId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();

                entity.HasOne(e => e.Child)
                    .WithMany()
                    .HasForeignKey(e => e.ChildId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();

                entity.HasOne(e => e.Measurement)
                    .WithMany()
                    .HasForeignKey(e => e.MeasurementId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .IsRequired(false);
            });

            // Measurements
            modelBuilder.Entity<Measurement>()
                .HasIndex(m => new { m.ChildId, m.MeasurementTime })
                .HasDatabaseName("idx_measurements_child_time");

            modelBuilder.Entity<Measurement>()
                .Property(m => m.GlucoseUiState)
                .HasColumnName("glucose_ui_state")
                .HasMaxLength(16)
                .ValueGeneratedOnAddOrUpdate()
                .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);
            if (Database.IsNpgsql())
            {
                modelBuilder.Entity<Measurement>()
                    .Property(m => m.GlucoseUiState)
                    .HasComputedColumnSql(
                        "CASE "
                        + "WHEN glucose_value <= 3.1 OR glucose_value > 15.0 THEN 'Critical' "
                        + "WHEN glucose_value >= 4.0 AND glucose_value <= 10.0 THEN 'Normal' "
                        + "ELSE 'Attention' "
                        + "END",
                        stored: true);
            }
            else if (Database.IsSqlite())
            {
                modelBuilder.Entity<Measurement>()
                    .Property(m => m.GlucoseUiState)
                    .HasComputedColumnSql(
                        "CASE "
                        + "WHEN glucose_value <= 3.1 OR glucose_value > 15.0 THEN 'Critical' "
                        + "WHEN glucose_value >= 4.0 AND glucose_value <= 10.0 THEN 'Normal' "
                        + "ELSE 'Attention' "
                        + "END",
                        stored: true);
            }

            modelBuilder.Entity<Measurement>()
                .HasIndex(m => new { m.ChildId, m.GlucoseUiState, m.MeasurementTime })
                .HasDatabaseName("idx_measurements_child_uistate_time");

            // BackpackItems
            modelBuilder.Entity<BackpackItem>()
                .HasIndex(bi => bi.ChildId)
                .HasDatabaseName("idx_backpack_child");

            // AIRecommendations
            modelBuilder.Entity<AIRecommendation>()
                .HasIndex(ar => new { ar.ChildId, ar.CreatedAt })
                .HasDatabaseName("idx_recommendations_child");

            // MeasurementSchedules
            modelBuilder.Entity<MeasurementSchedule>()
                .HasIndex(ms => new { ms.ChildId, ms.ScheduledTime })
                .IsUnique();

            modelBuilder.Entity<MeasurementSchedule>()
                .HasIndex(ms => new { ms.ChildId, ms.ScheduledTime })
                .HasDatabaseName("idx_schedule_child");

            // BotUserContexts
            modelBuilder.Entity<BotUserContext>()
                .HasIndex(buc => buc.TelegramUserId)
                .IsUnique()
                .HasDatabaseName("idx_botcontext_telegram");

            modelBuilder.Entity<BotUserContext>()
                .Property(buc => buc.CreatedAt)
                .HasDefaultValueSql("NOW()");

            modelBuilder.Entity<BotUserContext>()
                .Property(buc => buc.LastActivityAt)
                .HasDefaultValueSql("NOW()");

            // AuditLogs
            modelBuilder.Entity<AuditLog>()
                .Property(a => a.CreatedAt)
                .HasDefaultValueSql("NOW()");

            // FaqArticles
            modelBuilder.Entity<FaqArticle>()
                .Property(f => f.CreatedAt)
                .HasDefaultValueSql("NOW()");

            modelBuilder.Entity<FaqArticle>()
                .Property(f => f.UpdatedAt)
                .HasDefaultValueSql("NOW()");

            // ExportJobs 
            modelBuilder.Entity<ExportJob>()
                .Property(e => e.CreatedAt)
                .HasDefaultValueSql("NOW()");

            // SyncLogs
            modelBuilder.Entity<SyncLog>()
                .Property(s => s.CreatedAt)
                .HasDefaultValueSql("NOW()");

            // RefreshTokens 
            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.ToTable("RefreshTokens");
                entity.Ignore(t => t.User);


                entity.Property(t => t.Token)
                    .IsRequired()
                    .HasMaxLength(64);

                entity.Property(t => t.UserId)
                    .IsRequired()
                    .HasMaxLength(128)
                    .HasColumnType("character varying(128)");

                entity.Property(t => t.RevokedReason)
                    .HasMaxLength(64);

                entity.Property(t => t.ReplacedByToken)
                    .HasMaxLength(64);

                entity.Property(t => t.CreatedByIp)
                    .HasMaxLength(64);

                entity.Property(t => t.CreatedByUserAgent)
                    .HasMaxLength(256);

                entity.Property(t => t.CreatedAt)
                    .HasDefaultValueSql("NOW()");

                // Уникальный индекс
                entity.HasIndex(t => t.Token)
                    .IsUnique()
                    .HasDatabaseName("IX_RefreshTokens_Token");

                // Индекс для revokeAll по пользователю
                entity.HasIndex(t => t.UserId)
                    .HasDatabaseName("IX_RefreshTokens_UserId");

                // Составной индекс для PurgeExpiredAsync
                entity.HasIndex(t => new { t.UserId, t.ExpiresAt })
                    .HasDatabaseName("IX_RefreshTokens_UserId_ExpiresAt");
            });

            modelBuilder.Entity<OnboardingEvent>(e =>
            {
                e.ToTable("onboardingevents");
                e.HasKey(o => o.OnboardingEventId);
                e.Property(o => o.CreatedAt).HasDefaultValueSql("NOW()");
                e.HasIndex(o => new { o.StepNumber, o.EventType })
                    .HasDatabaseName("idxonboardingeventssteptype")
                    .HasFilter("eventtype IN ('started', 'completed')");
                e.HasIndex(o => new { o.UserId, o.StepNumber })
                    .HasDatabaseName("idxonboardingeventsuseridstep");
                e.HasIndex(o => new { o.UserRole, o.EventType })
                    .HasDatabaseName("idxonboardingeventsroletype");
                e.HasIndex(o => o.CreatedAt)
                    .HasDatabaseName("idxonboardingeventscreatedat");
                e.HasOne(o => o.User)
                    .WithMany()
                    .HasForeignKey(o => o.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
