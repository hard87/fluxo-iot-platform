using Fluxo.Application.Common.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Fluxo.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        IHostEnvironment environment,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _environment = environment;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception while processing request {Method} {Path}.",
                context.Request.Method,
                context.Request.Path);

            await WriteProblemDetailsAsync(context, ex);
        }
    }

    private async Task WriteProblemDetailsAsync(HttpContext context, Exception exception)
    {
        var (status, title, safeDetail) = exception switch
        {
            ValidationException or ArgumentException => (
                StatusCodes.Status400BadRequest,
                "Validation failed",
                "The request payload is invalid."),
            NotFoundException => (
                StatusCodes.Status404NotFound,
                "Resource not found",
                "The requested resource was not found."),
            ConflictException => (
                StatusCodes.Status409Conflict,
                "Conflict",
                "The request conflicts with the current resource state."),
            UnauthorizedException => (
                StatusCodes.Status401Unauthorized,
                "Unauthorized",
                "Authentication failed."),
            ForbiddenException => (
                StatusCodes.Status403Forbidden,
                "Forbidden",
                "You do not have permission to perform this action."),
            _ => (
                StatusCodes.Status500InternalServerError,
                "Unexpected error",
                "An internal error occurred while processing the request.")
        };

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = _environment.IsDevelopment() ? exception.Message : safeDetail,
            Instance = context.Request.Path
        };
        problem.Extensions["traceId"] = context.TraceIdentifier;

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem);
    }
}
