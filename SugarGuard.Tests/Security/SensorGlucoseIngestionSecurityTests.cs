using SugarGuard.Junior.Core.Sensors;

namespace SugarGuard.Tests.Security;

public sealed class SensorGlucoseIngestionSecurityTests
{
    [Fact]
    public void IsTrustedForMedicalUse_UnauthenticatedBroadcast_ReturnsFalse()
    {
        var now = DateTime.UtcNow;
        var reading = new SensorGlucoseReading(
            2.8,
            now,
            now,
            "JugglucoBroadcast",
            null,
            null,
            SensorReadingTrust.UntrustedExternalBroadcast);

        Assert.False(SensorGlucoseTrustPolicy.IsTrustedForMedicalUse(reading));
    }

    [Fact]
    public void TryConfirmLocally_StaleBroadcast_IsRejected()
    {
        var now = DateTime.UtcNow;
        var reading = new SensorGlucoseReading(
            5.4,
            now.AddMinutes(-20),
            now.AddMinutes(-20),
            "JugglucoBroadcast",
            null,
            null,
            SensorReadingTrust.UntrustedExternalBroadcast);

        var confirmed = SensorGlucoseTrustPolicy.TryConfirmLocally(
            reading,
            now,
            out var result,
            out var error);

        Assert.False(confirmed);
        Assert.Null(result);
        Assert.NotNull(error);
    }
}
