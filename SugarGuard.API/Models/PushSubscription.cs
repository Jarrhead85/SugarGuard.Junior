using System.ComponentModel.DataAnnotations;

namespace SugarGuard.API.Models;

public sealed class PushSubscriptionRequest
{
    [Required]
    [Url]
    [StringLength(2048)]
    public string Endpoint { get; init; } = string.Empty;

    [Required]
    [StringLength(256)]
    public string P256Dh { get; init; } = string.Empty;

    [Required]
    [StringLength(128)]
    public string Auth { get; init; } = string.Empty;

    [StringLength(512)]
    public string? UserAgent { get; init; }
}
