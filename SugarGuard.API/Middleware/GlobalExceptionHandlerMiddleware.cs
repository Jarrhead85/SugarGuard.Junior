using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarGuard.API.Exceptions;

namespace SugarGuard.API.Middleware;

/// <summary>
/// Middleware для централизованной обработки исключений
/// </summary>
public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

    public GlobalExceptionHandlerMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            await HandleExceptionAsync(context, exception);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        _logger.LogError(
            exception,
            "Произошло необработанное исключение. TraceId: {TraceId}, Path: {Path}",
            context.TraceIdentifier,
            context.Request.Path);

        (HttpStatusCode statusCode, string errorCode, string message) = exception switch
        {
            GlucoseValidationException ex => (
                HttpStatusCode.BadRequest,
                "GLUCOSE_VALIDATION_ERROR",
                ex.Message
            ),
            GigaChatTimeoutException ex => (
                HttpStatusCode.ServiceUnavailable,
                "GIGACHAT_TIMEOUT",
                ex.Message
            ),
            DbUpdateException ex => (
                HttpStatusCode.Conflict,
                "CONFLICT",
                "Произошёл конфликт при обновлении базы данных. Пожалуйста, попробуйте снова."
            ),
            ArgumentException ex => (
                HttpStatusCode.BadRequest,
                "VALIDATION_ERROR",
                ex.Message
            ),
            UnauthorizedAccessException ex => (
                HttpStatusCode.Unauthorized,
                "UNAUTHORIZED",
                ex.Message
            ),
            KeyNotFoundException ex => (
                HttpStatusCode.NotFound,
                "NOT_FOUND",
                ex.Message
            ),
            _ => (
                HttpStatusCode.InternalServerError,
                "INTERNAL_ERROR",
                "Произошла внутренняя ошибка сервера. Пожалуйста, обратитесь в службу поддержки."
            )
        };

        var problemDetails = new ProblemDetails
        {
            Status = (int)statusCode,
            Title = "Request Failed",
            Detail = message,
            Type = $"https://httpstatuses.com/{(int)statusCode}",
            Instance = context.Request.Path
        };
        problemDetails.Extensions["traceId"] = context.TraceIdentifier;
        problemDetails.Extensions["errorCode"] = errorCode;

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)statusCode;

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(problemDetails, jsonOptions);
        await context.Response.WriteAsync(json);
    }
}
