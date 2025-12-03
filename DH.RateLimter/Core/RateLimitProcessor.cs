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

    /// 用于限制请求的键锁。
    private static readonly AsyncKeyLock AsyncLock = new();

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
        var now = DateTime.UtcNow;

        // 简化：使用锁保护，但减少缓存操作
        using (await AsyncLock.WriterLockAsync(counterId).ConfigureAwait(false))
        {
            var entry = await _counterStore.GetAsync(counterId, cancellationToken).ConfigureAwait(false);

            RateLimitCounter counter;

            // 检查是否在时间窗口内（默认值的Timestamp是DateTime.MinValue）
            if (entry.Timestamp != default && entry.Timestamp.AddSeconds(rateValve.Duration) >= now)
            {
                // 在时间窗口内，增加计数
                counter = new RateLimitCounter
                {
                    Timestamp = entry.Timestamp,
                    Count = entry.Count + 1
                };
            }
            else
            {
                // 新的时间窗口或首次访问
                counter = new RateLimitCounter
                {
                    Timestamp = now,
                    Count = 1
                };
            }

            // 存储更新后的计数器
            await _counterStore.SetAsync(counterId, counter, TimeSpan.FromSeconds(rateValve.Duration), cancellationToken).ConfigureAwait(false);

            return counter;
        }
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
