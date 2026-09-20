using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using ReverseProxy.Application.Interfaces;

namespace ReverseProxy.Infrastructure.Caching;

public class TrafficBasedCachePolicy : ICachePolicy
{
    private readonly HashSet<string> _whitelistedPaths;
    private readonly ConcurrentDictionary<string, long> _requestCounters = new(StringComparer.OrdinalIgnoreCase);

    public TrafficBasedCachePolicy(IConfiguration config)
    {
        var paths = config.GetSection("Caching:Whitelist").Get<List<string>>() ?? new List<string> { "/api/products", "/api/categories" };
        _whitelistedPaths = new HashSet<string>(paths, StringComparer.OrdinalIgnoreCase);
    }

    public bool IsCacheable(string method, string path)
    {
        if (!string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase)) return false;

        return _whitelistedPaths.Contains(path);
    }

    public void RecordRequest(string path)
    {
        _requestCounters.AddOrUpdate(path, 1, (_, count) => count + 1);
    }

    public TimeSpan GetCacheDuration(string path)
    {
        _requestCounters.TryGetValue(path, out var count);

        return count switch
        {
            > 100 => TimeSpan.FromMinutes(10),
            > 20 => TimeSpan.FromMinutes(2),
            _ => TimeSpan.FromSeconds(15)
        };
    }
}