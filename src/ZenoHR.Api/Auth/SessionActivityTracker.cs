// REQ-SEC-004: Session activity tracker — records and retrieves last activity timestamps per user.
// VUL-013: Provides the backing store for idle session timeout enforcement.
// Uses IMemoryCache with sliding expiration to automatically evict stale entries.

using Microsoft.Extensions.Caching.Memory;

namespace ZenoHR.Api.Auth;

/// <summary>
/// Tracks the last activity timestamp for each authenticated user session.
/// Used by <see cref="SessionTimeoutMiddleware"/> to enforce idle timeout policies.
/// </summary>
/// <remarks>
/// Backed by <see cref="IMemoryCache"/> with a sliding expiration equal to the standard
/// idle timeout. Entries are automatically evicted when the user has been idle beyond
/// the maximum timeout window, preventing unbounded memory growth.
/// </remarks>
public sealed class SessionActivityTracker
{
    private readonly IMemoryCache _cache;

    private static readonly TimeSpan SlidingExpiration =
        TimeSpan.FromMinutes(SessionPolicy.StandardIdleTimeoutMinutes + 5);

    /// <summary>Cache key prefix to namespace session activity entries.</summary>
    private const string KeyPrefix = "session:activity:";

    /// <summary>
    /// Initialises a new instance of <see cref="SessionActivityTracker"/>.
    /// </summary>
    /// <param name="cache">The memory cache instance (registered via <c>AddMemoryCache()</c>).</param>
    public SessionActivityTracker(IMemoryCache cache)
    {
        ArgumentNullException.ThrowIfNull(cache);
        _cache = cache;
    }

    /// <summary>
    /// Records the user's latest activity timestamp.
    /// </summary>
    /// <param name="userId">The Firebase UID or unique user identifier.</param>
    /// <param name="timestamp">The UTC timestamp of the activity.</param>
    public void RecordActivity(string userId, DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var options = new MemoryCacheEntryOptions
        {
            SlidingExpiration = SlidingExpiration,
        };

        _cache.Set(KeyPrefix + userId, timestamp, options);
    }

    /// <summary>
    /// Retrieves the last recorded activity timestamp for the specified user.
    /// </summary>
    /// <param name="userId">The Firebase UID or unique user identifier.</param>
    /// <returns>
    /// The <see cref="DateTimeOffset"/> of the last activity, or <c>null</c> if no activity
    /// has been recorded (first request or cache entry expired).
    /// </returns>
    public DateTimeOffset? GetLastActivity(string userId)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return _cache.TryGetValue<DateTimeOffset>(KeyPrefix + userId, out var timestamp)
            ? timestamp
            : null;
    }
}
