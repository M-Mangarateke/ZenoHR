// REQ-HR-003, CTL-SARS-001, PRD-16: PayrollRun aggregate root with state machine.
// Firestore schema: docs/schemas/firestore-collections.md §8.1.
// State machine: Draft → Calculated → Finalized → Filed.
// Finalized records are IMMUTABLE — no fields can be updated except the terminal Filed transition.
using ZenoHR.Domain.Common;
using ZenoHR.Domain.Errors;
using ZenoHR.Module.Payroll.Calculation;
using ZenoHR.Module.Payroll.Events;

namespace ZenoHR.Module.Payroll.Aggregates;

/// <summary>
/// Aggregate root representing one payroll execution cycle (monthly or weekly).
/// Holds aggregate totals, lifecycle state, and the list of included employees.
/// Per-employee results are stored in the <c>payroll_runs/{id}/payroll_results</c> sub-collection
/// and modelled separately as <c>PayrollResult</c> entities.
/// </summary>
public sealed class PayrollRun
{
    // ── Identity ──────────────────────────────────────────────────────────────

    /// <summary>Firestore document ID. Deterministic: <c>pr_&lt;YYYY&gt;_&lt;MM&gt;_&lt;seq&gt;</c> for monthly.</summary>
    public string Id { get; }

    /// <summary>Tenant isolation key. Every query filters by this field.</summary>
    public string TenantId { get; }

    // ── Period ────────────────────────────────────────────────────────────────

    /// <summary>Period identifier. Monthly: <c>YYYY-MM</c>. Weekly: <c>YYYY-W&lt;WW&gt;</c>.</summary>
    public string Period { get; }

    /// <summary>Pay frequency: Monthly or Weekly.</summary>
    public PayFrequency RunType { get; }

    /// <summary>Tax year this run belongs to. Used to select the correct <c>StatutoryRuleSet</c>.</summary>
    public TaxYear TaxYear { get; }

    // ── Status ────────────────────────────────────────────────────────────────

    /// <summary>Current lifecycle state. See state machine in <see cref="PayrollRunStatus"/>.</summary>
    public PayrollRunStatus Status { get; private set; }

    /// <summary>Data lifecycle state (POPIA retention compliance).</summary>
    public PayrollRunDataStatus DataStatus { get; private set; }

    // ── Rule set version ──────────────────────────────────────────────────────

    /// <summary>Statutory rule set version used for all calculations in this run.</summary>
    public string RuleSetVersion { get; private set; }

    // ── Employees ─────────────────────────────────────────────────────────────

    /// <summary>Employee IDs included in this run. Populated during Draft status.</summary>
    public IReadOnlyList<string> EmployeeIds { get; private set; }

    /// <summary>Number of employees included. Matches <see cref="EmployeeIds"/>.Count after calculation.</summary>
    public int EmployeeCount => EmployeeIds.Count;

    // ── Aggregate totals (populated on Calculated transition) ─────────────────

    /// <summary>Total gross pay for all employees. Zero until Calculated.</summary>
    public MoneyZAR GrossTotal { get; private set; }

    /// <summary>Total PAYE withheld. Zero until Calculated.</summary>
    public MoneyZAR PayeTotal { get; private set; }

    /// <summary>Total UIF (employee + employer). Zero until Calculated.</summary>
    public MoneyZAR UifTotal { get; private set; }

    /// <summary>Total SDL (employer only). Zero until Calculated.</summary>
    public MoneyZAR SdlTotal { get; private set; }

    /// <summary>Total ETI incentive amount. Zero until Calculated.</summary>
    public MoneyZAR EtiTotal { get; private set; }

    /// <summary>Total deductions (PAYE + UIF employee + pension + medical + other). Zero until Calculated.</summary>
    public MoneyZAR DeductionTotal { get; private set; }

    /// <summary>Total net pay (gross − deductions). Zero until Calculated.</summary>
    public MoneyZAR NetTotal { get; private set; }

    // ── Compliance ────────────────────────────────────────────────────────────

    /// <summary>
    /// Compliance flags written during calculation (e.g. <c>"CTL-SARS-001:PASS"</c>).
    /// Critical flags (not :PASS or :WARN) block the Finalize transition.
    /// </summary>
    public IReadOnlyList<string> ComplianceFlags { get; private set; }

    /// <summary>
    /// SHA-256 checksum of sorted payroll result payloads + rule set version.
    /// Set on finalization. Integrity proof for immutability enforcement.
    /// </summary>
    public string? Checksum { get; private set; }

    // ── Audit fields ──────────────────────────────────────────────────────────

    /// <summary>Actor ID (firebase_uid) who created this run.</summary>
    public string InitiatedBy { get; }

    /// <summary>Server timestamp when this document was created.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>Timestamp when calculation completed. Null until Calculated.</summary>
    public DateTimeOffset? CalculatedAt { get; private set; }

    /// <summary>Actor ID of Director or HRManager who finalized the run.</summary>
    public string? FinalizedBy { get; private set; }

    /// <summary>Timestamp of finalization. Null until Finalized.</summary>
    public DateTimeOffset? FinalizedAt { get; private set; }

    /// <summary>Timestamp when EMP201 CSV was downloaded (Filed). Null until Filed.</summary>
    public DateTimeOffset? FiledAt { get; private set; }

    /// <summary>Idempotency token for creation. Prevents duplicate run creation on retries.</summary>
    public string IdempotencyKey { get; }

    public string SchemaVersion { get; } = "1.0";

    // ── Domain events ─────────────────────────────────────────────────────────

    private readonly List<object> _domainEvents = [];

    /// <summary>
    /// Pops all accumulated domain events, clearing the internal list.
    /// Called by the infrastructure layer after the aggregate is persisted.
    /// </summary>
    public IReadOnlyList<object> PopDomainEvents()
    {
        var events = _domainEvents.ToList();
        _domainEvents.Clear();
        return events;
    }

    // ── Constructor (private — use factory methods) ───────────────────────────

    private PayrollRun(
        string id,
        string tenantId,
        string period,
        PayFrequency runType,
        TaxYear taxYear,
        string ruleSetVersion,
        IReadOnlyList<string> employeeIds,
        string initiatedBy,
        string idempotencyKey,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        Period = period;
        RunType = runType;
        TaxYear = taxYear;
        RuleSetVersion = ruleSetVersion;
        EmployeeIds = employeeIds;
        InitiatedBy = initiatedBy;
        IdempotencyKey = idempotencyKey;
        CreatedAt = createdAt;
        Status = PayrollRunStatus.Draft;
        DataStatus = PayrollRunDataStatus.Active;
        GrossTotal = MoneyZAR.Zero;
        PayeTotal = MoneyZAR.Zero;
        UifTotal = MoneyZAR.Zero;
        SdlTotal = MoneyZAR.Zero;
        EtiTotal = MoneyZAR.Zero;
        DeductionTotal = MoneyZAR.Zero;
        NetTotal = MoneyZAR.Zero;
        ComplianceFlags = [];
    }

    // ── Factory method ────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new <see cref="PayrollRun"/> in <see cref="PayrollRunStatus.Draft"/> status
    /// and raises <see cref="PayrollRunCreatedEvent"/>.
    /// </summary>
    /// <param name="id">Deterministic Firestore document ID.</param>
    /// <param name="tenantId">Tenant isolation key.</param>
    /// <param name="period">Period identifier (e.g. <c>2026-03</c> for March 2026).</param>
    /// <param name="runType">Monthly or Weekly.</param>
    /// <param name="employeeIds">Employee IDs to include in this run.</param>
    /// <param name="ruleSetVersion">Statutory rule set version string.</param>
    /// <param name="initiatedBy">Firebase UID of the creating Director or HRManager.</param>
    /// <param name="idempotencyKey">Idempotency token for creation.</param>
    /// <param name="now">Creation timestamp (injected for testability).</param>
    /// <returns><see cref="Result{T}"/> wrapping the new run, or a validation error.</returns>
    public static Result<PayrollRun> Create(
        string id,
        string tenantId,
        string period,
        PayFrequency runType,
        IReadOnlyList<string> employeeIds,
        string ruleSetVersion,
        string initiatedBy,
        string idempotencyKey,
        DateTimeOffset now)
    {
        // REQ-HR-003: Validate inputs at aggregate boundary.
        if (string.IsNullOrWhiteSpace(id))
            return Result<PayrollRun>.Failure(ZenoHrErrorCode.ValidationFailed, "PayrollRun id is required.");
        if (string.IsNullOrWhiteSpace(tenantId))
            return Result<PayrollRun>.Failure(ZenoHrErrorCode.ValidationFailed, "TenantId is required.");
        if (string.IsNullOrWhiteSpace(period))
            return Result<PayrollRun>.Failure(ZenoHrErrorCode.ValidationFailed, "Period is required.");
        if (runType == PayFrequency.Unknown)
            return Result<PayrollRun>.Failure(ZenoHrErrorCode.ValidationFailed, "RunType must be Monthly or Weekly.");
        if (employeeIds.Count == 0)
            return Result<PayrollRun>.Failure(ZenoHrErrorCode.ValidationFailed, "At least one employee is required.");
        if (string.IsNullOrWhiteSpace(ruleSetVersion))
            return Result<PayrollRun>.Failure(ZenoHrErrorCode.ValidationFailed, "RuleSetVersion is required.");
        if (string.IsNullOrWhiteSpace(initiatedBy))
            return Result<PayrollRun>.Failure(ZenoHrErrorCode.ValidationFailed, "InitiatedBy (actor ID) is required.");

        var taxYear = TaxYear.ForDate(DateOnly.FromDateTime(now.UtcDateTime));
        var run = new PayrollRun(id, tenantId, period, runType, taxYear, ruleSetVersion,
            employeeIds, initiatedBy, idempotencyKey, now);

        run._domainEvents.Add(new PayrollRunCreatedEvent(id, period, runType.ToString()) { TenantId = tenantId, ActorId = initiatedBy });

        return Result<PayrollRun>.Success(run);
    }

    // ── State machine transitions ─────────────────────────────────────────────

    /// <summary>
    /// Transitions the run from <see cref="PayrollRunStatus.Draft"/> to
    /// <see cref="PayrollRunStatus.Calculated"/> once the calculation pipeline completes.
    /// Raises <see cref="PayrollRunCalculatedEvent"/>.
    /// </summary>
    public Result<PayrollRun> MarkCalculated(
        MoneyZAR grossTotal,
        MoneyZAR payeTotal,
        MoneyZAR uifTotal,
        MoneyZAR sdlTotal,
        MoneyZAR etiTotal,
        MoneyZAR deductionTotal,
        MoneyZAR netTotal,
        IReadOnlyList<string> complianceFlags,
        string actorId,
        DateTimeOffset now)
    {
        if (Status != PayrollRunStatus.Draft)
            return Result<PayrollRun>.Failure(ZenoHrErrorCode.PayrollRunInWrongState,
                $"Cannot mark Calculated: run is in {Status} status (expected Draft).");

        GrossTotal = grossTotal;
        PayeTotal = payeTotal;
        UifTotal = uifTotal;
        SdlTotal = sdlTotal;
        EtiTotal = etiTotal;
        DeductionTotal = deductionTotal;
        NetTotal = netTotal;
        ComplianceFlags = complianceFlags;
        CalculatedAt = now;
        Status = PayrollRunStatus.Calculated;

        _domainEvents.Add(new PayrollRunCalculatedEvent(
            Id, Period, EmployeeCount, grossTotal, payeTotal, uifTotal, sdlTotal, netTotal,
            complianceFlags) { TenantId = TenantId, ActorId = actorId });

        return Result<PayrollRun>.Success(this);
    }

    /// <summary>
    /// Finalizes the run, making it <strong>immutable</strong>.
    /// Requires <see cref="PayrollRunStatus.Calculated"/> status.
    /// Blocks if any critical compliance flag is present.
    /// Raises <see cref="PayrollRunFinalizedEvent"/>.
    /// </summary>
    /// <param name="checksum">SHA-256 checksum of sorted payroll result payloads + rule set version.</param>
    /// <param name="finalizedBy">Firebase UID of Director or HRManager who clicked Finalize &amp; Lock.</param>
    /// <param name="now">Finalization timestamp.</param>
    public Result<PayrollRun> Finalize(string checksum, string finalizedBy, DateTimeOffset now)
    {
        if (Status != PayrollRunStatus.Calculated)
            return Result<PayrollRun>.Failure(ZenoHrErrorCode.PayrollRunInWrongState,
                $"Cannot finalize: run is in {Status} status (expected Calculated).");

        // Critical compliance flags (not :PASS or :WARN) block finalization (CTL-SARS-001).
        var criticalFlags = ComplianceFlags
            .Where(f => !f.EndsWith(":PASS", StringComparison.OrdinalIgnoreCase)
                     && !f.EndsWith(":WARN", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (criticalFlags.Count > 0)
            return Result<PayrollRun>.Failure(ZenoHrErrorCode.ComplianceCheckFailed,
                $"Finalization blocked by {criticalFlags.Count} critical compliance flag(s): {string.Join(", ", criticalFlags)}.");

        if (string.IsNullOrWhiteSpace(checksum))
            return Result<PayrollRun>.Failure(ZenoHrErrorCode.ValidationFailed, "Checksum is required for finalization.");
        if (string.IsNullOrWhiteSpace(finalizedBy))
            return Result<PayrollRun>.Failure(ZenoHrErrorCode.ValidationFailed, "FinalizedBy (actor ID) is required.");

        Checksum = checksum;
        FinalizedBy = finalizedBy;
        FinalizedAt = now;
        Status = PayrollRunStatus.Finalized;

        _domainEvents.Add(new PayrollRunFinalizedEvent(
            Id, Period, EmployeeCount, GrossTotal, PayeTotal, NetTotal, checksum)
            { TenantId = TenantId, ActorId = finalizedBy });

        return Result<PayrollRun>.Success(this);
    }

    /// <summary>
    /// Marks the run as Filed after the EMP201 CSV has been downloaded.
    /// This is the only permitted write on an otherwise immutable Finalized document.
    /// Raises <see cref="PayrollRunFiledEvent"/>.
    /// </summary>
    public Result<PayrollRun> MarkFiled(string actorId, DateTimeOffset now)
    {
        if (Status != PayrollRunStatus.Finalized)
            return Result<PayrollRun>.Failure(ZenoHrErrorCode.PayrollRunInWrongState,
                $"Cannot mark Filed: run is in {Status} status (expected Finalized).");

        FiledAt = now;
        Status = PayrollRunStatus.Filed;

        _domainEvents.Add(new PayrollRunFiledEvent(Id, Period) { TenantId = TenantId, ActorId = actorId });

        return Result<PayrollRun>.Success(this);
    }

    // ── Guard: immutability enforcement ──────────────────────────────────────

    /// <summary>
    /// Returns true when the run is in a terminal, immutable state.
    /// Infrastructure layer must enforce this: no fields except <c>status</c>
    /// (for the Filed transition) may be written to Firestore after this returns true.
    /// </summary>
    public bool IsImmutable => Status == PayrollRunStatus.Finalized || Status == PayrollRunStatus.Filed;

    // ── Reconstitution (from Firestore) ───────────────────────────────────────

    /// <summary>
    /// Reconstitutes a <see cref="PayrollRun"/> from raw Firestore fields.
    /// No domain events are raised — this is a read-path operation only.
    /// </summary>
    public static PayrollRun Reconstitute(
        string id, string tenantId, string period, PayFrequency runType, TaxYear taxYear,
        string ruleSetVersion, IReadOnlyList<string> employeeIds, string initiatedBy,
        string idempotencyKey, DateTimeOffset createdAt, PayrollRunStatus status,
        PayrollRunDataStatus dataStatus, MoneyZAR grossTotal, MoneyZAR payeTotal,
        MoneyZAR uifTotal, MoneyZAR sdlTotal, MoneyZAR etiTotal, MoneyZAR deductionTotal,
        MoneyZAR netTotal, IReadOnlyList<string> complianceFlags, string? checksum,
        DateTimeOffset? calculatedAt, string? finalizedBy, DateTimeOffset? finalizedAt,
        DateTimeOffset? filedAt)
    {
        var run = new PayrollRun(id, tenantId, period, runType, taxYear, ruleSetVersion,
            employeeIds, initiatedBy, idempotencyKey, createdAt)
        {
            Status = status,
            DataStatus = dataStatus,
            GrossTotal = grossTotal,
            PayeTotal = payeTotal,
            UifTotal = uifTotal,
            SdlTotal = sdlTotal,
            EtiTotal = etiTotal,
            DeductionTotal = deductionTotal,
            NetTotal = netTotal,
            ComplianceFlags = complianceFlags,
            Checksum = checksum,
            CalculatedAt = calculatedAt,
            FinalizedBy = finalizedBy,
            FinalizedAt = finalizedAt,
            FiledAt = filedAt
        };
        return run;
    }
}
