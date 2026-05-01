using System;

namespace DH.RateLimter
{
    /// <summary>
    /// 阀门
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public abstract class Valve : Attribute
    {
        /// <summary>
        /// 策略
        /// </summary>
        public Policy Policy { set; get; } = Policy.Ip;

        /// <summary>
        /// 策略Key
        /// </summary>
        /// <remarks>
        /// Policy == Policy.Header是，PolicyKey指定为对应Header的key
        /// Policy == Policy.Query是，PolicyKey指定为对应Query的key
        /// </remarks>
        public string PolicyKey { set; get; }

        /// <summary>
        /// 当识别值为空时处理方式
        /// </summary>
        /// <remarks>
        /// 默认值为 WhenNull.Pass。
        /// WhenNull.Pass 只会跳过当前这条限流规则，不会直接放行整个请求；同一方法上的其他规则仍会继续执行。
        /// WhenNull.Intercept 会为当前请求生成空值标识并继续参与限流，适合 Cookie、Header、Query、Form 等缺失时仍需要兜底限流的场景。
        /// </remarks>
        public WhenNull WhenNull { set; get; } = WhenNull.Pass;

        /// <summary>
        /// 优先级
        /// </summary>
        public int Priority { set; get; }

        /// <summary>
        /// 返回数据格式
        /// </summary>
        public ReturnType ReturnType { get; set; } = ReturnType.Json_DResult;
    }

    /// <summary>
    /// 频率阀门
    /// </summary>
    public class RateValve : Valve
    {
        /// <summary>
        /// 限制次数
        /// </summary>
        public int Limit { set; get; } = 1;

        /// <summary>
        /// 计时间隔(单位：秒)
        /// </summary>
        public int Duration { set; get; } = 60;

    }

    // 注意：黑白名单功能暂未实现
    // 如需黑白名单功能，建议在业务代码中自行实现
}
