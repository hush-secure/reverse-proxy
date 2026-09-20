using Microsoft.Extensions.Hosting;
using ReverseProxy.Application.Interfaces;

namespace ReverseProxy.Infrastructure.Caching;

public class CacheCleanupBackgroundService : BackgroundService
{
    private readonly ICacheStore _cacheStore;

    public CacheCleanupBackgroundService(ICacheStore cacheStore)
    {
        _cacheStore = cacheStore;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            await _cacheStore.RemoveExpiredAsync();
        }
    }
}