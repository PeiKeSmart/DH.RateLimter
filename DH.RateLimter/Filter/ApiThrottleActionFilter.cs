using System.Reflection;

using DH.RateLimter.Extensions;

using Microsoft.AspNetCore.Mvc.Filters;

using Pek.Configs;

namespace DH.RateLimter.Filter;

public class ApiThrottleActionFilter : IAsyncActionFilter, IAsyncPageFilter
{
    private readonly RateLimitProcessor _processor;
    private readonly RateLimterOptions _options;

    //Api名称
    private String _api = null;
    private IEnumerable<Valve> _valves;

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

    /// <summary>
    /// 处理接口
    /// </summary>
    /// <returns></returns>
    private async Task<(Boolean result, Valve valve)> HandleAsync(FilterContext context)
    {
        //预处理数据
        var method = context.GetHandlerMethod();

        _api = method.DeclaringType.FullName + "." + method.Name;

        _valves = method.GetCustomAttributes<Valve>(true);

        //检查是否过载
        var result = await CheckAsync(context).ConfigureAwait(false);
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

    /// <summary>
    /// 检查过载
    /// </summary>
    /// <returns></returns>
    private async Task<(Boolean result, Valve valve)> CheckAsync(FilterContext context)
    {
        // 优化：预先过滤有效的限流规则，避免运行时检查
        var activeRateValves = _valves
            .OfType<RateValve>()
            .Where(v => v.Duration > 0 && v.Limit > 0)
            .OrderByDescending(x => x.Priority);

        foreach (var rateValve in activeRateValves)
        {
            // 取得识别值
            var policyValue = context.HttpContext.GetPolicyValue(_options, rateValve.Policy, rateValve.PolicyKey);

            // 优化的WhenNull处理：一次调用完成所有逻辑
            var (shouldProcess, finalPolicyValue) = context.HttpContext.ProcessPolicyValueWithWhenNull(rateValve, policyValue);
            if (!shouldProcess)
            {
                continue; // 根据WhenNull设置跳过此规则
            }

            // 限流检查
            var rateLimitCounter = await _processor.ProcessRequestAsync(_api, finalPolicyValue, rateValve, context.HttpContext.RequestAborted).ConfigureAwait(false);

            if (rateLimitCounter.Count > rateValve.Limit)
            {
                return (false, rateValve);
            }
        }

        return (true, null);
    }
}
