using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SugarGuard.API.Middleware;

public class ApiProblemDetailsResultFilter : IAsyncResultFilter
{
    public Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Result is not ObjectResult objectResult)
        {
            return next();
        }

        var statusCode = objectResult.StatusCode ?? context.HttpContext.Response.StatusCode;
        if (statusCode < 400)
        {
            return next();
        }

        if (objectResult.Value is ProblemDetails)
        {
            return next();
        }

        var (detail, errorCode) = ExtractDetailAndCode(objectResult.Value);

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = "Request Failed",
            Detail = detail ?? "The request could not be processed.",
            Type = $"https://httpstatuses.com/{statusCode}",
            Instance = context.HttpContext.Request.Path
        };
        problemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        if (!string.IsNullOrWhiteSpace(errorCode))
        {
            problemDetails.Extensions["errorCode"] = errorCode;
        }

        context.Result = new ObjectResult(problemDetails)
        {
            StatusCode = statusCode,
            ContentTypes = { "application/problem+json" }
        };

        return next();
    }

    private static (string? detail, string? errorCode) ExtractDetailAndCode(object? value)
    {
        if (value is null)
        {
            return (null, null);
        }

        if (value is string text)
        {
            return (text, null);
        }

        var type = value.GetType();
        var errorCode = GetPropertyValue(type, value, "error");
        var detail = GetPropertyValue(type, value, "message")
            ?? GetPropertyValue(type, value, "detail");

        return (detail, errorCode);
    }

    private static string? GetPropertyValue(Type type, object instance, string name)
    {
        var property = type.GetProperty(name, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
        return property?.GetValue(instance)?.ToString();
    }
}
