using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using SugarGuard.Web;
using SugarGuard.Web.Components;
using SugarGuard.Web.Services;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

// -----------------------------------------------------------------------
// Razor-компоненты + Blazor Server
// -----------------------------------------------------------------------
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// -----------------------------------------------------------------------
// HTTP-контекст (middleware, IP, security-заголовки)
// -----------------------------------------------------------------------
builder.Services.AddHttpContextAccessor();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto |
        ForwardedHeaders.XForwardedHost;

    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// -----------------------------------------------------------------------
// Аутентификация и авторизация (JWT)
// -----------------------------------------------------------------------

// Blazor — авторизация через каскадные параметры
builder.Services
    .AddAuthentication(SugarGuard.Web.Security.PassThroughAuthenticationHandler.SchemeName)
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, SugarGuard.Web.Security.PassThroughAuthenticationHandler>(
        SugarGuard.Web.Security.PassThroughAuthenticationHandler.SchemeName,
        _ => { });
builder.Services.AddAuthorizationCore();
// ASP.NET Core [Authorize] на минимальных API/контроллерах
builder.Services.AddAuthorization();
// Каскадный AuthenticationState для всего дерева компонентов
builder.Services.AddCascadingAuthenticationState();

// Хранилище JWT access- и refresh-токенов в браузерном localStorage (JS Interop)
builder.Services.AddScoped<ITokenStore, LocalStorageTokenStore>();

// HTTP-обработчик: подставляет Authorization: Bearer <token> в каждый запрос к API
builder.Services.AddScoped<JwtAuthorizationHandler>();

builder.Services.AddScoped<JwtAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(
    sp => sp.GetRequiredService<JwtAuthStateProvider>());

// -----------------------------------------------------------------------
// Типизированные HTTP-клиенты
// -----------------------------------------------------------------------

// Typed-клиент для login / register / refresh / verify (без Bearer — pre-auth)
builder.Services.AddScoped<IAuthApiClient, AuthApiClient>();

// Сервис текущего пользователя (читает Claims из AuthenticationState)
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// Основной API-клиент для работы с данными (добавляет Bearer через JwtAuthorizationHandler)
builder.Services.AddScoped<SugarGuardApiService>();

// -----------------------------------------------------------------------
// HTTP-клиент к бэкенду API
// -----------------------------------------------------------------------
builder.Services.AddHttpClient("SugarGuardApi", client =>
{
    var baseUrl = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7001";
    client.BaseAddress = new Uri(baseUrl);
    // Таймаут 30 секунд — достаточно для медицинских данных
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddHttpMessageHandler<JwtAuthorizationHandler>()
.ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();

    if (builder.Environment.IsDevelopment())
    {
        handler.ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    }

    return handler;
});

builder.Services.AddHttpClient("SugarGuardApiPublic", client =>
{
    var baseUrl = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7001";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(15);
});

// Backward-compatible scoped HttpClient for older admin pages. It uses the
// same base address and JWT handler as SugarGuardApiService.
builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("SugarGuardApi"));

// -----------------------------------------------------------------------
// Локализация (ru-RU)
// -----------------------------------------------------------------------
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// -----------------------------------------------------------------------
// Сборка приложения
// -----------------------------------------------------------------------
var app = builder.Build();

// -----------------------------------------------------------------------
// Локализация middleware
// -----------------------------------------------------------------------
var ruCulture = new CultureInfo("ru-RU");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(ruCulture),
    SupportedCultures = new List<CultureInfo> { ruCulture },
    SupportedUICultures = new List<CultureInfo> { ruCulture }
});

// -----------------------------------------------------------------------
// Security Headers (XSS, clickjacking, MIME-sniffing)
// -----------------------------------------------------------------------
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;

    // Запрет встраивания в iframe (clickjacking)
    headers["X-Frame-Options"] = "DENY";
    // Запрет MIME-sniffing Content-Type
    headers["X-Content-Type-Options"] = "nosniff";
    // Минимальная утечка referrer при переходе на внешние ресурсы
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    // Запрет доступа к камере, микрофону, геолокации без явного разрешения
    headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

    // Content Security Policy:
    // - Шрифты разрешены с api.fontshare.com и fonts.gstatic.com
    // - SignalR WebSocket разрешён через connect-src wss://*
    headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +   // 'unsafe-inline' нужен Blazor Server
        "style-src 'self' 'unsafe-inline' https://api.fontshare.com https://fonts.googleapis.com; " +
        "font-src 'self' data: https://api.fontshare.com https://cdn.fontshare.com https://fonts.gstatic.com; " +
        "connect-src 'self' https://cdn.jsdelivr.net wss://* ws://*; " +   // SignalR WebSocket
        "img-src 'self' data:; " +
        "frame-ancestors 'none';";

    await next();
});

// -----------------------------------------------------------------------
// HTTP-пайплайн
// -----------------------------------------------------------------------

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    // Страница ошибок в Production
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;

    // Запрет MIME-sniffing.
    headers["X-Content-Type-Options"] = "nosniff";

    // Запрет iframe (clickjacking).
    headers["X-Frame-Options"] = "DENY";

    // Не отдавать Referer с PHI-параметрами на внешние ресурсы.
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

    // Отключить неиспользуемые browser features.
    headers["Permissions-Policy"] =
        "accelerometer=(), camera=(), geolocation=(self), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()";

    // CSP: разрешить inline styles (нужно Blazor), но не inline scripts.
    // 'self' для скриптов и стилей; data: для favicon.
    if (!headers.ContainsKey("Content-Security-Policy"))
    {
        headers["Content-Security-Policy"] =
            "default-src 'self'; " +
            "script-src 'self'; " +
            // Blazor использует inline style для динамических компонентов.
            "style-src 'self' 'unsafe-inline'; " +
            "img-src 'self' data: https:; " +
            "font-src 'self' data:; " +
            "connect-src 'self' https://gigachat.devices.sberbank.ru; " +
            "frame-ancestors 'none'; " +
            "form-action 'self'; " +
            "base-uri 'self'; " +
            "object-src 'none';";
    }

    await next();
});

// HTTPS termination and HTTP -> HTTPS redirects are handled by nginx on VDS.

app.UseAuthentication();
app.UseAuthorization();

// CSRF-защита (обязательна для Blazor Server с интерактивными компонентами)
app.UseAntiforgery();

// Serve wwwroot directly. Endpoint-based static-asset manifests are tied to a
// particular publish output and can be temporarily stale during a rolling
// deployment, which must never break the interactive portal.
app.UseStaticFiles();

app.MapGet("/uploads/{**path}", async (
    string path,
    IHttpClientFactory httpClientFactory,
    HttpContext context,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(path)
        || path.Contains("..", StringComparison.Ordinal)
        || path.Any(character => !(char.IsLetterOrDigit(character) || character is '/' or '-' or '_' or '.')))
    {
        return Results.BadRequest();
    }

    var client = httpClientFactory.CreateClient("SugarGuardApiPublic");
    using var response = await client.GetAsync($"uploads/{path}", HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    if (!response.IsSuccessStatusCode)
    {
        return Results.NotFound();
    }

    context.Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
    context.Response.Headers.CacheControl = "public,max-age=86400";
    await response.Content.CopyToAsync(context.Response.Body, cancellationToken);
    return Results.Empty;
}).AllowAnonymous();

// Razor-компоненты с поддержкой Blazor Server
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
