using Microsoft.AspNetCore.Components;

namespace SugarGuard.Web.Utilities;

/// <summary>
/// Защита от open-redirect атак через query-параметр
/// </summary>
public static class SafeRedirect
{
    /// <summary>
    /// Редиректит на returnUrl, если он безопасен
    /// </summary>
    public static void SafeRedirectTo(this NavigationManager navigation, string? returnUrl, string defaultPath = "/parent/dashboard")
    {
        var safe = SanitizeReturnUrl(returnUrl, defaultPath);
        navigation.NavigateTo(safe, forceLoad: false);
    }

    /// <summary>
    /// Возвращает безопасный путь
    /// </summary>
    public static string SanitizeReturnUrl(string? returnUrl, string defaultPath = "/parent/dashboard")
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
            return defaultPath;

        // Длина
        if (returnUrl.Length > 2048)
            return defaultPath;

        // Должен начинаться с '/'
        if (returnUrl[0] != '/')
            return defaultPath;

        // Запрет protocol-relative URL 
        if (returnUrl.Length >= 2 && (returnUrl[1] == '/' || returnUrl[1] == '\\'))
            return defaultPath;

        // Запрет явных URL с протоколом 
        if (returnUrl.Contains('\n') || returnUrl.Contains('\r'))
            return defaultPath;

        return returnUrl;
    }
}

