using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;
using SugarGuard.Junior.Services.Interfaces;

namespace SugarGuard.Junior.Services.Implementations;

public sealed class AppUpdateService(HttpClient httpClient, ILogger<AppUpdateService> logger) : IAppUpdateService
{
    public async Task CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var release = await httpClient.GetFromJsonAsync<MobileAppVersionResponse>("api/mobile-app/version", cancellationToken);
            if (release is null || !Version.TryParse(release.Version, out var latest)
                || !Version.TryParse(AppInfo.VersionString, out var installed)
                || latest <= installed || !Uri.TryCreate(release.DownloadUrl, UriKind.Absolute, out var downloadUri))
            {
                return;
            }

            var notes = string.IsNullOrWhiteSpace(release.ReleaseNotes)
                ? "Доступна новая версия SugarGuard Junior."
                : release.ReleaseNotes;

            var shouldOpen = await MainThread.InvokeOnMainThreadAsync(() =>
                Shell.Current.DisplayAlert("Доступно обновление", $"Версия {release.Version}\n\n{notes}", "Скачать", "Позже"));
            if (shouldOpen)
            {
                await Launcher.Default.OpenAsync(downloadUri);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or NotSupportedException or FormatException)
        {
            logger.LogInformation(ex, "Проверка обновления мобильного приложения недоступна.");
        }
    }

    private sealed record MobileAppVersionResponse(string Version, string DownloadUrl, string? ReleaseNotes);
}
