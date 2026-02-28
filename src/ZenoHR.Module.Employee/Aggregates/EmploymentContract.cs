// REQ-HR-001, REQ-HR-003, CTL-BCEA-003: Employment contract entity.
// Firestore schema: docs/schemas/firestore-collections.md §5.4.
// Only one contract may be active per employee at any time.
// Base salary stored as MoneyZAR (string in Firestore) — never float/double.
using ZenoHR.Domain.Common;
using ZenoHR.Domain.Errors;
using ZenoHR.Module.Employee.Events;

namespace ZenoHR.Module.Employee.Aggregates;

/// <summary>
/// Entity representing one employment contract for an employee.
/// Contracts form a non-overlapping timeline. Only one may have
/// <see cref="IsActive"/> == <c>true</c> at any point in time.
/// </summary>
public sealed class EmploymentContract
{
    // ── Identity ──────────────────────────────────────────────────────────────

    /// <summary>Firestore document ID. Pattern: <c>con_&lt;uuid7&gt;</c>. Immutable.</summary>
    public string ContractId { get; }

    /// <summary>Tenant isolation key. Immutable.</summary>
    public string TenantId { get; }

    /// <summary>FK reference to <c>employees</c> collection. Immutable.</summary>
    public string EmployeeId { get; }

    // ── Period ────────────────────────────────────────────────────────────────

    /// <summary>Contract effective start date.</summary>
    public DateOnly StartDate { get; }

    /// <summary>Contract end date. Null for indefinite (permanent) contracts.</summary>
    public DateOnly? EndDate { get; private set; }

    // ── Salary ────────────────────────────────────────────────────────────────

    /// <summary>Salary denomination: monthly, weekly, or hourly.</summary>
    public SalaryBasis SalaryBasis { get; }

    /// <summary>Base salary in South African Rand. Stored as string in Firestore for decimal precision.</summary>
    public MoneyZAR BaseSalary { get; private set; }

    /// <summary>Contracted ordinary hours per week. Used for BCEA working-time validation.</summary>
    public decimal OrdinaryHoursPerWeek { get; }

    /// <summary>Reference to the active hours policy version at contract creation.</summary>
    public string OrdinaryHoursPolicyVersion { get; }

    // ── Classification ────────────────────────────────────────────────────────

    /// <summary>Occupational level (e.g. Junior, Senior, Management, Executive).</summary>
    public string OccupationalLevel { get; }

    /// <summary>Whether this is the current active contract for the employee.</summary>
    public bool IsActive { get; private set; }

    // ── Audit ─────────────────────────────────────────────────────────────────

    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public string CreatedBy { get; }
    public string SchemaVersion { get; } = "1.0";

    // ── Domain events ─────────────────────────────────────────────────────────

    private readonly List<object> _domainEvents = [];

    /// <summary>Pops accumulated domain events after the entity is persisted.</summary>
    public IReadOnlyList<object> PopDomainEvents()
    {
        var events = _domainEvents.ToList();
        _domainEvents.Clear();
        return events;
    }

    // ── Constructor (private — use factory) ───────────────────────────────────

    private EmploymentContract(
        string contractId,
        string tenantId,
        string employeeId,
        DateOnly startDate,
        DateOnly? endDate,
        SalaryBasis salaryBasis,
        MoneyZAR baseSalary,
        decimal ordinaryHoursPerWeek,
        string ordinaryHoursPolicyVersion,
        string occupationalLevel,
        string actorId,
        DateTimeOffset now)
    {
        ContractId = contractId;
        TenantId = tenantId;
        EmployeeId = employeeId;
        StartDate = startDate;
        EndDate = endDate;
        SalaryBasis = salaryBasis;
        BaseSalary = baseSalary;
        OrdinaryHoursPerWeek = ordinaryHoursPerWeek;
        OrdinaryHoursPolicyVersion = ordinaryHoursPolicyVersion;
        OccupationalLevel = occupationalLevel;
        IsActive = true;
        CreatedAt = now;
        UpdatedAt = now;
        CreatedBy = actorId;
    }

    // ── Factory ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new <see cref="EmploymentContract"/> in Active state.
    /// Raises <see cref="EmployeeUpdatedEvent"/> on the parent employee.
    /// </summary>
    public static Result<EmploymentContract> Create(
        string contractId,
        string tenantId,
        string employeeId,
        DateOnly startDate,
        DateOnly? endDate,
        SalaryBasis salaryBasis,
        MoneyZAR baseSalary,
        decimal ordinaryHoursPerWeek,
        string ordinaryHoursPolicyVersion,
        string occupationalLevel,
        string actorId,
        DateTimeOffset now)
    {
        // REQ-HR-001: Validate at aggregate boundary.
        if (string.IsNullOrWhiteSpace(contractId))
            return Result<EmploymentContract>.Failure(ZenoHrErrorCode.ValidationFailed, "ContractId is required.");
        if (string.IsNullOrWhiteSpace(tenantId))
            return Result<EmploymentContract>.Failure(ZenoHrErrorCode.ValidationFailed, "TenantId is required.");
        if (string.IsNullOrWhiteSpace(employeeId))
            return Result<EmploymentContract>.Failure(ZenoHrErrorCode.ValidationFailed, "EmployeeId is required.");
        if (salaryBasis == SalaryBasis.Unknown)
            return Result<EmploymentContract>.Failure(ZenoHrErrorCode.ValidationFailed, "SalaryBasis must not be Unknown.");
        if (baseSalary <= MoneyZAR.Zero)
            return Result<EmploymentContract>.Failure(ZenoHrErrorCode.ValueOutOfRange, "BaseSalary must be positive.");
        if (ordinaryHoursPerWeek <= 0 || ordinaryHoursPerWeek > 45)
            return Result<EmploymentContract>.Failure(ZenoHrErrorCode.ValueOutOfRange,
                "OrdinaryHoursPerWeek must be between 1 and 45 (BCEA §9 maximum).");
        if (string.IsNullOrWhiteSpace(occupationalLevel))
            return Result<EmploymentContract>.Failure(ZenoHrErrorCode.ValidationFailed, "OccupationalLevel is required.");
        if (string.IsNullOrWhiteSpace(ordinaryHoursPolicyVersion))
            return Result<EmploymentContract>.Failure(ZenoHrErrorCode.ValidationFailed, "OrdinaryHoursPolicyVersion is required.");
        if (endDate.HasValue && endDate.Value < startDate)
            return Result<EmploymentContract>.Failure(ZenoHrErrorCode.ValueOutOfRange,
                "EndDate must be on or after StartDate.");

        var contract = new EmploymentContract(
            contractId, tenantId, employeeId, startDate, endDate,
            salaryBasis, baseSalary, ordinaryHoursPerWeek, ordinaryHoursPolicyVersion,
            occupationalLevel, actorId, now);

        contract._domainEvents.Add(new EmployeeUpdatedEvent(employeeId, ["contract"])
            { TenantId = tenantId, ActorId = actorId });

        return Result<EmploymentContract>.Success(contract);
    }

    // ── Mutations ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Updates the base salary on the active contract.
    /// Used for annual increments — does NOT create a new contract version.
    /// </summary>
    public Result<EmploymentContract> UpdateSalary(MoneyZAR newSalary, DateTimeOffset now, string actorId)
    {
        if (!IsActive)
            return Result<EmploymentContract>.Failure(ZenoHrErrorCode.ContractNotFound,
                "Cannot update salary on an inactive contract.");
        if (newSalary <= MoneyZAR.Zero)
            return Result<EmploymentContract>.Failure(ZenoHrErrorCode.ValueOutOfRange, "New salary must be positive.");

        BaseSalary = newSalary;
        UpdatedAt = now;

        _domainEvents.Add(new EmployeeUpdatedEvent(EmployeeId, ["base_salary_zar"])
            { TenantId = TenantId, ActorId = actorId });
        return Result<EmploymentContract>.Success(this);
    }

    /// <summary>
    /// Deactivates this contract (superseded by a newer contract).
    /// </summary>
    public void Deactivate(DateTimeOffset now)
    {
        IsActive = false;
        UpdatedAt = now;
    }

    // ── Reconstitution ────────────────────────────────────────────────────────

    /// <summary>Reconstitutes a contract from Firestore. No domain events raised.</summary>
    public static EmploymentContract Reconstitute(
        string contractId, string tenantId, string employeeId,
        DateOnly startDate, DateOnly? endDate, SalaryBasis salaryBasis,
        MoneyZAR baseSalary, decimal ordinaryHoursPerWeek,
        string ordinaryHoursPolicyVersion, string occupationalLevel,
        bool isActive, DateTimeOffset createdAt, DateTimeOffset updatedAt, string createdBy)
    {
        var c = new EmploymentContract(contractId, tenantId, employeeId, startDate, endDate,
            salaryBasis, baseSalary, ordinaryHoursPerWeek, ordinaryHoursPolicyVersion,
            occupationalLevel, createdBy, createdAt)
        {
            IsActive = isActive,
            UpdatedAt = updatedAt,
        };
        return c;
    }
}
