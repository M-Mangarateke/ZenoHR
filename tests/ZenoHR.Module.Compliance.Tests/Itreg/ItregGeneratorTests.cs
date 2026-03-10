// TC-COMP-ITREG: ITREG export file generator tests.
// CTL-SARS-006: SARS income tax registration — e@syFile-compatible format.

using FluentAssertions;
using ZenoHR.Infrastructure.Services.Filing.Itreg;

namespace ZenoHR.Module.Compliance.Tests.Itreg;

/// <summary>
/// Tests for <see cref="ItregGenerator"/> covering:
/// - H/D/T record structure and field layout
/// - Validation of required inputs (tenantId, employerPayeReference, records)
/// - Null field handling (contactNumber, emailAddress)
/// - Date formatting (ISO 8601)
/// </summary>
public sealed class ItregGeneratorTests
{
    private static readonly DateTimeOffset GeneratedAt =
        new(2026, 3, 10, 14, 30, 0, TimeSpan.Zero);

    private static ItregRecord BuildRecord(
        string employeeId = "EMP-001",
        string? contactNumber = "+27821234567",
        string? emailAddress = "zanele@zenowethu.co.za") =>
        new(
            EmployeeId: employeeId,
            FullName: "Zanele Dlamini",
            IdNumber: "8001015009087",
            DateOfBirth: new DateOnly(1980, 1, 1),
            ResidentialAddress: "123 Main Street, Sandton, Johannesburg",
            PostalCode: "2196",
            EmploymentStartDate: new DateOnly(2025, 6, 15),
            EmployerPayeReference: "7234567890",
            ContactNumber: contactNumber,
            EmailAddress: emailAddress);

    // ── TC-COMP-ITREG-001: Single record produces valid CSV ─────────────────

    [Fact]
    public void Generate_SingleRecord_ProducesValidCsv()
    {
        // TC-COMP-ITREG-001: Output must contain H, D, and T records
        var records = new List<ItregRecord> { BuildRecord() }.AsReadOnly();

        var result = ItregGenerator.Generate("tenant-001", "7234567890", records, GeneratedAt);

        result.IsSuccess.Should().BeTrue();
        var lines = result.Value.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(3);
        lines[0].Should().StartWith("H;ITREG;");
        lines[1].Should().StartWith("D;EMP-001;");
        lines[2].Should().StartWith("T;1");
    }

    // ── TC-COMP-ITREG-002: Multiple records — header count correct ──────────

    [Fact]
    public void Generate_MultipleRecords_HeaderCountCorrect()
    {
        // TC-COMP-ITREG-002: H record count field and T record count must both equal 3
        var records = new List<ItregRecord>
        {
            BuildRecord("EMP-001"),
            BuildRecord("EMP-002"),
            BuildRecord("EMP-003"),
        }.AsReadOnly();

        var result = ItregGenerator.Generate("tenant-001", "7234567890", records, GeneratedAt);

        result.IsSuccess.Should().BeTrue();
        var lines = result.Value.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Header last field = record count
        var headerFields = lines[0].Split(';');
        headerFields[^1].Trim().Should().Be("3");

        // Trailer field = record count
        var trailerFields = lines[^1].Split(';');
        trailerFields[1].Trim().Should().Be("3");

        // D records count
        lines.Count(l => l.StartsWith("D;")).Should().Be(3);
    }

    // ── TC-COMP-ITREG-003: Null contact and email → empty fields ────────────

    [Fact]
    public void Generate_NullContactAndEmail_OutputsEmptyFields()
    {
        // TC-COMP-ITREG-003: Null contactNumber/emailAddress → empty delimited fields
        var records = new List<ItregRecord>
        {
            BuildRecord(contactNumber: null, emailAddress: null),
        }.AsReadOnly();

        var result = ItregGenerator.Generate("tenant-001", "7234567890", records, GeneratedAt);

        result.IsSuccess.Should().BeTrue();
        var dLine = result.Value.Split('\n', StringSplitOptions.RemoveEmptyEntries)[1];
        var fields = dLine.Split(';');

        // Contact number (index 8) and email (index 9) should be empty
        fields[8].Should().BeEmpty();
        fields[9].Trim().Should().BeEmpty();
    }

    // ── TC-COMP-ITREG-004: Empty records → failure ──────────────────────────

    [Fact]
    public void Generate_EmptyRecords_ReturnsFailure()
    {
        // TC-COMP-ITREG-004: At least 1 record required
        var records = new List<ItregRecord>().AsReadOnly();

        var result = ItregGenerator.Generate("tenant-001", "7234567890", records, GeneratedAt);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("At least one employee record");
    }

    // ── TC-COMP-ITREG-005: Empty tenantId → failure ─────────────────────────

    [Fact]
    public void Generate_EmptyTenantId_ReturnsFailure()
    {
        // TC-COMP-ITREG-005: Tenant ID is required
        var records = new List<ItregRecord> { BuildRecord() }.AsReadOnly();

        var result = ItregGenerator.Generate("", "7234567890", records, GeneratedAt);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("Tenant ID");
    }

    // ── TC-COMP-ITREG-006: Date formatting uses ISO 8601 ────────────────────

    [Fact]
    public void Generate_DateFormatting_UsesIso8601()
    {
        // TC-COMP-ITREG-006: All dates must be yyyy-MM-dd format
        var records = new List<ItregRecord> { BuildRecord() }.AsReadOnly();

        var result = ItregGenerator.Generate("tenant-001", "7234567890", records, GeneratedAt);

        result.IsSuccess.Should().BeTrue();
        var content = result.Value;

        // Header date: 2026-03-10
        content.Should().Contain("2026-03-10");

        // D record DOB: 1980-01-01, employment start: 2025-06-15
        var dLine = content.Split('\n', StringSplitOptions.RemoveEmptyEntries)[1];
        dLine.Should().Contain("1980-01-01");
        dLine.Should().Contain("2025-06-15");
    }

    // ── TC-COMP-ITREG-007: Empty employer PAYE reference → failure ──────────

    [Fact]
    public void Generate_EmptyEmployerPayeReference_ReturnsFailure()
    {
        // TC-COMP-ITREG-007: Employer PAYE reference is required
        var records = new List<ItregRecord> { BuildRecord() }.AsReadOnly();

        var result = ItregGenerator.Generate("tenant-001", "", records, GeneratedAt);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("Employer PAYE reference");
    }
}
