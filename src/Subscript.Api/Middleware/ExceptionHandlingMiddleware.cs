using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Subscript.Application.Common;
using Subscript.Domain.Common;

namespace Subscript.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await WriteProblemAsync(context, ex);
        }
    }

    private async Task WriteProblemAsync(HttpContext context, Exception exception)
    {
        var (status, code, title) = exception switch
        {
            DomainException domain => (StatusCodes.Status400BadRequest, domain.Code, domain.Message),
            NotFoundException notFound => (StatusCodes.Status404NotFound, notFound.Code, notFound.Message),
            ConflictException conflict => (StatusCodes.Status409Conflict, conflict.Code, conflict.Message),
            _ => (StatusCodes.Status500InternalServerError, "internal_error", "An unexpected error occurred.")
        };

        if (status >= 500)
        {
            logger.LogError(exception, "Unhandled exception");
        }
        else
        {
            logger.LogWarning(exception, "Request failed with {Code}", code);
        }

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = title,
            Instance = context.Request.Path,
            Type = $"https://httpstatuses.com/{status}"
        };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = context.TraceIdentifier;

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = status;
        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
    }
}
