using System.Diagnostics;

namespace StreetFood.API.Middleware;

public sealed class RequestIdLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestIdLoggingMiddleware> _logger;

    public RequestIdLoggingMiddleware(RequestDelegate next, ILogger<RequestIdLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var id = context.Request.Headers["X-Request-Id"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(id))
        {
            id = Activity.Current?.Id ?? Guid.NewGuid().ToString("N");
        }

        context.Response.Headers["X-Request-Id"] = id;
        var path = context.Request.Path + context.Request.QueryString;
        var method = context.Request.Method;
        var devId = context.Request.Headers["X-Device-Id"].FirstOrDefault();

        var sw = Stopwatch.StartNew();
        try
        {
            await _next(context);
        }
        finally
        {
            sw.Stop();
            _logger.LogInformation(
                "HTTP {Method} {Path} requestId={RequestId} deviceId={DeviceId} status={Status} {Ms}ms",
                method, path, id, devId ?? "(none)", context.Response.StatusCode, sw.ElapsedMilliseconds);
        }
    }
}
