using System.Collections.Concurrent;
using System.Reflection;

using DH.RateLimter.Extensions;

using Microsoft.AspNetCore.Mvc.Filters;

using NewLife.Log;

using Pek.Configs;

namespace DH.RateLimter.Filter;

public class ApiThrottleActionFilter : IAsyncActionFilter, IAsyncPageFilter
{
    private readonly record struct CachedRateValve(RateValve Valve, String PolicyStr, String PolicyKeyHash);

    private readonly RateLimitProcessor _processor;
    private readonly RateLimterOptions _options;

    /// <summary>方法元数据缓存：MethodInfo -> (API名称, 有效的限流阀门数组)</summary>
    private static readonly ConcurrentDictionary<MethodInfo, (String Api, CachedRateValve[] Valves)> _methodCache = new();

    public ApiThrottleActionFilter(RateLimitProcessor processor, RateLimterOptions options)
    {
        _processor = processor;
        _options = options;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!RateLimterSetting.Current.AllowRateLimter) await next().ConfigureAwait(false);
        else
        {
            var result = await HandleAsync(context).ConfigureAwait(false);
            if (result.result)
            {
                await next().ConfigureAwait(false);
            }
            else
            {
                context.Result = _options.onIntercepted(context.HttpContext, result.valve, IntercepteWhere.ActionFilter);
            }
        }
    }

    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        if (!RateLimterSetting.Current.AllowRateLimter) await next().ConfigureAwait(false);
        else
        {
            var result = await HandleAsync(context).ConfigureAwait(false);
            if (result.result)
            {
                await next().ConfigureAwait(false);
            }
            else
            {
                context.Result = _options.onIntercepted(context.HttpContext, result.valve, IntercepteWhere.PageFilter);
            }
        }
    }

    public async Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => await Task.CompletedTask.ConfigureAwait(false);

    /// <summary>处理接口</summary>
    private async Task<(Boolean result, Valve valve)> HandleAsync(FilterContext context)
    {
        // 预处理数据
        var method = context.GetHandlerMethod();

        // 从缓存获取或计算 API 名称和有效阀门列表
        var (api, valves) = _methodCache.GetOrAdd(method, m =>
        {
            var apiName = (m.DeclaringType.FullName + "." + m.Name).ToLower();
            var rateValves = m.GetCustomAttributes<Valve>(true)
                .OfType<RateValve>()
                .Where(v => v.Duration > 0 && v.Limit > 0)
                .OrderByDescending(x => x.Priority)
                .Select(v => new CachedRateValve(
                    v,
                    v.Policy.ToString().ToLower(),
                    String.IsNullOrWhiteSpace(v.PolicyKey) ? null : Common.EncryptMD5Short(v.PolicyKey)))
                .ToArray();
            return (apiName, rateValves);
        });

        // 检查是否过载
        var result = await CheckAsync(context, api, valves).ConfigureAwait(false);
        if (result.result)
        {
            context.HttpContext.Request.Headers[Common.HeaderStatusKey] = "1";
        }
        else
        {
            context.HttpContext.Request.Headers[Common.HeaderStatusKey] = "0";
        }

        return result;
    }

    /// <summary>检查过载</summary>
    private async Task<(Boolean result, Valve valve)> CheckAsync(FilterContext context, String api, CachedRateValve[] valves)
    {
        // valves 已在 HandleAsync 中预过滤和排序，直接遍历
        foreach (var cachedRateValve in valves)
        {
            var rateValve = cachedRateValve.Valve;

            // 取得识别值（原始值，用于日志）
            var policyValue = context.HttpContext.GetPolicyValue(_options, rateValve.Policy, rateValve.PolicyKey);
            // 保留原始IP用于日志（仅IP策略时）
            var rawIp = rateValve.Policy == Policy.Ip ? _options.OnIpAddress(context.HttpContext) : null;

            // 优化的WhenNull处理：一次调用完成所有逻辑
            var (shouldProcess, finalPolicyValue) = context.HttpContext.ProcessPolicyValueWithWhenNull(rateValve, policyValue);
            if (!shouldProcess)
            {
                continue; // 根据WhenNull设置跳过此规则
            }

            // 限流检查
            var rateLimitCounter = await _processor.ProcessRequestAsync(
                api,
                finalPolicyValue,
                rawIp,
                rateValve,
                cachedRateValve.PolicyStr,
                cachedRateValve.PolicyKeyHash,
                context.HttpContext.RequestAborted).ConfigureAwait(false);

            if (rateLimitCounter.Count > rateValve.Limit)
            {
                var ipInfo = rawIp != null ? $", IP={rawIp}" : "";
                XTrace.WriteLine($"[RateLimiter] 触发限流! API={api}, Policy={rateValve.Policy}{ipInfo}, Count={rateLimitCounter.Count}, Limit={rateValve.Limit}, Duration={rateValve.Duration}s");
                return (false, rateValve);
            }
        }

        return (true, null);
    }
}
