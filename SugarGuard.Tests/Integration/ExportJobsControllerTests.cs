// Feature: sugarguard-project-completion

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FsCheck;
using FsCheck.Xunit;
using Hangfire;
using Hangfire.InMemory;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Data;
using SugarGuard.API.DTOs;
using SugarGuard.Domain.Entities;
using SugarGuard.Application.Audit;
using SugarGuard.Domain.Enums;

namespace SugarGuard.Tests.Integration;

// ---------------------------------------------------------------------------
// Custom WebApplicationFactory
// ---------------------------------------------------------------------------

/// <summary>
/// Replaces the real PostgreSQL database and Hangfire with in-memory
/// equivalents so integration tests run without any external infrastructure.
/// </summary>
public sealed class ExportJobsWebApplicationFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// Unique database name per factory instance to guarantee test isolation.
    /// </summary>
    public readonly string DbName = $"ExportJobsTests_{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddDebug();
        });

        // Provide a dummy connection string so Program.cs validation passes.
        // The actual DbContext will be replaced with InMemory below.
        builder.UseSetting(
            "ConnectionStrings:DefaultConnection",
            "Host=localhost;Database=test;Username=test;Password=test");

        // H-6 (release 1.0.0): фиксируем JWT secret в конфигурации, чтобы
        // тесты подписывали токены тем же ключом, что использует API.
        // Без этого Program.cs генерирует РАНДОМНЫЙ secret при каждом
        // запуске, и тестовые токены не проходят валидацию (401).
        builder.UseSetting("Jwt:Secret", JwtSecretForTests);
        builder.UseSetting("Jwt:Issuer", "SugarGuardAPI");
        builder.UseSetting("Jwt:Audience", "SugarGuardClients");

        builder.ConfigureServices(services =>
        {
            // ----------------------------------------------------------------
            // Replace PostgreSQL DbContext with InMemory.
            //
            // We remove the existing DbContextOptions<AppDbContext> descriptor
            // (registered by AddDbContext(...UseNpgsql...)) and replace it
            // with one that uses InMemory. The AppDbContext descriptor itself
            // is left in place — it will pick up the new options.
            // ----------------------------------------------------------------
            var optionsDescriptor = services.FirstOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));

            if (optionsDescriptor != null)
            {
                services.Remove(optionsDescriptor);
            }

            // Add InMemory options as a singleton (matches the default lifetime
            // used by AddDbContext for options).
            services.AddSingleton<DbContextOptions<AppDbContext>>(_ =>
                new DbContextOptionsBuilder<AppDbContext>()
                    .UseInMemoryDatabase(DbName)
                    .Options);

            // ----------------------------------------------------------------
            // Replace Hangfire PostgreSQL storage with in-memory storage
            // so tests don't require a real database connection.
            // ----------------------------------------------------------------
            services.AddHangfire(cfg => cfg.UseInMemoryStorage());

            // ----------------------------------------------------------------
            // Replace IAuditService with a no-op to avoid DB dependency
            // ----------------------------------------------------------------
            services.RemoveAll<IAuditService>();
            services.AddScoped<IAuditService, NoOpAuditService>();

            // Keep the controller integration test deterministic: it verifies
            // enqueue orchestration, not Hangfire storage behavior.
            services.RemoveAll<IBackgroundEnqueuer>();
            services.AddSingleton<IBackgroundEnqueuer, NoOpBackgroundEnqueuer>();
        });
    }

    // -----------------------------------------------------------------------
    // H-6: фиксированный dev-secret для тестов — должен совпадать с тем,
    // что устанавливает WebApplicationFactory в конфиг.
    // -----------------------------------------------------------------------
    internal const string JwtSecretForTests = "ExportJobsTestSecret_MustMatchFactorySetting_2026";
}

/// <summary>
/// No-op audit service used in integration tests to avoid writing to the DB.
/// </summary>
internal sealed class NoOpAuditService : IAuditService
{
    public Task WriteAsync(
        string action,
        string? targetType = null,
        string? targetId = null,
        string? details = null,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class NoOpBackgroundEnqueuer : IBackgroundEnqueuer
{
    public void EnqueueExportJob(Guid exportJobId)
    {
    }
}

// ---------------------------------------------------------------------------
// Test collection — shared factory instance
// ---------------------------------------------------------------------------

[CollectionDefinition("ExportJobs")]
public sealed class ExportJobsCollection : ICollectionFixture<ExportJobsWebApplicationFactory> { }

// ---------------------------------------------------------------------------
// Integration tests
// ---------------------------------------------------------------------------

/// <summary>
/// Integration tests for <c>POST /api/export-jobs</c> and
/// <c>GET /api/export-jobs</c>.
///
/// Validates Requirements 18.1, 18.2, 18.3, 18.4.
/// </summary>
[Collection("ExportJobs")]
public sealed class ExportJobsControllerTests : IAsyncLifetime
{
    // -----------------------------------------------------------------------
    // Constants — JWT signing
    // -----------------------------------------------------------------------

    /// <summary>
    /// H-6 (release 1.0.0): использует тот же секрет, что фабрика
    /// устанавливает в <c>Jwt:Secret</c> конфигурации. В Program.cs
    /// dev-секрет теперь генерируется случайно при каждом запуске, поэтому
    /// фабрика ДОЛЖНА переопределить его до старта API.
    /// </summary>
    private const string JwtSecret = ExportJobsWebApplicationFactory.JwtSecretForTests;
    private const string JwtIssuer = "SugarGuardAPI";
    private const string JwtAudience = "SugarGuardClients";

    // -----------------------------------------------------------------------
    // Test data
    // -----------------------------------------------------------------------

    private static readonly Guid TestUserId = Guid.NewGuid();
    private static readonly Guid TestChildId = Guid.NewGuid();

    // -----------------------------------------------------------------------
    // Fields
    // -----------------------------------------------------------------------

    private readonly ExportJobsWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ExportJobsControllerTests(ExportJobsWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // -----------------------------------------------------------------------
    // IAsyncLifetime — seed test data before each test class run
    // -----------------------------------------------------------------------

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Ensure the in-memory database is clean for this test run
        await db.Database.EnsureCreatedAsync();

        // Seed a parent user
        if (!await db.Users.AnyAsync(u => u.UserId == TestUserId))
        {
            db.Users.Add(new User
            {
                UserId = TestUserId,
                Role = UserRole.Parent,
                EmailForLogin = "testparent@example.com",
                CreatedAt = DateTime.UtcNow
            });
        }

        // Seed a child
        if (!await db.Children.AnyAsync(c => c.ChildId == TestChildId))
        {
            db.Children.Add(new Child
            {
                ChildId = TestChildId,
                FirstName = "Test",
                LastName = "Child",
                DateOfBirth = new DateOnly(2015, 1, 1),
                DiabetesType = "Type1",
                TimeZoneId = "UTC",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        // Link parent → child
        if (!await db.ParentChildLinks.AnyAsync(l => l.ParentUserId == TestUserId && l.ChildId == TestChildId))
        {
            db.ParentChildLinks.Add(new ParentChildLink
            {
                LinkId = Guid.NewGuid(),
                ParentUserId = TestUserId,
                ChildId = TestChildId,
                CreatedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Generates a signed JWT for the test user with the Parent role.
    /// Uses the same secret and issuer/audience as the API's Development fallback.
    /// </summary>
    private static string GenerateTestJwt(Guid userId, string role = "Parent")
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: JwtIssuer,
            audience: JwtAudience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Attaches a Bearer token to the shared <see cref="_client"/>.
    /// </summary>
    private void AuthorizeClient(string token)
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>
    /// Removes the Authorization header from the shared <see cref="_client"/>.
    /// </summary>
    private void DeauthorizeClient()
    {
        _client.DefaultRequestHeaders.Authorization = null;
    }

    // -----------------------------------------------------------------------
    // Req 18.1 — POST with valid params → success, job is created
    // -----------------------------------------------------------------------

    /// <summary>
    /// When a valid <see cref="CreateExportJobRequest"/> is POSTed by an
    /// authenticated user, the API MUST return a success response and the
    /// returned job MUST have been created.
    ///
    /// Validates: Requirement 18.1
    /// </summary>
    [Fact]
    public async Task Post_ValidRequest_ReturnsSuccessAndJobIsCreated()
    {
        // Feature: sugarguard-project-completion

        // ARRANGE
        var token = GenerateTestJwt(TestUserId);
        AuthorizeClient(token);

        var request = new CreateExportJobRequest
        {
            ChildId = TestChildId,
            PeriodFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            PeriodTo = new DateTime(2025, 6, 30, 23, 59, 59, DateTimeKind.Utc),
            Format = "csv"
        };

        // ACT
        var response = await _client.PostAsJsonAsync("/api/export-jobs", request);
        var rawBody = await response.Content.ReadAsStringAsync();

        // ASSERT — success status (200 OK or 201 Created)
        Assert.True(
            response.IsSuccessStatusCode,
            $"Expected success status code but got {(int)response.StatusCode} {response.StatusCode}. Body: {rawBody}");

        var body = JsonSerializer.Deserialize<ExportJobResponse>(
            rawBody,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(body);
        Assert.Equal(TestChildId, body.ChildId);
        Assert.NotEqual(Guid.Empty, body.ExportJobId);

        // After the async pipeline refactor, the controller returns immediately
        // after enqueueing the job — Status is "queued". A background processor
        // (Hangfire or inline fallback) updates it to "processing" → "completed"
        // asynchronously, so the client must poll GET /api/export-jobs/{id}/status
        // to see the terminal state. We only assert on the post-enqueue state here.
        Assert.Equal("queued", body.Status);
    }

    // -----------------------------------------------------------------------
    // Req 18.2 — POST without authentication → 401
    // -----------------------------------------------------------------------

    /// <summary>
    /// When <c>POST /api/export-jobs</c> is called without an Authorization
    /// header, the API MUST return HTTP 401 Unauthorized.
    ///
    /// Validates: Requirement 18.2
    /// </summary>
    [Fact]
    public async Task Post_WithoutAuthentication_Returns401()
    {
        // Feature: sugarguard-project-completion

        // ARRANGE — no auth header
        DeauthorizeClient();

        var request = new CreateExportJobRequest
        {
            ChildId = TestChildId,
            PeriodFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            PeriodTo = new DateTime(2025, 6, 30, 23, 59, 59, DateTimeKind.Utc),
            Format = "csv"
        };

        // ACT
        var response = await _client.PostAsJsonAsync("/api/export-jobs", request);

        // ASSERT
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // -----------------------------------------------------------------------
    // Req 18.3 — GET with authenticated user → 200, returns list
    // -----------------------------------------------------------------------

    /// <summary>
    /// When <c>GET /api/export-jobs</c> is called with a valid Bearer token,
    /// the API MUST return HTTP 200 with a JSON array of export jobs.
    ///
    /// Validates: Requirement 18.3
    /// </summary>
    [Fact]
    public async Task Get_WithAuthenticatedUser_Returns200AndList()
    {
        // Feature: sugarguard-project-completion

        // ARRANGE
        var token = GenerateTestJwt(TestUserId);
        AuthorizeClient(token);

        // ACT
        var response = await _client.GetAsync("/api/export-jobs");

        // ASSERT
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<List<ExportJobResponse>>();
        Assert.NotNull(body);
        // The list may be empty or contain previously created jobs — both are valid
        Assert.IsType<List<ExportJobResponse>>(body);
    }

    /// <summary>
    /// Validates that dashboard summary computes critical events from SQL-safe
    /// predicates and returns the expected count for a seeded child.
    /// </summary>
    [Fact]
    public async Task DashboardSummary_ComputesCriticalEventsSuccessfully()
    {
        var token = GenerateTestJwt(TestUserId);
        AuthorizeClient(token);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            db.Measurements.AddRange(
                new Measurement
                {
                    MeasurementId = Guid.NewGuid(),
                    ChildId = TestChildId,
                    GlucoseValue = 2.9m,
                    MeasurementTime = DateTime.UtcNow.AddMinutes(-30),
                    CreatedAt = DateTime.UtcNow.AddMinutes(-30)
                },
                new Measurement
                {
                    MeasurementId = Guid.NewGuid(),
                    ChildId = TestChildId,
                    GlucoseValue = 6.0m,
                    MeasurementTime = DateTime.UtcNow.AddMinutes(-20),
                    CreatedAt = DateTime.UtcNow.AddMinutes(-20)
                },
                new Measurement
                {
                    MeasurementId = Guid.NewGuid(),
                    ChildId = TestChildId,
                    GlucoseValue = 16.1m,
                    MeasurementTime = DateTime.UtcNow.AddMinutes(-10),
                    CreatedAt = DateTime.UtcNow.AddMinutes(-10)
                });

            await db.SaveChangesAsync();
        }

        var response = await _client.GetAsync($"/api/dashboard/{TestChildId}/summary");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var summary = await response.Content.ReadFromJsonAsync<DashboardSummaryResponse>();
        Assert.NotNull(summary);
        Assert.Equal(2, summary.CriticalEvents);
    }

    // -----------------------------------------------------------------------
    // Req 18.4 — POST with PeriodTo < PeriodFrom → 400 with error message
    // -----------------------------------------------------------------------

    /// <summary>
    /// When <c>POST /api/export-jobs</c> is called with an invalid date range
    /// (PeriodTo strictly less than PeriodFrom), the API MUST return HTTP 400
    /// with a non-empty error message.
    ///
    /// Validates: Requirement 18.4
    /// </summary>
    [Fact]
    public async Task Post_InvalidDateRange_Returns400WithErrorMessage()
    {
        // Feature: sugarguard-project-completion

        // ARRANGE
        var token = GenerateTestJwt(TestUserId);
        AuthorizeClient(token);

        var request = new CreateExportJobRequest
        {
            ChildId = TestChildId,
            PeriodFrom = new DateTime(2025, 6, 30, 0, 0, 0, DateTimeKind.Utc),  // later date
            PeriodTo = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),     // earlier date → invalid
            Format = "csv"
        };

        // ACT
        var response = await _client.PostAsJsonAsync("/api/export-jobs", request);

        // ASSERT — HTTP 400
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // ASSERT — response body contains a non-empty error message
        var rawBody = await response.Content.ReadAsStringAsync();
        Assert.False(
            string.IsNullOrWhiteSpace(rawBody),
            "The 400 response body must contain a descriptive error message.");

        // Verify the body is valid JSON with ProblemDetails-style error fields
        using var doc = JsonDocument.Parse(rawBody);
        Assert.True(
            doc.RootElement.TryGetProperty("detail", out var detailProp) ||
            doc.RootElement.TryGetProperty("message", out _) ||
            doc.RootElement.TryGetProperty("error", out _),
            "The 400 response body must contain a 'detail', 'error' or 'message' field.");

        if (doc.RootElement.TryGetProperty("detail", out var detail))
        {
            Assert.False(
                string.IsNullOrWhiteSpace(detail.GetString()),
                "The 'detail' field in the 400 response must not be empty.");
        }
        else if (doc.RootElement.TryGetProperty("message", out var msg))
        {
            Assert.False(
                string.IsNullOrWhiteSpace(msg.GetString()),
                "The 'message' field in the 400 response must not be empty.");
        }
    }
}

// ---------------------------------------------------------------------------
// Property-based tests (FsCheck) — separate class, own factory instance
// ---------------------------------------------------------------------------

/// <summary>
/// Property-based integration tests for <c>POST /api/export-jobs</c>.
///
/// Property 7: For any <see cref="CreateExportJobRequest"/> where
/// <c>PeriodTo</c> is strictly less than <c>PeriodFrom</c>,
/// <c>POST /api/export-jobs</c> SHALL return HTTP 400 with a non-empty
/// error message.
///
/// Validates: Requirement 18.4
/// </summary>
public sealed class ExportJobsControllerPropertyTests : IDisposable
{
    // Feature: sugarguard-project-completion, Property 7: ExportJobsController — invalid date range always returns HTTP 400

    // H-6 (release 1.0.0): dev-секрет теперь РАНДОМНЫЙ. Используем
    // тот же секрет, что фабрика устанавливает в Jwt:Secret.
    private const string JwtSecret = ExportJobsWebApplicationFactory.JwtSecretForTests;
    private const string JwtIssuer = "SugarGuardAPI";
    private const string JwtAudience = "SugarGuardClients";

    private static readonly Guid PropertyTestUserId = Guid.NewGuid();
    private static readonly Guid PropertyTestChildId = Guid.NewGuid();

    private readonly ExportJobsWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ExportJobsControllerPropertyTests()
    {
        _factory = new ExportJobsWebApplicationFactory();
        _client = _factory.CreateClient();

        // Seed test data synchronously via Task.Run to satisfy constructor constraint
        Task.Run(SeedAsync).GetAwaiter().GetResult();

        // Attach auth header for all property test requests
        var token = GenerateTestJwt(PropertyTestUserId);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        if (!await db.Users.AnyAsync(u => u.UserId == PropertyTestUserId))
        {
            db.Users.Add(new User
            {
                UserId = PropertyTestUserId,
                Role = UserRole.Parent,
                EmailForLogin = "proptest@example.com",
                CreatedAt = DateTime.UtcNow
            });
        }

        if (!await db.Children.AnyAsync(c => c.ChildId == PropertyTestChildId))
        {
            db.Children.Add(new Child
            {
                ChildId = PropertyTestChildId,
                FirstName = "Prop",
                LastName = "Test",
                DateOfBirth = new DateOnly(2015, 1, 1),
                DiabetesType = "Type1",
                TimeZoneId = "UTC",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        if (!await db.ParentChildLinks.AnyAsync(l => l.ParentUserId == PropertyTestUserId && l.ChildId == PropertyTestChildId))
        {
            db.ParentChildLinks.Add(new ParentChildLink
            {
                LinkId = Guid.NewGuid(),
                ParentUserId = PropertyTestUserId,
                ChildId = PropertyTestChildId,
                CreatedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();
    }

    private static string GenerateTestJwt(Guid userId, string role = "Parent")
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: JwtIssuer,
            audience: JwtAudience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Property 7: For ANY pair of dates where PeriodTo &lt; PeriodFrom,
    /// POST /api/export-jobs MUST return HTTP 400 with a non-empty error message.
    ///
    /// Validates: Requirement 18.4
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(InvalidDateRangeArbitrary) })]
    public Property InvalidDateRange_AlwaysReturns400((DateTime PeriodFrom, DateTime PeriodTo) range)
    {
        // Feature: sugarguard-project-completion, Property 7: ExportJobsController — invalid date range always returns HTTP 400

        var (periodFrom, periodTo) = range;

        // Precondition: PeriodTo must be strictly less than PeriodFrom
        return Prop.When(
            periodTo < periodFrom,
            () =>
            {
                var request = new CreateExportJobRequest
                {
                    ChildId = PropertyTestChildId,
                    PeriodFrom = periodFrom,
                    PeriodTo = periodTo,
                    Format = "csv"
                };

                var response = _client.PostAsJsonAsync("/api/export-jobs", request)
                    .GetAwaiter().GetResult();

                var rawBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                var is400 = response.StatusCode == HttpStatusCode.BadRequest;
                var hasBody = !string.IsNullOrWhiteSpace(rawBody);

                return (is400 && hasBody).Label(
                    $"Expected 400 with body. Got {(int)response.StatusCode}, body='{rawBody}'");
            });
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}

// ---------------------------------------------------------------------------
// FsCheck Arbitrary — generates (PeriodFrom, PeriodTo) pairs where PeriodTo < PeriodFrom
// ---------------------------------------------------------------------------

/// <summary>
/// Generates date pairs where <c>PeriodTo</c> is strictly less than
/// <c>PeriodFrom</c>, ensuring the property test exercises the invalid-range
/// validation path.
/// </summary>
public static class InvalidDateRangeArbitrary
{
    public static Arbitrary<(DateTime PeriodFrom, DateTime PeriodTo)> Generate()
    {
        // Base date range: 2020-01-01 to 2030-12-31 (in ticks)
        var minTicks = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
        var maxTicks = new DateTime(2030, 12, 31, 0, 0, 0, DateTimeKind.Utc).Ticks;
        var rangeTicks = maxTicks - minTicks;

        var gen = from offsetA in Gen.Choose(1, (int)(rangeTicks / TimeSpan.TicksPerDay))
                  from offsetB in Gen.Choose(1, (int)(rangeTicks / TimeSpan.TicksPerDay))
                  let dateA = new DateTime(minTicks + (long)offsetA * TimeSpan.TicksPerDay, DateTimeKind.Utc)
                  let dateB = new DateTime(minTicks + (long)offsetB * TimeSpan.TicksPerDay, DateTimeKind.Utc)
                  // Ensure PeriodTo < PeriodFrom by ordering: larger = PeriodFrom, smaller = PeriodTo
                  let periodFrom = dateA > dateB ? dateA : dateB
                  let periodTo = dateA > dateB ? dateB : dateA
                  // Skip equal dates (need strictly less than)
                  where periodTo < periodFrom
                  select (periodFrom, periodTo);

        return Arb.From(gen);
    }
}
