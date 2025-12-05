using DH.RateLimter.Store;

using NewLife;

using Pek.Configs;

namespace DH.RateLimter;

public class RateLimitProcessor
{
    private readonly IRateLimitStore<RateLimitCounter> _counterStore;

    public RateLimitProcessor(IRateLimitStore<RateLimitCounter> counterStore)
    {
        _counterStore = counterStore;
    }

    public virtual async Task<RateLimitCounter> ProcessRequestAsync(String api, String policyValue, Valve valve, CancellationToken cancellationToken = default)
    {
        return await ProcessRequestAsync(api, policyValue, null, valve, cancellationToken).ConfigureAwait(false);
    }

    public virtual async Task<RateLimitCounter> ProcessRequestAsync(String api, String policyValue, String rawIdentifier, Valve valve, CancellationToken cancellationToken = default)
    {
        if (valve is not RateValve rateValve)
        {
            return new RateLimitCounter { Timestamp = DateTime.UtcNow, Count = 1 };
        }

        var counterId = BuildCounterKey(api, valve.Policy, valve.PolicyKey, policyValue);
        var duration = TimeSpan.FromSeconds(rateValve.Duration);

        // 使用原子递增操作，仅在键首次创建时设置过期时间
        // 这确保了：
        // 1. 计数器在 Duration 时间后自动过期清零
        // 2. 后续请求不会刷新过期时间
        var count = await _counterStore.IncrementAsync(counterId, duration, cancellationToken).ConfigureAwait(false);

        return new RateLimitCounter
        {
            Timestamp = DateTime.UtcNow,
            Count = count
        };
    }

    protected virtual String BuildCounterKey(String api, Policy policy, String policyKey, String policyValue)
    {
        // 简化：直接构建，避免过度缓存
        var prefix = RedisSetting.Current.CacheKeyPrefix;
        var policyStr = policy.ToString().ToLower();

        // 使用简单的字符串插值，性能足够好
        if (!policyKey.IsNullOrWhiteSpace() && !policyValue.IsNullOrWhiteSpace())
        {
            return $"{prefix}:rl:{policyStr}:{Common.EncryptMD5Short(policyKey)}:{Common.EncryptMD5Short(policyValue)}:{api.ToLower()}";
        }
        else if (!policyValue.IsNullOrWhiteSpace())
        {
            return $"{prefix}:rl:{policyStr}:{Common.EncryptMD5Short(policyValue)}:{api.ToLower()}";
        }
        else
        {
            return $"{prefix}:rl:{policyStr}:{api.ToLower()}";
        }
    }
}
