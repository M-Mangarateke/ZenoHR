// REQ-OPS-004: Notification template service — generates typed notification requests with HTML bodies.

using System.Globalization;
using ZenoHR.Domain.Errors;

namespace ZenoHR.Infrastructure.Services.Notifications;

/// <summary>
/// Generates <see cref="NotificationRequest"/> instances with pre-formatted HTML bodies
/// for each notification type. Validates all inputs before constructing the request.
/// </summary>
public sealed class NotificationTemplateService
{
    /// <summary>
    /// Creates a "payslip ready" notification for the given employee.
    /// </summary>
    public static Result<NotificationRequest> CreatePayslipReadyNotification(
        string tenantId, string email, string name, string period, string netPay)
    {
        var validation = ValidateCommonFields(tenantId, email, name);
        if (validation.IsFailure)
            return Result<NotificationRequest>.Failure(validation.Error);

        if (string.IsNullOrWhiteSpace(period))
            return Result<NotificationRequest>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "Period is required.");

        if (string.IsNullOrWhiteSpace(netPay))
            return Result<NotificationRequest>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "NetPay is required.");

        var subject = string.Format(CultureInfo.InvariantCulture, "Your payslip for {0} is ready", period);
        var body = string.Format(
            CultureInfo.InvariantCulture,
            "<html><body><h2>Payslip Ready</h2><p>Dear {0},</p><p>Your payslip for <strong>{1}</strong> is now available.</p><p>Net Pay: <strong>{2}</strong></p><p>Please log in to ZenoHR to view your full payslip.</p></body></html>",
            name, period, netPay);

        var metadata = new Dictionary<string, string>
        {
            ["period"] = period,
            ["net_pay"] = netPay
        };

        return Result<NotificationRequest>.Success(new NotificationRequest(
            tenantId, email, name, NotificationType.PayslipReady, subject, body, metadata));
    }

    /// <summary>
    /// Creates a "leave approved" notification for the given employee.
    /// </summary>
    public static Result<NotificationRequest> CreateLeaveApprovedNotification(
        string tenantId, string email, string name, string leaveType, string startDate, string endDate)
    {
        var validation = ValidateCommonFields(tenantId, email, name);
        if (validation.IsFailure)
            return Result<NotificationRequest>.Failure(validation.Error);

        if (string.IsNullOrWhiteSpace(leaveType))
            return Result<NotificationRequest>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "LeaveType is required.");

        if (string.IsNullOrWhiteSpace(startDate))
            return Result<NotificationRequest>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "StartDate is required.");

        if (string.IsNullOrWhiteSpace(endDate))
            return Result<NotificationRequest>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "EndDate is required.");

        var subject = string.Format(CultureInfo.InvariantCulture, "Leave Approved: {0}", leaveType);
        var body = string.Format(
            CultureInfo.InvariantCulture,
            "<html><body><h2>Leave Approved</h2><p>Dear {0},</p><p>Your <strong>{1}</strong> leave request has been approved.</p><p>Period: <strong>{2}</strong> to <strong>{3}</strong></p><p>Please log in to ZenoHR for details.</p></body></html>",
            name, leaveType, startDate, endDate);

        var metadata = new Dictionary<string, string>
        {
            ["leave_type"] = leaveType,
            ["start_date"] = startDate,
            ["end_date"] = endDate
        };

        return Result<NotificationRequest>.Success(new NotificationRequest(
            tenantId, email, name, NotificationType.LeaveApproved, subject, body, metadata));
    }

    /// <summary>
    /// Creates a "leave rejected" notification for the given employee.
    /// </summary>
    public static Result<NotificationRequest> CreateLeaveRejectedNotification(
        string tenantId, string email, string name, string leaveType, string reason)
    {
        var validation = ValidateCommonFields(tenantId, email, name);
        if (validation.IsFailure)
            return Result<NotificationRequest>.Failure(validation.Error);

        if (string.IsNullOrWhiteSpace(leaveType))
            return Result<NotificationRequest>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "LeaveType is required.");

        if (string.IsNullOrWhiteSpace(reason))
            return Result<NotificationRequest>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "Reason is required.");

        var subject = string.Format(CultureInfo.InvariantCulture, "Leave Rejected: {0}", leaveType);
        var body = string.Format(
            CultureInfo.InvariantCulture,
            "<html><body><h2>Leave Rejected</h2><p>Dear {0},</p><p>Your <strong>{1}</strong> leave request has been rejected.</p><p>Reason: <strong>{2}</strong></p><p>Please contact your manager or HR for more information.</p></body></html>",
            name, leaveType, reason);

        var metadata = new Dictionary<string, string>
        {
            ["leave_type"] = leaveType,
            ["reason"] = reason
        };

        return Result<NotificationRequest>.Success(new NotificationRequest(
            tenantId, email, name, NotificationType.LeaveRejected, subject, body, metadata));
    }

    /// <summary>
    /// Creates a "compliance deadline" notification for the given recipient.
    /// </summary>
    public static Result<NotificationRequest> CreateComplianceDeadlineNotification(
        string tenantId, string email, string name, string filingType, string deadline, int daysRemaining)
    {
        var validation = ValidateCommonFields(tenantId, email, name);
        if (validation.IsFailure)
            return Result<NotificationRequest>.Failure(validation.Error);

        if (string.IsNullOrWhiteSpace(filingType))
            return Result<NotificationRequest>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "FilingType is required.");

        if (string.IsNullOrWhiteSpace(deadline))
            return Result<NotificationRequest>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "Deadline is required.");

        var subject = string.Format(CultureInfo.InvariantCulture, "Compliance Deadline: {0} due in {1} days", filingType, daysRemaining);
        var body = string.Format(
            CultureInfo.InvariantCulture,
            "<html><body><h2>Compliance Deadline Approaching</h2><p>Dear {0},</p><p>The <strong>{1}</strong> filing is due on <strong>{2}</strong> ({3} days remaining).</p><p>Please ensure all required documents are submitted before the deadline.</p></body></html>",
            name, filingType, deadline, daysRemaining);

        var metadata = new Dictionary<string, string>
        {
            ["filing_type"] = filingType,
            ["deadline"] = deadline,
            ["days_remaining"] = daysRemaining.ToString(CultureInfo.InvariantCulture)
        };

        return Result<NotificationRequest>.Success(new NotificationRequest(
            tenantId, email, name, NotificationType.ComplianceDeadline, subject, body, metadata));
    }

    /// <summary>
    /// Validates that the common required fields (tenantId, email, name) are non-empty.
    /// </summary>
    private static Result ValidateCommonFields(string tenantId, string email, string name)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return Result.Failure(ZenoHrErrorCode.RequiredFieldMissing, "TenantId is required.");

        if (string.IsNullOrWhiteSpace(email))
            return Result.Failure(ZenoHrErrorCode.RequiredFieldMissing, "Email is required.");

        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure(ZenoHrErrorCode.RequiredFieldMissing, "Name is required.");

        return Result.Success();
    }
}
