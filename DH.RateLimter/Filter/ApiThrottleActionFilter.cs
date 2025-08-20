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
        //循环验证是否过载
        foreach (var valve in _valves.OrderByDescending(x => x.Priority))
        {
            if (valve is RateValve rateValve)
            {
                //速率阀门
                if (rateValve.Duration <= 0 || rateValve.Limit <= 0)
                {
                    //不限流
                    continue;
                }

                //取得识别值
                var policyValue = context.HttpContext.GetPolicyValue(_options, valve.Policy, valve.PolicyKey);

                // 优化的WhenNull处理：一次调用完成所有逻辑
                var (shouldProcess, finalPolicyValue) = context.HttpContext.ProcessPolicyValueWithWhenNull(valve, policyValue);
                if (!shouldProcess)
                {
                    continue; // 根据WhenNull设置跳过此规则
                }

                // increment counter
                //判断是否过载
                var rateLimitCounter = await _processor.ProcessRequestAsync(_api, finalPolicyValue, valve, context.HttpContext.RequestAborted).ConfigureAwait(false);

                //XTrace.WriteLine($"[ApiThrottleActionFilter.CheckAsync]获取到的数据：{rateLimitCounter.Count}_{rateLimitCounter.Timestamp}");

                if (rateLimitCounter.Count > rateValve.Limit)
                {
                    return (false, valve);
                }
            }

            // 注意：黑白名单功能（BlackListValve、WhiteListValve）暂未实现
            // 如需黑白名单功能，建议在业务代码中自行实现或等待后续版本
        }

        return (true, null);
    }
}
