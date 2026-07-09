namespace SugarGuard.API.Security;

/// <summary>
/// Runtime JWT settings resolved from configuration and secret providers.
/// </summary>
public sealed class JwtSettings
{
    public required string Secret { get; init; }

    public string Issuer { get; init; } = "SugarGuardAPI";

    public string Audience { get; init; } = "SugarGuardClients";

    public int ExpiryHours { get; init; } = 24;

    public int RefreshTokenExpiryDays { get; init; } = 30;
}
