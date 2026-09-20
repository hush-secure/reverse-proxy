using Microsoft.AspNetCore.Http;
using ReverseProxy.Application.Interfaces;
using ReverseProxy.Infrastructure.Services;
using Microsoft.AspNetCore.Http;

namespace src.ReverseProxy.API.Middleware;

public class ReverseProxyMiddleware
{
    private readonly RequestDelegate _next;

    public ReverseProxyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IReverseProxyService proxyService)
    {
        if (context.Request.Path.StartsWithSegments("/hubs"))
        {
            await _next(context);
            return;
        }

        await proxyService.ForwardAsync(context);
    }
}