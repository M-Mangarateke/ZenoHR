// CTL-SARS-005: EMP601 certificate cancellation declaration generator.
// EMP601 cancels previously issued IRP5/IT3(a) certificates via SARS e@syFile.
// Format: semicolon-delimited CSV — H (header) + D (detail per record) + T (trailer totals).
// All decimal formatting uses CultureInfo.InvariantCulture to prevent locale-specific separators (CA1305).

using System.Globalization;
using System.Text;
using ZenoHR.Domain.Errors;

namespace ZenoHR.Infrastructure.Services.Filing.Emp601;

/// <summary>
/// Generates SARS EMP601 certificate cancellation declaration CSV files.
/// CTL-SARS-005: Employer submits EMP601 to cancel erroneous or duplicate IRP5/IT3(a) certificates.
/// </summary>
public sealed class Emp601Generator
{
    // CTL-SARS-005: InvariantCulture for all formatting — CA1305 compliance.
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>
    /// Generates a semicolon-delimited EMP601 cancellation CSV in SARS e@syFile format.
    /// </summary>
    /// <param name="tenantId">Tenant identifier — required.</param>
    /// <param name="taxYear">Four-digit tax year string, e.g. "2026" — required.</param>
    /// <param name="records">One or more cancellation records — must not be empty.</param>
    /// <param name="generatedAt">Timestamp at which this file is generated.</param>
    /// <returns>
    /// <see cref="Result{T}"/> containing the CSV string on success,
    /// or a <see cref="ZenoHrError"/> with <see cref="ZenoHrErrorCode.ValidationFailed"/> on invalid input.
    /// </returns>
    public static Result<string> Generate(
        string tenantId,
        string taxYear,
        IReadOnlyList<Emp601Record> records,
        DateTimeOffset generatedAt)
    {
        // CTL-SARS-005: Validate required inputs before generating.
        if (string.IsNullOrWhiteSpace(tenantId))
            return Result<string>.Failure(
                ZenoHrErrorCode.ValidationFailed,
                "EMP601: tenantId is required.");

        if (string.IsNullOrWhiteSpace(taxYear))
            return Result<string>.Failure(
                ZenoHrErrorCode.ValidationFailed,
                "EMP601: taxYear is required.");

        if (taxYear.Length != 4 || !taxYear.All(char.IsAsciiDigit))
            return Result<string>.Failure(
                ZenoHrErrorCode.InvalidFormat,
                string.Format(Invariant, "EMP601: taxYear must be a 4-digit year, received '{0}'.", taxYear));

        if (records == null || records.Count == 0)
            return Result<string>.Failure(
                ZenoHrErrorCode.ValidationFailed,
                "EMP601: at least one cancellation record is required.");

        var sb = new StringBuilder();

        // ── H record: file header ─────────────────────────────────────────────
        // Format: H;EMP601;{taxYear};{tenantId};{generatedAt:yyyy-MM-dd};{recordCount}
        sb.AppendLine(string.Join(";",
            "H",
            "EMP601",
            taxYear,
            tenantId,
            generatedAt.ToString("yyyy-MM-dd", Invariant),
            records.Count.ToString(Invariant)));

        // ── D records: one per cancellation entry ─────────────────────────────
        // Format: D;{employeeId};{employeeName};{idNumber};{taxRef};{originalCertNumber};
        //           {cancellationReason};{cancellationDate:yyyy-MM-dd};
        //           {originalPayeAmount:0.00};{originalGrossAmount:0.00};{replacementCertNumber}
        foreach (var record in records)
        {
            sb.AppendLine(string.Join(";",
                "D",
                record.EmployeeId,
                record.EmployeeName,
                record.IdNumber,
                record.TaxReferenceNumber,
                record.OriginalCertificateNumber,
                record.CancellationReason,
                record.CancellationDate.ToString("yyyy-MM-dd", Invariant),
                string.Format(Invariant, "{0:0.00}", record.OriginalPayeAmount.Amount),
                string.Format(Invariant, "{0:0.00}", record.OriginalGrossAmount.Amount),
                record.ReplacementCertificateNumber ?? string.Empty));
        }

        // ── T record: trailer totals ──────────────────────────────────────────
        // Format: T;{recordCount};{totalOriginalPaye:0.00};{totalOriginalGross:0.00}
        var totalPaye  = records.Sum(r => r.OriginalPayeAmount.Amount);
        var totalGross = records.Sum(r => r.OriginalGrossAmount.Amount);

        sb.AppendLine(string.Join(";",
            "T",
            records.Count.ToString(Invariant),
            string.Format(Invariant, "{0:0.00}", totalPaye),
            string.Format(Invariant, "{0:0.00}", totalGross)));

        return Result<string>.Success(sb.ToString());
    }
}
