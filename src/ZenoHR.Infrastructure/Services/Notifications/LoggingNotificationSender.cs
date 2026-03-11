// REQ-OPS-004: Logging notification sender — dev/test implementation that logs instead of sending.

using Microsoft.Extensions.Logging;
using ZenoHR.Domain.Errors;

namespace ZenoHR.Infrastructure.Services.Notifications;

/// <summary>
/// Development/testing implementation of <see cref="INotificationSender"/> that logs
/// notification details at Information level instead of delivering them.
/// </summary>
public sealed partial class LoggingNotificationSender : INotificationSender
{
    private readonly ILogger<LoggingNotificationSender> _logger;

    public LoggingNotificationSender(ILogger<LoggingNotificationSender> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<Result> SendAsync(NotificationRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.RecipientEmail))
        {
            return Task.FromResult(Result.Failure(
                ZenoHrErrorCode.RequiredFieldMissing,
                "RecipientEmail is required."));
        }

        if (string.IsNullOrWhiteSpace(request.Subject))
        {
            return Task.FromResult(Result.Failure(
                ZenoHrErrorCode.RequiredFieldMissing,
                "Subject is required."));
        }

        if (string.IsNullOrWhiteSpace(request.TenantId))
        {
            return Task.FromResult(Result.Failure(
                ZenoHrErrorCode.RequiredFieldMissing,
                "TenantId is required."));
        }

        LogNotificationSent(_logger, request.NotificationType, request.RecipientEmail, request.Subject);

        return Task.FromResult(Result.Success());
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "[NOTIFICATION] {Type} to {Email}: {Subject}")]
    private static partial void LogNotificationSent(
        ILogger logger, NotificationType type, string email, string subject);
}
