using Microsoft.AspNetCore.Http;

namespace ReverseProxy.Infrastructure.Services;

public interface IReverseProxyService
{
    Task ForwardAsync(HttpContext context);
}