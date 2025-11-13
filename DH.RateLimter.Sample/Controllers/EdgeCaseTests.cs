using Microsoft.AspNetCore.Mvc;

using Pek.Helpers;

namespace DH.RateLimter.Sample.Controllers;

/// <summary>
/// 边界情况和异常处理测试示例
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class EdgeCaseTestsController : ControllerBase
{
    /// <summary>
    /// 测试无效IP地址的处理
    /// </summary>
    [HttpGet("test-invalid-ip")]
    [RateValve(Policy = Policy.Ip, Limit = 10, Duration = 60, WhenNull = WhenNull.Intercept)]
    public IActionResult TestInvalidIp()
    {
        return Ok(new { message = "IP处理测试", ip = DHWeb.GetUserHost(HttpContext) });
    }

    /// <summary>
    /// 测试未认证用户的UserIdentity处理
    /// </summary>
    [HttpGet("test-no-user")]
    [RateValve(Policy = Policy.UserIdentity, Limit = 100, Duration = 3600, WhenNull = WhenNull.Pass)]
    [RateValve(Policy = Policy.Ip, Limit = 10, Duration = 60, Priority = 1)]
    public IActionResult TestNoUser()
    {
        var userId = User?.Identity?.Name;
        return Ok(new { 
            message = "用户身份测试", 
            userId = userId ?? "未认证用户",
            isAuthenticated = User?.Identity?.IsAuthenticated ?? false
        });
    }

    /// <summary>
    /// 测试不存在的Cookie处理
    /// </summary>
    [HttpGet("test-missing-cookie")]
    [RateValve(Policy = Policy.Cookie, PolicyKey = "nonexistent_cookie", Limit = 5, Duration = 300, WhenNull = WhenNull.Intercept)]
    public IActionResult TestMissingCookie()
    {
        var cookieValue = Request.Cookies["nonexistent_cookie"];
        return Ok(new { 
            message = "Cookie测试", 
            cookieExists = !string.IsNullOrEmpty(cookieValue),
            cookieValue = cookieValue ?? "不存在"
        });
    }

    /// <summary>
    /// 测试不存在的Header处理
    /// </summary>
    [HttpGet("test-missing-header")]
    [RateValve(Policy = Policy.Header, PolicyKey = "X-Nonexistent-Header", Limit = 5, Duration = 300, WhenNull = WhenNull.Pass)]
    public IActionResult TestMissingHeader()
    {
        var headerValue = Request.Headers["X-Nonexistent-Header"].FirstOrDefault();
        return Ok(new { 
            message = "Header测试", 
            headerExists = !string.IsNullOrEmpty(headerValue),
            headerValue = headerValue ?? "不存在"
        });
    }

    /// <summary>
    /// 测试非Form请求的Form策略处理
    /// </summary>
    [HttpGet("test-no-form")]
    [RateValve(Policy = Policy.Form, PolicyKey = "user_id", Limit = 5, Duration = 300, WhenNull = WhenNull.Pass)]
    public IActionResult TestNoForm()
    {
        return Ok(new { 
            message = "Form测试", 
            hasFormContentType = Request.HasFormContentType,
            contentType = Request.ContentType ?? "无"
        });
    }

    /// <summary>
    /// 测试空查询参数处理
    /// </summary>
    [HttpGet("test-empty-query")]
    [RateValve(Policy = Policy.Query, PolicyKey = "api_key", Limit = 10, Duration = 60, WhenNull = WhenNull.Intercept)]
    public IActionResult TestEmptyQuery([FromQuery] string api_key)
    {
        return Ok(new { 
            message = "Query测试", 
            apiKeyProvided = !string.IsNullOrEmpty(api_key),
            apiKey = api_key ?? "未提供"
        });
    }

    /// <summary>
    /// 测试多重空值策略的组合
    /// </summary>
    [HttpPost("test-multiple-empty")]
    // 优先检查Authorization（严格）
    [RateValve(Policy = Policy.Header, PolicyKey = "Authorization", Limit = 50, Duration = 3600, WhenNull = WhenNull.Intercept, Priority = 3)]
    // 然后检查用户身份（宽松）
    [RateValve(Policy = Policy.UserIdentity, Limit = 100, Duration = 3600, WhenNull = WhenNull.Pass, Priority = 2)]
    // 最后IP兜底
    [RateValve(Policy = Policy.Ip, Limit = 20, Duration = 3600, Priority = 1)]
    public IActionResult TestMultipleEmpty()
    {
        var auth = Request.Headers["Authorization"].FirstOrDefault();
        var userId = User?.Identity?.Name;
        var ip = DHWeb.GetUserHost(HttpContext);

        return Ok(new { 
            message = "多重空值测试",
            authProvided = !string.IsNullOrEmpty(auth),
            userAuthenticated = !string.IsNullOrEmpty(userId),
            clientIp = ip ?? "未知",
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// 测试异常情况的处理
    /// </summary>
    [HttpGet("test-exception-handling")]
    [RateValve(Policy = Policy.Cookie, PolicyKey = "test_cookie", Limit = 5, Duration = 300, WhenNull = WhenNull.Pass)]
    public IActionResult TestExceptionHandling()
    {
        try
        {
            // 模拟一些可能导致异常的操作
            var cookieValue = Request.Cookies["test_cookie"];
            
            return Ok(new { 
                message = "异常处理测试", 
                success = true,
                cookieValue = cookieValue ?? "空值"
            });
        }
        catch (Exception ex)
        {
            return Ok(new { 
                message = "捕获到异常", 
                success = false,
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// 测试WhenNull.Intercept的缓存键唯一性
    /// </summary>
    [HttpGet("test-cache-key-uniqueness")]
    [RateValve(Policy = Policy.Cookie, PolicyKey = "unique_test", Limit = 2, Duration = 60, WhenNull = WhenNull.Intercept)]
    public IActionResult TestCacheKeyUniqueness()
    {
        var cookieValue = Request.Cookies["unique_test"];
        var clientIp = DHWeb.GetUserHost(HttpContext);
        
        return Ok(new { 
            message = "缓存键唯一性测试",
            cookieProvided = !string.IsNullOrEmpty(cookieValue),
            cookieValue = cookieValue ?? "空值",
            clientIp = clientIp ?? "未知",
            note = "相同IP的用户在没有Cookie时应该共享限流计数器"
        });
    }

    /// <summary>
    /// 测试RequestPath策略
    /// </summary>
    [HttpGet("test-request-path")]
    [RateValve(Policy = Policy.RequestPath, Limit = 10, Duration = 60)]
    public IActionResult TestRequestPath()
    {
        return Ok(new { 
            message = "RequestPath测试",
            path = Request.Path.Value,
            method = Request.Method
        });
    }

    /// <summary>
    /// 压力测试：快速连续请求
    /// </summary>
    [HttpGet("stress-test")]
    [RateValve(Policy = Policy.Ip, Limit = 3, Duration = 10, WhenNull = WhenNull.Intercept)]
    public IActionResult StressTest()
    {
        return Ok(new { 
            message = "压力测试",
            timestamp = DateTime.UtcNow,
            requestId = Guid.NewGuid().ToString("N")[..8]
        });
    }
}
