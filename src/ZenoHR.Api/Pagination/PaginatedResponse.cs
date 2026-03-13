// REQ-OPS-001, VUL-027: Pagination support for list endpoints.
// Provides a standard paginated response wrapper and query parameter defaults.

namespace ZenoHR.Api.Pagination;

/// <summary>
/// Standard paginated response wrapper for list endpoints.
/// VUL-027: Prevents unbounded result sets that could cause memory exhaustion or slow responses.
/// </summary>
/// <typeparam name="T">The type of items in the response.</typeparam>
public sealed record PaginatedResponse<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Skip,
    int Take)
{
    /// <summary>Whether there are more items beyond the current page.</summary>
    public bool HasMore => Skip + Take < TotalCount;
}

/// <summary>
/// Pagination defaults and validation helpers.
/// </summary>
// VUL-027: Enforces maximum page size to prevent resource exhaustion.
public static class PaginationDefaults
{
    /// <summary>Default number of items per page.</summary>
    public const int DefaultTake = 50;

    /// <summary>Maximum number of items per page — prevents resource exhaustion.</summary>
    public const int MaxTake = 200;

    /// <summary>
    /// Normalises skip and take parameters to safe values.
    /// Skip is clamped to >= 0. Take is clamped to [1, MaxTake].
    /// </summary>
    public static (int Skip, int Take) Normalise(int? skip, int? take)
    {
        var safeSkip = Math.Max(0, skip ?? 0);
        var safeTake = Math.Clamp(take ?? DefaultTake, 1, MaxTake);
        return (safeSkip, safeTake);
    }

    /// <summary>
    /// Applies pagination to an in-memory collection and returns a <see cref="PaginatedResponse{T}"/>.
    /// </summary>
    public static PaginatedResponse<T> Apply<T>(
        IReadOnlyList<T> items, int? skip, int? take)
    {
        var (safeSkip, safeTake) = Normalise(skip, take);
        var totalCount = items.Count;
        var page = items.Skip(safeSkip).Take(safeTake).ToList().AsReadOnly();
        return new PaginatedResponse<T>(page, totalCount, safeSkip, safeTake);
    }
}
