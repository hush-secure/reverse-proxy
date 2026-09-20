using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using ReverseProxy.Application.Interfaces;

namespace ReverseProxy.Infrastructure.RateLimiting;

public class FixedWindowRateLimiter : IRateLimiter
{
    private readonly IMemoryCache _cache;
    private readonly int _windowMinutes;
    private readonly int _defaultLimit;
    private readonly Dictionary<string, int> _routeLimits;

    public int WindowSeconds => _windowMinutes * 60;

    public FixedWindowRateLimiter(IMemoryCache cache, IConfiguration config)
    {
        _cache = cache;
        _windowMinutes = config.GetValue<int>("RateLimiting:WindowMinutes", 1);
        _defaultLimit = config.GetValue<int>("RateLimiting:DefaultLimit", 60);
        
        _routeLimits = config.GetSection("RateLimiting:Routes")
            .Get<Dictionary<string, int>>()?
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase) 
            ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    }

    public bool IsRequestAllowed(string clientIp, string path)
    {
        bool isSpecificRoute = _routeLimits.TryGetValue(path, out int limit);
        if (!isSpecificRoute)
        {
            limit = _defaultLimit;
        }

        string cacheKey = isSpecificRoute 
            ? $"RL_{clientIp}_{path}" 
            : $"RL_{clientIp}_Global";

        var counter = _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_windowMinutes);
            return new RateLimitCounter();
        });

        var currentCount = Interlocked.Increment(ref counter!.Count);
        return currentCount <= limit;
    }

    private class RateLimitCounter
    {
        public int Count;
    }
}