using ReverseProxy.Application.Interfaces;
using ReverseProxy.Infrastructure.Caching;
using ReverseProxy.Infrastructure.HealthChecking;
using ReverseProxy.Infrastructure.LoadBalancing;
using ReverseProxy.Infrastructure.Logging.Services;
using ReverseProxy.Infrastructure.Metrics;
using ReverseProxy.Infrastructure.RateLimiting;
using ReverseProxy.Infrastructure.Services;
using src.ReverseProxy.API.Hubs;
using src.ReverseProxy.API.Middleware;
using src.ReverseProxy.API.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:5174")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddSingleton<IProxyMetricsCollector, ProxyMetricsCollector>();

builder.Services.AddSingleton<IBinaryLogger, BinaryLogger>();
builder.Services.AddSingleton<ILoadBalancer, WeightedLoadBalancer>();

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IRateLimiter, FixedWindowRateLimiter>();

builder.Services.AddSingleton<ICacheStore, InMemoryCacheStore>();
builder.Services.AddSingleton<ICachePolicy, TrafficBasedCachePolicy>();

builder.Services.AddHostedService<CacheCleanupBackgroundService>();
builder.Services.AddHostedService<HealthCheckBackgroundService>();
builder.Services.AddHostedService<StatsBroadcastService>();

builder.Services.AddHttpClient<IReverseProxyService, ReverseProxyService>();

var app = builder.Build();

app.UseCors("CorsPolicy");

app.UseMiddleware<LogRequstDataMiddleware>();
app.UseMiddleware<RateLimitingMiddleware>();
app.UseMiddleware<CachingMiddleware>();
app.UseMiddleware<ReverseProxyMiddleware>();

app.MapHub<StatsHub>("/hubs/stats");

app.Run();