using Microsoft.AspNetCore.Mvc;

namespace SugarGuard.API.Controllers;

/// <summary>Public metadata used by the mobile application to check for a release.</summary>
[ApiController]
[Route("api/mobile-app")]
public sealed class MobileAppController(IConfiguration configuration) : ControllerBase
{
    [HttpGet("version")]
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
    public ActionResult<MobileAppVersionResponse> GetVersion()
    {
        var version = configuration["MobileApp:Android:Version"];
        var downloadUrl = configuration["MobileApp:Android:DownloadUrl"];
        if (!Version.TryParse(version, out var parsedVersion) || string.IsNullOrWhiteSpace(downloadUrl))
        {
            return NotFound();
        }

        return Ok(new MobileAppVersionResponse(parsedVersion.ToString(), downloadUrl, configuration["MobileApp:Android:ReleaseNotes"]));
    }
}

public sealed record MobileAppVersionResponse(string Version, string DownloadUrl, string? ReleaseNotes);
