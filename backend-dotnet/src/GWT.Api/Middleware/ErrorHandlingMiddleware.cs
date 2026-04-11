using System.Net;
using System.Text.Json;

namespace GWT.Api.Middleware;

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
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
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, message) = exception switch
        {
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, exception.Message),
            KeyNotFoundException        => (HttpStatusCode.NotFound, exception.Message),
            InvalidOperationException   => (HttpStatusCode.Conflict, exception.Message),
            ArgumentException           => (HttpStatusCode.BadRequest, exception.Message),
            _                           => (HttpStatusCode.InternalServerError, "An unexpected error occurred.")
        };

        if (statusCode == HttpStatusCode.InternalServerError)
            _logger.LogError(exception, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);
        else
            _logger.LogWarning(exception, "{ExType} on {Method} {Path}", exception.GetType().Name, context.Request.Method, context.Request.Path);

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        // Return { error: "..." } matching the shape the frontend interceptor reads
        var response = new { error = message };
        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        await context.Response.WriteAsync(json);
    }
}
