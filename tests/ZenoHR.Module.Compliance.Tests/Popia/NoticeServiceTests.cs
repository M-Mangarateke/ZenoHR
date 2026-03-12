// CTL-POPIA-005: Tests for POPIA §18 data subject notice versioning and acknowledgment tracking.

using FluentAssertions;
using ZenoHR.Module.Compliance.Models;
using ZenoHR.Module.Compliance.Services;

namespace ZenoHR.Module.Compliance.Tests.Popia;

public sealed class NoticeServiceTests
{
    private readonly NoticeService _service = new();

    private const string TenantId = "tenant-1";
    private const string CreatedBy = "user-hr-001";

    private static DataProcessingNotice CreateTestNotice(
        string noticeId = "NTC-000001",
        string version = "1.0.0",
        bool isActive = true)
    {
        return new DataProcessingNotice
        {
            NoticeId = noticeId,
            TenantId = TenantId,
            Version = version,
            Title = "POPIA Processing Notice",
            Content = "We process your personal information for payroll and HR administration.",
            EffectiveFrom = DateTimeOffset.UtcNow,
            CreatedBy = CreatedBy,
            CreatedAt = DateTimeOffset.UtcNow,
            IsActive = isActive
        };
    }

    private static NoticeAcknowledgment CreateTestAck(
        string employeeId = "emp-001",
        string noticeId = "NTC-000001",
        string noticeVersion = "1.0.0")
    {
        return new NoticeAcknowledgment
        {
            AcknowledgmentId = "ACK-000001",
            TenantId = TenantId,
            EmployeeId = employeeId,
            NoticeId = noticeId,
            NoticeVersion = noticeVersion,
            AcknowledgedAt = DateTimeOffset.UtcNow
        };
    }

    // ── CreateNotice ──────────────────────────────────────────────────────────

    [Fact]
    public void CreateNotice_ValidInput_ReturnsSuccess()
    {
        var result = _service.CreateNotice(TenantId, "Privacy Notice", "Content here.", "1.0.0", CreatedBy);

        result.IsSuccess.Should().BeTrue();
        result.Value.NoticeId.Should().StartWith("NTC-");
        result.Value.TenantId.Should().Be(TenantId);
        result.Value.Version.Should().Be("1.0.0");
        result.Value.Title.Should().Be("Privacy Notice");
        result.Value.Content.Should().Be("Content here.");
        result.Value.IsActive.Should().BeTrue();
        result.Value.CreatedBy.Should().Be(CreatedBy);
    }

    [Fact]
    public void CreateNotice_EmptyTitle_ReturnsFailure()
    {
        var result = _service.CreateNotice(TenantId, "", "Content", "1.0.0", CreatedBy);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("Title");
    }

    [Fact]
    public void CreateNotice_EmptyTenantId_ReturnsFailure()
    {
        var result = _service.CreateNotice("", "Title", "Content", "1.0.0", CreatedBy);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("TenantId");
    }

    [Fact]
    public void CreateNotice_EmptyContent_ReturnsFailure()
    {
        var result = _service.CreateNotice(TenantId, "Title", "", "1.0.0", CreatedBy);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("Content");
    }

    [Fact]
    public void CreateNotice_EmptyVersion_ReturnsFailure()
    {
        var result = _service.CreateNotice(TenantId, "Title", "Content", "", CreatedBy);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("Version");
    }

    [Theory]
    [InlineData("1.0")]
    [InlineData("v1.0.0")]
    [InlineData("abc")]
    [InlineData("1.0.0.0")]
    [InlineData("1")]
    public void CreateNotice_InvalidVersionFormat_ReturnsFailure(string badVersion)
    {
        var result = _service.CreateNotice(TenantId, "Title", "Content", badVersion, CreatedBy);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("semantic version");
    }

    [Fact]
    public void CreateNotice_EmptyCreatedBy_ReturnsFailure()
    {
        var result = _service.CreateNotice(TenantId, "Title", "Content", "1.0.0", "");

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("CreatedBy");
    }

    [Fact]
    public void CreateNotice_GeneratesUniqueIds()
    {
        var r1 = _service.CreateNotice(TenantId, "Notice 1", "Content 1", "1.0.0", CreatedBy);
        var r2 = _service.CreateNotice(TenantId, "Notice 2", "Content 2", "1.0.0", CreatedBy);

        r1.IsSuccess.Should().BeTrue();
        r2.IsSuccess.Should().BeTrue();
        r1.Value.NoticeId.Should().NotBe(r2.Value.NoticeId);
    }

    // ── RecordAcknowledgment ──────────────────────────────────────────────────

    [Fact]
    public void RecordAcknowledgment_ValidInput_ReturnsSuccess()
    {
        var result = _service.RecordAcknowledgment(TenantId, "emp-001", "NTC-000001", "1.0.0", "192.168.1.1", "Mozilla/5.0");

        result.IsSuccess.Should().BeTrue();
        result.Value.AcknowledgmentId.Should().StartWith("ACK-");
        result.Value.TenantId.Should().Be(TenantId);
        result.Value.EmployeeId.Should().Be("emp-001");
        result.Value.NoticeId.Should().Be("NTC-000001");
        result.Value.NoticeVersion.Should().Be("1.0.0");
        result.Value.IpAddress.Should().Be("192.168.1.1");
        result.Value.UserAgent.Should().Be("Mozilla/5.0");
    }

    [Fact]
    public void RecordAcknowledgment_EmptyEmployeeId_ReturnsFailure()
    {
        var result = _service.RecordAcknowledgment(TenantId, "", "NTC-000001", "1.0.0");

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("EmployeeId");
    }

    [Fact]
    public void RecordAcknowledgment_EmptyNoticeId_ReturnsFailure()
    {
        var result = _service.RecordAcknowledgment(TenantId, "emp-001", "", "1.0.0");

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("NoticeId");
    }

    [Fact]
    public void RecordAcknowledgment_EmptyNoticeVersion_ReturnsFailure()
    {
        var result = _service.RecordAcknowledgment(TenantId, "emp-001", "NTC-000001", "");

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("NoticeVersion");
    }

    [Fact]
    public void RecordAcknowledgment_NullOptionalFields_Succeeds()
    {
        var result = _service.RecordAcknowledgment(TenantId, "emp-001", "NTC-000001", "1.0.0");

        result.IsSuccess.Should().BeTrue();
        result.Value.IpAddress.Should().BeNull();
        result.Value.UserAgent.Should().BeNull();
    }

    // ── RequiresAcknowledgment ────────────────────────────────────────────────

    [Fact]
    public void RequiresAcknowledgment_NoExistingAcks_ReturnsTrue()
    {
        var notice = CreateTestNotice();
        List<NoticeAcknowledgment> noAcks = [];

        _service.RequiresAcknowledgment(notice, noAcks).Should().BeTrue();
    }

    [Fact]
    public void RequiresAcknowledgment_WithMatchingAck_ReturnsFalse()
    {
        var notice = CreateTestNotice();
        var acks = new List<NoticeAcknowledgment> { CreateTestAck() };

        _service.RequiresAcknowledgment(notice, acks).Should().BeFalse();
    }

    [Fact]
    public void RequiresAcknowledgment_OldVersionAck_ReturnsTrue()
    {
        var notice = CreateTestNotice(version: "2.0.0");
        var acks = new List<NoticeAcknowledgment> { CreateTestAck(noticeVersion: "1.0.0") };

        _service.RequiresAcknowledgment(notice, acks).Should().BeTrue();
    }

    [Fact]
    public void RequiresAcknowledgment_DifferentNoticeId_ReturnsTrue()
    {
        var notice = CreateTestNotice(noticeId: "NTC-000002");
        var acks = new List<NoticeAcknowledgment> { CreateTestAck(noticeId: "NTC-000001") };

        _service.RequiresAcknowledgment(notice, acks).Should().BeTrue();
    }

    [Fact]
    public void RequiresAcknowledgment_MultipleAcksOnlyOldVersions_ReturnsTrue()
    {
        var notice = CreateTestNotice(version: "3.0.0");
        var acks = new List<NoticeAcknowledgment>
        {
            CreateTestAck(noticeVersion: "1.0.0"),
            CreateTestAck(noticeVersion: "2.0.0")
        };

        _service.RequiresAcknowledgment(notice, acks).Should().BeTrue();
    }

    // ── GetPendingEmployees ───────────────────────────────────────────────────

    [Fact]
    public void GetPendingEmployees_NoAcks_ReturnsAllEmployees()
    {
        var notice = CreateTestNotice();
        List<NoticeAcknowledgment> noAcks = [];
        var allEmployees = new List<string> { "emp-001", "emp-002", "emp-003" };

        var pending = _service.GetPendingEmployees(notice, noAcks, allEmployees);

        pending.Should().BeEquivalentTo(allEmployees);
    }

    [Fact]
    public void GetPendingEmployees_SomeAcked_ReturnsOnlyUnacknowledged()
    {
        var notice = CreateTestNotice();
        var acks = new List<NoticeAcknowledgment>
        {
            CreateTestAck(employeeId: "emp-001"),
            CreateTestAck(employeeId: "emp-003")
        };
        var allEmployees = new List<string> { "emp-001", "emp-002", "emp-003" };

        var pending = _service.GetPendingEmployees(notice, acks, allEmployees);

        pending.Should().BeEquivalentTo(["emp-002"]);
    }

    [Fact]
    public void GetPendingEmployees_AllAcked_ReturnsEmpty()
    {
        var notice = CreateTestNotice();
        var acks = new List<NoticeAcknowledgment>
        {
            CreateTestAck(employeeId: "emp-001"),
            CreateTestAck(employeeId: "emp-002")
        };
        var allEmployees = new List<string> { "emp-001", "emp-002" };

        var pending = _service.GetPendingEmployees(notice, acks, allEmployees);

        pending.Should().BeEmpty();
    }

    [Fact]
    public void GetPendingEmployees_OldVersionAcks_ReturnsAllForNewVersion()
    {
        var notice = CreateTestNotice(version: "2.0.0");
        var acks = new List<NoticeAcknowledgment>
        {
            CreateTestAck(employeeId: "emp-001", noticeVersion: "1.0.0"),
            CreateTestAck(employeeId: "emp-002", noticeVersion: "1.0.0")
        };
        var allEmployees = new List<string> { "emp-001", "emp-002" };

        var pending = _service.GetPendingEmployees(notice, acks, allEmployees);

        pending.Should().BeEquivalentTo(allEmployees);
    }

    [Fact]
    public void GetPendingEmployees_EmptyEmployeeList_ReturnsEmpty()
    {
        var notice = CreateTestNotice();
        List<NoticeAcknowledgment> noAcks = [];
        List<string> noEmployees = [];

        var pending = _service.GetPendingEmployees(notice, noAcks, noEmployees);

        pending.Should().BeEmpty();
    }
}
