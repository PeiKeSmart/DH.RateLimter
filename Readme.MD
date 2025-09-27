# DH.RateLimter - 限流管理库

[![NuGet](https://img.shields.io/nuget/v/DH.RateLimter.svg)](https://www.nuget.org/packages/DH.RateLimter)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

DH.RateLimter 是一个功能强大的 ASP.NET Core 限流管理库，支持多种限流策略，可以轻松实现 WebApi 接口的访问频率控制。

## 🚀 特性

- **多种限流策略**：支持 IP、用户身份、Request Header、Request QueryString、Cookie、Form 等多种限流策略
- **多规则并用**：支持在同一个 API 上应用多个限流规则，按优先级顺序执行
- **完善的空值处理**：支持 WhenNull 配置，灵活处理策略值为空的情况
- **灵活的拦截方式**：支持全局拦截和单独 API 拦截
- **高效的限流算法**：基于滑动时间窗口的计数器算法
- **高性能**：基于异步锁和缓存存储，确保高并发场景下的性能
- **易于集成**：简单的配置即可快速集成到现有项目中
- **自定义返回格式**：支持多种返回数据格式

## 📦 安装

### NuGet 包管理器

```bash
Install-Package DH.RateLimter
```

### .NET CLI

```bash
dotnet add package DH.RateLimter
```

### PackageReference

```xml
<PackageReference Include="DH.RateLimter" Version="4.14.*" />
```

## 🛠️ 快速开始

### 1. 注册服务

在 `Program.cs` 或 `Startup.cs` 中注册限流服务：

```csharp
using DH.RateLimter;

// 注册限流服务
builder.Services.AddRateLimter(options =>
{
    // 配置被拦截时的处理逻辑
    options.onIntercepted = (context, valve, where) =>
    {
        // 设置 HTTP 状态码为 429 (Too Many Requests)
        context.Response.StatusCode = 429;

        if (valve.ReturnType == ReturnType.Json_DGResult)
        {
            return new DGResult(StateCode.Busy, "访问过于频繁，请稍后重试！");
        }
        else if (valve.ReturnType == ReturnType.Json_DResult)
        {
            return new JsonResult(new DResult { code = 99, msg = "访问过于频繁，请稍后重试！" });
        }
        else
        {
            return new RateLimterResult { Content = "访问过于频繁，请稍后重试！" };
        }
    };

    // 自定义获取用户身份的方法（可选）
    options.OnUserIdentity = context => context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    // 自定义获取客户端IP的方法（可选）
    options.OnIpAddress = context => context.GetIpAddress();
});
```

### 2. 使用限流特性

在控制器或 Action 方法上使用限流特性：

```csharp
[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    // 基于 IP 的限流：每分钟最多 10 次请求
    [HttpGet("ip-limit")]
    [RateValve(Policy = Policy.Ip, Limit = 10, Duration = 60)]
    public IActionResult GetWithIpLimit()
    {
        return Ok("Success");
    }

    // 基于用户身份的限流：每30秒最多 5 次请求
    [HttpGet("user-limit")]
    [RateValve(Policy = Policy.UserIdentity, Limit = 5, Duration = 30)]
    public IActionResult GetWithUserLimit()
    {
        return Ok("Success");
    }

    // 基于请求头的限流
    [HttpGet("header-limit")]
    [RateValve(Policy = Policy.Header, PolicyKey = "X-API-Key", Limit = 20, Duration = 60)]
    public IActionResult GetWithHeaderLimit()
    {
        return Ok("Success");
    }

    // 基于 Cookie 的限流
    [HttpGet("cookie-limit")]
    [RateValve(Policy = Policy.Cookie, PolicyKey = "session_id", Limit = 30, Duration = 60)]
    public IActionResult GetWithCookieLimit()
    {
        return Ok("Success");
    }

    // 基于表单数据的限流
    [HttpPost("form-limit")]
    [RateValve(Policy = Policy.Form, PolicyKey = "user_id", Limit = 10, Duration = 300)]
    public IActionResult PostWithFormLimit([FromForm] string user_id, [FromForm] string data)
    {
        return Ok("Success");
    }

    // 多重限流策略（按优先级执行）
    [HttpGet("multiple-limits")]
    [RateValve(Policy = Policy.Ip, Limit = 100, Duration = 60, Priority = 1)]           // 低优先级
    [RateValve(Policy = Policy.UserIdentity, Limit = 50, Duration = 60, Priority = 2)]  // 高优先级
    public IActionResult GetWithMultipleLimits()
    {
        return Ok("Success");
    }
}
```

## � 多规则并用机制

DH.RateLimter 支持在同一个 API 方法上应用多个限流规则，这是它的核心优势之一。

### 工作原理

1. **多特性支持**：可以在同一个方法上使用多个 `[RateValve]` 特性
2. **优先级排序**：所有规则按 `Priority` 属性从高到低排序执行
3. **短路机制**：一旦任何一个规则被触发（超限），立即拦截请求，不再检查后续规则
4. **独立计数**：每个规则都有独立的计数器，互不影响
5. **WhenNull处理**：支持策略值为空时的灵活处理机制

### 多规则示例

```csharp
[HttpPost("api/upload")]
// 优先级 3：防止短期内频繁请求（最高优先级）
[RateValve(Policy = Policy.Ip, Limit = 5, Duration = 60, Priority = 3)]
// 优先级 2：用户每小时限制
[RateValve(Policy = Policy.UserIdentity, Limit = 50, Duration = 3600, Priority = 2)]
// 优先级 1：全局 IP 每日限制（最低优先级）
[RateValve(Policy = Policy.Ip, Limit = 1000, Duration = 86400, Priority = 1)]
public async Task<IActionResult> UploadFile(IFormFile file)
{
    return Ok("Upload successful");
}
```

### 执行顺序说明

以上示例的执行顺序为：

1. **首先检查**：IP 每分钟是否超过 5 次（Priority = 3）
2. **然后检查**：用户每小时是否超过 50 次（Priority = 2）
3. **最后检查**：IP 每天是否超过 1000 次（Priority = 1）

只要任何一个规则被触发，请求就会被拦截。

### 混合策略示例

```csharp
[HttpGet("api/sensitive-data")]
// 严格的用户身份验证（最高优先级）
[RateValve(Policy = Policy.Header, PolicyKey = "Authorization", Limit = 50, Duration = 3600, WhenNull = WhenNull.Intercept, Priority = 10)]
// 用户身份限流
[RateValve(Policy = Policy.UserIdentity, Limit = 100, Duration = 3600, WhenNull = WhenNull.Pass, Priority = 5)]
// IP兜底限流
[RateValve(Policy = Policy.Ip, Limit = 20, Duration = 3600, Priority = 1)]
public IActionResult GetSensitiveData()
{
    return Ok("Sensitive data");
}
```

### 最佳实践

1. **合理设置优先级**：
   - 强制认证检查：Priority = 10（最高）
   - 用户身份限流：Priority = 5-8
   - IP限流（兜底）：Priority = 1-4（最低）

2. **避免冲突规则**：
   - 不要设置相同策略和时间窗口的重复规则
   - 确保短期限制的 Limit 小于长期限制

3. **性能考虑**：
   - 将最可能触发的规则设置为高优先级
   - 避免设置过多规则（建议不超过 5 个）

## �📋 限流策略详解

### Policy 枚举值

| 策略 | 说明 | PolicyKey 要求 |
|------|------|----------------|
| `Policy.Ip` | 基于客户端 IP 地址限流 | 不需要 |
| `Policy.UserIdentity` | 基于用户身份限流 | 不需要 |
| `Policy.Header` | 基于请求头限流 | 需要指定 Header 键名 |
| `Policy.Query` | 基于查询参数限流 | 需要指定查询参数键名 |
| `Policy.RequestPath` | 基于请求路径限流 | 不需要 |
| `Policy.Cookie` | 基于 Cookie 限流 | 需要指定 Cookie 键名 |
| `Policy.Form` | 基于表单数据限流 | 需要指定表单字段键名 |

### RateValve 属性

| 属性 | 类型 | 说明 | 默认值 |
|------|------|------|--------|
| `Policy` | `Policy` | 限流策略 | `Policy.Ip` |
| `PolicyKey` | `string` | 策略键（某些策略需要） | `null` |
| `Limit` | `int` | 限制次数 | `1` |
| `Duration` | `int` | 时间窗口（秒） | `60` |
| `Priority` | `int` | 优先级（数值越大优先级越高） | `0` |
| `WhenNull` | `WhenNull` | 识别值为空时的处理方式 | `WhenNull.Pass` |
| `ReturnType` | `ReturnType` | 返回数据格式 | `ReturnType.Json_DResult` |

## 🎯 使用示例

### 示例 1：API 接口限流

```csharp
[ApiController]
[Route("api/[controller]")]
public class ProductController : ControllerBase
{
    // 每个 IP 每分钟最多访问 30 次
    [HttpGet]
    [RateValve(Policy = Policy.Ip, Limit = 30, Duration = 60)]
    public async Task<IActionResult> GetProducts()
    {
        // 业务逻辑
        return Ok(await GetProductsAsync());
    }

    // 认证用户每分钟最多创建 5 个产品
    [HttpPost]
    [RateValve(Policy = Policy.UserIdentity, Limit = 5, Duration = 60)]
    public async Task<IActionResult> CreateProduct([FromBody] Product product)
    {
        // 业务逻辑
        return Ok(await CreateProductAsync(product));
    }
}
```

### 示例 2：基于 API Key 的限流

```csharp
[HttpGet("premium")]
[RateValve(Policy = Policy.Header, PolicyKey = "X-API-Key", Limit = 1000, Duration = 3600)]
public IActionResult GetPremiumData()
{
    return Ok("Premium data");
}
```

### 示例 3：基于 Cookie 的会话限流

```csharp
[HttpGet("user-dashboard")]
[RateValve(Policy = Policy.Cookie, PolicyKey = "session_id", Limit = 100, Duration = 3600)]
public IActionResult GetUserDashboard()
{
    // 基于会话 Cookie 进行限流，防止同一会话过度访问
    return Ok("Dashboard data");
}

[HttpPost("vote")]
[RateValve(Policy = Policy.Cookie, PolicyKey = "user_token", Limit = 1, Duration = 86400)] // 每天只能投票一次
public IActionResult Vote([FromBody] VoteRequest request)
{
    // 基于用户 Cookie 限制投票频率
    return Ok("Vote recorded");
}
```

### 示例 4：基于表单数据的限流

```csharp
[HttpPost("send-message")]
[RateValve(Policy = Policy.Form, PolicyKey = "sender_id", Limit = 10, Duration = 3600)]
public IActionResult SendMessage([FromForm] string sender_id, [FromForm] string message)
{
    // 基于表单中的发送者ID进行限流
    return Ok("Message sent");
}

[HttpPost("submit-feedback")]
[RateValve(Policy = Policy.Form, PolicyKey = "email", Limit = 3, Duration = 86400)]
public IActionResult SubmitFeedback([FromForm] string email, [FromForm] string feedback)
{
    // 基于邮箱地址限制反馈提交频率
    return Ok("Feedback submitted");
}
```

### 示例 5：多层限流策略

```csharp
[HttpPost("upload")]
// 全局 IP 限制：每小时 100 次
[RateValve(Policy = Policy.Ip, Limit = 100, Duration = 3600, Priority = 1)]
// 用户限制：每小时 50 次
[RateValve(Policy = Policy.UserIdentity, Limit = 50, Duration = 3600, Priority = 2)]
// 短期突发限制：每分钟 5 次
[RateValve(Policy = Policy.UserIdentity, Limit = 5, Duration = 60, Priority = 3)]
public async Task<IActionResult> UploadFile(IFormFile file)
{
    // 文件上传逻辑
    return Ok();
}
```

### 示例 6：Cookie 和 Form 混合策略

```csharp
[HttpPost("purchase")]
// 基于会话限制：每个会话每小时最多购买 5 次
[RateValve(Policy = Policy.Cookie, PolicyKey = "session_id", Limit = 5, Duration = 3600, Priority = 3)]
// 基于用户ID限制：每个用户每天最多购买 20 次
[RateValve(Policy = Policy.Form, PolicyKey = "user_id", Limit = 20, Duration = 86400, Priority = 2)]
// 基于IP限制：每个IP每小时最多 100 次购买请求
[RateValve(Policy = Policy.Ip, Limit = 100, Duration = 3600, Priority = 1)]
public IActionResult Purchase([FromForm] string user_id, [FromForm] decimal amount)
{
    // 购买逻辑
    return Ok("Purchase successful");
}
```

### 示例 7：自定义黑白名单实现

由于框架暂未内置黑白名单功能，可以在业务代码中自行实现：

```csharp
[HttpGet("sensitive")]
[RateValve(Policy = Policy.Ip, Limit = 100, Duration = 3600)]
public IActionResult GetSensitiveData()
{
    var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();

    // 自定义黑名单检查
    if (IsInBlackList(clientIp))
    {
        return StatusCode(403, "IP被禁止访问");
    }

    // 自定义白名单检查（如果需要）
    var adminToken = Request.Headers["X-Admin-Token"].FirstOrDefault();
    if (!IsValidAdminToken(adminToken))
    {
        return StatusCode(403, "需要有效的管理员Token");
    }

    return Ok("Sensitive data");
}

private bool IsInBlackList(string ip)
{
    // 实现黑名单检查逻辑
    var blackList = new[] { "192.168.1.100", "10.0.0.50" };
    return blackList.Contains(ip);
}

private bool IsValidAdminToken(string token)
{
    // 实现白名单Token验证逻辑
    var validTokens = new[] { "admin_token_123", "super_admin_456" };
    return validTokens.Contains(token);
}
```

## ⚙️ 高级配置

### 自定义用户身份识别

```csharp
services.AddRateLimter(options =>
{
    // 自定义用户身份获取逻辑
    options.OnUserIdentity = context =>
    {
        // 优先从 JWT Token 中获取用户 ID
        var userId = context.User?.FindFirst("user_id")?.Value;
        if (!string.IsNullOrEmpty(userId))
            return userId;

        // 备选方案：从请求头获取
        return context.Request.Headers["X-User-ID"].FirstOrDefault();
    };
});
```

### 自定义 IP 地址获取

```csharp
services.AddRateLimter(options =>
{
    options.OnIpAddress = context =>
    {
        // 支持负载均衡器的真实 IP 获取
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        return context.Connection.RemoteIpAddress?.ToString();
    };
});
```

### 自定义拦截响应

```csharp
services.AddRateLimter(options =>
{
    options.onIntercepted = (context, valve, where) =>
    {
        // 记录限流日志
        var logger = context.RequestServices.GetService<ILogger<Program>>();
        logger?.LogWarning($"Rate limit exceeded for {context.Connection.RemoteIpAddress}");

        // 设置响应头
        context.Response.Headers.Add("X-RateLimit-Limit", valve.Limit.ToString());
        context.Response.Headers.Add("X-RateLimit-Remaining", "0");
        context.Response.StatusCode = 429;

        // 返回自定义错误信息
        return new JsonResult(new
        {
            error = "rate_limit_exceeded",
            message = "请求过于频繁，请稍后重试",
            retry_after = valve.Duration
        });
    };
});
```

## 🔧 配置选项

### WhenNull 处理方式

当策略识别值为空时的处理方式：

- `WhenNull.Pass`：通过请求，跳过限流检查（默认）
- `WhenNull.Intercept`：拦截请求，继续进行限流检查

#### WhenNull 详细说明

| 枚举值 | 行为 | 使用场景 | 示例 |
|--------|------|----------|------|
| `WhenNull.Pass` | 策略值为空时跳过此限流规则 | 可选认证、宽松限制 | 游客访问、可选API Key |
| `WhenNull.Intercept` | 策略值为空时仍进行限流检查 | 强制认证、严格限制 | 必须登录、必须提供Token |

#### WhenNull 使用示例

##### 示例1：可选Cookie认证（宽松模式）

```csharp
[HttpGet("public-api")]
[RateValve(
    Policy = Policy.Cookie,
    PolicyKey = "user_token",
    Limit = 100,
    Duration = 3600,
    WhenNull = WhenNull.Pass  // Cookie不存在时跳过限流，允许游客访问
)]
public IActionResult GetPublicData()
{
    return Ok("Public data");
}
```

##### 示例2：必须提供认证（严格模式）

```csharp
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
    return Ok("Secure action completed");
}
```

##### 示例3：用户身份回退策略

```csharp
[HttpGet("user-data")]
// 优先基于用户ID限流
[RateValve(
    Policy = Policy.UserIdentity,
    Limit = 1000,
    Duration = 3600,
    WhenNull = WhenNull.Pass,  // 未登录用户跳过此规则
    Priority = 2
)]
// 回退到IP限流（更严格）
[RateValve(
    Policy = Policy.Ip,
    Limit = 100,
    Duration = 3600,
    Priority = 1
)]
public IActionResult GetUserData()
{
    return Ok("User data");
}
```

##### 示例4：多重验证策略

```csharp
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
    return Ok("Admin action completed");
}
```

#### WhenNull 工作流程

```text
1. 获取策略值（如Cookie、Header等）
   ├─ 策略值不为空 → 继续限流检查
   └─ 策略值为空
       ├─ WhenNull.Pass → 跳过此限流规则，检查下一个规则
       └─ WhenNull.Intercept → 继续限流检查（通常会因为空值而触发限制）
```

#### WhenNull 最佳实践

1. **可选认证场景**：使用 `WhenNull.Pass`
   - 游客和登录用户都可以访问的API
   - 可选的API Key验证

2. **强制认证场景**：使用 `WhenNull.Intercept`
   - 必须登录才能访问的API
   - 必须提供有效Token的API

3. **分层限流策略**：
   - 高优先级规则使用 `WhenNull.Pass`（宽松）
   - 低优先级规则作为兜底（严格）

4. **调试建议**：
   - 开发环境可以使用 `WhenNull.Pass` 便于测试
   - 生产环境根据安全需求选择合适的策略

### ReturnType 返回格式

- `ReturnType.Json_DGResult`：返回 DGResult 格式
- `ReturnType.Json_DResult`：返回 DResult 格式（默认）
- `ReturnType.Text`：返回纯文本格式

## 🏗️ 架构设计

### 核心组件

1. **RateLimitProcessor**：限流处理器，负责计数和验证
2. **IRateLimitStore**：存储接口，支持缓存和持久化
3. **ApiThrottleActionFilter**：Action 过滤器，拦截请求
4. **AsyncKeyLock**：异步键锁，确保并发安全

### 存储机制

默认使用内存缓存存储限流计数器，支持：

- 自动过期清理
- 高并发读写
- 分布式部署（通过 Redis）

## 🚨 注意事项

1. **时间窗口**：Duration 参数以秒为单位，建议根据业务需求合理设置
2. **优先级**：多个限流策略时，优先级高的先执行
3. **性能考虑**：大量并发时建议使用 Redis 作为存储后端
4. **IP 获取**：确保正确配置负载均衡器的 IP 转发头
5. **用户身份**：确保用户认证中间件在限流中间件之前注册
6. **WhenNull 策略**：
   - `WhenNull.Pass`：策略值为空时跳过限流检查，适用于可选认证场景
   - `WhenNull.Intercept`：策略值为空时仍进行限流检查，适用于强制认证场景
   - 合理设置 WhenNull 可以实现灵活的认证和限流策略
7. **Cookie 策略**：
   - 确保客户端发送了指定的 Cookie
   - Cookie 值为空时根据 `WhenNull` 设置处理
   - 适用于会话级别的限流控制
8. **Form 策略**：
   - 仅适用于 POST 请求的表单数据
   - 确保表单包含指定的字段名
   - 适用于基于用户输入的限流控制
9. **异常处理**：
   - 框架内置了完善的异常处理机制
   - 当策略值获取失败时，会自动触发 WhenNull 逻辑
   - 无效的IP地址、不存在的Form数据等都会被安全处理
10. **策略组合**：
    - 可以使用多个不同的策略和 WhenNull 设置实现复杂的限流逻辑
    - 建议将宽松的规则设置为高优先级，严格的规则作为兜底
11. **缓存键唯一性**：
    - WhenNull.Intercept 模式下，空值会基于客户端IP生成唯一标识
    - 确保不同用户不会意外共享限流计数器

## 📄 许可证

本项目采用 [MIT](LICENSE) 许可证。

## 🤝 贡献

欢迎提交 Issue 和 Pull Request！

## 📞 支持

- 官网：<https://www.yuanrenyi.com/>
- 仓库：<https://github.com/PeiKeSmart/DH.RateLimter>
- 公司：湖北登灏科技有限公司

---

© 2020-2025 湖北登灏科技有限公司