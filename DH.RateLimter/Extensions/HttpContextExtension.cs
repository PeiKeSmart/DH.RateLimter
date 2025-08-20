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
    internal static String GetFormValue(this HttpContext context, String key)
    {
        var value = context.Request.Form[key].FirstOrDefault();
        return value;
    }

    internal static String GetPolicyValue(this HttpContext context, RateLimterOptions options, Policy policy, String policyKey)
    {
        return policy switch
        {
            Policy.Ip => Common.IpToNum(options.OnIpAddress(context)),
            Policy.UserIdentity => options.OnUserIdentity(context),
            Policy.Header => context.GetHeaderValue(policyKey),
            Policy.Query => context.GetQueryValue(policyKey),
            Policy.RequestPath => context.GetRequestPath(),
            Policy.Cookie => context.GetCookieValue(policyKey),
            Policy.Form => context.GetFormValue(policyKey),
            _ => throw new ArgumentException("参数出错", nameof(policy)),
        };
    }
}
