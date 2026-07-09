using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using SugarGuard.API.Middleware;

namespace SugarGuard.Tests.API.Middleware;

public sealed class GlobalExceptionHandlerMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenDbUpdateHasUniqueViolation_ReturnsConflict()
    {
        var postgresException = new PostgresException(
            "duplicate key value violates unique constraint",
            "ERROR",
            "ERROR",
            PostgresErrorCodes.UniqueViolation);

        var context = CreateContext();
        var middleware = CreateMiddleware(new DbUpdateException("Duplicate value.", postgresException));

        await middleware.InvokeAsync(context);

        Assert.Equal((int)HttpStatusCode.Conflict, context.Response.StatusCode);
        Assert.Equal("CONFLICT", await ReadErrorCodeAsync(context));
    }

    [Fact]
    public async Task InvokeAsync_WhenDbUpdateIsNotUniqueViolation_ReturnsInternalServerError()
    {
        var context = CreateContext();
        var middleware = CreateMiddleware(new DbUpdateException("Database unavailable."));

        await middleware.InvokeAsync(context);

        Assert.Equal((int)HttpStatusCode.InternalServerError, context.Response.StatusCode);
        Assert.Equal("DATABASE_ERROR", await ReadErrorCodeAsync(context));
    }

    private static GlobalExceptionHandlerMiddleware CreateMiddleware(Exception exception)
        => new(
            _ => throw exception,
            NullLogger<GlobalExceptionHandlerMiddleware>.Instance);

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.TraceIdentifier = "test-trace";
        context.Request.Path = "/api/test";
        return context;
    }

    private static async Task<string?> ReadErrorCodeAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        return document.RootElement
            .GetProperty("errorCode")
            .GetString();
    }
}
