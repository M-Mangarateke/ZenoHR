// CTL-SARS-004: EMP701 prior-year reconciliation adjustment generator.
// EMP701 is submitted when an employer discovers discrepancies in a previously submitted
// EMP501 annual reconciliation without resubmitting the entire EMP501.
// Format: semicolon-delimited per SARS e@syFile specification.
// All decimal formatting uses CultureInfo.InvariantCulture (CA1305).

using System.Globalization;
using System.Text;
using ZenoHR.Domain.Common;
using ZenoHR.Domain.Errors;

namespace ZenoHR.Infrastructure.Services.Filing.Emp701;

/// <summary>
/// Generates SARS EMP701 prior-year reconciliation adjustment files in semicolon-delimited
/// e@syFile CSV format. Used to correct discrepancies in previously submitted EMP501 annual
/// reconciliations without resubmitting the full EMP501.
/// CTL-SARS-004: Prior-year adjustment must reference original certificate number and state reason.
/// </summary>
public sealed class Emp701Generator
{
    // CTL-SARS-004: All decimal formatting uses InvariantCulture to prevent locale-specific separators.
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>
    /// Generates a semicolon-delimited EMP701 CSV file in SARS e@syFile format.
    /// </summary>
    /// <param name="tenantId">Employer tenant identifier (required).</param>
    /// <param name="taxYear">4-digit prior tax year being corrected, e.g. "2025".</param>
    /// <param name="records">One record per employee adjustment (at least 1 required).</param>
    /// <param name="generatedAt">UTC timestamp of generation.</param>
    /// <returns>The generated CSV content, or a <see cref="Result{T}"/> failure with a validation error.</returns>
    public static Result<string> Generate(
        string tenantId,
        string taxYear,
        IReadOnlyList<Emp701AdjustmentRecord> records,
        DateTimeOffset generatedAt)
    {
        // CTL-SARS-004: Validate required inputs before generating.
        if (string.IsNullOrWhiteSpace(tenantId))
            return Result<string>.Failure(
                ZenoHrErrorCode.RequiredFieldMissing,
                "EMP701: tenantId is required and must not be empty.");

        if (string.IsNullOrWhiteSpace(taxYear)
            || !int.TryParse(taxYear, NumberStyles.None, Invariant, out _)
            || taxYear.Length != 4)
            return Result<string>.Failure(
                ZenoHrErrorCode.InvalidFormat,
                string.Format(Invariant,
                    "EMP701: taxYear must be a 4-digit numeric year (e.g. '2025'). Got: '{0}'.",
                    taxYear));

        if (records == null || records.Count == 0)
            return Result<string>.Failure(
                ZenoHrErrorCode.ValidationFailed,
                "EMP701: at least one adjustment record is required.");

        var sb = new StringBuilder();
        var recordCount = records.Count;

        // ── H record: file header ──────────────────────────────────────────────
        // H;EMP701;{taxYear};{tenantId};{generatedAt:yyyy-MM-dd};{recordCount}
        sb.AppendLine(string.Join(";",
            "H",
            "EMP701",
            taxYear,
            tenantId,
            generatedAt.ToString("yyyy-MM-dd", Invariant),
            recordCount.ToString(Invariant)));

        // ── D records: one per employee adjustment ─────────────────────────────
        // D;{employeeId};{employeeName};{idNumber};{taxRef};{originalCertNumber};
        //   {adjustmentReason};{adjustmentDate:yyyy-MM-dd};
        //   {originalPaye:0.00};{adjustedPaye:0.00};{payeDiff:0.00};
        //   {originalGross:0.00};{adjustedGross:0.00};{grossDiff:0.00};
        //   {originalUif:0.00};{adjustedUif:0.00};{uifDiff:0.00};{notes}
        foreach (var record in records)
        {
            sb.AppendLine(string.Join(";",
                "D",
                record.EmployeeId,
                record.EmployeeName,
                record.IdNumber,
                record.TaxReferenceNumber,
                record.OriginalCertificateNumber,
                record.AdjustmentReason,
                record.AdjustmentDate.ToString("yyyy-MM-dd", Invariant),
                FormatAmount(record.OriginalPayeAmount),
                FormatAmount(record.AdjustedPayeAmount),
                FormatAmount(record.PayeDifference),
                FormatAmount(record.OriginalGrossAmount),
                FormatAmount(record.AdjustedGrossAmount),
                FormatAmount(record.GrossDifference),
                FormatAmount(record.OriginalUifAmount),
                FormatAmount(record.AdjustedUifAmount),
                FormatAmount(record.UifDifference),
                record.Notes ?? string.Empty));
        }

        // ── T record: trailer with aggregate totals ───────────────────────────
        // T;{recordCount};{totalPayeDiff:0.00};{totalGrossDiff:0.00};{totalUifDiff:0.00}
        var totalPayeDiff  = records.Aggregate(MoneyZAR.Zero, (acc, r) => acc + r.PayeDifference);
        var totalGrossDiff = records.Aggregate(MoneyZAR.Zero, (acc, r) => acc + r.GrossDifference);
        var totalUifDiff   = records.Aggregate(MoneyZAR.Zero, (acc, r) => acc + r.UifDifference);

        sb.AppendLine(string.Join(";",
            "T",
            recordCount.ToString(Invariant),
            FormatAmount(totalPayeDiff),
            FormatAmount(totalGrossDiff),
            FormatAmount(totalUifDiff)));

        return Result<string>.Success(sb.ToString());
    }

    // CTL-SARS-004: Invariant culture formatting — satisfies CA1305.
    // Negative amounts are rendered with a leading minus sign (e.g. "-500.00").
    private static string FormatAmount(MoneyZAR money) =>
        string.Format(Invariant, "{0:0.00}", money.Amount);
}
