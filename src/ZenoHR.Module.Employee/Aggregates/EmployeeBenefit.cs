// REQ-HR-001, REQ-HR-003: Employee benefit subcollection entity.
// Subcollection: employees/{emp_id}/benefits — medical aid, pension, provident, group life.
// Contribution rates drive deduction line items in payroll_results.

using ZenoHR.Domain.Errors;

namespace ZenoHR.Module.Employee.Aggregates;

/// <summary>
/// Employee benefit membership (subcollection of employees/{emp_id}/benefits).
/// REQ-HR-003: Contribution rates (employee + employer) feed directly into payroll deduction calculations.
/// Stored as decimal strings per MoneyZAR precision rules.
/// </summary>
public sealed class EmployeeBenefit
{
    // ── Identity ──────────────────────────────────────────────────────────────

    public string BenefitId { get; }
    public string TenantId { get; }
    public string EmployeeId { get; }

    // ── Benefit details ───────────────────────────────────────────────────────

    public BenefitType BenefitType { get; }
    public string ProviderName { get; private set; }
    public string MembershipNumber { get; private set; }
    public string PlanName { get; private set; }

    /// <summary>Employee contribution as a decimal rate (e.g. 0.075 = 7.5%).</summary>
    public decimal EmployeeContributionRate { get; private set; }

    /// <summary>Employer contribution as a decimal rate (e.g. 0.075 = 7.5%).</summary>
    public decimal EmployerContributionRate { get; private set; }

    // ── Validity ──────────────────────────────────────────────────────────────

    public DateOnly EffectiveFrom { get; }
    public DateOnly? EffectiveTo { get; private set; }
    public bool IsActive { get; private set; }

    // ── Audit ─────────────────────────────────────────────────────────────────

    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public string SchemaVersion { get; } = "1.0";

    // ── Constructor ───────────────────────────────────────────────────────────

    private EmployeeBenefit(
        string benefitId, string tenantId, string employeeId,
        BenefitType benefitType, string providerName, string membershipNumber, string planName,
        decimal employeeContributionRate, decimal employerContributionRate,
        DateOnly effectiveFrom, bool isActive, DateTimeOffset now)
    {
        BenefitId = benefitId;
        TenantId = tenantId;
        EmployeeId = employeeId;
        BenefitType = benefitType;
        ProviderName = providerName;
        MembershipNumber = membershipNumber;
        PlanName = planName;
        EmployeeContributionRate = employeeContributionRate;
        EmployerContributionRate = employerContributionRate;
        EffectiveFrom = effectiveFrom;
        IsActive = isActive;
        CreatedAt = now;
        UpdatedAt = now;
    }

    // ── Factory ───────────────────────────────────────────────────────────────

    public static Result<EmployeeBenefit> Create(
        string benefitId, string tenantId, string employeeId,
        BenefitType benefitType, string providerName, string membershipNumber, string planName,
        decimal employeeContributionRate, decimal employerContributionRate,
        DateOnly effectiveFrom, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(benefitId))
            return Result<EmployeeBenefit>.Failure(ZenoHrErrorCode.ValidationFailed, "BenefitId is required.");
        if (string.IsNullOrWhiteSpace(tenantId))
            return Result<EmployeeBenefit>.Failure(ZenoHrErrorCode.ValidationFailed, "TenantId is required.");
        if (string.IsNullOrWhiteSpace(employeeId))
            return Result<EmployeeBenefit>.Failure(ZenoHrErrorCode.ValidationFailed, "EmployeeId is required.");
        if (benefitType == BenefitType.Unknown)
            return Result<EmployeeBenefit>.Failure(ZenoHrErrorCode.ValidationFailed, "BenefitType must not be Unknown.");
        if (string.IsNullOrWhiteSpace(providerName))
            return Result<EmployeeBenefit>.Failure(ZenoHrErrorCode.ValidationFailed, "ProviderName is required.");
        if (string.IsNullOrWhiteSpace(membershipNumber))
            return Result<EmployeeBenefit>.Failure(ZenoHrErrorCode.ValidationFailed, "MembershipNumber is required.");
        if (employeeContributionRate < 0 || employeeContributionRate > 1)
            return Result<EmployeeBenefit>.Failure(ZenoHrErrorCode.ValueOutOfRange, "EmployeeContributionRate must be between 0 and 1.");
        if (employerContributionRate < 0 || employerContributionRate > 1)
            return Result<EmployeeBenefit>.Failure(ZenoHrErrorCode.ValueOutOfRange, "EmployerContributionRate must be between 0 and 1.");

        return Result<EmployeeBenefit>.Success(new EmployeeBenefit(
            benefitId, tenantId, employeeId, benefitType,
            providerName, membershipNumber, planName,
            employeeContributionRate, employerContributionRate,
            effectiveFrom, isActive: true, now));
    }

    // ── Mutations ─────────────────────────────────────────────────────────────

    public void Deactivate(DateOnly effectiveTo, DateTimeOffset now)
    {
        IsActive = false;
        EffectiveTo = effectiveTo;
        UpdatedAt = now;
    }

    public void UpdateRates(decimal employeeRate, decimal employerRate, DateTimeOffset now)
    {
        EmployeeContributionRate = employeeRate;
        EmployerContributionRate = employerRate;
        UpdatedAt = now;
    }

    // ── Reconstitution ────────────────────────────────────────────────────────

    public static EmployeeBenefit Reconstitute(
        string benefitId, string tenantId, string employeeId,
        BenefitType benefitType, string providerName, string membershipNumber, string planName,
        decimal employeeContributionRate, decimal employerContributionRate,
        DateOnly effectiveFrom, DateOnly? effectiveTo, bool isActive,
        DateTimeOffset createdAt, DateTimeOffset updatedAt)
    {
        var b = new EmployeeBenefit(
            benefitId, tenantId, employeeId, benefitType,
            providerName, membershipNumber, planName,
            employeeContributionRate, employerContributionRate,
            effectiveFrom, isActive, createdAt)
        {
            EffectiveTo = effectiveTo,
            UpdatedAt = updatedAt,
        };
        return b;
    }
}

// ── Enums ─────────────────────────────────────────────────────────────────────

public enum BenefitType
{
    Unknown = 0,
    MedicalAid = 1,
    PensionFund = 2,
    ProvidentFund = 3,
    GroupLife = 4,
}
