// CTL-POPIA-010, CTL-POPIA-011: POPIA breach notification service.
// Manages breach registration, status transitions, overdue detection, and regulator notifications.

using System.Globalization;
using ZenoHR.Domain.Errors;
using ZenoHR.Module.Compliance.Models;

namespace ZenoHR.Module.Compliance.Services;

/// <summary>
/// Service for managing POPIA breach lifecycle — registration, status transitions,
/// 72-hour deadline tracking, and Information Regulator notification generation.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Instance methods for DI compatibility")]
public sealed class BreachNotificationService
{
    /// <summary>Register a new personal information security breach.</summary>
    public Result<BreachRecord> RegisterBreach(
        string tenantId,
        string title,
        string description,
        BreachSeverity severity,
        string discoveredBy,
        string[] affectedDataCategories,
        int estimatedAffectedSubjects,
        string rootCause,
        string[] remediationSteps,
        DateTimeOffset discoveredAt)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return Result<BreachRecord>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "TenantId is required.");

        if (string.IsNullOrWhiteSpace(title))
            return Result<BreachRecord>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "Title is required.");

        if (string.IsNullOrWhiteSpace(description))
            return Result<BreachRecord>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "Description is required.");

        if (severity == BreachSeverity.Unknown)
            return Result<BreachRecord>.Failure(ZenoHrErrorCode.ValidationFailed, "Severity must be specified (not Unknown).");

        if (string.IsNullOrWhiteSpace(discoveredBy))
            return Result<BreachRecord>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "DiscoveredBy is required.");

        if (affectedDataCategories is null || affectedDataCategories.Length == 0)
            return Result<BreachRecord>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "At least one affected data category is required.");

        if (estimatedAffectedSubjects < 0)
            return Result<BreachRecord>.Failure(ZenoHrErrorCode.ValueOutOfRange, "EstimatedAffectedSubjects cannot be negative.");

        var breachId = string.Format(CultureInfo.InvariantCulture, "BRE-{0}-{1}", discoveredAt.Year, Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..8]);

        var record = new BreachRecord
        {
            BreachId = breachId,
            TenantId = tenantId,
            Title = title,
            Description = description,
            Severity = severity,
            Status = BreachStatus.Detected,
            DiscoveredAt = discoveredAt,
            DiscoveredBy = discoveredBy,
            AffectedDataCategories = affectedDataCategories,
            EstimatedAffectedSubjects = estimatedAffectedSubjects,
            RootCause = rootCause,
            RemediationSteps = remediationSteps
        };

        return Result<BreachRecord>.Success(record);
    }

    /// <summary>Advance breach to a new status (forward-only transitions).</summary>
    public Result<BreachRecord> UpdateStatus(BreachRecord existing, BreachStatus newStatus, DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(existing);

        if (newStatus <= existing.Status)
        {
            return Result<BreachRecord>.Failure(
                ZenoHrErrorCode.InvalidBreachStatusTransition,
                string.Format(CultureInfo.InvariantCulture,
                    "Cannot transition from {0} to {1}. Status can only move forward.",
                    existing.Status, newStatus));
        }

        var updated = existing with { Status = newStatus };

        updated = newStatus switch
        {
            BreachStatus.Contained => updated with { ContainedAt = timestamp },
            BreachStatus.RegulatorNotified => updated with { RegulatorNotifiedAt = timestamp },
            BreachStatus.SubjectsNotified => updated with { SubjectsNotifiedAt = timestamp },
            BreachStatus.Closed => updated with { ClosedAt = timestamp },
            _ => updated
        };

        return Result<BreachRecord>.Success(updated);
    }

    /// <summary>Return breaches past the 72-hour POPIA §22 notification deadline.</summary>
    public IReadOnlyList<BreachRecord> GetOverdueBreaches(IReadOnlyList<BreachRecord> breaches)
    {
        ArgumentNullException.ThrowIfNull(breaches);
        return breaches.Where(b => b.IsOverdue).ToList();
    }

    /// <summary>Generate formatted notification text for the Information Regulator.</summary>
    public Result<string> GenerateRegulatorNotification(BreachRecord breach)
    {
        ArgumentNullException.ThrowIfNull(breach);

        if (breach.Status is not (BreachStatus.Contained or BreachStatus.NotificationPending))
        {
            return Result<string>.Failure(
                ZenoHrErrorCode.BreachNotInRequiredStatus,
                string.Format(CultureInfo.InvariantCulture,
                    "Breach must be in Contained or NotificationPending status to generate notification. Current: {0}.",
                    breach.Status));
        }

        var notification = string.Format(
            CultureInfo.InvariantCulture,
            """
            POPIA SECTION 22 — BREACH NOTIFICATION
            ========================================
            Breach Reference: {0}
            Breach Title: {1}
            Discovery Date: {2:yyyy-MM-dd HH:mm:ss} UTC

            DESCRIPTION OF BREACH
            {3}

            CATEGORIES OF PERSONAL INFORMATION AFFECTED
            {4}

            ESTIMATED NUMBER OF AFFECTED DATA SUBJECTS
            {5}

            ROOT CAUSE
            {6}

            CONTAINMENT AND REMEDIATION STEPS
            {7}

            RESPONSIBLE PARTY
            {8} — Data Protection Officer

            This notification is made in compliance with POPIA Act §22.
            """,
            breach.BreachId,
            breach.Title,
            breach.DiscoveredAt,
            breach.Description,
            string.Join(", ", breach.AffectedDataCategories),
            breach.EstimatedAffectedSubjects,
            breach.RootCause,
            string.Join("\n", breach.RemediationSteps.Select((s, i) =>
                string.Format(CultureInfo.InvariantCulture, "{0}. {1}", i + 1, s))),
            breach.TenantId);

        return Result<string>.Success(notification);
    }
}
