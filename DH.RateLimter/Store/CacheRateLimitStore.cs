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

        // 仅首次创建时设置过期时间
        // count == 1 表示这是新键的第一个请求，需要设置过期时间
        if (count == 1)
        {
            _cache.SetExpire(id, expirationTime);
        }

        return Task.FromResult(count);
    }
}
