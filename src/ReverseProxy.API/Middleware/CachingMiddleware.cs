using Microsoft.AspNetCore.Http;
using ReverseProxy.Application.Interfaces;
using ReverseProxy.Domain.Entities;

namespace src.ReverseProxy.API.Middleware;

public class CachingMiddleware
{
    private readonly RequestDelegate _next;
    
    private static readonly HashSet<string> HopByHopHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Transfer-Encoding", "Connection", "Keep-Alive", "Proxy-Authenticate",
        "Proxy-Authorization", "TE", "Trailers", "Upgrade"
    };

    public CachingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context, 
        ICacheStore cacheStore, 
        ICachePolicy cachePolicy, 
        IProxyMetricsCollector metricsCollector)
    {
        var requestPath = context.Request.Path.Value ?? string.Empty;

        cachePolicy.RecordRequest(requestPath);

        if (!cachePolicy.IsCacheable(context.Request.Method, requestPath))
        {
            await _next(context);
            return;
        }

        var cacheKey = $"{context.Request.Method}:{context.Request.Path}{context.Request.QueryString}";

        var cachedEntry = await cacheStore.GetAsync(cacheKey);
        if (cachedEntry != null)
        {
            metricsCollector.RecordCacheHit(); 
            context.Response.StatusCode = cachedEntry.StatusCode;
            foreach (var header in cachedEntry.Headers)
            {
                context.Response.Headers[header.Key] = header.Value;
            }
            context.Response.Headers["X-Cache"] = "HIT";
            await context.Response.Body.WriteAsync(cachedEntry.Body);
            return;
        }
        
        metricsCollector.RecordCacheMiss();

        var originalBodyStream = context.Response.Body;
        using var responseBuffer = new MemoryStream();
        context.Response.Body = responseBuffer;

        try
        {
            await _next(context);

            responseBuffer.Seek(0, SeekOrigin.Begin);

            if (context.Response.StatusCode == StatusCodes.Status200OK)
            {
                var bodyBytes = responseBuffer.ToArray();

                var headersDict = context.Response.Headers
                    .Where(h => !HopByHopHeaders.Contains(h.Key))
                    .ToDictionary(
                        h => h.Key, 
                        h => h.Value.Select(v => v ?? string.Empty).ToArray(), 
                        StringComparer.OrdinalIgnoreCase
                    );

                var duration = cachePolicy.GetCacheDuration(requestPath);

                var entry = new CacheEntry
                {
                    StatusCode = context.Response.StatusCode,
                    Body = bodyBytes,
                    Headers = headersDict,
                    ExpiryUtc = DateTime.UtcNow.Add(duration)
                };

                await cacheStore.SetAsync(cacheKey, entry);

                context.Response.Headers["X-Cache"] = "MISS";
                await responseBuffer.CopyToAsync(originalBodyStream);
            }
            else
            {
                await responseBuffer.CopyToAsync(originalBodyStream);
            }
        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }
}