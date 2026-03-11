// REQ-OPS-004: Notification request record — immutable DTO for outbound notifications.

namespace ZenoHR.Infrastructure.Services.Notifications;

/// <summary>
/// Immutable request representing a single notification to be sent.
/// Carries all data needed by any <see cref="INotificationSender"/> implementation.
/// </summary>
public sealed record NotificationRequest(
    string TenantId,
    string RecipientEmail,
    string RecipientName,
    NotificationType NotificationType,
    string Subject,
    string Body,
    Dictionary<string, string>? Metadata = null);
