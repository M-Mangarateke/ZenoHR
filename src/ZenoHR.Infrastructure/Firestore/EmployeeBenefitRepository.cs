// REQ-HR-001, REQ-HR-003: Employee benefit Firestore repository.
// Subcollection: employees/{emp_id}/benefits.
// Contribution rates feed payroll deduction calculations (REQ-HR-003).

using Google.Cloud.Firestore;
using ZenoHR.Domain.Errors;
using ZenoHR.Module.Employee.Aggregates;

namespace ZenoHR.Infrastructure.Firestore;

/// <summary>
/// Repository for the <c>employees/{emp_id}/benefits</c> subcollection.
/// REQ-HR-003: Active benefit rates are fetched by the payroll engine to calculate deductions.
/// Contribution rates stored as decimal strings (MoneyZAR precision rules).
/// </summary>
public sealed class EmployeeBenefitRepository
{
    private readonly FirestoreDb _db;

    public EmployeeBenefitRepository(FirestoreDb db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    private CollectionReference BenefitsCollection(string employeeId) =>
        _db.Collection("employees").Document(employeeId).Collection("benefits");

    // ── Reads ─────────────────────────────────────────────────────────────────

    /// <summary>Returns all benefit records for an employee.</summary>
    public async Task<IReadOnlyList<EmployeeBenefit>> ListByEmployeeAsync(
        string tenantId, string employeeId, CancellationToken ct = default)
    {
        var snapshot = await BenefitsCollection(employeeId)
            .WhereEqualTo("tenant_id", tenantId)
            .GetSnapshotAsync(ct);
        return snapshot.Documents.Select(FromSnapshot).ToList();
    }

    /// <summary>
    /// Returns only active benefits for an employee.
    /// Used by the payroll engine to compute deduction amounts (REQ-HR-003).
    /// </summary>
    public async Task<IReadOnlyList<EmployeeBenefit>> ListActiveByEmployeeAsync(
        string tenantId, string employeeId, CancellationToken ct = default)
    {
        var snapshot = await BenefitsCollection(employeeId)
            .WhereEqualTo("tenant_id", tenantId)
            .WhereEqualTo("is_active", true)
            .GetSnapshotAsync(ct);
        return snapshot.Documents.Select(FromSnapshot).ToList();
    }

    /// <summary>Gets a specific benefit record by ID.</summary>
    public async Task<Result<EmployeeBenefit>> GetByIdAsync(
        string tenantId, string employeeId, string benefitId, CancellationToken ct = default)
    {
        var docRef = BenefitsCollection(employeeId).Document(benefitId);
        var snapshot = await docRef.GetSnapshotAsync(ct);

        if (!snapshot.Exists)
            return Result<EmployeeBenefit>.Failure(ZenoHrErrorCode.BenefitNotFound, $"Benefit {benefitId} not found.");

        if (snapshot.TryGetValue<string>("tenant_id", out var snapshotTenantId)
            && !string.Equals(snapshotTenantId, tenantId, StringComparison.Ordinal))
            return Result<EmployeeBenefit>.Failure(ZenoHrErrorCode.BenefitNotFound, $"Benefit {benefitId} not found.");

        return Result<EmployeeBenefit>.Success(FromSnapshot(snapshot));
    }

    // ── Writes ────────────────────────────────────────────────────────────────

    /// <summary>Upserts a benefit record.</summary>
    public async Task<Result> SaveAsync(
        string employeeId, EmployeeBenefit benefit, CancellationToken ct = default)
    {
        var docRef = BenefitsCollection(employeeId).Document(benefit.BenefitId);
        await docRef.SetAsync(ToDocument(benefit), cancellationToken: ct);
        return Result.Success();
    }

    // ── Hydration ─────────────────────────────────────────────────────────────

    private static EmployeeBenefit FromSnapshot(DocumentSnapshot s)
    {
        DateOnly? effectiveTo = null;
        if (s.TryGetValue<Timestamp>("effective_to", out var etTs))
            effectiveTo = DateOnly.FromDateTime(etTs.ToDateTime());

        // Contribution rates stored as decimal strings per MoneyZAR precision rules
        var empRate = decimal.Parse(
            s.GetValue<string>("employee_contribution_rate"),
            System.Globalization.CultureInfo.InvariantCulture);
        var erRate = decimal.Parse(
            s.GetValue<string>("employer_contribution_rate"),
            System.Globalization.CultureInfo.InvariantCulture);

        return EmployeeBenefit.Reconstitute(
            benefitId: s.Id,
            tenantId: s.GetValue<string>("tenant_id"),
            employeeId: s.GetValue<string>("employee_id"),
            benefitType: ParseBenefitType(s.GetValue<string>("benefit_type")),
            providerName: s.GetValue<string>("provider_name"),
            membershipNumber: s.GetValue<string>("membership_number"),
            planName: s.GetValue<string>("plan_name"),
            employeeContributionRate: empRate,
            employerContributionRate: erRate,
            effectiveFrom: DateOnly.FromDateTime(s.GetValue<Timestamp>("effective_from").ToDateTime()),
            effectiveTo: effectiveTo,
            isActive: s.GetValue<bool>("is_active"),
            createdAt: s.GetValue<Timestamp>("created_at").ToDateTimeOffset(),
            updatedAt: s.GetValue<Timestamp>("updated_at").ToDateTimeOffset());
    }

    private static Dictionary<string, object?> ToDocument(EmployeeBenefit b) => new()
    {
        ["tenant_id"] = b.TenantId,
        ["benefit_id"] = b.BenefitId,
        ["employee_id"] = b.EmployeeId,
        ["benefit_type"] = ToBenefitTypeString(b.BenefitType),
        ["provider_name"] = b.ProviderName,
        ["membership_number"] = b.MembershipNumber,
        ["plan_name"] = b.PlanName,
        // MoneyZAR precision: rates stored as decimal strings (e.g. "0.07500")
        ["employee_contribution_rate"] = b.EmployeeContributionRate.ToString("G29", System.Globalization.CultureInfo.InvariantCulture),
        ["employer_contribution_rate"] = b.EmployerContributionRate.ToString("G29", System.Globalization.CultureInfo.InvariantCulture),
        ["effective_from"] = Timestamp.FromDateTime(b.EffectiveFrom.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)),
        ["effective_to"] = b.EffectiveTo.HasValue
            ? Timestamp.FromDateTime(b.EffectiveTo.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc))
            : (object?)null,
        ["is_active"] = b.IsActive,
        ["created_at"] = Timestamp.FromDateTimeOffset(b.CreatedAt),
        ["updated_at"] = Timestamp.FromDateTimeOffset(b.UpdatedAt),
        ["schema_version"] = b.SchemaVersion,
    };

    private static string ToBenefitTypeString(BenefitType t) => t switch
    {
        BenefitType.MedicalAid => "medical_aid",
        BenefitType.PensionFund => "pension_fund",
        BenefitType.ProvidentFund => "provident_fund",
        BenefitType.GroupLife => "group_life",
        _ => "medical_aid",
    };

    private static BenefitType ParseBenefitType(string v) => v switch
    {
        "medical_aid" => BenefitType.MedicalAid,
        "pension_fund" => BenefitType.PensionFund,
        "provident_fund" => BenefitType.ProvidentFund,
        "group_life" => BenefitType.GroupLife,
        _ => BenefitType.Unknown,
    };
}
