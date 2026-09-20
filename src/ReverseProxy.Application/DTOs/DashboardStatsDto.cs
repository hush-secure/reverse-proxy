namespace ReverseProxy.Application.DTOs;

public class ServerStatusDto
{
    public string Id { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
    public int Weight { get; set; }
}

public class DashboardStatsDto
{
    public long TotalRequests { get; set; }
    public long CacheHits { get; set; }
    public long CacheMisses { get; set; }
    public double CacheHitRatio => (CacheHits + CacheMisses) > 0 
        ? Math.Round((double)CacheHits / (CacheHits + CacheMisses) * 100, 2) 
        : 0;

    public long RateLimitBlocked { get; set; }
    public List<ServerStatusDto> BackendServers { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}