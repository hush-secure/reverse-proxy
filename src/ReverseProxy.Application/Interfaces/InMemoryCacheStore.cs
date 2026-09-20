using System.Collections.Concurrent;
using ReverseProxy.Application.Interfaces;
using ReverseProxy.Domain.Entities;
using ReverseProxy.Infrastructure.Caching;

namespace ReverseProxy.Infrastructure.Caching;

public class InMemoryCacheStore : ICacheStore
{
    private readonly ConcurrentDictionary<string, CacheEntry> _store = new();

    public Task<CacheEntry?> GetAsync(string key)
    {
        if (_store.TryGetValue(key, out var entry))
        {
            if (!entry.IsExpired)
            {
                return Task.FromResult<CacheEntry?>(entry);
            }

            _store.TryRemove(key, out _);
        }

        return Task.FromResult<CacheEntry?>(null);
    }

    public Task SetAsync(string key, CacheEntry entry)
    {
        _store[key] = entry;
        return Task.CompletedTask;
    }

    public Task RemoveExpiredAsync()
    {
        foreach (var (key, entry) in _store)
        {
            if (entry.IsExpired)
            {
                _store.TryRemove(key, out _);
            }
        }

        return Task.CompletedTask;
    }
}