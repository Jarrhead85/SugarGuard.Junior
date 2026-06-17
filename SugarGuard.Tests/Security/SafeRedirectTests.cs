using SugarGuard.Web.Utilities;

namespace SugarGuard.Tests.Security;

/// <summary>
/// Unit-тесты для <see cref="SafeRedirect"/>.
/// <para>
/// Защита от open-redirect: <c>returnUrl</c> из query string должен быть
/// локальным path, иначе — fallback на default.
/// </para>
/// </summary>
public class SafeRedirectTests
{
    [Theory]
    [InlineData("/parent/dashboard")]
    [InlineData("/doctor/patients/abc-123")]
    [InlineData("/admin/users?page=2")]
    [InlineData("/path/with#fragment")]
    [InlineData("/path?query=1&other=2")]
    public void SanitizeReturnUrl_LocalPaths_ReturnAsIs(string input)
    {
        Assert.Equal(input, SafeRedirect.SanitizeReturnUrl(input));
    }

    [Theory]
    [InlineData("https://evil.com/phishing")]
    [InlineData("http://evil.com")]
    [InlineData("//evil.com")]
    [InlineData("/\\evil.com")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("not-a-path")]
    public void SanitizeReturnUrl_ExternalOrInvalid_ReturnsDefault(string input)
    {
        Assert.Equal("/parent/dashboard", SafeRedirect.SanitizeReturnUrl(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SanitizeReturnUrl_Empty_ReturnsDefault(string? input)
    {
        Assert.Equal("/parent/dashboard", SafeRedirect.SanitizeReturnUrl(input));
    }

    [Fact]
    public void SanitizeReturnUrl_TooLong_ReturnsDefault()
    {
        var longPath = "/" + new string('a', 3000);
        Assert.Equal("/parent/dashboard", SafeRedirect.SanitizeReturnUrl(longPath));
    }

    [Fact]
    public void SanitizeReturnUrl_CustomDefault_Honored()
    {
        Assert.Equal("/login", SafeRedirect.SanitizeReturnUrl(null, "/login"));
        Assert.Equal("/login", SafeRedirect.SanitizeReturnUrl("//evil.com", "/login"));
    }

    [Fact]
    public void SanitizeReturnUrl_CrlfInjection_Blocked()
    {
        Assert.Equal("/parent/dashboard", SafeRedirect.SanitizeReturnUrl("/path\r\nLocation: evil.com"));
    }
}
