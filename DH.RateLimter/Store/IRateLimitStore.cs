using System;
using System.Threading;
using System.Threading.Tasks;

namespace DH.RateLimter.Store
{
    public interface IRateLimitStore<T>
    {
        Task<bool> ExistsAsync(string id, CancellationToken cancellationToken = default);

        Task<T> GetAsync(string id, CancellationToken cancellationToken = default);

        Task RemoveAsync(string id, CancellationToken cancellationToken = default);

        Task SetAsync(string id, T entry, TimeSpan? expirationTime = null, CancellationToken cancellationToken = default);

        /// <summary>原子性递增计数器，并在首次创建时设置过期时间</summary>
        /// <param name="id">键名</param>
        /// <param name="expirationTime">仅在键首次创建时设置的过期时间</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>递增后的计数值</returns>
        Task<Int64> IncrementAsync(String id, TimeSpan expirationTime, CancellationToken cancellationToken = default);
    }
}
