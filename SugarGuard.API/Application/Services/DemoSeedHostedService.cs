using Microsoft.EntityFrameworkCore;
using SugarGuard.API.Data;
using SugarGuard.API.Security;
using SugarGuard.Domain.Entities;
using SugarGuard.Domain.Enums;
using System.Security.Cryptography;

namespace SugarGuard.API.Application.Services;

public sealed class DemoSeedHostedService : IHostedService
{
    private static readonly Guid DemoChildId = new("7fdbd8ec-0e0c-4bcf-93ac-8c0d1b968319");

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DemoSeedHostedService> _logger;

    public DemoSeedHostedService(
        IServiceScopeFactory scopeFactory,
        IHostEnvironment environment,
        IConfiguration configuration,
        ILogger<DemoSeedHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _environment = environment;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
        {
            _logger.LogWarning("Запуск DemoSeedHostedService вне Development заблокирован.");
            return;
        }

        var enabled = _configuration.GetValue<bool>("DemoSeed:Enabled")
            || string.Equals(
                Environment.GetEnvironmentVariable("DEMO_SEED_ENABLED"),
                "true",
                StringComparison.OrdinalIgnoreCase);

        if (!enabled)
            return;

        var parentEmail = GetSetting("DemoSeed:ParentEmail", "DEMO_PARENT_EMAIL", "parent.demo@sugar-guard.ru");
        var doctorEmail = GetSetting("DemoSeed:DoctorEmail", "DEMO_DOCTOR_EMAIL", "doctor.demo@sugar-guard.ru");
        var password = GetSetting("DemoSeed:Password", "DEMO_PASSWORD", "DemoSugar2026!");

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var crypto = scope.ServiceProvider.GetRequiredService<ICryptoService>();

        await EnsureDemoSchemaAsync(db, cancellationToken);

        var now = DateTime.UtcNow;

        var parent = await EnsureUserAsync(db, crypto, parentEmail, password, UserRole.Parent, now, cancellationToken);
        var doctor = await EnsureUserAsync(db, crypto, doctorEmail, password, UserRole.Doctor, now, cancellationToken);
        var child = await EnsureChildAsync(db, now, cancellationToken);

        await EnsureParentLinkAsync(db, parent.UserId, child.ChildId, now, cancellationToken);
        await EnsureDoctorLinkAsync(db, doctor.UserId, child.ChildId, parent.UserId, now, cancellationToken);
        await EnsureDiabetesSettingsAsync(db, child.ChildId, now, cancellationToken);
        await EnsureMeasurementsAsync(db, child.ChildId, now, cancellationToken);
        await EnsureBackpackAsync(db, child.ChildId, now, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "Демо-данные подготовлены. Parent={ParentEmail}; Doctor={DoctorEmail}; ChildId={ChildId}",
            parentEmail,
            doctorEmail,
            child.ChildId);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private string GetSetting(string configKey, string envKey, string fallback)
    {
        return Environment.GetEnvironmentVariable(envKey)
            ?? _configuration[configKey]
            ?? fallback;
    }

    private static async Task EnsureDemoSchemaAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE parent_child_links ADD COLUMN IF NOT EXISTS linkedbyuserid uuid;
            ALTER TABLE parent_child_links ADD COLUMN IF NOT EXISTS notes character varying(1000);
            ALTER TABLE doctor_child_links ADD COLUMN IF NOT EXISTS isactive boolean NOT NULL DEFAULT true;
            ALTER TABLE doctor_child_links ADD COLUMN IF NOT EXISTS deactivatedat timestamp with time zone;
            ALTER TABLE doctor_child_links ADD COLUMN IF NOT EXISTS linkedbyuserid uuid;
            ALTER TABLE doctor_child_links ADD COLUMN IF NOT EXISTS notes character varying(1000);
            """,
            cancellationToken);
    }

    private static async Task<User> EnsureUserAsync(
        AppDbContext db,
        ICryptoService crypto,
        string email,
        string password,
        UserRole role,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.EmailForLogin == normalizedEmail, cancellationToken);
        var credentials = HashPassword(password);

        if (user is null)
        {
            user = new User
            {
                EmailForLogin = normalizedEmail,
                EncryptedEmail = crypto.Encrypt(normalizedEmail),
                EncryptedFirstName = crypto.Encrypt(role == UserRole.Doctor ? "Демо" : "Анна"),
                EncryptedLastName = crypto.Encrypt(role == UserRole.Doctor ? "Эндокринолог" : "Родитель"),
                PasswordHash = credentials.HashBase64,
                PasswordSalt = credentials.SaltBase64,
                Role = role,
                IsActive = true,
                IsEmailVerified = true,
                EmailVerifiedAt = now,
                OnboardingCompleted = true,
                OnboardingCompletedAt = now,
                OnboardingCurrentStep = 3,
                CreatedAt = now
            };
            db.Users.Add(user);
            return user;
        }

        user.PasswordHash = credentials.HashBase64;
        user.PasswordSalt = credentials.SaltBase64;
        user.Role = role;
        user.IsActive = true;
        user.IsEmailVerified = true;
        user.EmailVerifiedAt ??= now;
        user.OnboardingCompleted = true;
        user.OnboardingCompletedAt ??= now;
        user.OnboardingCurrentStep = Math.Max(user.OnboardingCurrentStep, 3);
        user.EncryptedEmail ??= crypto.Encrypt(normalizedEmail);

        return user;
    }

    private static async Task<Child> EnsureChildAsync(
        AppDbContext db,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var child = await db.Children.FirstOrDefaultAsync(c => c.ChildId == DemoChildId, cancellationToken);
        if (child is not null)
        {
            child.SetupCompleted = true;
            child.SetupCompletedAt ??= now;
            child.UpdatedAt = now;
            return child;
        }

        child = new Child
        {
            ChildId = DemoChildId,
            FirstName = "Миша",
            LastName = "Смирнов",
            DateOfBirth = new DateOnly(2016, 5, 10),
            Weight = 30.0m,
            Height = 130.0m,
            DiabetesType = "Type1",
            DiagnosisDate = new DateOnly(2021, 4, 1),
            TimeZoneId = "Europe/Moscow",
            CurrentInsulins = "[]",
            SetupCompleted = true,
            SetupCompletedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Children.Add(child);
        return child;
    }

    private static async Task EnsureParentLinkAsync(
        AppDbContext db,
        Guid parentUserId,
        Guid childId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var exists = await db.ParentChildLinks.AnyAsync(
            l => l.ParentUserId == parentUserId && l.ChildId == childId,
            cancellationToken);

        if (!exists)
        {
            db.ParentChildLinks.Add(new ParentChildLink
            {
                LinkId = Guid.NewGuid(),
                ParentUserId = parentUserId,
                ChildId = childId,
                LinkedByUserId = parentUserId,
                Notes = "Demo link",
                CreatedAt = now
            });
        }
    }

    private static async Task EnsureDoctorLinkAsync(
        AppDbContext db,
        Guid doctorUserId,
        Guid childId,
        Guid linkedByUserId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var link = await db.DoctorChildLinks.FirstOrDefaultAsync(
            l => l.DoctorUserId == doctorUserId && l.ChildId == childId,
            cancellationToken);

        if (link is null)
        {
            db.DoctorChildLinks.Add(new DoctorChildLink
            {
                LinkId = Guid.NewGuid(),
                DoctorUserId = doctorUserId,
                ChildId = childId,
                LinkedByUserId = linkedByUserId,
                Notes = "Demo doctor access",
                IsActive = true,
                CreatedAt = now
            });
            return;
        }

        link.IsActive = true;
        link.DeactivatedAt = null;
    }

    private static async Task EnsureDiabetesSettingsAsync(
        AppDbContext db,
        Guid childId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var exists = await db.DiabetesSettings.AnyAsync(s => s.ChildId == childId, cancellationToken);
        if (!exists)
        {
            db.DiabetesSettings.Add(new DiabetesSettings
            {
                ChildId = childId,
                TargetRangeMin = 4.0m,
                TargetRangeMax = 10.0m,
                InsulinSensitivity = 1.5m,
                CarbInsulinRatio = 10.0m,
                UpdatedAt = now
            });
        }
    }

    private static async Task EnsureMeasurementsAsync(
        AppDbContext db,
        Guid childId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var hasMeasurements = await db.Measurements.AnyAsync(m => m.ChildId == childId, cancellationToken);
        if (hasMeasurements)
            return;

        db.Measurements.AddRange(
            new Measurement
            {
                ChildId = childId,
                GlucoseValue = 6.4m,
                MeasurementTime = now.AddHours(-5),
                ChildState = "normal",
                Notes = "Демо: утреннее измерение",
                DataSource = "demo_seed",
                CreatedAt = now.AddHours(-5)
            },
            new Measurement
            {
                ChildId = childId,
                GlucoseValue = 8.2m,
                MeasurementTime = now.AddHours(-2),
                ChildState = "after_meal",
                Notes = "Демо: после перекуса",
                DataSource = "demo_seed",
                CreatedAt = now.AddHours(-2)
            },
            new Measurement
            {
                ChildId = childId,
                GlucoseValue = 5.9m,
                MeasurementTime = now.AddMinutes(-25),
                ChildState = "normal",
                Notes = "Демо: последнее значение",
                DataSource = "demo_seed",
                CreatedAt = now.AddMinutes(-25)
            });
    }

    private static async Task EnsureBackpackAsync(
        AppDbContext db,
        Guid childId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var hasBackpack = await db.BackpackItems.AnyAsync(i => i.ChildId == childId, cancellationToken);
        if (hasBackpack)
            return;

        db.BackpackItems.AddRange(
            new BackpackItem
            {
                ChildId = childId,
                SnackName = "Сок яблочный 200 мл",
                BreadUnits = 1.8m,
                AddedBy = "demo",
                CreatedAt = now
            },
            new BackpackItem
            {
                ChildId = childId,
                SnackName = "Батончик злаковый",
                BreadUnits = 1.2m,
                AddedBy = "demo",
                CreatedAt = now
            });
    }

    private static (string HashBase64, string SaltBase64) HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        using var pbkdf2 = new Rfc2898DeriveBytes(
            password,
            salt,
            600_000,
            HashAlgorithmName.SHA256);

        var hash = pbkdf2.GetBytes(32);
        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }
}
