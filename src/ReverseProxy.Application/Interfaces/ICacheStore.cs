using ReverseProxy.Domain.Entities;

namespace ReverseProxy.Application.Interfaces;

public interface ICacheStore
{
    Task<CacheEntry?> GetAsync(string key);
    Task SetAsync(string key, CacheEntry entry);
    Task RemoveExpiredAsync();
}