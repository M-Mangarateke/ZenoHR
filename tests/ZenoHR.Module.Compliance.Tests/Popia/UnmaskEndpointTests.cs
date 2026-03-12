// CTL-POPIA-002, VUL-020: Tests for PII unmask endpoint logic and audit service.
// Validates purpose code enforcement, field name validation, and audit record creation.

using FluentAssertions;
using ZenoHR.Api.DTOs;
using ZenoHR.Module.Compliance.Services;

namespace ZenoHR.Module.Compliance.Tests.Popia;

public sealed class UnmaskEndpointTests
{
    private readonly UnmaskAuditService _auditService = new();

    // ── Purpose code validation ───────────────────────────────────────────

    [Fact]
    // CTL-POPIA-002: Valid purpose code should be accepted by UnmaskRequest.IsValid().
    public void IsValid_ApprovedPurposeCode_ReturnsTrue()
    {
        var request = new UnmaskRequest
        {
            FieldName = "national_id",
            PurposeCode = "PAYROLL_PROCESSING",
        };

        request.IsValid().Should().BeTrue();
    }

    [Fact]
    // CTL-POPIA-002: Invalid purpose code must be rejected.
    public void IsValid_InvalidPurposeCode_ReturnsFalse()
    {
        var request = new UnmaskRequest
        {
            FieldName = "national_id",
            PurposeCode = "INVALID_CODE",
        };

        request.IsValid().Should().BeFalse();
    }

    [Fact]
    // CTL-POPIA-002: Empty field name must be rejected.
    public void IsValid_EmptyFieldName_ReturnsFalse()
    {
        var request = new UnmaskRequest
        {
            FieldName = "",
            PurposeCode = "PAYROLL_PROCESSING",
        };

        request.IsValid().Should().BeFalse();
    }

    [Fact]
    // CTL-POPIA-002: Whitespace-only field name must be rejected.
    public void IsValid_WhitespaceFieldName_ReturnsFalse()
    {
        var request = new UnmaskRequest
        {
            FieldName = "   ",
            PurposeCode = "SARS_FILING",
        };

        request.IsValid().Should().BeFalse();
    }

    [Fact]
    // CTL-POPIA-002: Unrecognized field name must be rejected.
    public void IsValid_InvalidFieldName_ReturnsFalse()
    {
        var request = new UnmaskRequest
        {
            FieldName = "salary",
            PurposeCode = "PAYROLL_PROCESSING",
        };

        request.IsValid().Should().BeFalse();
    }

    [Theory]
    [InlineData("PAYROLL_PROCESSING")]
    [InlineData("SARS_FILING")]
    [InlineData("BCEA_COMPLIANCE")]
    [InlineData("HR_INVESTIGATION")]
    [InlineData("AUDIT_REVIEW")]
    [InlineData("EMPLOYEE_REQUEST")]
    [InlineData("SYSTEM_ADMIN")]
    // CTL-POPIA-002: All 7 approved purpose codes must be accepted.
    public void IsValid_AllSevenApprovedPurposeCodes_AreAccepted(string purposeCode)
    {
        var request = new UnmaskRequest
        {
            FieldName = "tax_reference",
            PurposeCode = purposeCode,
        };

        request.IsValid().Should().BeTrue();
    }

    [Fact]
    // CTL-POPIA-002: Exactly 7 approved purpose codes must exist.
    public void ApprovedPurposeCodes_ContainsExactlySeven()
    {
        UnmaskRequest.ApprovedPurposeCodes.Should().HaveCount(7);
    }

    [Theory]
    [InlineData("national_id")]
    [InlineData("tax_reference")]
    [InlineData("bank_account")]
    // CTL-POPIA-002: All three allowed field names must pass validation.
    public void IsValid_AllThreeAllowedFields_AreAccepted(string fieldName)
    {
        var request = new UnmaskRequest
        {
            FieldName = fieldName,
            PurposeCode = "PAYROLL_PROCESSING",
        };

        request.IsValid().Should().BeTrue();
    }

    // ── Audit service ────────────────────────────────────────────────────

    [Fact]
    // CTL-POPIA-002: Successful unmask must create an audit record with correct fields.
    public void CreateUnmaskAuditRecord_ValidInput_CreatesRecordWithCorrectFields()
    {
        var now = DateTimeOffset.UtcNow;

        var record = _auditService.CreateUnmaskAuditRecord(
            tenantId: "tenant-1",
            actorId: "user-001",
            actorRole: "HRManager",
            employeeId: "emp-123",
            fieldName: "national_id",
            purposeCode: "PAYROLL_PROCESSING",
            justification: null,
            occurredAt: now);

        record.Should().NotBeNull();
        record.TenantId.Should().Be("tenant-1");
        record.ActorId.Should().Be("user-001");
        record.ActorRole.Should().Be("HRManager");
        record.EmployeeId.Should().Be("emp-123");
        record.FieldName.Should().Be("national_id");
        record.PurposeCode.Should().Be("PAYROLL_PROCESSING");
        record.OccurredAt.Should().Be(now);
    }

    [Fact]
    // CTL-POPIA-002: Audit record metadata must include purpose code and field name.
    public void CreateUnmaskAuditRecord_MetadataContainsPurposeCodeAndField()
    {
        var record = _auditService.CreateUnmaskAuditRecord(
            tenantId: "tenant-1",
            actorId: "user-002",
            actorRole: "Director",
            employeeId: "emp-456",
            fieldName: "tax_reference",
            purposeCode: "SARS_FILING",
            justification: "Annual reconciliation filing",
            occurredAt: DateTimeOffset.UtcNow);

        record.Metadata.Should().NotBeNullOrWhiteSpace();
        record.Metadata.Should().Contain("tax_reference");
        record.Metadata.Should().Contain("SARS_FILING");
        record.Metadata.Should().Contain("Annual reconciliation filing");
    }

    [Fact]
    // CTL-POPIA-002: Audit record should capture bank_account field correctly.
    public void CreateUnmaskAuditRecord_BankAccountField_RecordsFieldName()
    {
        var record = _auditService.CreateUnmaskAuditRecord(
            tenantId: "tenant-1",
            actorId: "user-001",
            actorRole: "HRManager",
            employeeId: "emp-789",
            fieldName: "bank_account",
            purposeCode: "EMPLOYEE_REQUEST",
            justification: null,
            occurredAt: DateTimeOffset.UtcNow);

        record.FieldName.Should().Be("bank_account");
        record.PurposeCode.Should().Be("EMPLOYEE_REQUEST");
        record.Metadata.Should().Contain("bank_account");
    }

    [Fact]
    // CTL-POPIA-002: Audit service must reject null/empty required parameters.
    public void CreateUnmaskAuditRecord_NullTenantId_ThrowsArgumentException()
    {
        var act = () => _auditService.CreateUnmaskAuditRecord(
            tenantId: "",
            actorId: "user-001",
            actorRole: "HRManager",
            employeeId: "emp-123",
            fieldName: "national_id",
            purposeCode: "PAYROLL_PROCESSING",
            justification: null,
            occurredAt: DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    // CTL-POPIA-002: Audit service must reject empty purpose code.
    public void CreateUnmaskAuditRecord_EmptyPurposeCode_ThrowsArgumentException()
    {
        var act = () => _auditService.CreateUnmaskAuditRecord(
            tenantId: "tenant-1",
            actorId: "user-001",
            actorRole: "HRManager",
            employeeId: "emp-123",
            fieldName: "national_id",
            purposeCode: "",
            justification: null,
            occurredAt: DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    // CTL-POPIA-002: Audit service must reject empty employee ID.
    public void CreateUnmaskAuditRecord_EmptyEmployeeId_ThrowsArgumentException()
    {
        var act = () => _auditService.CreateUnmaskAuditRecord(
            tenantId: "tenant-1",
            actorId: "user-001",
            actorRole: "HRManager",
            employeeId: "",
            fieldName: "national_id",
            purposeCode: "PAYROLL_PROCESSING",
            justification: null,
            occurredAt: DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    // CTL-POPIA-002: Audit service must reject empty field name.
    public void CreateUnmaskAuditRecord_EmptyFieldName_ThrowsArgumentException()
    {
        var act = () => _auditService.CreateUnmaskAuditRecord(
            tenantId: "tenant-1",
            actorId: "user-001",
            actorRole: "HRManager",
            employeeId: "emp-123",
            fieldName: "",
            purposeCode: "PAYROLL_PROCESSING",
            justification: null,
            occurredAt: DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }
}
