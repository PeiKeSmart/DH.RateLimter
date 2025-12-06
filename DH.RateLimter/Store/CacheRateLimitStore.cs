using NewLife.Caching;

namespace DH.RateLimter.Store;

public class CacheRateLimitStore<T> : IRateLimitStore<T>
{
    private readonly ICache _cache;

    public CacheRateLimitStore()
    {
        _cache = Pek.Webs.HttpContext.Current.RequestServices.GetRequiredService<ICacheProvider>().Cache;
    }

    public Task<bool> ExistsAsync(string id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_cache.ContainsKey(id));
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

    /// <inheritdoc/>
    public Task<Int64> IncrementAsync(String id, TimeSpan expirationTime, CancellationToken cancellationToken = default)
    {
        // 先递增，Redis INCR 在键不存在时会创建键（值为1）
        var count = _cache.Increment(id, 1);

        // 检查 TTL：仅当键没有过期时间时才设置
        // GetExpire 返回值：
        //   > 0：剩余过期时间
        //   = 0：永不过期（需要修复）
        //   < 0：键不存在（理论上不会，因为刚 INCR 过）
        var ttl = _cache.GetExpire(id);
        if (ttl.TotalSeconds <= 0)
        {
            _cache.SetExpire(id, expirationTime);
        }

        return Task.FromResult(count);
    }
}
