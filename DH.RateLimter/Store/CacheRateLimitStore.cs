﻿using NewLife.Caching;
using NewLife.Log;

using Pek.Configs;

namespace DH.RateLimter.Store;

public class CacheRateLimitStore<T> : IRateLimitStore<T>
{
    private readonly ICache _cache;

    public CacheRateLimitStore()
    {
        if (RedisSetting.Current.RedisEnabled)
        {
            _cache = Pek.Webs.HttpContext.Current.RequestServices.GetRequiredService<FullRedis>();

            if (_cache == null)
            {
                XTrace.WriteLine($"Redis缓存对象为空，请检查是否注入FullRedis");
            }
        }
        else
        {
            _cache = Pek.Webs.HttpContext.Current.RequestServices.GetRequiredService<ICacheProvider>().Cache;
        }
    }

    public Task<bool> ExistsAsync(string id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_cache.ContainsKey("id"));
    }

    public Task<T> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_cache.Get<T>(id));
    }

    public Task RemoveAsync(string id, CancellationToken cancellationToken = default)
    {
        _cache.Remove(id);

        return Task.CompletedTask;
    }

    public Task SetAsync(string id, T entry, TimeSpan? expirationTime = null, CancellationToken cancellationToken = default)
    {
        var cacheTime = 0;
        if (expirationTime.HasValue)
        {
            cacheTime = (Int32)expirationTime.Value.TotalSeconds;
        }

        _cache.Set(id, entry, cacheTime);

        return Task.CompletedTask;
    }

}
