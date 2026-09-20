using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using ReverseProxy.Application.DTOs;
using ReverseProxy.Application.Interfaces;

namespace src.ReverseProxy.API.Middleware;

public class LogRequstDataMiddleware
{
    private readonly RequestDelegate _next;

    public LogRequstDataMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context, 
        IBinaryLogger binaryLogger, 
        IProxyMetricsCollector metricsCollector)
    {
        metricsCollector.RecordRequest();

        var stopwatch = Stopwatch.StartNew();
        var startTime = DateTimeOffset.UtcNow;

        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        var routePath = context.Request.Path;

        var originalBodyStream = context.Response.Body;
        using var responseBodyMemoryStream = new MemoryStream();
        context.Response.Body = responseBodyMemoryStream;

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();

            var statusCode = context.Response.StatusCode;
            var durationMs = stopwatch.ElapsedMilliseconds;
            var responseSizeInBytes = responseBodyMemoryStream.Length;

            var startOfYear = new DateTimeOffset(startTime.Year, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var dayOffset = (startTime - startOfYear).Days;

            var logDto = new RequestLogDto(
                ipAddress,
                routePath,
                statusCode,
                durationMs,
                responseSizeInBytes,
                dayOffset
            );

            await binaryLogger.LogAsync(logDto);

            responseBodyMemoryStream.Position = 0;
            await responseBodyMemoryStream.CopyToAsync(originalBodyStream);
            context.Response.Body = originalBodyStream;
        }
    }
}