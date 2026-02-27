// REQ-OPS-002: Domain events — modules communicate ONLY via MediatR domain events.
// No module may directly call another module's services or repositories.
// All domain events carry TenantId for multi-tenant isolation.

using MediatR;

namespace ZenoHR.Domain.Events;

/// <summary>
/// Base record for all ZenoHR domain events.
/// Domain events are raised AFTER a state change is persisted (post-commit).
/// Handlers react asynchronously via MediatR INotificationHandler.
/// <para>
/// Naming convention: [Aggregate][PastTense]Event
/// Examples: EmployeeCreatedEvent, PayrollRunFinalizedEvent, LeaveRequestApprovedEvent.
/// </para>
/// </summary>
public abstract record DomainEvent : INotification
{
    /// <summary>Unique event identifier. Use UUIDv7 for time-ordered correlation.</summary>
    public Guid EventId { get; } = Guid.NewGuid();

    /// <summary>UTC timestamp when the domain event occurred (i.e., when the change was committed).</summary>
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Tenant that this event belongs to.
    /// MUST be propagated to all downstream handlers and audit records.
    /// </summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>
    /// The authenticated user who triggered the action that raised this event.
    /// Matches <c>firebase_uid</c> on the actor's employee/user record.
    /// </summary>
    public string ActorId { get; init; } = string.Empty;
}
