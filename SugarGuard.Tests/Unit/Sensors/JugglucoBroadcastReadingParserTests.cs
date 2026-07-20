using SugarGuard.Junior.Core.Sensors;

namespace SugarGuard.Tests.Unit.Sensors;

public sealed class JugglucoBroadcastReadingParserTests
{
    [Fact]
    public void TryParse_UsesMgDlValueFromGlucodataMessage()
    {
        var receivedAt = new DateTime(2026, 7, 21, 12, 0, 0, DateTimeKind.Utc);
        var payload = new JugglucoBroadcastPayload(
            JugglucoBroadcastContract.GlucodataMinuteAction,
            Glucose: 7.3,
            MgDl: 126,
            TimestampUnixMilliseconds: new DateTimeOffset(receivedAt).ToUnixTimeMilliseconds(),
            Rate: 0.12,
            SensorSerialNumber: "ABC123");

        var parsed = JugglucoBroadcastReadingParser.TryParse(payload, receivedAt, out var reading, out var error);

        Assert.True(parsed, error);
        Assert.NotNull(reading);
        Assert.Equal(7.0, reading.GlucoseMmolPerLiter);
        Assert.Equal(receivedAt, reading.MeasurementTimeUtc);
        Assert.Equal("Juggluco", reading.Source);
    }

    [Fact]
    public void TryParse_ConvertsXdripMilligramsToMmol()
    {
        var receivedAt = new DateTime(2026, 7, 21, 12, 0, 0, DateTimeKind.Utc);
        var payload = new JugglucoBroadcastPayload(
            JugglucoBroadcastContract.XdripBgEstimateAction,
            Glucose: 90,
            MgDl: null,
            TimestampUnixMilliseconds: null,
            Rate: -0.1,
            SensorSerialNumber: null);

        var parsed = JugglucoBroadcastReadingParser.TryParse(payload, receivedAt, out var reading, out var error);

        Assert.True(parsed, error);
        Assert.NotNull(reading);
        Assert.Equal(5.0, reading.GlucoseMmolPerLiter);
        Assert.Equal(receivedAt, reading.MeasurementTimeUtc);
    }

    [Fact]
    public void TryParse_RejectsOutOfRangeValue()
    {
        var payload = new JugglucoBroadcastPayload(
            JugglucoBroadcastContract.GlucodataMinuteAction,
            Glucose: null,
            MgDl: 720,
            TimestampUnixMilliseconds: null,
            Rate: null,
            SensorSerialNumber: null);

        var parsed = JugglucoBroadcastReadingParser.TryParse(payload, DateTime.UtcNow, out var reading, out var error);

        Assert.False(parsed);
        Assert.Null(reading);
        Assert.NotNull(error);
    }
}
