using Microsoft.AspNetCore.Http;
using ReverseProxy.Application.Interfaces;

namespace src.ReverseProxy.API.Middleware;

public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;

    public RateLimitingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context, 
        IRateLimiter rateLimiter, 
        IProxyMetricsCollector metricsCollector)
    {
        var clientIp = ResolveClientIp(context);
        var path = context.Request.Path.Value ?? string.Empty;

        if (!rateLimiter.IsRequestAllowed(clientIp, path))
        {
            metricsCollector.RecordRateLimitBlocked();
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers["Retry-After"] = rateLimiter.WindowSeconds.ToString();
            await context.Response.WriteAsync($"Rate limit exceeded. Please try again after {rateLimiter.WindowSeconds} seconds.");
            return;
        }

        await _next(context);
    }

    private static string ResolveClientIp(HttpContext context)
    {
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            var originalIp = forwardedFor.Split(',')[0].Trim();
            if (!string.IsNullOrEmpty(originalIp))
            {
                return originalIp;
            }
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}