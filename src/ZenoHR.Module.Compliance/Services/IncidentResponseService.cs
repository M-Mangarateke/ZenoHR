// CTL-POPIA-012, REQ-SEC-009: Incident response & containment lifecycle service.
// POPIA §19 requires defined incident response procedures with documented evidence at each step.

using System.Globalization;
using ZenoHR.Domain.Errors;
using ZenoHR.Module.Compliance.Models;

namespace ZenoHR.Module.Compliance.Services;

/// <summary>
/// Service for managing the incident response lifecycle.
/// Enforces a forward-only state machine:
/// Detected→Classified→Contained→Investigating→Recovered→PostReview→Closed.
/// Each transition produces a new immutable <see cref="IncidentResponse"/> record.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Instance methods for DI compatibility")]
public sealed class IncidentResponseService
{
    private static int _counter;

    /// <summary>
    /// Classify a detected security incident — creates a new incident response record.
    /// Transitions from Detected → Classified.
    /// </summary>
    public Result<IncidentResponse> ClassifyIncident(
        string tenantId,
        string securityIncidentId,
        IncidentSeverityLevel severity,
        string classifiedBy,
        DateTimeOffset timestamp)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return Result<IncidentResponse>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "TenantId is required.");

        if (string.IsNullOrWhiteSpace(securityIncidentId))
            return Result<IncidentResponse>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "SecurityIncidentId is required.");

        if (severity == IncidentSeverityLevel.Unknown)
            return Result<IncidentResponse>.Failure(ZenoHrErrorCode.ValidationFailed, "Severity must be specified (not Unknown).");

        if (string.IsNullOrWhiteSpace(classifiedBy))
            return Result<IncidentResponse>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "ClassifiedBy is required.");

        var seq = Interlocked.Increment(ref _counter);
        var incidentId = string.Format(CultureInfo.InvariantCulture, "IR-{0}-{1:D4}", timestamp.Year, seq);

        var response = new IncidentResponse
        {
            IncidentId = incidentId,
            TenantId = tenantId,
            SecurityIncidentId = securityIncidentId,
            Severity = severity,
            Status = IncidentResponseStatus.Classified,
            ClassifiedBy = classifiedBy,
            ClassifiedAt = timestamp,
        };

        return Result<IncidentResponse>.Success(response);
    }

    /// <summary>
    /// Record a containment action — adds the action and transitions to Contained.
    /// Valid from Classified or Contained (multiple containment actions allowed).
    /// </summary>
    public Result<IncidentResponse> RecordContainment(
        IncidentResponse existing,
        ContainmentAction action)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(action);

        if (existing.Status is not (IncidentResponseStatus.Classified or IncidentResponseStatus.Contained))
        {
            return Result<IncidentResponse>.Failure(
                ZenoHrErrorCode.InvalidIncidentStatusTransition,
                string.Format(CultureInfo.InvariantCulture,
                    "Cannot record containment for a response in {0} status. Must be Classified or Contained.",
                    existing.Status));
        }

        var updatedActions = new List<ContainmentAction>(existing.ContainmentActions) { action };

        var updated = existing with
        {
            Status = IncidentResponseStatus.Contained,
            ContainmentActions = updatedActions.AsReadOnly(),
        };

        return Result<IncidentResponse>.Success(updated);
    }

    /// <summary>
    /// Start investigation — transitions from Contained → Investigating.
    /// </summary>
    public Result<IncidentResponse> StartInvestigation(
        IncidentResponse existing,
        string investigatorId)
    {
        ArgumentNullException.ThrowIfNull(existing);

        if (existing.Status != IncidentResponseStatus.Contained)
        {
            return Result<IncidentResponse>.Failure(
                ZenoHrErrorCode.InvalidIncidentStatusTransition,
                string.Format(CultureInfo.InvariantCulture,
                    "Cannot start investigation for a response in {0} status. Must be Contained.",
                    existing.Status));
        }

        if (string.IsNullOrWhiteSpace(investigatorId))
            return Result<IncidentResponse>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "InvestigatorId is required.");

        var updated = existing with
        {
            Status = IncidentResponseStatus.Investigating,
            InvestigationNotes = string.Format(CultureInfo.InvariantCulture, "Investigation started by {0}.", investigatorId),
        };

        return Result<IncidentResponse>.Success(updated);
    }

    /// <summary>
    /// Record recovery — transitions from Investigating → Recovered.
    /// </summary>
    public Result<IncidentResponse> RecordRecovery(
        IncidentResponse existing,
        string recoveredBy,
        string notes,
        DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(existing);

        if (existing.Status != IncidentResponseStatus.Investigating)
        {
            return Result<IncidentResponse>.Failure(
                ZenoHrErrorCode.InvalidIncidentStatusTransition,
                string.Format(CultureInfo.InvariantCulture,
                    "Cannot record recovery for a response in {0} status. Must be Investigating.",
                    existing.Status));
        }

        if (string.IsNullOrWhiteSpace(recoveredBy))
            return Result<IncidentResponse>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "RecoveredBy is required.");

        var updated = existing with
        {
            Status = IncidentResponseStatus.Recovered,
            RecoveredBy = recoveredBy,
            RecoveredAt = timestamp,
            InvestigationNotes = string.IsNullOrWhiteSpace(notes)
                ? existing.InvestigationNotes
                : existing.InvestigationNotes + " " + notes,
        };

        return Result<IncidentResponse>.Success(updated);
    }

    /// <summary>
    /// Submit post-review — transitions from Recovered → PostReview.
    /// </summary>
    public Result<IncidentResponse> SubmitPostReview(
        IncidentResponse existing,
        string reviewNotes,
        string reviewedBy,
        DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(existing);

        if (existing.Status != IncidentResponseStatus.Recovered)
        {
            return Result<IncidentResponse>.Failure(
                ZenoHrErrorCode.InvalidIncidentStatusTransition,
                string.Format(CultureInfo.InvariantCulture,
                    "Cannot submit post-review for a response in {0} status. Must be Recovered.",
                    existing.Status));
        }

        if (string.IsNullOrWhiteSpace(reviewedBy))
            return Result<IncidentResponse>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "ReviewedBy is required.");

        var updated = existing with
        {
            Status = IncidentResponseStatus.PostReview,
            PostReviewNotes = reviewNotes,
            ReviewedBy = reviewedBy,
            ReviewedAt = timestamp,
        };

        return Result<IncidentResponse>.Success(updated);
    }

    /// <summary>
    /// Close incident — transitions from PostReview → Closed.
    /// </summary>
    public Result<IncidentResponse> CloseIncident(
        IncidentResponse existing,
        string closedBy,
        DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(existing);

        if (existing.Status != IncidentResponseStatus.PostReview)
        {
            return Result<IncidentResponse>.Failure(
                ZenoHrErrorCode.InvalidIncidentStatusTransition,
                string.Format(CultureInfo.InvariantCulture,
                    "Cannot close a response in {0} status. Must be PostReview.",
                    existing.Status));
        }

        if (string.IsNullOrWhiteSpace(closedBy))
            return Result<IncidentResponse>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "ClosedBy is required.");

        var updated = existing with
        {
            Status = IncidentResponseStatus.Closed,
            ClosedBy = closedBy,
            ClosedAt = timestamp,
        };

        return Result<IncidentResponse>.Success(updated);
    }
}
