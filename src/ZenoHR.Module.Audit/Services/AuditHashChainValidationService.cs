// REQ-OPS-004: AuditHashChainValidationService — walks the entire audit event chain for a tenant
// and verifies integrity of every hash link. Critical for tamper detection.
// CTL-POPIA-012: Hash chain integrity proves PII access logs have not been modified.
// A broken chain is a Sev-1 defect and must trigger an incident response.

using ZenoHR.Domain.Errors;
using ZenoHR.Module.Audit.Domain;

namespace ZenoHR.Module.Audit.Services;

/// <summary>
/// Validates the integrity of the full audit event hash chain for a tenant.
/// <para>
/// This service is designed to be invoked by:
/// <list type="bullet">
///   <item>A scheduled background job (daily integrity check).</item>
///   <item>The compliance verification workflow before generating evidence packs.</item>
///   <item>The audit trail UI health check indicator.</item>
/// </list>
/// </para>
/// <para>
/// It walks all audit events in chronological order, verifying:
/// <list type="number">
///   <item>Each event's stored hash matches the recomputed hash of its canonical JSON.</item>
///   <item>The genesis event has no previous hash.</item>
///   <item>Each subsequent event's previous_event_hash matches the prior event's event_hash.</item>
///   <item>The total verified count matches the expected count from chain metadata.</item>
/// </list>
/// </para>
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Instance methods for DI compatibility")]
public sealed class AuditHashChainValidationService
{
    /// <summary>
    /// Callback signature for fetching audit events in chronological order (oldest first).
    /// The service is decoupled from infrastructure — the caller provides the data access function.
    /// Parameters: (tenantId, offset, pageSize, cancellationToken) => events.
    /// </summary>
    public delegate Task<IReadOnlyList<AuditEvent>> FetchEventsDelegate(
        string tenantId, int offset, int pageSize, CancellationToken ct);

    private const int DefaultPageSize = 500;

    /// <summary>
    /// Validates the full hash chain for a tenant by walking all events in chronological order.
    /// REQ-OPS-004: A broken chain is a Sev-1 defect.
    /// </summary>
    /// <param name="tenantId">Tenant whose chain to validate.</param>
    /// <param name="fetchEvents">
    /// Function that fetches events from the repository in chronological order.
    /// Accepts (tenantId, offset, pageSize, ct) and returns a page of events.
    /// </param>
    /// <param name="expectedEventCount">
    /// Optional expected total count (from chain metadata). If provided, a mismatch
    /// after walking all events produces a gap detection warning.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="ChainValidationReport"/> describing the outcome.</returns>
    public async Task<ChainValidationReport> ValidateFullChainAsync(
        string tenantId,
        FetchEventsDelegate fetchEvents,
        long? expectedEventCount = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(fetchEvents);

        var totalVerified = 0;
        string? lastVerifiedHash = null;
        var startedAt = DateTimeOffset.UtcNow;

        var offset = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var page = await fetchEvents(tenantId, offset, DefaultPageSize, ct);
            if (page.Count == 0)
                break;

            // Verify within this page, connecting to the previous page's last hash
            for (var i = 0; i < page.Count; i++)
            {
                var current = page[i];

                // Invariant 1: stored hash must match recomputed hash
                if (!current.VerifyHash())
                {
                    return ChainValidationReport.Broken(
                        tenantId, totalVerified, current.EventId,
                        $"Event {current.EventId} at index {totalVerified}: stored hash does not match recomputed hash (tampered).",
                        startedAt);
                }

                if (totalVerified == 0)
                {
                    // Invariant 2: genesis event must have no previous hash
                    if (current.PreviousEventHash is not (null or ""))
                    {
                        return ChainValidationReport.Broken(
                            tenantId, 0, current.EventId,
                            $"Genesis event {current.EventId} has a non-null PreviousEventHash — chain origin is invalid.",
                            startedAt);
                    }
                }
                else
                {
                    // Invariant 3: PreviousEventHash must match the preceding event's EventHash
                    if (!string.Equals(current.PreviousEventHash, lastVerifiedHash, StringComparison.OrdinalIgnoreCase))
                    {
                        return ChainValidationReport.Broken(
                            tenantId, totalVerified, current.EventId,
                            $"Event {current.EventId} at index {totalVerified}: PreviousEventHash does not match prior event's hash. " +
                            $"Expected '{lastVerifiedHash}', got '{current.PreviousEventHash}'.",
                            startedAt);
                    }
                }

                lastVerifiedHash = current.EventHash;
                totalVerified++;
            }

            offset += page.Count;

            // If we got fewer than a full page, we've reached the end
            if (page.Count < DefaultPageSize)
                break;
        }

        // Invariant 4: if expected count was provided, verify no gaps
        bool hasGap = expectedEventCount.HasValue && totalVerified != expectedEventCount.Value;
        string? gapWarning = hasGap
            ? $"Expected {expectedEventCount} events but verified {totalVerified} — possible gap or deletion detected."
            : null;

        return ChainValidationReport.Intact(tenantId, totalVerified, lastVerifiedHash, gapWarning, startedAt);
    }
}

/// <summary>
/// Report produced by <see cref="AuditHashChainValidationService.ValidateFullChainAsync"/>.
/// </summary>
/// <param name="TenantId">Tenant whose chain was validated.</param>
/// <param name="IsIntact">Whether the entire chain is intact and unmodified.</param>
/// <param name="EventsVerified">Total number of events successfully verified.</param>
/// <param name="FirstBrokenEventId">Event ID where the chain first broke, or <c>null</c> if intact.</param>
/// <param name="LastVerifiedHash">Hash of the last successfully verified event, or <c>null</c> if empty chain.</param>
/// <param name="BreakDescription">Human-readable description of the break, or <c>null</c> if intact.</param>
/// <param name="GapWarning">Warning if event count doesn't match expected count, or <c>null</c>.</param>
/// <param name="ValidatedAt">Timestamp when validation started.</param>
/// <param name="Duration">Time taken to complete the validation.</param>
public sealed record ChainValidationReport(
    string TenantId,
    bool IsIntact,
    int EventsVerified,
    string? FirstBrokenEventId,
    string? LastVerifiedHash,
    string? BreakDescription,
    string? GapWarning,
    DateTimeOffset ValidatedAt,
    TimeSpan Duration)
{
    internal static ChainValidationReport Intact(
        string tenantId, int eventsVerified, string? lastHash, string? gapWarning, DateTimeOffset startedAt)
        => new(tenantId, IsIntact: true, eventsVerified, FirstBrokenEventId: null,
            LastVerifiedHash: lastHash, BreakDescription: null, GapWarning: gapWarning,
            ValidatedAt: startedAt, Duration: DateTimeOffset.UtcNow - startedAt);

    internal static ChainValidationReport Broken(
        string tenantId, int eventsVerified, string brokenEventId, string description, DateTimeOffset startedAt)
        => new(tenantId, IsIntact: false, eventsVerified, FirstBrokenEventId: brokenEventId,
            LastVerifiedHash: null, BreakDescription: description, GapWarning: null,
            ValidatedAt: startedAt, Duration: DateTimeOffset.UtcNow - startedAt);
}
