using Microsoft.Extensions.Logging.Abstractions;
using SugarGuard.API.Application.Services;

namespace SugarGuard.Tests.Application.Services;

public sealed class StatisticsCalculationServiceTests
{
    [Fact]
    public void GetPeriodRange_Month_UsesTrailingThirtyDaysInsteadOfCalendarMonth()
    {
        var service = new StatisticsCalculationService(NullLogger<StatisticsCalculationService>.Instance);
        var now = new DateTime(2026, 8, 9, 14, 30, 0, DateTimeKind.Utc);

        var (from, to, label) = service.GetPeriodRange("month", now);

        Assert.Equal(now.AddDays(-30), from);
        Assert.Equal(now, to);
        Assert.Equal("30 дней", label);
    }
}
