// REQ-OPS-004: Tests for LoggingNotificationSender — validates logging-based notification delivery.

using FluentAssertions;
using Microsoft.Extensions.Logging;
using ZenoHR.Domain.Errors;
using ZenoHR.Infrastructure.Services.Notifications;

namespace ZenoHR.Module.Compliance.Tests.Notifications;

public sealed class LoggingNotificationSenderTests
{
    private readonly CapturingLogger _logger = new();
    private readonly LoggingNotificationSender _sut;

    public LoggingNotificationSenderTests()
    {
        _sut = new LoggingNotificationSender(_logger);
    }

    private static NotificationRequest CreateValidRequest() =>
        new(
            TenantId: "tenant-1",
            RecipientEmail: "john@zenowethu.co.za",
            RecipientName: "John Doe",
            NotificationType: NotificationType.PayslipReady,
            Subject: "Your payslip is ready",
            Body: "<html><body>Payslip</body></html>");

    [Fact]
    public async Task SendAsync_ValidRequest_ReturnsSuccess()
    {
        var result = await _sut.SendAsync(CreateValidRequest(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task SendAsync_EmptyRecipientEmail_ReturnsFailure()
    {
        var request = CreateValidRequest() with { RecipientEmail = "" };

        var result = await _sut.SendAsync(request, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.RequiredFieldMissing);
    }

    [Fact]
    public async Task SendAsync_EmptySubject_ReturnsFailure()
    {
        var request = CreateValidRequest() with { Subject = "" };

        var result = await _sut.SendAsync(request, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.RequiredFieldMissing);
    }

    [Fact]
    public async Task SendAsync_LogsNotification()
    {
        var request = CreateValidRequest();

        await _sut.SendAsync(request, CancellationToken.None);

        _logger.Entries.Should().ContainSingle();
        var entry = _logger.Entries[0];
        entry.LogLevel.Should().Be(LogLevel.Information);
        entry.Message.Should().Contain("PayslipReady");
        entry.Message.Should().Contain("john@zenowethu.co.za");
        entry.Message.Should().Contain("Your payslip is ready");
    }

    /// <summary>
    /// Simple capturing logger for test verification.
    /// Avoids NSubstitute issues with LoggerMessage source-generated generic TState types.
    /// </summary>
    private sealed class CapturingLogger : ILogger<LoggingNotificationSender>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, formatter(state, exception)));
        }

        public sealed record LogEntry(LogLevel LogLevel, string Message);
    }
}
