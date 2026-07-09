using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SugarGuard.API.Exceptions;

namespace SugarGuard.API.Middleware;

/// <summary>
/// Middleware для централизованной обработки исключений
/// </summary>
public sealed class GlobalExceptionHandlerMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

    /// <summary>
    /// Инициализирует middleware централизованной обработки исключений.
    /// </summary>
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
            DbUpdateException ex when IsUniqueConstraintViolation(ex) => (
                HttpStatusCode.Conflict,
                "CONFLICT",
                "Произошёл конфликт при обновлении базы данных. Пожалуйста, попробуйте снова."
            ),
            DbUpdateException => (
                HttpStatusCode.InternalServerError,
                "DATABASE_ERROR",
                "Произошла ошибка при обновлении базы данных. Пожалуйста, обратитесь в службу поддержки."
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

        var json = JsonSerializer.Serialize(problemDetails, JsonOptions);
        await context.Response.WriteAsync(json);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        for (var current = exception.InnerException; current is not null; current = current.InnerException)
        {
            if (current is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                return true;
            }
        }

        return false;
    }
}
