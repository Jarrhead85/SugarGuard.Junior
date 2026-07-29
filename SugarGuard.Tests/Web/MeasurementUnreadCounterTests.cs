using SugarGuard.Web.Services;
using SugarGuard.Web.ViewModels;

namespace SugarGuard.Tests.Web;

public sealed class MeasurementUnreadCounterTests
{
    private static readonly DateTime Now = new(2026, 7, 29, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Count_AfterMeasurementsWereViewed_DoesNotShowTheSameEntriesAgain()
    {
        var viewedAt = Now;
        var measurements = new[]
        {
            CreateMeasurement(createdAt: Now.AddMinutes(-5)),
            CreateMeasurement(createdAt: Now.AddMinutes(-1))
        };

        var count = MeasurementUnreadCounter.Count(measurements, viewedAt, Now.AddMinutes(1));

        Assert.Equal(0, count);
    }

    [Fact]
    public void Count_AfterMeasurementsWereViewed_ShowsOnlyEntriesReceivedLater()
    {
        var viewedAt = Now;
        var measurements = new[]
        {
            CreateMeasurement(createdAt: Now.AddMinutes(-1)),
            CreateMeasurement(createdAt: Now.AddMinutes(1))
        };

        var count = MeasurementUnreadCounter.Count(measurements, viewedAt, Now.AddMinutes(2));

        Assert.Equal(1, count);
    }

    [Fact]
    public void Count_WithoutViewMarker_UsesOneHourInitialWindow()
    {
        var measurements = new[]
        {
            CreateMeasurement(createdAt: Now.AddMinutes(-59)),
            CreateMeasurement(createdAt: Now.AddHours(-1).AddTicks(-1))
        };

        var count = MeasurementUnreadCounter.Count(measurements, seenAt: null, Now);

        Assert.Equal(1, count);
    }

    [Fact]
    public void Count_UsesMeasurementTime_WhenCreatedAtIsMissing()
    {
        var measurement = CreateMeasurement(
            measurementTime: Now.AddMinutes(-5),
            createdAt: default);

        var count = MeasurementUnreadCounter.Count(new[] { measurement }, Now.AddMinutes(-10), Now);

        Assert.Equal(1, count);
    }

    private static MeasurementVm CreateMeasurement(DateTime createdAt, DateTime? measurementTime = null) => new()
    {
        MeasurementId = Guid.NewGuid(),
        ChildId = Guid.NewGuid(),
        GlucoseValue = 6.1m,
        MeasurementTime = measurementTime ?? createdAt,
        CreatedAt = createdAt
    };
}
