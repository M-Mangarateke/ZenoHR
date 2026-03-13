// REQ-HR-001, REQ-HR-003, CTL-BCEA-003: EmploymentContract Firestore repository.
// Collection: employment_contracts (root, not subcollection — cross-employee queries needed).
// base_salary_zar stored as string for decimal precision (MoneyZAR rule).

using Microsoft.Extensions.Logging;
using System.Globalization;
using Google.Cloud.Firestore;
using ZenoHR.Domain.Common;
using ZenoHR.Domain.Errors;
using ZenoHR.Module.Employee.Aggregates;

namespace ZenoHR.Infrastructure.Firestore;

/// <summary>
/// Firestore repository for the <c>employment_contracts</c> root collection.
/// Contracts are NOT subcollections — they are queried cross-employee for compliance reporting.
/// base_salary_zar is stored as a string to preserve decimal precision (CTL-SARS-001).
/// </summary>
public sealed class EmploymentContractRepository : BaseFirestoreRepository<EmploymentContract>
{
    public EmploymentContractRepository(FirestoreDb db, ILogger<EmploymentContractRepository> logger) : base(db, logger) { }

    protected override string CollectionName => "employment_contracts";
    protected override ZenoHrErrorCode NotFoundErrorCode => ZenoHrErrorCode.ContractNotFound;

    // ── Hydration ────────────────────────────────────────────────────────────

    protected override EmploymentContract FromSnapshot(DocumentSnapshot snapshot)
    {
        // base_salary_zar stored as string for decimal precision
        var salaryStr = snapshot.GetValue<string>("base_salary_zar");
        var baseSalary = new MoneyZAR(decimal.Parse(salaryStr,
            CultureInfo.InvariantCulture));

        var salaryBasis = Enum.TryParse<SalaryBasis>(
            snapshot.GetValue<string>("salary_basis"), out var sb) ? sb : SalaryBasis.Unknown;

        DateOnly? endDate = null;
        if (snapshot.TryGetValue<Timestamp>("end_date", out var endTs))
            endDate = DateOnly.FromDateTime(endTs.ToDateTime());

        return EmploymentContract.Reconstitute(
            contractId: snapshot.Id,
            tenantId: snapshot.GetValue<string>("tenant_id"),
            employeeId: snapshot.GetValue<string>("employee_id"),
            startDate: DateOnly.FromDateTime(snapshot.GetValue<Timestamp>("start_date").ToDateTime()),
            endDate: endDate,
            salaryBasis: salaryBasis,
            baseSalary: baseSalary,
            ordinaryHoursPerWeek: ReadDecimal(snapshot, "ordinary_hours_per_week"),
            ordinaryHoursPolicyVersion: snapshot.GetValue<string>("ordinary_hours_policy_version"),
            occupationalLevel: snapshot.GetValue<string>("occupational_level"),
            isActive: snapshot.GetValue<bool>("is_active"),
            createdAt: snapshot.GetValue<Timestamp>("created_at").ToDateTimeOffset(),
            updatedAt: snapshot.GetValue<Timestamp>("updated_at").ToDateTimeOffset(),
            createdBy: snapshot.GetValue<string>("created_by"));
    }

    // ── Serialisation ────────────────────────────────────────────────────────

    protected override Dictionary<string, object?> ToDocument(EmploymentContract c) => new()
    {
        ["tenant_id"] = c.TenantId,
        ["contract_id"] = c.ContractId,
        ["employee_id"] = c.EmployeeId,
        ["start_date"] = Timestamp.FromDateTime(c.StartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)),
        ["end_date"] = c.EndDate.HasValue
            ? Timestamp.FromDateTime(c.EndDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc))
            : (object?)null,
        // salary_basis stored as PascalCase string (matches enum name)
        ["salary_basis"] = c.SalaryBasis.ToString(),
        // base_salary_zar stored as string for decimal precision — never float/double
        ["base_salary_zar"] = c.BaseSalary.Amount.ToString("G", CultureInfo.InvariantCulture),
        // ordinary_hours_per_week stored as string for decimal precision
        ["ordinary_hours_per_week"] = c.OrdinaryHoursPerWeek.ToString(CultureInfo.InvariantCulture),
        ["ordinary_hours_policy_version"] = c.OrdinaryHoursPolicyVersion,
        ["occupational_level"] = c.OccupationalLevel,
        ["is_active"] = c.IsActive,
        ["created_at"] = Timestamp.FromDateTimeOffset(c.CreatedAt),
        ["updated_at"] = Timestamp.FromDateTimeOffset(c.UpdatedAt),
        ["created_by"] = c.CreatedBy,
        ["schema_version"] = c.SchemaVersion,
    };

    // ── Public reads ─────────────────────────────────────────────────────────

    /// <summary>Gets a contract by ID, verifying tenant ownership.</summary>
    public Task<Result<EmploymentContract>> GetByContractIdAsync(
        string tenantId, string contractId, CancellationToken ct = default)
        => GetByIdAsync(tenantId, contractId, ct);

    /// <summary>
    /// Gets the currently active contract for an employee.
    /// Only one contract may be active per employee at any time.
    /// REQ-HR-003: Payroll engines require the active contract to compute base salary.
    /// </summary>
    public async Task<Result<EmploymentContract>> GetActiveContractAsync(
        string tenantId, string employeeId, CancellationToken ct = default)
    {
        var query = TenantQuery(tenantId)
            .WhereEqualTo("employee_id", employeeId)
            .WhereEqualTo("is_active", true)
            .Limit(1);

        var results = await ExecuteQueryAsync(query, ct);
        return results.Count == 0
            ? Result<EmploymentContract>.Failure(ZenoHrErrorCode.ContractNotFound,
                $"No active contract found for employee '{employeeId}'.")
            : Result<EmploymentContract>.Success(results[0]);
    }

    /// <summary>
    /// Lists all contracts for an employee (active and inactive), ordered by start date descending.
    /// </summary>
    public Task<IReadOnlyList<EmploymentContract>> ListByEmployeeAsync(
        string tenantId, string employeeId, CancellationToken ct = default)
    {
        var query = TenantQuery(tenantId)
            .WhereEqualTo("employee_id", employeeId)
            .OrderByDescending("start_date");
        return ExecuteQueryAsync(query, ct);
    }

    /// <summary>
    /// Lists contracts expiring within the given date range (for compliance reminders).
    /// CTL-BCEA-003
    /// </summary>
    public Task<IReadOnlyList<EmploymentContract>> ListExpiringBetweenAsync(
        string tenantId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var fromTs = Timestamp.FromDateTime(from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        var toTs = Timestamp.FromDateTime(to.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

        var query = TenantQuery(tenantId)
            .WhereGreaterThanOrEqualTo("end_date", fromTs)
            .WhereLessThanOrEqualTo("end_date", toTs);
        return ExecuteQueryAsync(query, ct);
    }

    // ── Writes ───────────────────────────────────────────────────────────────

    /// <summary>Upserts a contract. Overwrites if exists (for salary updates and deactivation).</summary>
    public Task<Result> SaveAsync(EmploymentContract contract, CancellationToken ct = default)
        => SetDocumentAsync(contract.ContractId, contract, ct);

    // ── Helpers ──────────────────────────────────────────────────────────────

    // Prefer string (precision-safe); fall back to double/long for legacy data
    private static decimal ReadDecimal(DocumentSnapshot snapshot, string field)
    {
        if (snapshot.TryGetValue<string>(field, out var s) && decimal.TryParse(s, CultureInfo.InvariantCulture, out var parsed)) return parsed;
        if (snapshot.TryGetValue<double>(field, out var d)) return (decimal)d;
        if (snapshot.TryGetValue<long>(field, out var l)) return l;
        return 0m;
    }
}
