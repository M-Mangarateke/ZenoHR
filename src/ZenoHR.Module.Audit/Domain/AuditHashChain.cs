// REQ-OPS-004: AuditHashChain — verifies the integrity of an ordered sequence of AuditEvents.
// A broken chain indicates tampering. Breaking the chain is a Sev-1 defect.
// Called by the compliance verification job and the audit trail UI health check.

namespace ZenoHR.Module.Audit.Domain;

/// <summary>
/// Verifies the integrity of a hash-chained sequence of <see cref="AuditEvent"/> records.
/// </summary>
public static class AuditHashChain
{
    /// <summary>
    /// Verifies the integrity of an ordered list of <see cref="AuditEvent"/> records.
    /// <para>
    /// The following invariants are checked for each event:
    /// <list type="number">
    ///   <item>The stored <see cref="AuditEvent.EventHash"/> matches the recomputed hash of its canonical JSON.</item>
    ///   <item>The first event has no <see cref="AuditEvent.PreviousEventHash"/> (genesis).</item>
    ///   <item>Each subsequent event's <see cref="AuditEvent.PreviousEventHash"/> matches the preceding event's <see cref="AuditEvent.EventHash"/>.</item>
    /// </list>
    /// </para>
    /// </summary>
    /// <param name="events">
    /// Events in chronological order (oldest first). Must be non-null. May be empty.
    /// All events must belong to the same tenant — the caller is responsible for filtering by <c>tenant_id</c>.
    /// </param>
    /// <returns>A <see cref="AuditChainVerificationResult"/> describing the outcome.</returns>
    public static AuditChainVerificationResult Verify(IReadOnlyList<AuditEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        if (events.Count == 0)
        {
            return new AuditChainVerificationResult(
                IsIntact: true,
                EventsVerified: 0,
                FirstBrokenEventId: null,
                FirstBrokenIndex: null);
        }

        for (var i = 0; i < events.Count; i++)
        {
            var current = events[i];

            // Invariant 1: each event's stored hash must match its recomputed hash.
            if (!current.VerifyHash())
            {
                return new AuditChainVerificationResult(
                    IsIntact: false,
                    EventsVerified: i,
                    FirstBrokenEventId: current.EventId,
                    FirstBrokenIndex: i);
            }

            if (i == 0)
            {
                // Invariant 2: genesis event must have no previous hash (null or empty string).
                if (current.PreviousEventHash is not (null or ""))
                {
                    return new AuditChainVerificationResult(
                        IsIntact: false,
                        EventsVerified: 0,
                        FirstBrokenEventId: current.EventId,
                        FirstBrokenIndex: 0);
                }

                continue;
            }

            // Invariant 3: each subsequent event's PreviousEventHash must equal the prior event's EventHash.
            var previous = events[i - 1];
            if (!string.Equals(current.PreviousEventHash, previous.EventHash, StringComparison.OrdinalIgnoreCase))
            {
                return new AuditChainVerificationResult(
                    IsIntact: false,
                    EventsVerified: i,
                    FirstBrokenEventId: current.EventId,
                    FirstBrokenIndex: i);
            }
        }

        return new AuditChainVerificationResult(
            IsIntact: true,
            EventsVerified: events.Count,
            FirstBrokenEventId: null,
            FirstBrokenIndex: null);
    }
}

/// <summary>
/// Result of a hash chain verification operation.
/// </summary>
/// <param name="IsIntact">Whether the entire chain is intact and unmodified.</param>
/// <param name="EventsVerified">Number of events verified before the check stopped (equals total count when intact).</param>
/// <param name="FirstBrokenEventId">Event ID of the first broken link, or <c>null</c> if the chain is intact.</param>
/// <param name="FirstBrokenIndex">Zero-based index of the first broken event, or <c>null</c> if intact.</param>
public sealed record AuditChainVerificationResult(
    bool IsIntact,
    int EventsVerified,
    string? FirstBrokenEventId,
    int? FirstBrokenIndex);
