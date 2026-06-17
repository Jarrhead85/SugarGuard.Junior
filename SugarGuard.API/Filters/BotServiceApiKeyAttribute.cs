using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SugarGuard.API.Application.Interfaces;

namespace SugarGuard.API.Filters;

/// <summary>
/// Атрибут для авторизации service-to-service запросов от Telegram-бота
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class BotServiceApiKeyAttribute : Attribute, IAsyncAuthorizationFilter
{
    public const string HeaderName = "X-Bot-Auth";

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var http = context.HttpContext;
        var validator = http.RequestServices.GetService<IBotApiKeyValidator>();
        if (validator is null)
        {
            context.Result = new StatusCodeResult(StatusCodes.Status503ServiceUnavailable);
            return;
        }

        if (!http.Request.Headers.TryGetValue(HeaderName, out var headerValues) ||
            string.IsNullOrEmpty(headerValues.ToString()))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var provided = headerValues.ToString();
        if (!await validator.ValidateAsync(provided, http.RequestAborted))
        {
            context.Result = new UnauthorizedResult();
        }
    }
}

