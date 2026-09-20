namespace ReverseProxy.Domain.Entities;

public class CacheEntry
{
    public int StatusCode { get; set; }
    public byte[] Body { get; set; } = Array.Empty<byte>();
    public Dictionary<string, string[]> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiryUtc { get; set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiryUtc;
}