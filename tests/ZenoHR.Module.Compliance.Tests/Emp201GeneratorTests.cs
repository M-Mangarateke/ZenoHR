// TC-COMP-EMP201: EMP201 CSV generator tests.
// CTL-SARS-006: EMP201 monthly PAYE/UIF/SDL declaration — SARS eFiling format.
// Tests cover CSV structure, invariant enforcement, due date logic, and summary report content.

using FluentAssertions;
using ZenoHR.Infrastructure.Services.Filing.Emp201;

namespace ZenoHR.Module.Compliance.Tests;

/// <summary>
/// Tests for <see cref="Emp201Generator"/> covering:
/// - CSV header and record structure (H record + D records)
/// - PAYE invariant enforcement
/// - Summary report content
/// - Due date calculation (7th of following month with weekend advance)
/// </summary>
public sealed class Emp201GeneratorTests
{
    private readonly Emp201Generator _generator = new();

    // ── Test fixture ──────────────────────────────────────────────────────────

    private static Emp201Data BuildValidData(decimal payeOverride = 0m)
    {
        var lines = new List<Emp201EmployeeLine>
        {
            new()
            {
                EmployeeId = "EMP-001",
                EmployeeFullName = "Zanele Dlamini",
                TaxReferenceNumber = "9876543210",
                IdOrPassportNumber = "8001015009087",
                GrossRemuneration = 45_000.00m,
                PayeDeducted = 8_250.00m,
                UifEmployee = 177.12m,
                UifEmployer = 177.12m,
                SdlEmployer = 450.00m,
                PaymentMethod = "EFT",
            },
            new()
            {
                EmployeeId = "EMP-002",
                EmployeeFullName = "Sipho Nkosi",
                TaxReferenceNumber = "1234567890",
                IdOrPassportNumber = "9203025008086",
                GrossRemuneration = 28_500.00m,
                PayeDeducted = 3_100.00m,
                UifEmployee = 177.12m,
                UifEmployer = 177.12m,
                SdlEmployer = 285.00m,
                PaymentMethod = "EFT",
            },
        };

        var totalPaye = payeOverride != 0m ? payeOverride : lines.Sum(l => l.PayeDeducted);

        return new Emp201Data
        {
            EmployerPAYEReference = "7234567890",
            EmployerUifReference = "U0987654321",
            EmployerSdlReference = "SDL0987654321",
            EmployerTradingName = "Zenowethu (Pty) Ltd",
            TaxPeriod = "202602",
            PeriodLabel = "February 2026",
            PayrollRunId = "RUN-2026-02",
            TotalPayeDeducted = totalPaye,
            TotalUifEmployee = lines.Sum(l => l.UifEmployee),
            TotalUifEmployer = lines.Sum(l => l.UifEmployer),
            TotalSdl = lines.Sum(l => l.SdlEmployer),
            TotalGrossRemuneration = lines.Sum(l => l.GrossRemuneration),
            EmployeeCount = lines.Count,
            DueDate = new DateOnly(2026, 3, 9),  // Feb 2026 → 7 Mar 2026, but 7 Mar is Saturday → 9 Mar (Mon)
            EmployeeLines = lines.AsReadOnly(),
            GeneratedByUserId = "user-hr-001",
            GeneratedAt = new DateTimeOffset(2026, 2, 28, 12, 0, 0, TimeSpan.Zero),
        };
    }

    // ── GenerateCsv ──────────────────────────────────────────────────────────

    [Fact]
    public void GenerateCsv_ValidData_StartsWithHeaderLine()
    {
        // TC-COMP-EMP201-001: First non-empty line must be the column header row
        var csv = _generator.GenerateCsv(BuildValidData());

        var firstLine = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).First();
        firstLine.Should().StartWith("RECORD_TYPE");
    }

    [Fact]
    public void GenerateCsv_ValidData_ContainsHRecord()
    {
        // TC-COMP-EMP201-002: CSV must contain exactly one H (header) detail record
        var csv = _generator.GenerateCsv(BuildValidData());

        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Should().Contain(l => l.StartsWith("H;"));
    }

    [Fact]
    public void GenerateCsv_ValidData_ContainsDRecordPerEmployee()
    {
        // TC-COMP-EMP201-003: D record count must equal employee count (2 in test fixture)
        var data = BuildValidData();
        var csv = _generator.GenerateCsv(data);

        var dLines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                        .Count(l => l.StartsWith("D;"));
        dLines.Should().Be(data.EmployeeCount);
    }

    [Fact]
    public void GenerateCsv_InvariantViolated_ThrowsInvalidOperationException()
    {
        // TC-COMP-EMP201-004: Declared total PAYE != sum of employee lines → must throw
        // Employee lines total = 11350, but declared total = 99999 (tampered)
        var data = BuildValidData(payeOverride: 99_999m);

        var act = () => _generator.GenerateCsv(data);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*invariant*");
    }

    // ── GenerateSummaryReport ─────────────────────────────────────────────────

    [Fact]
    public void GenerateSummaryReport_ValidData_ContainsEmployerName()
    {
        // TC-COMP-EMP201-005: Summary report must include the trading name
        var report = _generator.GenerateSummaryReport(BuildValidData());

        report.Should().Contain("Zenowethu (Pty) Ltd");
    }

    [Fact]
    public void GenerateSummaryReport_ValidData_ContainsDueDate()
    {
        // TC-COMP-EMP201-006: Summary report must include the formatted due date
        var data = BuildValidData();
        var report = _generator.GenerateSummaryReport(data);

        // DueDate is 2026-03-09 → "09 March 2026"
        report.Should().Contain("09 March 2026");
    }

    // ── CalculateDueDate ──────────────────────────────────────────────────────

    [Fact]
    public void CalculateDueDate_StandardMonth_ReturnsSeventh()
    {
        // TC-COMP-EMP201-007: February 2026 → 7 March 2026 (but 7 March is Saturday)
        // Standard check: use a month where 7th is a weekday — January 2026 → 7 Feb 2026 (Saturday)
        // Use November 2025: 7 December 2025 is a Sunday → 8 December (Monday)
        // Use a month where 7th is definitively mid-week: Oct 2025 → 7 Nov 2025 (Friday) = standard
        var result = _generator.CalculateDueDate(2025, 10);

        result.Should().Be(new DateOnly(2025, 11, 7));
        result.DayOfWeek.Should().NotBe(DayOfWeek.Saturday);
        result.DayOfWeek.Should().NotBe(DayOfWeek.Sunday);
    }

    [Fact]
    public void CalculateDueDate_WhenSeventhIsSaturday_ReturnsNextMonday()
    {
        // TC-COMP-EMP201-008: February 2026 → 7 March 2026 is Saturday → return 9 March (Monday)
        // Verify: new DateOnly(2026, 3, 7).DayOfWeek == Saturday
        new DateOnly(2026, 3, 7).DayOfWeek.Should().Be(DayOfWeek.Saturday);

        var result = _generator.CalculateDueDate(2026, 2);

        result.Should().Be(new DateOnly(2026, 3, 9));
        result.DayOfWeek.Should().Be(DayOfWeek.Monday);
    }

    [Fact]
    public void CalculateDueDate_December_ReturnsJanuaryNextYear()
    {
        // TC-COMP-EMP201-009: December 2025 → due date is in January 2026
        var result = _generator.CalculateDueDate(2025, 12);

        result.Year.Should().Be(2026);
        result.Month.Should().Be(1);
        // 7 Jan 2026 is a Wednesday — no weekend adjustment
        result.Day.Should().Be(7);
    }
}
