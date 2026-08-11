using SugarGuard.API.Application.Interfaces;

namespace SugarGuard.API.Application.Services;

/// <summary>
/// Restricts Web Push delivery to the official browser push services supported
/// by SugarGuard. This is an SSRF boundary, so configured or arbitrary hosts are
/// intentionally not accepted here.
/// </summary>
public sealed class WebPushEndpointValidator : IWebPushEndpointValidator
{
    public const int MaximumEndpointLength = 2048;

    private static readonly HashSet<string> ExactHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "fcm.googleapis.com",
        "web.push.apple.com"
    };

    private static readonly string[] HostSuffixes =
    {
        "push.services.mozilla.com",
        "notify.windows.com",
        "notify.live.net"
    };

    public bool TryValidateAndNormalize(string? endpoint, out string normalizedEndpoint)
    {
        normalizedEndpoint = string.Empty;

        if (string.IsNullOrWhiteSpace(endpoint) || endpoint.Length > MaximumEndpointLength)
        {
            return false;
        }

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || uri.Port != 443
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Fragment)
            || uri.HostNameType != UriHostNameType.Dns
            || !IsAllowedHost(uri.IdnHost))
        {
            return false;
        }

        normalizedEndpoint = uri.AbsoluteUri;
        return normalizedEndpoint.Length <= MaximumEndpointLength;
    }

    private static bool IsAllowedHost(string host)
    {
        if (ExactHosts.Contains(host))
        {
            return true;
        }

        return HostSuffixes.Any(suffix =>
            string.Equals(host, suffix, StringComparison.OrdinalIgnoreCase)
            || host.EndsWith($".{suffix}", StringComparison.OrdinalIgnoreCase));
    }
}
