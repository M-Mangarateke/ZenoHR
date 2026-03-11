// REQ-OPS-004: Notification sender interface — abstraction for email/SMS/push delivery.

using ZenoHR.Domain.Errors;

namespace ZenoHR.Infrastructure.Services.Notifications;

/// <summary>
/// Sends a notification to a recipient. Implementations may deliver via email, SMS,
/// push notification, or simply log (for development/testing).
/// </summary>
public interface INotificationSender
{
    /// <summary>
    /// Sends the given notification request to the recipient.
    /// Returns <see cref="Result.Success()"/> on successful delivery,
    /// or a failure <see cref="Result"/> with an appropriate error code.
    /// </summary>
    Task<Result> SendAsync(NotificationRequest request, CancellationToken ct);
}
