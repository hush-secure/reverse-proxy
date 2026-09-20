using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using ReverseProxy.Application.Interfaces;

namespace ReverseProxy.Infrastructure.HealthChecking;

public class HealthCheckBackgroundService : BackgroundService
{
    private readonly ILoadBalancer _loadBalancer;
    private readonly HttpClient _httpClient;
    private readonly int _threshold;
    private const string HealthPath = "/health-check-a1b2c3d4-e5f6-7890-abcd-ef1234567890";

    public HealthCheckBackgroundService(ILoadBalancer loadBalancer, HttpClient httpClient, IConfiguration config)
    {
        _loadBalancer = loadBalancer;
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(3);
        _threshold = config.GetValue<int>("HealthCheck:UnhealthyThreshold", 3);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var servers = _loadBalancer.GetServerStatuses();
            var now = DateTime.UtcNow;

            var dueServers = servers.Where(server =>
            {
                var interval = server.IsAlive ? TimeSpan.FromSeconds(30) : TimeSpan.FromMinutes(2);
                return now - server.LastCheckedUtc >= interval;
            }).ToList();

            if (dueServers.Count > 0)
            {
                var checkTasks = dueServers.Select(async server =>
                {
                    bool isSuccess = await PingServerAsync(server.Option.Url, stoppingToken);
                    _loadBalancer.RecordHealthCheckResult(server.Option.Id, isSuccess, _threshold);
                });

                await Task.WhenAll(checkTasks);
            }

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }

    private async Task<bool> PingServerAsync(string baseUrl, CancellationToken cancellationToken)
    {
        try
        {
            var targetUrl = $"{baseUrl.TrimEnd('/')}{HealthPath}";
            using var response = await _httpClient.GetAsync(targetUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }
}