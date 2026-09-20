using ReverseProxy.Application.Interfaces;

namespace ReverseProxy.Infrastructure.Metrics;

public class ProxyMetricsCollector : IProxyMetricsCollector
{
    private long _totalRequests;
    private long _cacheHits;
    private long _cacheMisses;
    private long _rateLimitBlocked;

    public void RecordRequest() => Interlocked.Increment(ref _totalRequests);
    public void RecordCacheHit() => Interlocked.Increment(ref _cacheHits);
    public void RecordCacheMiss() => Interlocked.Increment(ref _cacheMisses);
    public void RecordRateLimitBlocked() => Interlocked.Increment(ref _rateLimitBlocked);

    public (long TotalRequests, long CacheHits, long CacheMisses, long RateLimitBlocked) GetMetricsSnapshot()
    {
        return (
            Interlocked.Read(ref _totalRequests),
            Interlocked.Read(ref _cacheHits),
            Interlocked.Read(ref _cacheMisses),
            Interlocked.Read(ref _rateLimitBlocked)
        );
    }
}