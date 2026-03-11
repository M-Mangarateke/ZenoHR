// REQ-OPS-004: Notification type enum for email/notification service.

namespace ZenoHR.Infrastructure.Services.Notifications;

/// <summary>
/// Identifies the category of notification being sent.
/// Used for template selection, routing, and audit logging.
/// </summary>
public enum NotificationType
{
    Unknown = 0,
    PayslipReady = 1,
    LeaveApproved = 2,
    LeaveRejected = 3,
    ComplianceDeadline = 4,
    Emp201Reminder = 5,
    EtiExpiring = 6,
    BreachAlert = 7,
    PasswordExpiring = 8
}
