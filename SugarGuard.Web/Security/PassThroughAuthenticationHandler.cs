using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace SugarGuard.Web.Security;

public sealed class PassThroughAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "SugarGuardWeb";

    public PassThroughAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        return Task.FromResult(AuthenticateResult.NoResult());
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        var returnUrl = BuildSafeReturnUrl();
        var loginUrl = string.IsNullOrWhiteSpace(returnUrl)
            ? "/login"
            : $"/login?returnUrl={Uri.EscapeDataString(returnUrl)}";

        Response.Redirect(loginUrl);
        return Task.CompletedTask;
    }

    private string? BuildSafeReturnUrl()
    {
        var path = Request.PathBase.Add(Request.Path).ToString();

        if (string.IsNullOrWhiteSpace(path)
            || path.Equals("/login", StringComparison.OrdinalIgnoreCase)
            || !path.StartsWith("/", StringComparison.Ordinal))
        {
            return null;
        }

        var query = Request.QueryString.HasValue ? Request.QueryString.Value : string.Empty;
        return path + query;
    }
}
