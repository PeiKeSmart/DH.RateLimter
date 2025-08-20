using System.Security.Claims;

using NewLife;

namespace DH.RateLimter.Extensions;

public static class HttpContextExtension
{
    /// <summary>
    /// 取得客户端IP地址
    /// </summary>
    internal static String GetIpAddress(this HttpContext context)
    {
        var request = context.Request;

        var str = "";
        if (str.IsNullOrEmpty()) str = request.Headers["HTTP_X_FORWARDED_FOR"];
        if (str.IsNullOrEmpty()) str = request.Headers["X-Real-IP"];
        if (str.IsNullOrEmpty()) str = request.Headers["X-Forwarded-For"];
        if (str.IsNullOrEmpty()) str = request.Headers["REMOTE_ADDR"];
        //if (str.IsNullOrEmpty()) str = request.Headers["Host"];
        if (str.IsNullOrEmpty())
        {
            var addr = context.Connection?.RemoteIpAddress;
            if (addr != null)
            {
                if (addr.IsIPv4MappedToIPv6) addr = addr.MapToIPv4();
                str = addr + "";
            }
        }

        return str;
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
    /// 检查策略值是否为空并根据WhenNull设置处理
    /// </summary>
    /// <param name="context">HTTP上下文</param>
    /// <param name="valve">阀门</param>
    /// <param name="policyValue">策略值</param>
    /// <returns>是否应该继续处理（true=继续处理限流检查，false=跳过此规则）</returns>
    internal static Boolean ShouldProcessWhenPolicyValueEmpty(this HttpContext context, Valve valve, String policyValue)
    {
        if (!String.IsNullOrEmpty(policyValue))
        {
            return true; // 策略值不为空，继续处理
        }

        // 策略值为空时的处理
        return valve.WhenNull switch
        {
            WhenNull.Pass => false,      // 通过，不进行限流检查
            WhenNull.Intercept => true,  // 拦截，进行限流检查
            _ => false
        };
    }

    /// <summary>
    /// 为空值策略生成唯一的策略值
    /// </summary>
    /// <param name="context">HTTP上下文</param>
    /// <param name="valve">阀门</param>
    /// <param name="originalPolicyValue">原始策略值</param>
    /// <returns>处理后的策略值</returns>
    internal static String GetProcessedPolicyValue(this HttpContext context, Valve valve, String originalPolicyValue)
    {
        if (!String.IsNullOrEmpty(originalPolicyValue))
        {
            return originalPolicyValue; // 策略值不为空，直接返回
        }

        // 策略值为空且设置为Intercept时，生成基于IP的唯一标识
        if (valve.WhenNull == WhenNull.Intercept)
        {
            var clientIp = context.Connection?.RemoteIpAddress?.ToString() ?? "unknown";
            return $"empty_value_{Common.EncryptMD5Short(clientIp)}";
        }

        return originalPolicyValue; // 其他情况返回原值
    }
}
