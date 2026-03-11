// CTL-SARS-004: SARS IRP3 tax directive — instructs employer on special PAYE withholding.
// REQ-HR-003: Monetary values use MoneyZAR (decimal-backed). Never float/double.
using ZenoHR.Domain.Common;

namespace ZenoHR.Module.Payroll.Models;

/// <summary>
/// Immutable record representing a SARS IRP3 tax directive issued to an employer.
/// Covers all 4 directive types: IRP3(a), IRP3(b), IRP3(c), IRP3(s).
/// </summary>
public sealed record TaxDirective
{
    /// <summary>System-generated identifier (format: DIR-{year}-{seq:D4}).</summary>
    public required string DirectiveId { get; init; }

    /// <summary>Tenant isolation key.</summary>
    public required string TenantId { get; init; }

    /// <summary>Employee this directive applies to.</summary>
    public required string EmployeeId { get; init; }

    /// <summary>SARS-issued directive number (7–10 digits).</summary>
    public required string DirectiveNumber { get; init; }

    /// <summary>IRP3 directive type.</summary>
    public required TaxDirectiveType Type { get; init; }

    /// <summary>Lifecycle status.</summary>
    public required TaxDirectiveStatus Status { get; init; }

    /// <summary>Start of the directive's effective period (inclusive).</summary>
    public required DateOnly EffectiveFrom { get; init; }

    /// <summary>End of the directive's effective period (inclusive).</summary>
    public required DateOnly EffectiveTo { get; init; }

    /// <summary>
    /// Directive tax rate as a percentage (0–100). Used by IRP3(c) and IRP3(s).
    /// Null for IRP3(a) and IRP3(b).
    /// </summary>
    public decimal? DirectiveRate { get; init; }

    /// <summary>
    /// Lump sum amount subject to the directive. Used by IRP3(a) and IRP3(b).
    /// Null for IRP3(c) and IRP3(s).
    /// </summary>
    public MoneyZAR? LumpSumAmount { get; init; }

    /// <summary>
    /// Tax payable on the lump sum as determined by SARS. Used by IRP3(a) and IRP3(b).
    /// Null for IRP3(c) and IRP3(s).
    /// </summary>
    public MoneyZAR? TaxOnLumpSum { get; init; }

    /// <summary>Authority or office that issued the directive (e.g., "SARS Pretoria").</summary>
    public required string IssuedBy { get; init; }

    /// <summary>Timestamp when the directive was issued.</summary>
    public required DateTimeOffset IssuedAt { get; init; }

    /// <summary>Optional notes or reference information.</summary>
    public string? Notes { get; init; }

    // ── Computed properties ────────────────────────────────────────────────────

    /// <summary>
    /// Whether this directive is currently active: status is Active and today falls
    /// within the effective date range (inclusive).
    /// </summary>
    public bool IsActive
    {
        get
        {
            if (Status != TaxDirectiveStatus.Active)
                return false;

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            return today >= EffectiveFrom && today <= EffectiveTo;
        }
    }

    /// <summary>
    /// Whether this directive has expired: today is past EffectiveTo and the directive
    /// has not been explicitly revoked.
    /// </summary>
    public bool IsExpired
    {
        get
        {
            if (Status == TaxDirectiveStatus.Revoked)
                return false;

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            return today > EffectiveTo;
        }
    }
}
