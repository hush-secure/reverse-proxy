using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ReverseProxy.Application.DTOs;
using ReverseProxy.Application.Interfaces;
using src.ReverseProxy.API.Hubs;

namespace src.ReverseProxy.API.Services;

public class StatsBroadcastService : BackgroundService
{
    private readonly IHubContext<StatsHub> _hubContext;
    private readonly IProxyMetricsCollector _metrics;
    private readonly ILoadBalancer _loadBalancer;
    private readonly ILogger<StatsBroadcastService> _logger;

    public StatsBroadcastService(
        IHubContext<StatsHub> hubContext,
        IProxyMetricsCollector metrics,
        ILoadBalancer loadBalancer,
        ILogger<StatsBroadcastService> logger)
    {
        _hubContext = hubContext;
        _metrics = metrics;
        _loadBalancer = loadBalancer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var (total, hits, misses, blocked) = _metrics.GetMetricsSnapshot();
                var backendStatuses = _loadBalancer.GetServerStatuses();

                var stats = new DashboardStatsDto
                {
                    TotalRequests = total,
                    CacheHits = hits,
                    CacheMisses = misses,
                    RateLimitBlocked = blocked,
                    BackendServers = backendStatuses.Select(s => new ServerStatusDto
                    {
                        Id = s.Option.Id,
                        Url = s.Option.Url,
                        IsHealthy = s.IsAlive,
                        Weight = s.Option.Weight
                    }).ToList(),
                    Timestamp = DateTime.UtcNow
                };

                await _hubContext.Clients.All.SendAsync("ReceiveStatsUpdate", stats, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while broadcasting dashboard stats.");
            }

            await Task.Delay(1000, stoppingToken);
        }
    }
}