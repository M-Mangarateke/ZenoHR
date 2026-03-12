// CTL-POPIA-005: Data Subject Notice Versioning service.
// Manages creation, acknowledgment tracking, and pending-employee detection for POPIA §18 notices.

using System.Globalization;
using System.Text.RegularExpressions;
using ZenoHR.Domain.Errors;
using ZenoHR.Module.Compliance.Models;

namespace ZenoHR.Module.Compliance.Services;

/// <summary>
/// Service for managing POPIA §18 data processing notices — versioned notice creation,
/// employee acknowledgment recording, and pending acknowledgment detection.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Instance methods for DI compatibility")]
public sealed partial class NoticeService
{
    private static int _noticeCounter;
    private static int _ackCounter;

    /// <summary>
    /// Regex for validating semantic version strings (major.minor.patch).
    /// </summary>
    [GeneratedRegex(@"^\d+\.\d+\.\d+$", RegexOptions.CultureInvariant)]
    private static partial Regex SemVerRegex();

    /// <summary>Create a new versioned data processing notice.</summary>
    // CTL-POPIA-005
    public Result<DataProcessingNotice> CreateNotice(
        string tenantId,
        string title,
        string content,
        string version,
        string createdBy)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return Result<DataProcessingNotice>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "TenantId is required.");

        if (string.IsNullOrWhiteSpace(title))
            return Result<DataProcessingNotice>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "Title is required.");

        if (string.IsNullOrWhiteSpace(content))
            return Result<DataProcessingNotice>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "Content is required.");

        if (string.IsNullOrWhiteSpace(version))
            return Result<DataProcessingNotice>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "Version is required.");

        if (!SemVerRegex().IsMatch(version))
            return Result<DataProcessingNotice>.Failure(ZenoHrErrorCode.InvalidFormat, "Version must be in semantic version format (e.g., 1.0.0).");

        if (string.IsNullOrWhiteSpace(createdBy))
            return Result<DataProcessingNotice>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "CreatedBy is required.");

        var seq = Interlocked.Increment(ref _noticeCounter);
        var noticeId = string.Format(CultureInfo.InvariantCulture, "NTC-{0:D6}", seq);
        var now = DateTimeOffset.UtcNow;

        var notice = new DataProcessingNotice
        {
            NoticeId = noticeId,
            TenantId = tenantId,
            Version = version,
            Title = title,
            Content = content,
            EffectiveFrom = now,
            CreatedBy = createdBy,
            CreatedAt = now,
            IsActive = true
        };

        return Result<DataProcessingNotice>.Success(notice);
    }

    /// <summary>Record an employee's acknowledgment of a specific notice version.</summary>
    // CTL-POPIA-005
    public Result<NoticeAcknowledgment> RecordAcknowledgment(
        string tenantId,
        string employeeId,
        string noticeId,
        string noticeVersion,
        string? ipAddress = null,
        string? userAgent = null)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return Result<NoticeAcknowledgment>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "TenantId is required.");

        if (string.IsNullOrWhiteSpace(employeeId))
            return Result<NoticeAcknowledgment>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "EmployeeId is required.");

        if (string.IsNullOrWhiteSpace(noticeId))
            return Result<NoticeAcknowledgment>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "NoticeId is required.");

        if (string.IsNullOrWhiteSpace(noticeVersion))
            return Result<NoticeAcknowledgment>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "NoticeVersion is required.");

        var seq = Interlocked.Increment(ref _ackCounter);
        var ackId = string.Format(CultureInfo.InvariantCulture, "ACK-{0:D6}", seq);

        var ack = new NoticeAcknowledgment
        {
            AcknowledgmentId = ackId,
            TenantId = tenantId,
            EmployeeId = employeeId,
            NoticeId = noticeId,
            NoticeVersion = noticeVersion,
            AcknowledgedAt = DateTimeOffset.UtcNow,
            IpAddress = ipAddress,
            UserAgent = userAgent
        };

        return Result<NoticeAcknowledgment>.Success(ack);
    }

    /// <summary>
    /// Returns true if the given notice version has not been acknowledged in the existing acknowledgments.
    /// A new version always requires re-acknowledgment even if a prior version was acknowledged.
    /// </summary>
    // CTL-POPIA-005
    public bool RequiresAcknowledgment(DataProcessingNotice notice, IReadOnlyList<NoticeAcknowledgment> existing)
    {
        ArgumentNullException.ThrowIfNull(notice);
        ArgumentNullException.ThrowIfNull(existing);

        return !existing.Any(a =>
            string.Equals(a.NoticeId, notice.NoticeId, StringComparison.Ordinal) &&
            string.Equals(a.NoticeVersion, notice.Version, StringComparison.Ordinal));
    }

    /// <summary>
    /// Returns the list of employee IDs who have not yet acknowledged the current notice version.
    /// </summary>
    // CTL-POPIA-005
    public IReadOnlyList<string> GetPendingEmployees(
        DataProcessingNotice notice,
        IReadOnlyList<NoticeAcknowledgment> acks,
        IReadOnlyList<string> allEmployeeIds)
    {
        ArgumentNullException.ThrowIfNull(notice);
        ArgumentNullException.ThrowIfNull(acks);
        ArgumentNullException.ThrowIfNull(allEmployeeIds);

        var acknowledgedEmployees = new HashSet<string>(
            acks.Where(a =>
                    string.Equals(a.NoticeId, notice.NoticeId, StringComparison.Ordinal) &&
                    string.Equals(a.NoticeVersion, notice.Version, StringComparison.Ordinal))
                .Select(a => a.EmployeeId),
            StringComparer.Ordinal);

        return allEmployeeIds
            .Where(id => !acknowledgedEmployees.Contains(id))
            .ToList();
    }
}
