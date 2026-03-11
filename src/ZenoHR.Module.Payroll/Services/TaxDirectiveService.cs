// CTL-SARS-004: Tax directive registration, validation, status transitions, and active lookup.
// REQ-HR-003: All monetary values use MoneyZAR (decimal-backed).
using System.Globalization;
using System.Text.RegularExpressions;
using ZenoHR.Domain.Common;
using ZenoHR.Domain.Errors;
using ZenoHR.Module.Payroll.Models;

namespace ZenoHR.Module.Payroll.Services;

/// <summary>
/// Domain service for SARS IRP3 tax directives — registration, validation, lifecycle management.
/// </summary>
public sealed partial class TaxDirectiveService
{
    private int _sequenceCounter;

    // ── Directive number validation: 7–10 digits only ──────────────────────────

    [GeneratedRegex(@"^\d{7,10}$", RegexOptions.Compiled)]
    private static partial Regex DirectiveNumberPattern();

    // ── Forward-only status transitions ────────────────────────────────────────

    private static readonly Dictionary<TaxDirectiveStatus, TaxDirectiveStatus[]> AllowedTransitions = new()
    {
        [TaxDirectiveStatus.Pending] = [TaxDirectiveStatus.Active, TaxDirectiveStatus.Revoked],
        [TaxDirectiveStatus.Active] = [TaxDirectiveStatus.Expired, TaxDirectiveStatus.Revoked],
        [TaxDirectiveStatus.Expired] = [TaxDirectiveStatus.Revoked],
        [TaxDirectiveStatus.Revoked] = [],
    };

    // ── RegisterDirective ──────────────────────────────────────────────────────

    /// <summary>
    /// Validates and registers a new tax directive. Returns the created directive or a validation failure.
    /// </summary>
    public Result<TaxDirective> RegisterDirective(
        string tenantId,
        string employeeId,
        string directiveNumber,
        TaxDirectiveType type,
        DateOnly effectiveFrom,
        DateOnly effectiveTo,
        decimal? directiveRate,
        MoneyZAR? lumpSumAmount,
        MoneyZAR? taxOnLumpSum,
        string issuedBy,
        DateTimeOffset issuedAt,
        string? notes = null)
    {
        // CTL-SARS-004: Validate required fields
        if (string.IsNullOrWhiteSpace(tenantId))
            return Result<TaxDirective>.Failure(ZenoHrErrorCode.TaxDirectiveValidationFailed,
                "TenantId is required.");

        if (string.IsNullOrWhiteSpace(employeeId))
            return Result<TaxDirective>.Failure(ZenoHrErrorCode.TaxDirectiveValidationFailed,
                "EmployeeId is required.");

        if (string.IsNullOrWhiteSpace(directiveNumber))
            return Result<TaxDirective>.Failure(ZenoHrErrorCode.TaxDirectiveValidationFailed,
                "DirectiveNumber is required.");

        if (!DirectiveNumberPattern().IsMatch(directiveNumber))
            return Result<TaxDirective>.Failure(ZenoHrErrorCode.TaxDirectiveValidationFailed,
                string.Format(CultureInfo.InvariantCulture,
                    "DirectiveNumber must be 7-10 digits. Got: '{0}'.", directiveNumber));

        if (type == TaxDirectiveType.Unknown)
            return Result<TaxDirective>.Failure(ZenoHrErrorCode.TaxDirectiveValidationFailed,
                "Type must not be Unknown.");

        if (effectiveTo <= effectiveFrom)
            return Result<TaxDirective>.Failure(ZenoHrErrorCode.TaxDirectiveValidationFailed,
                string.Format(CultureInfo.InvariantCulture,
                    "EffectiveTo ({0}) must be after EffectiveFrom ({1}).",
                    effectiveTo.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    effectiveFrom.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));

        if (string.IsNullOrWhiteSpace(issuedBy))
            return Result<TaxDirective>.Failure(ZenoHrErrorCode.TaxDirectiveValidationFailed,
                "IssuedBy is required.");

        // CTL-SARS-004: Type-specific validation
        var typeValidation = ValidateTypeSpecificFields(type, directiveRate, lumpSumAmount, taxOnLumpSum);
        if (typeValidation.IsFailure)
            return typeValidation;

        // Generate directive ID
        var seq = Interlocked.Increment(ref _sequenceCounter);
        var directiveId = string.Format(CultureInfo.InvariantCulture,
            "DIR-{0}-{1}", DateTime.UtcNow.Year, seq.ToString("D4", CultureInfo.InvariantCulture));

        var directive = new TaxDirective
        {
            DirectiveId = directiveId,
            TenantId = tenantId,
            EmployeeId = employeeId,
            DirectiveNumber = directiveNumber,
            Type = type,
            Status = TaxDirectiveStatus.Pending,
            EffectiveFrom = effectiveFrom,
            EffectiveTo = effectiveTo,
            DirectiveRate = directiveRate,
            LumpSumAmount = lumpSumAmount,
            TaxOnLumpSum = taxOnLumpSum,
            IssuedBy = issuedBy,
            IssuedAt = issuedAt,
            Notes = notes
        };

        return Result<TaxDirective>.Success(directive);
    }

    // ── UpdateStatus ───────────────────────────────────────────────────────────

    /// <summary>
    /// Transitions the directive to a new status. Only forward transitions are allowed.
    /// </summary>
    public static Result<TaxDirective> UpdateStatus(TaxDirective existing, TaxDirectiveStatus newStatus)
    {
        ArgumentNullException.ThrowIfNull(existing);

        if (newStatus == TaxDirectiveStatus.Unknown)
            return Result<TaxDirective>.Failure(ZenoHrErrorCode.TaxDirectiveInvalidStatusTransition,
                "Cannot transition to Unknown status.");

        if (!AllowedTransitions.TryGetValue(existing.Status, out var allowed) || !allowed.Contains(newStatus))
            return Result<TaxDirective>.Failure(ZenoHrErrorCode.TaxDirectiveInvalidStatusTransition,
                string.Format(CultureInfo.InvariantCulture,
                    "Cannot transition from {0} to {1}.", existing.Status, newStatus));

        return Result<TaxDirective>.Success(existing with { Status = newStatus });
    }

    // ── GetActiveDirective ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns the active directive for an employee, or a failure if none is active.
    /// </summary>
    public static Result<TaxDirective> GetActiveDirective(IReadOnlyList<TaxDirective> directives, string employeeId)
    {
        ArgumentNullException.ThrowIfNull(directives);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var active = directives.FirstOrDefault(d =>
            d.EmployeeId == employeeId &&
            d.Status == TaxDirectiveStatus.Active &&
            today >= d.EffectiveFrom &&
            today <= d.EffectiveTo);

        return active is not null
            ? Result<TaxDirective>.Success(active)
            : Result<TaxDirective>.Failure(ZenoHrErrorCode.TaxDirectiveNotFound,
                string.Format(CultureInfo.InvariantCulture,
                    "No active tax directive found for employee '{0}'.", employeeId));
    }

    // ── GetExpiredDirectives ───────────────────────────────────────────────────

    /// <summary>
    /// Returns all directives that have passed their EffectiveTo date and are not revoked.
    /// </summary>
    public static IReadOnlyList<TaxDirective> GetExpiredDirectives(IReadOnlyList<TaxDirective> directives)
    {
        ArgumentNullException.ThrowIfNull(directives);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return directives
            .Where(d => d.Status != TaxDirectiveStatus.Revoked && today > d.EffectiveTo)
            .ToList()
            .AsReadOnly();
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private static Result<TaxDirective> ValidateTypeSpecificFields(
        TaxDirectiveType type,
        decimal? directiveRate,
        MoneyZAR? lumpSumAmount,
        MoneyZAR? taxOnLumpSum)
    {
        switch (type)
        {
            case TaxDirectiveType.IRP3a:
            case TaxDirectiveType.IRP3b:
                if (lumpSumAmount is null)
                    return Result<TaxDirective>.Failure(ZenoHrErrorCode.TaxDirectiveValidationFailed,
                        string.Format(CultureInfo.InvariantCulture,
                            "{0} requires LumpSumAmount.", type));
                if (taxOnLumpSum is null)
                    return Result<TaxDirective>.Failure(ZenoHrErrorCode.TaxDirectiveValidationFailed,
                        string.Format(CultureInfo.InvariantCulture,
                            "{0} requires TaxOnLumpSum.", type));
                break;

            case TaxDirectiveType.IRP3c:
            case TaxDirectiveType.IRP3s:
                if (directiveRate is null)
                    return Result<TaxDirective>.Failure(ZenoHrErrorCode.TaxDirectiveValidationFailed,
                        string.Format(CultureInfo.InvariantCulture,
                            "{0} requires DirectiveRate.", type));
                if (directiveRate.Value < 0m || directiveRate.Value > 100m)
                    return Result<TaxDirective>.Failure(ZenoHrErrorCode.TaxDirectiveValidationFailed,
                        string.Format(CultureInfo.InvariantCulture,
                            "DirectiveRate must be between 0 and 100. Got: {0}.", directiveRate.Value));
                break;
        }

        return Result<TaxDirective>.Success(null!); // Placeholder — caller only checks IsFailure
    }
}
