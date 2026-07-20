using Hangfire;
using Hangfire.Dashboard;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SugarGuard.API.Application.Ai;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Application.Services;
using SugarGuard.API.Configuration;
using SugarGuard.API.Data;
using SugarGuard.API.Infrastructure.BackgroundServices;
using SugarGuard.API.Infrastructure.Jobs;
using SugarGuard.API.Middleware;
using SugarGuard.API.Policies;
using SugarGuard.API.Security;
using SugarGuard.API.Services;
using SugarGuard.Application.Audit;
using SugarGuard.Application.Dashboard;
using SugarGuard.Application.Glucose;
using SugarGuard.Application.Repositories;
using SugarGuard.Application.Security;
using SugarGuard.Domain.Entities;
using SugarGuard.Domain.Enums;
using SugarGuard.Infrastructure.Glucose;
using SugarGuard.Infrastructure.Repositories;
using SugarGuard.Infrastructure.Security;
using SugarGuard.Infrastructure.Sync;
using SugarGuard.MaxBot.Abstractions;
using SugarGuard.MaxBot.Services;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
var isDevelopment = builder.Environment.IsDevelopment();

// Секреты и строка подключения
var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
    ?? builder.Configuration["Jwt:Secret"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "SugarGuardAPI";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "SugarGuardClients";
var jwtExpiryHours = builder.Configuration.GetValue<int>("Jwt:ExpiryHours", 24);
var refreshTokenExpiryDays = builder.Configuration.GetValue<int>("Jwt:RefreshTokenExpiryDays", 30);

if (string.IsNullOrWhiteSpace(jwtSecret))
{
    if (isDevelopment)
    {
        var randomBytes = new byte[64];
        System.Security.Cryptography.RandomNumberGenerator.Fill(randomBytes);
        jwtSecret = Convert.ToBase64String(randomBytes);

        Console.Error.WriteLine(
            "[SECURITY WARNING] JWT secret is not configured. " +
            "Generated RANDOM per-process dev-only secret. " +
            "All JWTs issued in this dev run are valid ONLY until restart. " +
            "Set JWT_SECRET_KEY env var or Jwt:Secret in appsettings " +
            "for stable tokens.");
    }
    else
    {
        throw new InvalidOperationException("JWT_SECRET_KEY is required in production.");
    }
}

var jwtSettings = new JwtSettings
{
    Secret = jwtSecret,
    Issuer = jwtIssuer,
    Audience = jwtAudience,
    ExpiryHours = jwtExpiryHours,
    RefreshTokenExpiryDays = refreshTokenExpiryDays
};

var demoEmailBypassSettings = new DemoEmailBypassSettings
{
    Enabled = builder.Configuration.GetValue<bool>("DemoEmailBypass:Enabled")
        || string.Equals(
            Environment.GetEnvironmentVariable("DEMO_EMAIL_BYPASS_ENABLED"),
            "true",
            StringComparison.OrdinalIgnoreCase)
};

var gigaChatClientId = Environment.GetEnvironmentVariable("GIGACHAT_CLIENT_ID")
    ?? builder.Configuration["GigaChat:ClientId"];
var gigaChatClientSecret = Environment.GetEnvironmentVariable("GIGACHAT_CLIENT_SECRET")
    ?? builder.Configuration["GigaChat:ClientSecret"];
var connectionString = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING")
    ?? builder.Configuration.GetConnectionString("DefaultConnection");

var phiEncryptionKey = Environment.GetEnvironmentVariable("PHI_ENCRYPTION_KEY")
    ?? builder.Configuration["Crypto:PhiEncryptionKey"];

if (!isDevelopment)
{
    if (string.IsNullOrWhiteSpace(gigaChatClientId))
        throw new InvalidOperationException("GIGACHAT_CLIENT_ID is required in production.");
    if (string.IsNullOrWhiteSpace(gigaChatClientSecret))
        throw new InvalidOperationException("GIGACHAT_CLIENT_SECRET is required in production.");
    if (string.IsNullOrWhiteSpace(connectionString))
        throw new InvalidOperationException("DATABASE_CONNECTION_STRING is required in production.");
    if (string.IsNullOrWhiteSpace(phiEncryptionKey))
        throw new InvalidOperationException("PHI_ENCRYPTION_KEY is required in production.");
}

var useSqlite = isDevelopment && string.IsNullOrWhiteSpace(connectionString);
if (useSqlite)
{
    connectionString = "Data Source=sugarguard_dev.db";
    Console.WriteLine("[DEV] Using SQLite fallback. Set DATABASE_CONNECTION_STRING to use PostgreSQL.");
}
else if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Database connection string is not configured. " +
        "Set DATABASE_CONNECTION_STRING or ConnectionStrings:DefaultConnection.");
}

builder.Configuration["GigaChat:ClientId"] = gigaChatClientId;
builder.Configuration["GigaChat:ClientSecret"] = gigaChatClientSecret;

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto |
        ForwardedHeaders.XForwardedHost;

    options.KnownProxies.Clear();
    options.KnownProxies.Add(System.Net.IPAddress.Loopback);
    options.KnownProxies.Add(System.Net.IPAddress.IPv6Loopback);

    var configuredCidrs = builder.Configuration
        .GetSection("ForwardedHeaders:KnownProxies:Cidrs")
        .Get<string[]>();
    if (configuredCidrs is not null)
    {
        foreach (var cidr in configuredCidrs)
        {
            if (IPNetwork.TryParse(cidr, out var network))
            {
                options.KnownNetworks.Add(network);
            }
        }
    }
    options.ForwardLimit = 2;
});

builder.Services.AddSingleton(jwtSettings);
builder.Services.AddSingleton(demoEmailBypassSettings);
builder.Services.AddOptions<AiClinicalContextOptions>()
    .Bind(builder.Configuration.GetSection(AiClinicalContextOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                     ?? context.User.FindFirst("sub")?.Value;
        var ip = Program.GetClientIp(context);
        var partitionKey = string.IsNullOrWhiteSpace(userId)
            ? $"anonymous:{ip}"
            : $"user:{userId}";

        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ =>
            new FixedWindowRateLimiterOptions
            {
                PermitLimit = string.IsNullOrWhiteSpace(userId) ? 100 : 300,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    options.AddPolicy("auth-login", context =>
    {
        var ip = Program.GetClientIp(context);

        return RateLimitPartition.GetFixedWindowLimiter($"login:{ip}", _ =>
            new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    options.AddFixedWindowLimiter("recommendations", cfg =>
    {
        cfg.PermitLimit = 10;
        cfg.Window = TimeSpan.FromMinutes(1);
        cfg.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        cfg.QueueLimit = 2;
    });

    options.AddFixedWindowLimiter("bot-login", cfg =>
    {
        cfg.PermitLimit = 5;
        cfg.Window = TimeSpan.FromMinutes(1);
        cfg.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        cfg.QueueLimit = 0;
    });

    options.OnRejected = async (context, cancellationToken) =>
    {
        var response = context.HttpContext.Response;

        var retryAfterSeconds = 60;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            retryAfterSeconds = (int)retryAfter.TotalSeconds;
        }
        else if (context.HttpContext.Request.Path
                     .StartsWithSegments("/api/auth/login", StringComparison.OrdinalIgnoreCase))
        {
            retryAfterSeconds = 15 * 60;
        }

        var message = retryAfterSeconds >= 60
            ? $"Слишком много попыток входа. Повторите через {retryAfterSeconds / 60} мин."
            : "Слишком много запросов. Повторите через минуту.";

        response.StatusCode = StatusCodes.Status429TooManyRequests;
        response.ContentType = "application/json; charset=utf-8";
        response.Headers["Retry-After"] = retryAfterSeconds.ToString();
        response.Headers["X-RateLimit-Reset"] =
            DateTimeOffset.UtcNow.AddSeconds(retryAfterSeconds).ToUnixTimeSeconds().ToString();

        var logger = context.HttpContext.RequestServices
            .GetRequiredService<ILogger<Program>>();

        logger.LogWarning(
            "RateLimit 429: Path={Path} IP={IP} RetryAfterSec={RetryAfter}.",
            context.HttpContext.Request.Path,
            context.HttpContext.Connection.RemoteIpAddress,
            retryAfterSeconds);

        await response.WriteAsJsonAsync(
            new { error = "rate_limit_exceeded", message },
            cancellationToken: cancellationToken);
    };
});

// Controllers
builder.Services.AddControllers(options =>
{
    options.Filters.Add<ApiProblemDetailsResultFilter>();
});

// Аутентификация и авторизация
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ParentOrDoctorOrAdmin", policy =>
        policy.RequireRole("Parent", "Doctor", "Admin", "SupportAdmin", "ChildDevice"));

    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("Admin", "SupportAdmin"));

    options.AddPolicy("DoctorOrAdmin", policy =>
        policy.RequireRole(
            UserRole.Doctor.ToString(),
            UserRole.Admin.ToString(),
            UserRole.SupportAdmin.ToString()));
});

// Инфраструктура
builder.Services.AddMemoryCache();

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (useSqlite)
        options.UseSqlite(connectionString);
    else
        options.UseNpgsql(connectionString);
});
builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<AppDbContext>());

builder.Services.AddDbContextFactory<AppDbContext>(options =>
{
    if (useSqlite)
        options.UseSqlite(connectionString);
    else
        options.UseNpgsql(connectionString);
});

builder.Services.AddDbContext<SyncDbContext>(options =>
{
    if (useSqlite)
        options.UseSqlite(connectionString);
    else
        options.UseNpgsql(connectionString);
});

// Репозитории
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IChildRepository, ChildRepository>();
builder.Services.AddScoped<IMeasurementRepository, MeasurementRepository>();
builder.Services.AddScoped<IParentChildLinkRepository, ParentChildLinkRepository>();
builder.Services.AddScoped<IDoctorChildLinkRepository, DoctorChildLinkRepository>();
builder.Services.AddScoped<IInviteCodeRepository, InviteCodeRepository>();
builder.Services.AddScoped<IDoctorNoteRepository, DoctorNoteRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IOnboardingEventRepository, OnboardingEventRepository>();
builder.Services.AddScoped<IExportJobRepository, ExportJobRepository>();
builder.Services.AddScoped<ISyncLogRepository, SyncLogRepository>();
builder.Services.AddScoped<IAIRecommendationRepository, AIRecommendationRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IBackpackItemRepository, BackpackItemRepository>();
builder.Services.AddScoped<IDiabetesSettingsRepository, DiabetesSettingsRepository>();
builder.Services.AddScoped<IPushSubscriptionRepository, PushSubscriptionRepository>();

if (!useSqlite)
{
    builder.Services.AddHangfire(configuration => configuration
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UsePostgreSqlStorage(options =>
        {
            options.UseNpgsqlConnection(connectionString);
        }, new PostgreSqlStorageOptions
        {
            QueuePollInterval = TimeSpan.FromSeconds(10),
            JobExpirationCheckInterval = TimeSpan.FromHours(1),
            CountersAggregateInterval = TimeSpan.FromMinutes(5),
            PrepareSchemaIfNecessary = true,
            TransactionSynchronisationTimeout = TimeSpan.FromMinutes(5),
            SchemaName = "hangfire"
        }));

    builder.Services.AddHangfireServer(options =>
    {
        options.WorkerCount = Environment.ProcessorCount;
        options.Queues = ["default", "cleanup"];
    });
}

// DI — сервисы
builder.Services.AddHttpContextAccessor();

// Auth / Security
builder.Services.AddScoped<IPasswordVerificationService, PasswordVerificationService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
builder.Services.AddScoped<IRolePermissionService, RolePermissionService>();
builder.Services.AddScoped<ICurrentUserContext, CurrentUserContext>();
builder.Services.AddScoped<IChildAccessService, ChildAccessService>();
builder.Services.AddSingleton<IServerMetricsService, ServerMetricsService>();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddScoped<IEmailService, DevEmailService>();
}
else
{
    builder.Services.AddScoped<IEmailService, SmtpEmailService>();
}

builder.Services.AddScoped<IVerificationService, VerificationService>();

builder.Services.AddSingleton<ICryptoService, CryptoService>();

builder.Services.AddSingleton<IConnectionCodeHasher, HmacConnectionCodeHasher>();

// Бизнес-логика
builder.Services.AddSingleton<IAuditDetailsRedactor, AuditDetailsRedactor>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IInviteCodeService, InviteCodeService>();
builder.Services.AddScoped<IDoctorNoteService, DoctorNoteService>();
builder.Services.AddScoped<IUserNotificationService, UserNotificationService>();
builder.Services.AddOptions<SupportEmailOptions>()
    .Bind(builder.Configuration.GetSection(SupportEmailOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.InboxEmail), "Не указан email поддержки.")
    .Validate(options => options.MaxAttachmentBytes is > 0 and <= 10 * 1024 * 1024, "Некорректный лимит вложения поддержки.")
    .Validate(options => options.MaxDiagnosticsBytes is > 0 and <= 2 * 1024 * 1024, "Некорректный лимит диагностического лога.")
    .ValidateOnStart();
builder.Services.AddScoped<ISupportConversationService, SupportConversationService>();
builder.Services.AddScoped<INutritionTrackerService, NutritionTrackerService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IChildrenService, ChildrenService>();
builder.Services.AddScoped<IAccountProfileService, AccountProfileService>();
builder.Services.AddSingleton<IUploadPathProvider, UploadPathProvider>();
builder.Services.AddScoped<IDiabetesSettingsService, DiabetesSettingsService>();
builder.Services.AddScoped<IOnboardingService, OnboardingService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IParentDashboardService, ParentDashboardService>();
builder.Services.AddScoped<IDoctorDashboardService, DoctorDashboardService>();
builder.Services.AddScoped<IBackpackService, BackpackService>();
builder.Services.AddScoped<IGlucoseStatusService, GlucoseStatusService>();
builder.Services.AddScoped<IGlucoseUiStateService, GlucoseUiStateService>();
builder.Services.AddScoped<IStatisticsCalculationService, StatisticsCalculationService>();
builder.Services.AddScoped<IRecommendationService, RecommendationService>();
builder.Services.AddScoped<IClinicalContextBuilder, ClinicalContextBuilder>();
builder.Services.AddScoped<IAiRecommendationSafetyPolicy, AiRecommendationSafetyPolicy>();
builder.Services.AddScoped<IAiRecommendationWorkflowService, AiRecommendationWorkflowService>();
builder.Services.AddScoped<IExportJobService, ExportJobService>();
builder.Services.AddScoped<IPdfExportService, PdfExportService>();
builder.Services.AddScoped<IHealthService, HealthService>();
builder.Services.AddScoped<IFaqContentService, FaqContentService>();
builder.Services.AddScoped<ISyncLogService, SyncLogService>();
builder.Services.AddScoped<IExportJobApiService, ExportJobApiService>();
builder.Services.AddScoped<ICsvExportService, CsvExportService>();
builder.Services.AddScoped<IBotUserContextService, BotUserContextService>();
builder.Services.AddScoped<IParentLinkService, ParentLinkService>();
builder.Services.AddScoped<IMeasurementsService, MeasurementsService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IBotApiKeyValidator, BotApiKeyValidatorAdapter>();

if (builder.Configuration.GetValue<bool>("DemoSeed:Enabled")
    || string.Equals(
        Environment.GetEnvironmentVariable("DEMO_SEED_ENABLED"),
        "true",
        StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddHostedService<DemoSeedHostedService>();
}

const string TelegramHttpClientName = TelegramNotificationService.HttpClientName;
builder.Services.AddHttpClient(TelegramHttpClientName, client =>
{
    client.BaseAddress = new Uri("https://api.telegram.org/");
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});
builder.Services.AddScoped<ITelegramNotificationService, TelegramNotificationService>();

const string MaxHttpClientName = MaxBotClient.HttpClientName;
builder.Services.AddHttpClient(MaxHttpClientName, client =>
{
    client.BaseAddress = new Uri("https://platform-api2.max.ru/");
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});
builder.Services.AddScoped<IMaxBotService, MaxBotService>();
builder.Services.AddScoped<IMaxBotClient, MaxBotClient>();
builder.Services.AddHostedService<MaxWebhookRegistrationService>();

// Web Push
builder.Services.AddScoped<IWebPushService, WebPushService>();

builder.Services.AddScoped<ExportJobProcessor>();

if (!useSqlite)
{
    builder.Services.AddSingleton<IBackgroundEnqueuer, HangfireBackgroundEnqueuer>();
}
else
{
    builder.Services.AddSingleton<IBackgroundEnqueuer, InlineBackgroundEnqueuer>();
}


// Фоновые задачи
if (!useSqlite)
{
    builder.Services.AddScoped<MidnightCleanupJob>();
    builder.Services.AddScoped<DailyParentSummaryJob>();
    builder.Services.AddScoped<SugarGuard.API.Application.Services.BackpackCleanupService>();
}

// GigaChat
builder.Services.AddSingleton<IGigaChatTokenCache, GigaChatTokenCache>();
builder.Services.AddHttpClient<IGigaChatService, GigaChatService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
})
    .AddPolicyHandler((serviceProvider, _) =>
    {
        var logger = serviceProvider.GetRequiredService<ILogger<GigaChatService>>();
        return GigaChatPolicyConfiguration.GetRetryPolicy(logger);
    })
    .AddPolicyHandler((serviceProvider, _) =>
    {
        var logger = serviceProvider.GetRequiredService<ILogger<GigaChatService>>();
        return GigaChatPolicyConfiguration.GetCircuitBreakerPolicy(logger);
    });

// Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SugarGuard API",
        Version = "v1",
        Description = "API for SugarGuard diabetes monitoring platform"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Введите JWT-токен (без префикса 'Bearer ')."
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id   = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("WebClient", policy =>
    {
        var allowedOrigins = builder.Configuration
            .GetSection("AllowedOrigins")
            .Get<string[]>()
            ?? ["https://localhost:7247", "https://localhost:5001"];

        var allowedHeaders = new[]
        {
            "Authorization",
            "Content-Type",
            "X-Requested-With",
            "X-CSRF-TOKEN",
            "X-Telegram-Webhook-Secret"
        };

        policy.WithOrigins(allowedOrigins)
              .WithHeaders(allowedHeaders)
              .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
              .AllowCredentials();
    });
});

builder.Services.AddHttpLogging(logging =>
{
    logging.LoggingFields = Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.RequestMethod
                          | Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.RequestPath
                          | Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.ResponseStatusCode
                          | Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.Duration;
    logging.RequestBodyLogLimit = 4096;
    logging.ResponseBodyLogLimit = 4096;
});

var app = builder.Build();

app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    await Program.EnsureDevelopmentSeedDataAsync(app);

    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "SugarGuard API v1");
        c.RoutePrefix = string.Empty;
    });

    if (!useSqlite)
    {
        app.UseHangfireDashboard("/hangfire", new DashboardOptions
        {
            Authorization = [new HangfireAuthorizationFilter(
                app.Services.GetRequiredService<IHttpContextAccessor>(),
                app.Services.GetRequiredService<ILogger<HangfireAuthorizationFilter>>())]
        });
    }
}

if (!app.Environment.IsDevelopment()
    && !useSqlite
    && builder.Configuration.GetValue<bool>("ApplyMigrationsOnStart"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    app.Logger.LogInformation("Database migrations applied.");
}

if (!app.Environment.IsDevelopment() && !useSqlite)
{
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = [new HangfireAuthorizationFilter(
            app.Services.GetRequiredService<IHttpContextAccessor>(),
            app.Services.GetRequiredService<ILogger<HangfireAuthorizationFilter>>())],
        IsReadOnlyFunc = ctx => !ctx.GetHttpContext()!.User.IsInRole("Admin")
    });
}

app.UseHttpLogging();
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
app.UseHttpsRedirection();
app.UseStaticFiles();

// Security headers (Helmet-like)
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["X-Permitted-Cross-Domain-Policies"] = "none";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    context.Response.Headers["Content-Security-Policy"] =
        "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";
    await next();
});

app.UseCors("WebClient");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapGet("/uploads/profiles/{fileName}", (string fileName, IUploadPathProvider uploadPaths) =>
{
    if (string.IsNullOrWhiteSpace(fileName)
        || fileName != Path.GetFileName(fileName)
        || fileName.Any(character => !(char.IsLetterOrDigit(character) || character is '-' or '_' or '.')))
    {
        return Results.BadRequest();
    }

    var extension = Path.GetExtension(fileName).ToLowerInvariant();
    var contentType = extension switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".webp" => "image/webp",
        _ => null
    };

    if (contentType is null)
    {
        return Results.BadRequest();
    }

    var path = uploadPaths.GetProfileFilePath(fileName);
    return File.Exists(path)
        ? Results.File(path, contentType, enableRangeProcessing: false)
        : Results.NotFound();
}).AllowAnonymous();

app.MapControllers();

if (!useSqlite)
{
    MidnightCleanupJob.ScheduleRecurringJob();
    DailyParentSummaryJob.ScheduleRecurringJob();
}

app.Run();

public partial class Program
{
    /// <summary>
    /// Создаёт тестового родителя, ребёнка и измерения, если их ещё нет в БД
    /// </summary>
    internal static async Task EnsureDevelopmentSeedDataAsync(WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
            return;

        const string defaultEmail = "parent.test@sugarguard.local";
        const string defaultPassword = "ParentTest123!";
        var childId = new Guid("7fdbd8ec-0e0c-4bcf-93ac-8c0d1b968319");

        var email = (app.Configuration["DevSeed:TestParentEmail"] ?? defaultEmail).Trim().ToLowerInvariant();
        var password = app.Configuration["DevSeed:TestParentPassword"] ?? defaultPassword;

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.EnsureCreatedAsync();

        var parent = await db.Users.FirstOrDefaultAsync(u => u.EmailForLogin == email);
        if (parent is null)
        {
            var credentials = HashPassword(password);
            parent = new User
            {
                EmailForLogin = email,
                PasswordHash = credentials.HashBase64,
                PasswordSalt = credentials.SaltBase64,
                Role = SugarGuard.Domain.Enums.UserRole.Parent,
                IsEmailVerified = true,
                CreatedAt = DateTime.UtcNow
            };
            db.Users.Add(parent);
        }
        else if (!parent.IsEmailVerified)
        {
            parent.IsEmailVerified = true;
        }

        var child = await db.Children.FirstOrDefaultAsync(c => c.ChildId == childId);
        if (child is null)
        {
            child = new Child
            {
                ChildId = childId,
                FirstName = "Тест",
                LastName = "Ребенок",
                DateOfBirth = new DateOnly(2016, 5, 10),
                Weight = 30.0m,
                Height = 130.0m,
                DiabetesType = "Type1",
                DiagnosisDate = new DateOnly(2021, 4, 1),
                TimeZoneId = "Europe/Moscow",
                CurrentInsulins = "[]",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Children.Add(child);
        }

        if (!await db.ParentChildLinks.AnyAsync(l =>
                l.ParentUserId == parent.UserId && l.ChildId == child.ChildId))
        {
            db.ParentChildLinks.Add(new ParentChildLink
            {
                ParentUserId = parent.UserId,
                ChildId = child.ChildId,
                CreatedAt = DateTime.UtcNow
            });
        }

        if (!await db.Measurements.AnyAsync(m => m.ChildId == child.ChildId))
        {
            var now = DateTime.UtcNow;
            db.Measurements.AddRange(
                new Measurement
                {
                    ChildId = child.ChildId,
                    GlucoseValue = 6.4m,
                    MeasurementTime = now.AddHours(-4),
                    ChildState = "normal",
                    Notes = "Seeded measurement",
                    DataSource = "manual",
                    CreatedAt = now.AddHours(-4)
                },
                new Measurement
                {
                    ChildId = child.ChildId,
                    GlucoseValue = 7.1m,
                    MeasurementTime = now.AddHours(-2),
                    ChildState = "after_meal",
                    Notes = "Seeded measurement",
                    DataSource = "manual",
                    CreatedAt = now.AddHours(-2)
                },
                new Measurement
                {
                    ChildId = child.ChildId,
                    GlucoseValue = 5.8m,
                    MeasurementTime = now.AddMinutes(-40),
                    ChildState = "normal",
                    Notes = "Seeded measurement",
                    DataSource = "manual",
                    CreatedAt = now.AddMinutes(-40)
                });
        }

        await db.SaveChangesAsync();

        app.Logger.LogWarning(
            "Development test account ensured. Email: {Email}; ChildId: {ChildId}. Password is configured via DevSeed:TestParentPassword.",
            email, childId);
    }

    /// <summary>
    /// Хэширует пароль с помощью PBKDF2 / SHA-256
    /// </summary>
    private static (string HashBase64, string SaltBase64) HashPassword(string password)
    {
        const int saltSize = 16;
        const int hashSize = 32;

        var salt = RandomNumberGenerator.GetBytes(saltSize);
        using var pbkdf2 = new Rfc2898DeriveBytes(
            password,
            salt,
            600_000,
            HashAlgorithmName.SHA256);

        var hash = pbkdf2.GetBytes(hashSize);
        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    internal static string GetClientIp(HttpContext context)
    {
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            var firstForwardedIp = forwardedFor.Split(',', StringSplitOptions.TrimEntries)[0];
            if (!string.IsNullOrWhiteSpace(firstForwardedIp))
            {
                return firstForwardedIp;
            }
        }

        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(realIp))
        {
            return realIp;
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
