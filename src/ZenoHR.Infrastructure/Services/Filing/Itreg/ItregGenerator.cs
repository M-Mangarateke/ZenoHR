// CTL-SARS-006: ITREG export file generator — SARS e@syFile-compatible format.
// REQ-HR-001: Employer-assisted income tax registration for employees without tax references.
// All string formatting uses InvariantCulture to prevent locale-specific separators (CA1305).

using System.Globalization;
using System.Text;
using ZenoHR.Domain.Errors;

namespace ZenoHR.Infrastructure.Services.Filing.Itreg;

/// <summary>
/// Generates SARS ITREG export files for employee income tax registration.
/// CTL-SARS-006: Semicolon-delimited format (H/D/T record layout) per SARS e@syFile specification.
/// </summary>
public sealed class ItregGenerator
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>
    /// Generates the ITREG semicolon-delimited export content.
    /// CTL-SARS-006: H (header), D (detail), T (trailer) record layout.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="employerPayeReference">Employer's SARS PAYE reference number.</param>
    /// <param name="records">Employee records to include in the registration file.</param>
    /// <param name="generatedAt">Timestamp of file generation.</param>
    /// <returns>Result containing the export file content string, or a failure.</returns>
    public static Result<string> Generate(
        string tenantId,
        string employerPayeReference,
        IReadOnlyList<ItregRecord> records,
        DateTimeOffset generatedAt)
    {
        // ── Validation ──────────────────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Result<string>.Failure(
                ZenoHrErrorCode.RequiredFieldMissing,
                "Tenant ID is required for ITREG generation.");
        }

        if (string.IsNullOrWhiteSpace(employerPayeReference))
        {
            return Result<string>.Failure(
                ZenoHrErrorCode.RequiredFieldMissing,
                "Employer PAYE reference is required for ITREG generation.");
        }

        if (records is null || records.Count == 0)
        {
            return Result<string>.Failure(
                ZenoHrErrorCode.ValidationFailed,
                "At least one employee record is required for ITREG generation.");
        }

        // ── Build export content ────────────────────────────────────────────────
        var sb = new StringBuilder();

        // Header record
        sb.AppendLine(string.Join(";",
            "H",
            "ITREG",
            employerPayeReference,
            tenantId,
            generatedAt.ToString("yyyy-MM-dd", Invariant),
            records.Count.ToString(Invariant)));

        // Detail records
        foreach (var r in records)
        {
            sb.AppendLine(string.Join(";",
                "D",
                r.EmployeeId,
                r.FullName,
                r.IdNumber,
                r.DateOfBirth.ToString("yyyy-MM-dd", Invariant),
                r.ResidentialAddress,
                r.PostalCode,
                r.EmploymentStartDate.ToString("yyyy-MM-dd", Invariant),
                r.ContactNumber ?? string.Empty,
                r.EmailAddress ?? string.Empty));
        }

        // Trailer record
        sb.AppendLine(string.Join(";",
            "T",
            records.Count.ToString(Invariant)));

        return Result<string>.Success(sb.ToString());
    }
}
