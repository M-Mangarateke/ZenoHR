// REQ-SEC-005: ITenantScoped — marker interface for all tenant-isolated commands and queries.
// All MediatR requests that operate on tenant data must implement this interface.
// The ValidationBehaviour (and future TenantIsolationBehaviour) use this to enforce
// that tenant context is always present and propagated.

namespace ZenoHR.Domain.Contracts;

/// <summary>
/// Marker interface for all MediatR commands and queries that are scoped to a specific tenant.
/// <para>
/// Every command/query that touches tenant data MUST implement this interface.
/// Pipeline behaviours use <see cref="TenantId"/> to enforce multi-tenant isolation.
/// </para>
/// </summary>
public interface ITenantScoped
{
    /// <summary>
    /// The tenant this request is scoped to.
    /// Set from the authenticated user's claims before dispatching via MediatR.
    /// </summary>
    string TenantId { get; }
}
