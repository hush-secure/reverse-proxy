using ReverseProxy.Application.DTOs;

namespace ReverseProxy.Application.Interfaces;

public interface IProxyMetricsCollector
{
    void RecordRequest();
    void RecordCacheHit();
    void RecordCacheMiss();
    void RecordRateLimitBlocked();
    
    (long TotalRequests, long CacheHits, long CacheMisses, long RateLimitBlocked) GetMetricsSnapshot();
}