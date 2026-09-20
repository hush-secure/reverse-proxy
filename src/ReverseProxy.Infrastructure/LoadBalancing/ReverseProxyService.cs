using System.Net.Http;
using Microsoft.AspNetCore.Http;
using ReverseProxy.Application.Interfaces;

namespace ReverseProxy.Infrastructure.Services;

public class ReverseProxyService : IReverseProxyService
{
    private readonly HttpClient _httpClient;
    private readonly ILoadBalancer _loadBalancer;

    public ReverseProxyService(HttpClient httpClient, ILoadBalancer loadBalancer)
    {
        _httpClient = httpClient;
        _loadBalancer = loadBalancer;
    }

    public async Task ForwardAsync(HttpContext context)
    {
        var targetServer = _loadBalancer.GetNextServer();
        if (targetServer == null)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsync("No backend available");
            return;
        }

        var targetUrl = $"{targetServer.Url.TrimEnd('/')}{context.Request.Path}{context.Request.QueryString}";
        using var request = new HttpRequestMessage(new HttpMethod(context.Request.Method), targetUrl);

        if (context.Request.ContentLength > 0 || context.Request.Headers.ContainsKey("Transfer-Encoding"))
        {
            request.Content = new StreamContent(context.Request.Body);
        }

        foreach (var header in context.Request.Headers)
        {
            if (header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase)) continue;

            if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
            {
                request.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }
        }

        request.Headers.TryAddWithoutValidation("X-Forwarded-For", context.Connection.RemoteIpAddress?.ToString());
        request.Headers.TryAddWithoutValidation("X-Forwarded-Host", context.Request.Host.Value);

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);

        context.Response.StatusCode = (int)response.StatusCode;

        foreach (var header in response.Headers)
        {
            context.Response.Headers[header.Key] = header.Value.ToArray();
        }

        foreach (var header in response.Content.Headers)
        {
            context.Response.Headers[header.Key] = header.Value.ToArray();
        }

        context.Response.Headers.Remove("transfer-encoding");

        await response.Content.CopyToAsync(context.Response.Body);
    }
}