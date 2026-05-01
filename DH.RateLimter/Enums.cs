namespace DH.RateLimter
{
    /// <summary>
    /// 节流策略
    /// </summary>
    /// <remarks>修改时请务必同步修改HttpContextExtension=>GetPolicyValue方法</remarks>
    public enum Policy : short
    {
        /// <summary>
        /// IP地址
        /// </summary>
        Ip = 1,

        /// <summary>
        /// 用户身份
        /// </summary>
        UserIdentity = 2,

        /// <summary>
        /// Request Header
        /// </summary>
        Header = 3,

        /// <summary>
        /// Request Query
        /// </summary>
        Query = 4,

        /// <summary>
        /// 网址 Request path
        /// </summary>
        RequestPath = 5,

        /// <summary>
        /// Cookie
        /// </summary>
        Cookie = 6,

        /// <summary>
        /// Request Form
        /// </summary>
        Form = 7
    }

    /// <summary>
    /// 当识别值为空时处理方式
    /// </summary>
    public enum WhenNull : short
    {
        /// <summary>
        /// 跳过当前规则
        /// </summary>
        /// <remarks>仅当前规则不参与限流，不代表整个请求直接通过；同一方法上的其他规则仍会继续执行。</remarks>
        Pass = 0,

        /// <summary>
        /// 使用空值标识继续限流
        /// </summary>
        /// <remarks>当前规则会继续执行，并使用基于请求特征生成的空值标识参与限流。</remarks>
        Intercept = 1
    }

    /// <summary>
    /// 拦截位置
    /// </summary>
    public enum IntercepteWhere
    {
        ActionFilter,

        PageFilter,

        Middleware
    }

    public enum RosterType : short
    {
        /// <summary>
        /// 黑名单
        /// </summary>
        BlackList = 1,

        /// <summary>
        /// 白名单
        /// </summary>
        WhiteList = 2
    }

    /// <summary>
    /// 返回格式
    /// </summary>
    public enum ReturnType
    {
        Json_DGResult,
        Json_DResult,
        Text
    }
}
