using Microsoft.EntityFrameworkCore;
using SugarGuard.Infrastructure.Sync;

namespace SugarGuard.Tests.Sync;

/// <summary>
/// Example tests for <see cref="SyncConflictResolver"/>.
///
/// Uses the EF Core InMemory provider so no real database is required.
/// Validates Requirements 17.1, 17.2, 17.3.
/// </summary>
public class SyncConflictResolverTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Creates a fresh in-memory <see cref="SyncDbContext"/> for each test,
    /// using a unique database name to guarantee test isolation.
    /// </summary>
    private static SyncDbContext CreateContext(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<SyncDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;

        return new SyncDbContext(options);
    }

    // -----------------------------------------------------------------------
    // Req 17.1 / 17.2 — Same-timestamp conflict
    // -----------------------------------------------------------------------

    /// <summary>
    /// When two measurements with the same ChildId and MeasuredAt are submitted,
    /// the resolver MUST:
    ///   - retain exactly one measurement in the database, and
    ///   - mark the SyncLog entry for the duplicate with IsConflict = true.
    ///
    /// Validates: Requirements 17.1, 17.2
    /// </summary>
    [Fact]
    public async Task Submit_SameChildIdAndMeasuredAt_RetainsOneMeasurementAndSetsIsConflict()
    {
        // ARRANGE
        await using var db = CreateContext();
        var resolver = new SyncConflictResolver(db);

        var childId = Guid.NewGuid();
        var measuredAt = new DateTime(2025, 6, 1, 10, 0, 0, DateTimeKind.Utc);

        var first = new SyncMeasurement
        {
            ChildId = childId,
            GlucoseValue = 5.5m,
            MeasuredAt = measuredAt
        };

        var duplicate = new SyncMeasurement
        {
            ChildId = childId,
            GlucoseValue = 5.5m,   // same value — concurrent duplicate
            MeasuredAt = measuredAt // same timestamp → conflict
        };

        // ACT
        var firstLog = await resolver.SubmitAsync(first);
        var conflictLog = await resolver.SubmitAsync(duplicate);

        // ASSERT — exactly one measurement retained
        var measurementCount = await db.Measurements.CountAsync();
        Assert.Equal(1, measurementCount);

        // ASSERT — first submission was accepted without conflict
        Assert.False(firstLog.IsConflict,
            "The first submission should not be flagged as a conflict.");

        // ASSERT — second (duplicate) submission is flagged as a conflict
        Assert.True(conflictLog.IsConflict,
            "The duplicate submission must be marked IsConflict = true.");
    }

    /// <summary>
    /// Verifies that the SyncLog entry created for the conflicting submission
    /// is persisted in the database with IsConflict = true.
    ///
    /// Validates: Requirements 17.1, 17.2
    /// </summary>
    [Fact]
    public async Task Submit_SameTimestampConflict_PersistsSyncLogWithIsConflictTrue()
    {
        // ARRANGE
        await using var db = CreateContext();
        var resolver = new SyncConflictResolver(db);

        var childId = Guid.NewGuid();
        var measuredAt = new DateTime(2025, 6, 1, 12, 30, 0, DateTimeKind.Utc);

        var first     = new SyncMeasurement { ChildId = childId, GlucoseValue = 7.0m, MeasuredAt = measuredAt };
        var duplicate = new SyncMeasurement { ChildId = childId, GlucoseValue = 7.0m, MeasuredAt = measuredAt };

        // ACT
        await resolver.SubmitAsync(first);
        await resolver.SubmitAsync(duplicate);

        // ASSERT — the database contains a SyncLog entry with IsConflict = true
        var conflictLogs = await db.SyncLogs
            .Where(l => l.ChildId == childId && l.IsConflict)
            .ToListAsync();

        Assert.Single(conflictLogs);
    }

    // -----------------------------------------------------------------------
    // Req 17.3 — Different-timestamp measurements — both accepted, no conflict
    // -----------------------------------------------------------------------

    /// <summary>
    /// When two measurements with the same ChildId but DIFFERENT MeasuredAt
    /// timestamps are submitted, the resolver MUST:
    ///   - accept both measurements (two rows in the database), and
    ///   - not mark either SyncLog entry with IsConflict = true.
    ///
    /// Validates: Requirement 17.3
    /// </summary>
    [Fact]
    public async Task Submit_DifferentTimestamps_AcceptsBothWithoutConflictFlag()
    {
        // ARRANGE
        await using var db = CreateContext();
        var resolver = new SyncConflictResolver(db);

        var childId = Guid.NewGuid();

        var first = new SyncMeasurement
        {
            ChildId = childId,
            GlucoseValue = 5.0m,
            MeasuredAt = new DateTime(2025, 6, 1, 8, 0, 0, DateTimeKind.Utc)
        };

        var second = new SyncMeasurement
        {
            ChildId = childId,
            GlucoseValue = 6.5m,
            MeasuredAt = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc) // different time
        };

        // ACT
        var firstLog  = await resolver.SubmitAsync(first);
        var secondLog = await resolver.SubmitAsync(second);

        // ASSERT — both measurements are stored
        var measurementCount = await db.Measurements.CountAsync();
        Assert.Equal(2, measurementCount);

        // ASSERT — neither log entry is flagged as a conflict
        Assert.False(firstLog.IsConflict,
            "First measurement with unique timestamp must not be a conflict.");
        Assert.False(secondLog.IsConflict,
            "Second measurement with unique timestamp must not be a conflict.");
    }

    /// <summary>
    /// Verifies that no SyncLog entries with IsConflict = true exist when
    /// all submitted measurements have distinct timestamps.
    ///
    /// Validates: Requirement 17.3
    /// </summary>
    [Fact]
    public async Task Submit_DifferentTimestamps_NoConflictLogsInDatabase()
    {
        // ARRANGE
        await using var db = CreateContext();
        var resolver = new SyncConflictResolver(db);

        var childId = Guid.NewGuid();
        var baseTime = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        // Submit three measurements at different times
        for (int i = 0; i < 3; i++)
        {
            await resolver.SubmitAsync(new SyncMeasurement
            {
                ChildId = childId,
                GlucoseValue = 5.0m + i,
                MeasuredAt = baseTime.AddHours(i)
            });
        }

        // ASSERT — no conflict logs
        var conflictCount = await db.SyncLogs.CountAsync(l => l.IsConflict);
        Assert.Equal(0, conflictCount);

        // ASSERT — all three measurements stored
        var measurementCount = await db.Measurements.CountAsync();
        Assert.Equal(3, measurementCount);
    }

    // -----------------------------------------------------------------------
    // Edge case — different children, same timestamp → no conflict
    // -----------------------------------------------------------------------

    /// <summary>
    /// Two measurements with the same MeasuredAt but DIFFERENT ChildIds
    /// must both be accepted without conflict.
    /// </summary>
    [Fact]
    public async Task Submit_SameTimestampDifferentChildren_AcceptsBothWithoutConflict()
    {
        // ARRANGE
        await using var db = CreateContext();
        var resolver = new SyncConflictResolver(db);

        var measuredAt = new DateTime(2025, 6, 1, 10, 0, 0, DateTimeKind.Utc);

        var childA = new SyncMeasurement
        {
            ChildId = Guid.NewGuid(),
            GlucoseValue = 5.0m,
            MeasuredAt = measuredAt
        };

        var childB = new SyncMeasurement
        {
            ChildId = Guid.NewGuid(), // different child
            GlucoseValue = 6.0m,
            MeasuredAt = measuredAt   // same timestamp — but different child, so no conflict
        };

        // ACT
        var logA = await resolver.SubmitAsync(childA);
        var logB = await resolver.SubmitAsync(childB);

        // ASSERT
        Assert.Equal(2, await db.Measurements.CountAsync());
        Assert.False(logA.IsConflict);
        Assert.False(logB.IsConflict);
    }
}
