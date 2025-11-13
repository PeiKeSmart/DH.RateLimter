using Microsoft.AspNetCore.Mvc;

namespace DH.RateLimter.Sample.Controllers;

/// <summary>
/// WhenNull 功能使用示例
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class WhenNullExamplesController : ControllerBase
{
    /// <summary>
    /// 示例1：可选Cookie认证（宽松模式）
    /// Cookie不存在时跳过限流，允许游客访问
    /// </summary>
    [HttpGet("public-api")]
    [RateValve(
        Policy = Policy.Cookie, 
        PolicyKey = "user_token",
        Limit = 100, 
        Duration = 3600,
        WhenNull = WhenNull.Pass  // Cookie不存在时跳过限流
    )]
    public IActionResult GetPublicData()
    {
        var userToken = Request.Cookies["user_token"];
        if (string.IsNullOrEmpty(userToken))
        {
            return Ok(new { message = "游客访问", data = "公开数据" });
        }
        else
        {
            return Ok(new { message = "用户访问", data = "个性化数据", token = userToken });
        }
    }

    /// <summary>
    /// 示例2：必须提供认证（严格模式）
    /// Header不存在时仍进行限流检查，通常会被拦截
    /// </summary>
    [HttpPost("secure-api")]
    [RateValve(
        Policy = Policy.Header, 
        PolicyKey = "Authorization",
        Limit = 50, 
        Duration = 3600,
        WhenNull = WhenNull.Intercept  // Header不存在时仍进行限流检查
    )]
    public IActionResult SecureAction()
    {
        var authHeader = Request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader))
        {
            return Unauthorized("需要提供Authorization头");
        }
        
        return Ok(new { message = "安全操作完成", auth = authHeader });
    }

    /// <summary>
    /// 示例3：用户身份回退策略
    /// 登录用户和游客使用不同的限流策略
    /// </summary>
    [HttpGet("user-data")]
    // 优先基于用户ID限流（宽松）
    [RateValve(
        Policy = Policy.UserIdentity,
        Limit = 1000, 
        Duration = 3600,
        WhenNull = WhenNull.Pass,  // 未登录用户跳过此规则
        Priority = 2
    )]
    // 回退到IP限流（严格）
    [RateValve(
        Policy = Policy.Ip,
        Limit = 100, 
        Duration = 3600,
        Priority = 1
    )]
    public IActionResult GetUserData()
    {
        var userId = User?.Identity?.Name;
        if (string.IsNullOrEmpty(userId))
        {
            return Ok(new { message = "游客数据", limit = "IP限流：100次/小时" });
        }
        else
        {
            return Ok(new { message = "用户数据", userId, limit = "用户限流：1000次/小时" });
        }
    }

    /// <summary>
    /// 示例4：多重验证策略
    /// 必须同时满足多个认证条件
    /// </summary>
    [HttpPost("admin-action")]
    // 必须有管理员Token
    [RateValve(
        Policy = Policy.Header, 
        PolicyKey = "X-Admin-Token",
        Limit = 10, 
        Duration = 300,
        WhenNull = WhenNull.Intercept,  // 没有管理员Token时拦截
        Priority = 3
    )]
    // 必须有会话Cookie
    [RateValve(
        Policy = Policy.Cookie, 
        PolicyKey = "session_id",
        Limit = 20, 
        Duration = 600,
        WhenNull = WhenNull.Intercept,  // 没有会话时拦截
        Priority = 2
    )]
    // IP兜底限制
    [RateValve(
        Policy = Policy.Ip,
        Limit = 5, 
        Duration = 300,
        Priority = 1
    )]
    public IActionResult AdminAction()
    {
        var adminToken = Request.Headers["X-Admin-Token"].FirstOrDefault();
        var sessionId = Request.Cookies["session_id"];
        
        if (string.IsNullOrEmpty(adminToken))
        {
            return Unauthorized("需要管理员Token");
        }
        
        if (string.IsNullOrEmpty(sessionId))
        {
            return Unauthorized("需要有效会话");
        }
        
        return Ok(new { 
            message = "管理员操作完成", 
            adminToken = adminToken[..8] + "...", 
            sessionId = sessionId[..8] + "..." 
        });
    }

    /// <summary>
    /// 示例5：Form数据验证
    /// 基于表单中的用户ID进行限流
    /// </summary>
    [HttpPost("submit-form")]
    [RateValve(
        Policy = Policy.Form, 
        PolicyKey = "user_id",
        Limit = 10, 
        Duration = 3600,
        WhenNull = WhenNull.Intercept  // 没有user_id时拦截
    )]
    public IActionResult SubmitForm([FromForm] string user_id, [FromForm] string data)
    {
        if (string.IsNullOrEmpty(user_id))
        {
            return BadRequest("user_id是必需的");
        }
        
        return Ok(new { message = "表单提交成功", userId = user_id, data });
    }

    /// <summary>
    /// 示例6：Query参数验证
    /// 基于查询参数进行限流
    /// </summary>
    [HttpGet("search")]
    [RateValve(
        Policy = Policy.Query, 
        PolicyKey = "api_key",
        Limit = 1000, 
        Duration = 3600,
        WhenNull = WhenNull.Pass  // 没有api_key时使用IP限流
    )]
    [RateValve(
        Policy = Policy.Ip,
        Limit = 100, 
        Duration = 3600,
        Priority = 1
    )]
    public IActionResult Search([FromQuery] string api_key, [FromQuery] string keyword)
    {
        if (string.IsNullOrEmpty(api_key))
        {
            return Ok(new { 
                message = "游客搜索", 
                keyword, 
                limit = "IP限流：100次/小时",
                results = new[] { "结果1", "结果2" }
            });
        }
        else
        {
            return Ok(new { 
                message = "API搜索", 
                keyword, 
                apiKey = api_key[..8] + "...",
                limit = "API限流：1000次/小时",
                results = new[] { "详细结果1", "详细结果2", "详细结果3" }
            });
        }
    }
}
