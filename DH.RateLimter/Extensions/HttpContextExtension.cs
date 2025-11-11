using System.Collections.Concurrent;
using System.Security.Claims;

using NewLife;
using Pek.Helpers;

namespace DH.RateLimter.Extensions;

public static class HttpContextExtension
{
    /// <summary>
    /// 取得客户端IP地址
    /// </summary>
    internal static String GetIpAddress(this HttpContext context)
    {
        // 使用规范的DHWeb.GetUserHost API
        return DHWeb.GetUserHost(context);
    }

    /// <summary>
    /// 取得默认用户Identity值
    /// </summary>
    internal static String GetDefaultUserIdentity(this HttpContext context) => context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    /// <summary>
    /// 取得Header值
    /// </summary>
    internal static String GetHeaderValue(this HttpContext context, String key) => context.Request.Headers[key].FirstOrDefault();

    /// <summary>
    /// 取得Query值
    /// </summary>
    internal static String GetQueryValue(this HttpContext context, String key) => context.Request.Query[key].FirstOrDefault();

    /// <summary>
    /// 取得RequestPath
    /// </summary>
    internal static String GetRequestPath(this HttpContext context) => context.Request.Path.Value;

    /// <summary>
    /// 取得Cookie值
    /// </summary>
    internal static String GetCookieValue(this HttpContext context, String key)
    {
        context.Request.Cookies.TryGetValue(key, out var value);
        return value;
    }

    /// <summary>
    /// 取得Form值
    /// </summary>
    internal static String GetFormValue(this HttpContext context, String key) => context.GetSafeFormValue(key);

    internal static String GetPolicyValue(this HttpContext context, RateLimterOptions options, Policy policy, String policyKey)
    {
        try
        {
            return policy switch
            {
                Policy.Ip => GetSafeIpValue(context, options),
                Policy.UserIdentity => GetSafeUserIdentity(context, options),
                Policy.Header => context.GetHeaderValue(policyKey),
                Policy.Query => context.GetQueryValue(policyKey),
                Policy.RequestPath => context.GetRequestPath(),
                Policy.Cookie => context.GetCookieValue(policyKey),
                Policy.Form => context.GetSafeFormValue(policyKey),
                _ => throw new ArgumentException("参数出错", nameof(policy)),
            };
        }
        catch (Exception)
        {
            // 发生异常时返回空值，让WhenNull逻辑处理
            return null;
        }
    }

    /// <summary>
    /// 安全获取IP值
    /// </summary>
    private static String GetSafeIpValue(HttpContext context, RateLimterOptions options)
    {
        try
        {
            var ipAddress = options.OnIpAddress(context);
            if (String.IsNullOrEmpty(ipAddress))
            {
                return null;
            }
            return Common.IpToNum(ipAddress);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 安全获取用户身份
    /// </summary>
    private static String GetSafeUserIdentity(HttpContext context, RateLimterOptions options)
    {
        try
        {
            return options.OnUserIdentity(context);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 安全获取Form值
    /// </summary>
    private static String GetSafeFormValue(this HttpContext context, String key)
    {
        try
        {
            // 检查是否有Form数据
            if (!context.Request.HasFormContentType)
            {
                return null;
            }

            return context.Request.Form[key].FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// IP哈希缓存，避免重复计算MD5
    /// </summary>
    private static readonly ConcurrentDictionary<String, String> _ipHashCache = new();

    /// <summary>
    /// 处理策略值和WhenNull逻辑（优化版本）
    /// </summary>
    /// <param name="context">HTTP上下文</param>
    /// <param name="valve">阀门</param>
    /// <param name="policyValue">策略值</param>
    /// <returns>处理结果：(shouldProcess: 是否继续处理, finalPolicyValue: 最终策略值)</returns>
    internal static (Boolean shouldProcess, String finalPolicyValue) ProcessPolicyValueWithWhenNull(
        this HttpContext context, Valve valve, String policyValue)
    {
        // 策略值不为空，直接返回
        if (!String.IsNullOrEmpty(policyValue))
        {
            return (true, policyValue);
        }

        // 策略值为空时的处理
        return valve.WhenNull switch
        {
            WhenNull.Pass => (false, null),  // 跳过限流检查
            WhenNull.Intercept => (true, GetCachedEmptyValueIdentifier(context)), // 继续检查，生成唯一标识
            _ => (false, null)
        };
    }

    /// <summary>
    /// 获取缓存的空值标识符（基于IP）
    /// </summary>
    private static String GetCachedEmptyValueIdentifier(HttpContext context)
    {
        // 使用统一的IP获取方法，处理代理转发等场景
        var clientIp = context.GetIpAddress() ?? "unknown";
        return _ipHashCache.GetOrAdd(clientIp, ip => $"empty_value_{Common.EncryptMD5Short(ip)}");
    }
}
