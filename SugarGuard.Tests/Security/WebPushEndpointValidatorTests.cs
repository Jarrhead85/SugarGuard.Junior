using SugarGuard.API.Application.Services;

namespace SugarGuard.Tests.Security;

public sealed class WebPushEndpointValidatorTests
{
    private readonly WebPushEndpointValidator _validator = new();

    [Theory]
    [InlineData("https://fcm.googleapis.com/fcm/send/test")]
    [InlineData("https://updates.push.services.mozilla.com/wpush/v2/test")]
    [InlineData("https://web.push.apple.com/Q/test")]
    public void TryValidateAndNormalize_OfficialEndpoint_Accepts(string endpoint)
    {
        var accepted = _validator.TryValidateAndNormalize(endpoint, out var normalized);

        Assert.True(accepted);
        Assert.Equal(endpoint, normalized);
    }

    [Theory]
    [InlineData("http://fcm.googleapis.com/fcm/send/test")]
    [InlineData("https://127.0.0.1/internal")]
    [InlineData("https://169.254.169.254/latest/meta-data")]
    [InlineData("https://fcm.googleapis.com.evil.example/push")]
    [InlineData("https://user:password@fcm.googleapis.com/push")]
    [InlineData("https://fcm.googleapis.com:8443/push")]
    public void TryValidateAndNormalize_NonPushOrInternalEndpoint_Rejects(string endpoint)
    {
        var accepted = _validator.TryValidateAndNormalize(endpoint, out var normalized);

        Assert.False(accepted);
        Assert.Empty(normalized);
    }
}
