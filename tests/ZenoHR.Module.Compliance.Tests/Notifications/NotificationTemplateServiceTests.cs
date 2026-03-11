// REQ-OPS-004: Tests for NotificationTemplateService — validates template generation and input validation.

using FluentAssertions;
using ZenoHR.Domain.Errors;
using ZenoHR.Infrastructure.Services.Notifications;

namespace ZenoHR.Module.Compliance.Tests.Notifications;

public sealed class NotificationTemplateServiceTests
{
    // ── PayslipReady ───────────────────────────────────────────────────────────

    [Fact]
    public void CreatePayslipReady_ValidInput_ReturnsNotification()
    {
        var result = NotificationTemplateService.CreatePayslipReadyNotification(
            "tenant-1", "john@zenowethu.co.za", "John Doe", "March 2026", "R 25,000.00");

        result.IsSuccess.Should().BeTrue();
        result.Value.NotificationType.Should().Be(NotificationType.PayslipReady);
        result.Value.RecipientEmail.Should().Be("john@zenowethu.co.za");
        result.Value.RecipientName.Should().Be("John Doe");
        result.Value.TenantId.Should().Be("tenant-1");
        result.Value.Subject.Should().Contain("March 2026");
    }

    [Fact]
    public void CreatePayslipReady_EmptyEmail_ReturnsFailure()
    {
        var result = NotificationTemplateService.CreatePayslipReadyNotification(
            "tenant-1", "", "John Doe", "March 2026", "R 25,000.00");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.RequiredFieldMissing);
    }

    [Fact]
    public void CreatePayslipReady_BodyContainsPeriodAndNetPay()
    {
        var result = NotificationTemplateService.CreatePayslipReadyNotification(
            "tenant-1", "john@zenowethu.co.za", "John Doe", "March 2026", "R 25,000.00");

        result.IsSuccess.Should().BeTrue();
        result.Value.Body.Should().Contain("March 2026");
        result.Value.Body.Should().Contain("R 25,000.00");
    }

    // ── LeaveApproved ──────────────────────────────────────────────────────────

    [Fact]
    public void CreateLeaveApproved_ValidInput_ReturnsNotification()
    {
        var result = NotificationTemplateService.CreateLeaveApprovedNotification(
            "tenant-1", "jane@zenowethu.co.za", "Jane Smith", "Annual", "2026-03-15", "2026-03-20");

        result.IsSuccess.Should().BeTrue();
        result.Value.NotificationType.Should().Be(NotificationType.LeaveApproved);
        result.Value.RecipientEmail.Should().Be("jane@zenowethu.co.za");
        result.Value.Subject.Should().Contain("Annual");
    }

    [Fact]
    public void CreateLeaveApproved_EmptyName_ReturnsFailure()
    {
        var result = NotificationTemplateService.CreateLeaveApprovedNotification(
            "tenant-1", "jane@zenowethu.co.za", "", "Annual", "2026-03-15", "2026-03-20");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.RequiredFieldMissing);
    }

    [Fact]
    public void CreateLeaveApproved_BodyContainsLeaveTypeAndDates()
    {
        var result = NotificationTemplateService.CreateLeaveApprovedNotification(
            "tenant-1", "jane@zenowethu.co.za", "Jane Smith", "Sick", "2026-04-01", "2026-04-03");

        result.IsSuccess.Should().BeTrue();
        result.Value.Body.Should().Contain("Sick");
        result.Value.Body.Should().Contain("2026-04-01");
        result.Value.Body.Should().Contain("2026-04-03");
    }

    // ── LeaveRejected ──────────────────────────────────────────────────────────

    [Fact]
    public void CreateLeaveRejected_ValidInput_ReturnsNotification()
    {
        var result = NotificationTemplateService.CreateLeaveRejectedNotification(
            "tenant-1", "jane@zenowethu.co.za", "Jane Smith", "Annual", "Insufficient balance");

        result.IsSuccess.Should().BeTrue();
        result.Value.NotificationType.Should().Be(NotificationType.LeaveRejected);
        result.Value.Subject.Should().Contain("Annual");
    }

    [Fact]
    public void CreateLeaveRejected_BodyContainsReason()
    {
        var result = NotificationTemplateService.CreateLeaveRejectedNotification(
            "tenant-1", "jane@zenowethu.co.za", "Jane Smith", "Annual", "Insufficient balance");

        result.IsSuccess.Should().BeTrue();
        result.Value.Body.Should().Contain("Insufficient balance");
    }

    // ── ComplianceDeadline ─────────────────────────────────────────────────────

    [Fact]
    public void CreateComplianceDeadline_ValidInput_ReturnsNotification()
    {
        var result = NotificationTemplateService.CreateComplianceDeadlineNotification(
            "tenant-1", "hr@zenowethu.co.za", "HR Manager", "EMP201", "2026-04-07", 14);

        result.IsSuccess.Should().BeTrue();
        result.Value.NotificationType.Should().Be(NotificationType.ComplianceDeadline);
        result.Value.Subject.Should().Contain("EMP201");
    }

    [Fact]
    public void CreateComplianceDeadline_BodyContainsDaysRemaining()
    {
        var result = NotificationTemplateService.CreateComplianceDeadlineNotification(
            "tenant-1", "hr@zenowethu.co.za", "HR Manager", "EMP201", "2026-04-07", 7);

        result.IsSuccess.Should().BeTrue();
        result.Value.Body.Should().Contain("7");
        result.Value.Body.Should().Contain("EMP201");
        result.Value.Body.Should().Contain("2026-04-07");
    }

    [Fact]
    public void CreateComplianceDeadline_EmptyTenantId_ReturnsFailure()
    {
        var result = NotificationTemplateService.CreateComplianceDeadlineNotification(
            "", "hr@zenowethu.co.za", "HR Manager", "EMP201", "2026-04-07", 14);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.RequiredFieldMissing);
    }

    // ── Cross-cutting ──────────────────────────────────────────────────────────

    [Fact]
    public void AllNotifications_HaveCorrectType()
    {
        var payslip = NotificationTemplateService.CreatePayslipReadyNotification(
            "t", "e@x.com", "N", "P", "100");
        var leaveApproved = NotificationTemplateService.CreateLeaveApprovedNotification(
            "t", "e@x.com", "N", "Annual", "2026-01-01", "2026-01-02");
        var leaveRejected = NotificationTemplateService.CreateLeaveRejectedNotification(
            "t", "e@x.com", "N", "Annual", "No balance");
        var compliance = NotificationTemplateService.CreateComplianceDeadlineNotification(
            "t", "e@x.com", "N", "EMP201", "2026-04-07", 5);

        payslip.Value.NotificationType.Should().Be(NotificationType.PayslipReady);
        leaveApproved.Value.NotificationType.Should().Be(NotificationType.LeaveApproved);
        leaveRejected.Value.NotificationType.Should().Be(NotificationType.LeaveRejected);
        compliance.Value.NotificationType.Should().Be(NotificationType.ComplianceDeadline);
    }
}
