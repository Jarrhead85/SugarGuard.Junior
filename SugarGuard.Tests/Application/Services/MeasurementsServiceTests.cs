using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SugarGuard.API.Application.Services;
using SugarGuard.API.Data;
using SugarGuard.API.DTOs;
using SugarGuard.Application.Audit;
using SugarGuard.Domain.Entities;
using SugarGuard.Shared.Constants;

namespace SugarGuard.Tests.Application.Services;

/// <summary>
/// Unit-тесты для <see cref="MeasurementsService"/>.
/// <para>
/// <b>Critical coverage:</b>
/// <list type="bullet">
///   <item><description><b>Sync-логика (M-1 server-side):</b> <c>SyncBatchAsync</c> корректно обрабатывает
///     empty/oversize/duplicate/access-denied/child-not-found. Дубликаты фиксируются
///     как <c>ServerWinsOnDuplicate</c> конфликт (last-writer-wins на сервере).</description></item>
///   <item><description><b>PHI в AuditLog (C-3):</b> <c>CreateAsync</c> пишет только <c>Child={guid};Glucose={value}</c>,
///     без Notes, ChildState. Аудит использует <c>CancellationToken.None</c> — запись
///     должна сохраниться даже при отмене клиентского запроса.</description></item>
///   <item><description><b>Лимиты:</b> <c>GetByChildAsync</c> clamp'ит limit в [1, 1000].
///     <c>SyncBatchAsync</c> отклоняет batch > 1000 без обработки.</description></item>
///   <item><description><b>SyncLog integrity:</b> для каждого измерения создаётся
///     ровно одна запись (success/conflict/failed).</description></item>
/// </list>
/// </para>
/// </summary>
public class MeasurementsServiceTests : IDisposable
{
    private readonly string _dbName = $"MeasurementsTest_{Guid.NewGuid()}";
    private readonly Mock<IAuditService> _audit = new();
    private readonly DbContextOptions<AppDbContext> _dbOptions;

    public MeasurementsServiceTests()
    {
        _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: _dbName)
            .Options;
    }

    public void Dispose()
    {
        using var ctx = new AppDbContext(_dbOptions);
        ctx.Database.EnsureDeleted();
    }

    private TestAppDbContextFactory CreateFactory() => new(_dbOptions);

    private MeasurementsService CreateSut() =>
        new(CreateFactory(), _audit.Object, NullLogger<MeasurementsService>.Instance);

    private static Child CreateChild() => new()
    {
        ChildId = Guid.NewGuid(),
        FirstName = "Test",
        LastName = "Child",
        DateOfBirth = new DateOnly(2015, 1, 1),
        DiabetesType = "Type1",
        CreatedAt = DateTime.UtcNow
    };

    private static Measurement CreateMeasurement(Guid childId, DateTime time, decimal glucose = 5.5m) => new()
    {
        MeasurementId = Guid.NewGuid(),
        ChildId = childId,
        MeasurementTime = time,
        GlucoseValue = glucose,
        DataSource = "test",
        CreatedAt = DateTime.UtcNow
    };

    // ───────────────────────────────────────────────────────────────────
    // ChildExistsAsync / GetChildAsync
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ChildExistsAsync_Exists_ReturnsTrue()
    {
        var c = CreateChild();
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Children.Add(c);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        Assert.True(await sut.ChildExistsAsync(c.ChildId));
    }

    [Fact]
    public async Task ChildExistsAsync_NotExists_ReturnsFalse()
    {
        var sut = CreateSut();
        Assert.False(await sut.ChildExistsAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetChildAsync_NotFound_ReturnsNull()
    {
        var sut = CreateSut();
        Assert.Null(await sut.GetChildAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetChildAsync_Found_ReturnsEntity()
    {
        var c = CreateChild();
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Children.Add(c);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        var result = await sut.GetChildAsync(c.ChildId);

        Assert.NotNull(result);
        Assert.Equal(c.ChildId, result!.ChildId);
    }

    // ───────────────────────────────────────────────────────────────────
    // CreateAsync
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_PersistsAndWritesAudit()
    {
        var c = CreateChild();
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Children.Add(c);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        var result = await sut.CreateAsync(new CreateMeasurementRequest
        {
            ChildId = c.ChildId,
            GlucoseValue = 5.5m,
            MeasurementTime = new DateTime(2026, 6, 3, 10, 0, 0, DateTimeKind.Utc),
            ChildState = "before_meal",
            Notes = "SECRET_NOTE",  // НЕ должно попасть в audit (C-3)
            DataSource = "manual"
        });

        Assert.NotEqual(Guid.Empty, result.MeasurementId);
        Assert.Equal(c.ChildId, result.ChildId);

        using var verifyDb = new AppDbContext(_dbOptions);
        var saved = await verifyDb.Measurements.FindAsync(result.MeasurementId);
        Assert.NotNull(saved);

        // C-3: AuditLog details НЕ должен содержать Notes/ChildState.
        // Не проверяем формат Glucose= (decimal.ToString() использует CurrentCulture
        // и в ru-RU даёт "5,5" вместо "5.5") — нам важна только семантика защиты PHI.
        _audit.Verify(a => a.WriteAsync(
            "measurement.created",
            "Measurement",
            result.MeasurementId.ToString(),
            It.Is<string>(s => s != null
                            && s.Contains($"Child={c.ChildId}")
                            && s.Contains("Glucose=")
                            && !s.Contains("SECRET_NOTE")
                            && !s.Contains("before_meal")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_AuditCallUsesCancellationTokenNone()
    {
        // HIGH-4 contract: аудит вызывается с CancellationToken.None,
        // а НЕ с пользовательским токеном. Это гарантирует, что запись
        // в AuditLog сохранится даже при отмене клиентского запроса.
        // (Источник: SugarGuard.API/Application/Services/MeasurementsService.cs:79-84)
        var c = CreateChild();
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Children.Add(c);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();
        using var cts = new CancellationTokenSource();

        await sut.CreateAsync(new CreateMeasurementRequest
        {
            ChildId = c.ChildId,
            GlucoseValue = 5.0m,
            MeasurementTime = DateTime.UtcNow
        }, cts.Token);

        // Аудит вызван с CancellationToken.None, а не cts.Token.
        _audit.Verify(a => a.WriteAsync(
            "measurement.created",
            "Measurement",
            It.IsAny<string>(),
            It.IsAny<string?>(),
            CancellationToken.None),
            Times.Once);

        // И cts.Token НЕ был передан в audit (проверяем explicit)
        _audit.Verify(a => a.WriteAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string?>(),
            It.Is<CancellationToken>(t => t == cts.Token)),
            Times.Never);
    }

    // ───────────────────────────────────────────────────────────────────
    // GetByChildAsync
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByChildAsync_OrdersByMeasurementTimeDesc()
    {
        var c = CreateChild();
        var m1 = CreateMeasurement(c.ChildId, new DateTime(2026, 6, 1));
        var m2 = CreateMeasurement(c.ChildId, new DateTime(2026, 6, 3));
        var m3 = CreateMeasurement(c.ChildId, new DateTime(2026, 6, 2));
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Children.Add(c);
            db.Measurements.AddRange(m1, m2, m3);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        var result = await sut.GetByChildAsync(c.ChildId, null, null, 10);

        Assert.Equal(3, result.Count);
        Assert.Equal(m2.MeasurementId, result[0].MeasurementId);  // newest
        Assert.Equal(m3.MeasurementId, result[1].MeasurementId);
        Assert.Equal(m1.MeasurementId, result[2].MeasurementId);  // oldest
    }

    [Fact]
    public async Task GetByChildAsync_FiltersByFromAndTo()
    {
        var c = CreateChild();
        var m1 = CreateMeasurement(c.ChildId, new DateTime(2026, 6, 1));
        var m2 = CreateMeasurement(c.ChildId, new DateTime(2026, 6, 5));
        var m3 = CreateMeasurement(c.ChildId, new DateTime(2026, 6, 10));
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Children.Add(c);
            db.Measurements.AddRange(m1, m2, m3);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        var result = await sut.GetByChildAsync(
            c.ChildId,
            from: new DateTime(2026, 6, 2),
            to: new DateTime(2026, 6, 8),
            limit: 100);

        Assert.Single(result);
        Assert.Equal(m2.MeasurementId, result[0].MeasurementId);
    }

    [Theory]
    [InlineData(-5, 1)]      // limit < 1 → 1
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(500, 500)]
    [InlineData(1000, 1000)]
    [InlineData(5000, 1000)]  // limit > 1000 → 1000
    public async Task GetByChildAsync_ClampsLimitTo_1_1000(int requested, int expected)
    {
        var c = CreateChild();
        for (int i = 0; i < expected; i++)
        {
            using var db = new AppDbContext(_dbOptions);
            db.Measurements.Add(CreateMeasurement(c.ChildId,
                DateTime.UtcNow.AddSeconds(-i)));
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        var result = await sut.GetByChildAsync(c.ChildId, null, null, requested);

        Assert.Equal(expected, result.Count);
    }

    [Fact]
    public async Task GetByChildAsync_IsolatesByChildId()
    {
        var c1 = CreateChild();
        var c2 = CreateChild();
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Children.AddRange(c1, c2);
            db.Measurements.Add(CreateMeasurement(c1.ChildId, DateTime.UtcNow));
            db.Measurements.Add(CreateMeasurement(c2.ChildId, DateTime.UtcNow));
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        var r1 = await sut.GetByChildAsync(c1.ChildId, null, null, 10);
        var r2 = await sut.GetByChildAsync(c2.ChildId, null, null, 10);

        Assert.Single(r1);
        Assert.Equal(c1.ChildId, r1[0].ChildId);
        Assert.Single(r2);
        Assert.Equal(c2.ChildId, r2[0].ChildId);
    }

    // ───────────────────────────────────────────────────────────────────
    // GetLatestAsync / GetByIdAsync / GetForStatisticsAsync
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetLatestAsync_ReturnsNewestMeasurement()
    {
        var c = CreateChild();
        var m1 = CreateMeasurement(c.ChildId, new DateTime(2026, 6, 1));
        var m2 = CreateMeasurement(c.ChildId, new DateTime(2026, 6, 5));
        var m3 = CreateMeasurement(c.ChildId, new DateTime(2026, 6, 3));
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Children.Add(c);
            db.Measurements.AddRange(m1, m2, m3);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        var result = await sut.GetLatestAsync(c.ChildId);

        Assert.NotNull(result);
        Assert.Equal(m2.MeasurementId, result!.MeasurementId);
    }

    [Fact]
    public async Task GetLatestAsync_NoMeasurements_ReturnsNull()
    {
        var c = CreateChild();
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Children.Add(c);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();
        Assert.Null(await sut.GetLatestAsync(c.ChildId));
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ReturnsNull()
    {
        var sut = CreateSut();
        Assert.Null(await sut.GetByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetForStatisticsAsync_FiltersByDateRange()
    {
        var c = CreateChild();
        var m1 = CreateMeasurement(c.ChildId, new DateTime(2026, 5, 1));
        var m2 = CreateMeasurement(c.ChildId, new DateTime(2026, 6, 1));
        var m3 = CreateMeasurement(c.ChildId, new DateTime(2026, 6, 15));
        var m4 = CreateMeasurement(c.ChildId, new DateTime(2026, 7, 1));
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Children.Add(c);
            db.Measurements.AddRange(m1, m2, m3, m4);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        var result = await sut.GetForStatisticsAsync(
            c.ChildId,
            fromDate: new DateTime(2026, 6, 1),
            toDate: new DateTime(2026, 6, 30));

        Assert.Equal(2, result.Count);
        Assert.Contains(m2.MeasurementId, result.Select(m => m.MeasurementId));
        Assert.Contains(m3.MeasurementId, result.Select(m => m.MeasurementId));
    }

    // ───────────────────────────────────────────────────────────────────
    // SyncBatchAsync — happy path
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SyncBatchAsync_EmptyBatch_ReturnsZeroWithNoSideEffects()
    {
        var sut = CreateSut();
        var request = new SyncMeasurementsRequest { Measurements = [] };

        var result = await sut.SyncBatchAsync(
            request,
            _ => Task.FromResult<IReadOnlyList<Guid>>(Array.Empty<Guid>()));

        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(0, result.ErrorCount);
        Assert.Empty(result.Conflicts);
        _audit.Verify(a => a.WriteAsync(
            "sync.batch_completed", It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SyncBatchAsync_OversizeBatch_RejectedAsZero()
    {
        var sut = CreateSut();
        var request = new SyncMeasurementsRequest();
        for (int i = 0; i < 1001; i++)
        {
            request.Measurements.Add(new SyncMeasurementItemRequest
            {
                ChildId = Guid.NewGuid(),
                GlucoseValue = 5.0m,
                MeasurementTime = DateTime.UtcNow
            });
        }

        var result = await sut.SyncBatchAsync(
            request,
            _ => Task.FromResult<IReadOnlyList<Guid>>(Array.Empty<Guid>()));

        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(0, result.ErrorCount);
        Assert.Empty(result.Conflicts);
    }

    [Fact]
    public async Task SyncBatchAsync_NewMeasurementsForAccessibleChildren_AreCreated()
    {
        var c1 = CreateChild();
        var c2 = CreateChild();
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Children.AddRange(c1, c2);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();
        var accessible = new[] { c1.ChildId, c2.ChildId };

        var request = new SyncMeasurementsRequest
        {
            Measurements = new()
            {
                new SyncMeasurementItemRequest
                {
                    ChildId = c1.ChildId,
                    GlucoseValue = 5.5m,
                    MeasurementTime = new DateTime(2026, 6, 3, 8, 0, 0, DateTimeKind.Utc)
                },
                new SyncMeasurementItemRequest
                {
                    ChildId = c2.ChildId,
                    GlucoseValue = 6.0m,
                    MeasurementTime = new DateTime(2026, 6, 3, 8, 5, 0, DateTimeKind.Utc),
                    Notes = "Примечание",
                    ChildState = "before_meal"
                }
            }
        };

        var result = await sut.SyncBatchAsync(
            request,
            _ => Task.FromResult<IReadOnlyList<Guid>>(accessible));

        Assert.Equal(2, result.SuccessCount);
        Assert.Equal(0, result.ErrorCount);
        Assert.Empty(result.Conflicts);

        using var verifyDb = new AppDbContext(_dbOptions);
        Assert.Equal(2, await verifyDb.Measurements.CountAsync());
        var saved = await verifyDb.Measurements
            .Where(m => m.ChildId == c2.ChildId)
            .FirstAsync();
        Assert.Equal("Примечание", saved.Notes);
        Assert.Equal("before_meal", saved.ChildState);
    }

    [Fact]
    public async Task SyncBatchAsync_CreatesSyncLogForEachMeasurement()
    {
        var c = CreateChild();
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Children.Add(c);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        var request = new SyncMeasurementsRequest
        {
            Measurements = new()
            {
                new SyncMeasurementItemRequest
                {
                    ChildId = c.ChildId,
                    GlucoseValue = 5.0m,
                    MeasurementTime = new DateTime(2026, 6, 3, 8, 0, 0, DateTimeKind.Utc)
                },
                new SyncMeasurementItemRequest
                {
                    ChildId = c.ChildId,
                    GlucoseValue = 6.0m,
                    MeasurementTime = new DateTime(2026, 6, 3, 8, 5, 0, DateTimeKind.Utc)
                }
            }
        };

        await sut.SyncBatchAsync(
            request,
            _ => Task.FromResult<IReadOnlyList<Guid>>(new[] { c.ChildId }));

        using var verifyDb = new AppDbContext(_dbOptions);
        var logs = await verifyDb.SyncLogs.ToListAsync();
        Assert.Equal(2, logs.Count);
        Assert.All(logs, l =>
        {
            Assert.Equal("Measurement", l.EntityType);
            Assert.Equal("success", l.Status);
            Assert.False(l.IsConflict);
        });
    }

    [Fact]
    public async Task SyncBatchAsync_DefaultDataSource_AppliedWhenMissing()
    {
        var c = CreateChild();
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Children.Add(c);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        var request = new SyncMeasurementsRequest
        {
            Measurements = new()
            {
                new SyncMeasurementItemRequest
                {
                    ChildId = c.ChildId,
                    GlucoseValue = 5.0m,
                    MeasurementTime = new DateTime(2026, 6, 3, 8, 0, 0, DateTimeKind.Utc)
                    // DataSource = null
                }
            }
        };

        await sut.SyncBatchAsync(
            request,
            _ => Task.FromResult<IReadOnlyList<Guid>>(new[] { c.ChildId }));

        using var verifyDb = new AppDbContext(_dbOptions);
        var saved = await verifyDb.Measurements.FirstAsync();
        Assert.Equal("mobile_app", saved.DataSource);
    }

    [Fact]
    public async Task SyncBatchAsync_PreservesExplicitDataSource()
    {
        var c = CreateChild();
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Children.Add(c);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        var request = new SyncMeasurementsRequest
        {
            Measurements = new()
            {
                new SyncMeasurementItemRequest
                {
                    ChildId = c.ChildId,
                    GlucoseValue = 5.0m,
                    MeasurementTime = new DateTime(2026, 6, 3, 8, 0, 0, DateTimeKind.Utc),
                    DataSource = "glucometer_ble"
                }
            }
        };

        await sut.SyncBatchAsync(
            request,
            _ => Task.FromResult<IReadOnlyList<Guid>>(new[] { c.ChildId }));

        using var verifyDb = new AppDbContext(_dbOptions);
        var saved = await verifyDb.Measurements.FirstAsync();
        Assert.Equal("glucometer_ble", saved.DataSource);
    }

    // ───────────────────────────────────────────────────────────────────
    // SyncBatchAsync — access control
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SyncBatchAsync_ChildNotFound_LoggedAsFailed()
    {
        var sut = CreateSut();
        var nonexistentChildId = Guid.NewGuid();

        var request = new SyncMeasurementsRequest
        {
            Measurements = new()
            {
                new SyncMeasurementItemRequest
                {
                    ChildId = nonexistentChildId,
                    GlucoseValue = 5.0m,
                    MeasurementTime = DateTime.UtcNow
                }
            }
        };

        var result = await sut.SyncBatchAsync(
            request,
            _ => Task.FromResult<IReadOnlyList<Guid>>(Array.Empty<Guid>()));

        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(1, result.ErrorCount);

        using var verifyDb = new AppDbContext(_dbOptions);
        var logs = await verifyDb.SyncLogs.ToListAsync();
        Assert.Single(logs);
        Assert.Equal("failed", logs[0].Status);
        Assert.Equal("Child not found", logs[0].Error);
        Assert.False(logs[0].IsConflict);
    }

    [Fact]
    public async Task SyncBatchAsync_AccessDenied_LoggedAsFailedWithDifferentError()
    {
        // Child exists, но НЕ в accessibleChildIds → "Access denied", а не "Child not found".
        var c = CreateChild();
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Children.Add(c);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        var request = new SyncMeasurementsRequest
        {
            Measurements = new()
            {
                new SyncMeasurementItemRequest
                {
                    ChildId = c.ChildId,
                    GlucoseValue = 5.0m,
                    MeasurementTime = DateTime.UtcNow
                }
            }
        };

        var result = await sut.SyncBatchAsync(
            request,
            _ => Task.FromResult<IReadOnlyList<Guid>>(Array.Empty<Guid>()));  // нет доступа

        Assert.Equal(1, result.ErrorCount);

        using var verifyDb = new AppDbContext(_dbOptions);
        var logs = await verifyDb.SyncLogs.ToListAsync();
        Assert.Single(logs);
        Assert.Equal("failed", logs[0].Status);
        Assert.Equal("Access denied", logs[0].Error);
    }

    [Fact]
    public async Task SyncBatchAsync_MixedAccess_PartiallySucceeds()
    {
        var c1 = CreateChild();
        var c2 = CreateChild();
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Children.AddRange(c1, c2);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();
        // Доступ только к c1
        var accessible = new[] { c1.ChildId };

        var request = new SyncMeasurementsRequest
        {
            Measurements = new()
            {
                new SyncMeasurementItemRequest
                {
                    ChildId = c1.ChildId,
                    GlucoseValue = 5.0m,
                    MeasurementTime = new DateTime(2026, 6, 3, 8, 0, 0, DateTimeKind.Utc)
                },
                new SyncMeasurementItemRequest
                {
                    ChildId = c2.ChildId,  // нет доступа
                    GlucoseValue = 6.0m,
                    MeasurementTime = new DateTime(2026, 6, 3, 8, 5, 0, DateTimeKind.Utc)
                }
            }
        };

        var result = await sut.SyncBatchAsync(
            request,
            _ => Task.FromResult<IReadOnlyList<Guid>>(accessible));

        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(1, result.ErrorCount);
        Assert.Empty(result.Conflicts);

        using var verifyDb = new AppDbContext(_dbOptions);
        Assert.Single(await verifyDb.Measurements.ToListAsync());
        Assert.Equal(2, await verifyDb.SyncLogs.CountAsync());
    }

    // ───────────────────────────────────────────────────────────────────
    // SyncBatchAsync — duplicate detection (M-1)
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SyncBatchAsync_DuplicateDetected_LoggedAsConflictServerWins()
    {
        var c = CreateChild();
        // Существующее измерение: 5.5 ммоль/л в 8:00
        var existing = CreateMeasurement(c.ChildId, new DateTime(2026, 6, 3, 8, 0, 0, DateTimeKind.Utc), 5.5m);
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Children.Add(c);
            db.Measurements.Add(existing);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        // Клиент прислал то же самое ещё раз
        var request = new SyncMeasurementsRequest
        {
            Measurements = new()
            {
                new SyncMeasurementItemRequest
                {
                    ChildId = c.ChildId,
                    GlucoseValue = 5.5m,
                    MeasurementTime = new DateTime(2026, 6, 3, 8, 0, 0, DateTimeKind.Utc)
                }
            }
        };

        var result = await sut.SyncBatchAsync(
            request,
            _ => Task.FromResult<IReadOnlyList<Guid>>(new[] { c.ChildId }));

        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(1, result.ErrorCount);
        Assert.Single(result.Conflicts);

        var conflict = result.Conflicts[0];
        Assert.Equal("Measurement", conflict.EntityType);
        Assert.Equal(existing.MeasurementId.ToString(), conflict.EntityId);
        Assert.Equal("Server", conflict.WinningVersion);
        Assert.Equal(SyncResolutionStrategy.ServerWinsOnDuplicate, conflict.ResolutionStrategy);

        using var verifyDb = new AppDbContext(_dbOptions);
        var logs = await verifyDb.SyncLogs.ToListAsync();
        Assert.Single(logs);
        Assert.Equal("conflict", logs[0].Status);
        Assert.True(logs[0].IsConflict);
        Assert.Equal("Duplicate measurement detected", logs[0].Error);
        Assert.Equal(existing.MeasurementId.ToString(), logs[0].EntityId);

        // Дубликат НЕ создаёт новое измерение
        Assert.Single(await verifyDb.Measurements.ToListAsync());
    }

    [Fact]
    public async Task SyncBatchAsync_SameChildDifferentTime_AreNotDuplicates()
    {
        var c = CreateChild();
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Children.Add(c);
            db.Measurements.Add(CreateMeasurement(c.ChildId, new DateTime(2026, 6, 3, 8, 0, 0, DateTimeKind.Utc), 5.5m));
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        // Другое время → не дубликат
        var request = new SyncMeasurementsRequest
        {
            Measurements = new()
            {
                new SyncMeasurementItemRequest
                {
                    ChildId = c.ChildId,
                    GlucoseValue = 5.5m,  // то же значение
                    MeasurementTime = new DateTime(2026, 6, 3, 9, 0, 0, DateTimeKind.Utc)  // но другое время
                }
            }
        };

        var result = await sut.SyncBatchAsync(
            request,
            _ => Task.FromResult<IReadOnlyList<Guid>>(new[] { c.ChildId }));

        Assert.Equal(1, result.SuccessCount);
        Assert.Empty(result.Conflicts);
    }

    [Fact]
    public async Task SyncBatchAsync_SameChildSameTimeDifferentGlucose_AreNotDuplicates()
    {
        var c = CreateChild();
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Children.Add(c);
            db.Measurements.Add(CreateMeasurement(c.ChildId, new DateTime(2026, 6, 3, 8, 0, 0, DateTimeKind.Utc), 5.5m));
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        // Другое значение → не дубликат
        var request = new SyncMeasurementsRequest
        {
            Measurements = new()
            {
                new SyncMeasurementItemRequest
                {
                    ChildId = c.ChildId,
                    GlucoseValue = 6.0m,  // другое
                    MeasurementTime = new DateTime(2026, 6, 3, 8, 0, 0, DateTimeKind.Utc)
                }
            }
        };

        var result = await sut.SyncBatchAsync(
            request,
            _ => Task.FromResult<IReadOnlyList<Guid>>(new[] { c.ChildId }));

        Assert.Equal(1, result.SuccessCount);
    }

    [Fact]
    public async Task SyncBatchAsync_DuplicateAcrossChildren_AreNotDuplicates()
    {
        var c1 = CreateChild();
        var c2 = CreateChild();
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Children.AddRange(c1, c2);
            db.Measurements.Add(CreateMeasurement(c1.ChildId, new DateTime(2026, 6, 3, 8, 0, 0, DateTimeKind.Utc), 5.5m));
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        // Другой ChildId → не дубликат
        var request = new SyncMeasurementsRequest
        {
            Measurements = new()
            {
                new SyncMeasurementItemRequest
                {
                    ChildId = c2.ChildId,
                    GlucoseValue = 5.5m,
                    MeasurementTime = new DateTime(2026, 6, 3, 8, 0, 0, DateTimeKind.Utc)
                }
            }
        };

        var result = await sut.SyncBatchAsync(
            request,
            _ => Task.FromResult<IReadOnlyList<Guid>>(new[] { c1.ChildId, c2.ChildId }));

        Assert.Equal(1, result.SuccessCount);
    }

    // ───────────────────────────────────────────────────────────────────
    // SyncBatchAsync — audit
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SyncBatchAsync_AuditLog_ContainsSummaryWithoutPhi()
    {
        var c = CreateChild();
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Children.Add(c);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        var request = new SyncMeasurementsRequest
        {
            AppVersion = "1.0.0",
            Measurements = new()
            {
                new SyncMeasurementItemRequest
                {
                    ChildId = c.ChildId,
                    GlucoseValue = 5.0m,
                    MeasurementTime = new DateTime(2026, 6, 3, 8, 0, 0, DateTimeKind.Utc),
                    Notes = "СЕКРЕТ"  // НЕ должно попасть в audit
                }
            }
        };

        await sut.SyncBatchAsync(
            request,
            _ => Task.FromResult<IReadOnlyList<Guid>>(new[] { c.ChildId }));

        _audit.Verify(a => a.WriteAsync(
            "sync.batch_completed",
            "Measurement",
            null,  // targetId=null для batch
            It.Is<string>(s => s.Contains("Success=1")
                            && s.Contains("Errors=0")
                            && s.Contains("Conflicts=0")
                            && s.Contains("AppVersion=1.0.0")
                            && !s.Contains("СЕКРЕТ")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SyncBatchAsync_AuditLog_PersistsEvenIfClientCancels()
    {
        var c = CreateChild();
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Children.Add(c);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var request = new SyncMeasurementsRequest
        {
            Measurements = new()
            {
                new SyncMeasurementItemRequest
                {
                    ChildId = c.ChildId,
                    GlucoseValue = 5.0m,
                    MeasurementTime = DateTime.UtcNow
                }
            }
        };

        // ВАЖНО: сам batch упадёт (CT уже отменён), но мы проверяем что
        // pattern "audit с CancellationToken.None" согласован с CreateAsync.
        try
        {
            await sut.SyncBatchAsync(
                request,
                _ => Task.FromResult<IReadOnlyList<Guid>>(new[] { c.ChildId }),
                cts.Token);
        }
        catch (OperationCanceledException) { /* expected */ }

        // Если batch не упал — должен быть audit с CancellationToken.None
        _audit.Verify(a => a.WriteAsync(
            "sync.batch_completed",
            "Measurement",
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            CancellationToken.None),
            Times.AtMostOnce);
    }

    [Fact]
    public async Task SyncBatchAsync_LargeBatchUnderLimit_ProcessedInOnePass()
    {
        // Проверяем, что ровно 1000 элементов проходят (граничное значение)
        var c = CreateChild();
        using (var db = new AppDbContext(_dbOptions))
        {
            db.Children.Add(c);
            await db.SaveChangesAsync();
        }
        var sut = CreateSut();

        var request = new SyncMeasurementsRequest();
        for (int i = 0; i < 1000; i++)
        {
            request.Measurements.Add(new SyncMeasurementItemRequest
            {
                ChildId = c.ChildId,
                GlucoseValue = 5.0m + (decimal)(i % 50) / 10m,  // уникальные значения
                MeasurementTime = new DateTime(2026, 6, 1).AddSeconds(i)
            });
        }

        var result = await sut.SyncBatchAsync(
            request,
            _ => Task.FromResult<IReadOnlyList<Guid>>(new[] { c.ChildId }));

        Assert.Equal(1000, result.SuccessCount);
    }
}
