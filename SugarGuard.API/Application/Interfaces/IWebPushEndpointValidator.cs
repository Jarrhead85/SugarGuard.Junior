namespace SugarGuard.API.Application.Interfaces;

/// <summary>
/// Validates browser-provided Web Push endpoints before they can be persisted
/// or used for an outbound HTTP request.
/// </summary>
public interface IWebPushEndpointValidator
{
    /// <summary>
    /// Validates and canonicalizes an endpoint from a supported push provider.
    /// </summary>
    bool TryValidateAndNormalize(string? endpoint, out string normalizedEndpoint);
}
