// REQ-HR-001, REQ-HR-002, REQ-SEC-002, CTL-POPIA-001: Employee aggregate root.
// Firestore schema: docs/schemas/firestore-collections.md §5.1.
// Employment lifecycle: Active → Suspended → Active | Active → Terminated.
// Terminated employees are NEVER deleted — POPIA data retention enforced.
using ZenoHR.Domain.Common;
using ZenoHR.Domain.Errors;
using ZenoHR.Module.Employee.Events;

namespace ZenoHR.Module.Employee.Aggregates;

/// <summary>
/// Aggregate root representing a Zenowethu employee.
/// Holds identity, employment state, and POPIA-classified personal data.
/// Every Director, HRManager, Manager, and Employee user has exactly one
/// <see cref="Employee"/> document — <c>SaasAdmin</c> does not.
/// </summary>
public sealed class Employee
{
    // ── Identity ─────────────────────────────────────────────────────────────

    /// <summary>Firestore document ID. Pattern: <c>emp_&lt;uuid7&gt;</c>. Immutable.</summary>
    public string EmployeeId { get; }

    /// <summary>Tenant isolation key. Immutable after creation.</summary>
    public string TenantId { get; }

    /// <summary>Firebase Authentication UID. 1:1 with Firebase account. Immutable.</summary>
    public string FirebaseUid { get; }

    // ── Personal data (CTL-POPIA-001: data classification enforced in Firestore) ──

    /// <summary>Full legal name. Validated character set.</summary>
    public string LegalName { get; private set; }

    /// <summary>SA ID number or passport number. Data classification: restricted.</summary>
    public string NationalIdOrPassport { get; private set; }

    /// <summary>SARS tax reference number. Must pass format validation before payroll finalization.</summary>
    public string? TaxReference { get; private set; }

    /// <summary>Date of birth. Required for tax rebate tier and ETI eligibility age check.</summary>
    public DateOnly DateOfBirth { get; }

    /// <summary>Personal mobile/landline. Data classification: confidential.</summary>
    public string PersonalPhoneNumber { get; private set; }

    /// <summary>Personal email for payslip delivery. Data classification: internal.</summary>
    public string PersonalEmail { get; private set; }

    /// <summary>Work email. Null when same as Firebase Auth email.</summary>
    public string? WorkEmail { get; private set; }

    /// <summary>Marital status. Data classification: confidential.</summary>
    public string? MaritalStatus { get; private set; }

    /// <summary>ISO 3166-1 alpha-2 nationality code. Data classification: internal.</summary>
    public string Nationality { get; private set; }

    /// <summary>Gender as per Employment Equity Act reporting. Data classification: confidential.</summary>
    public string Gender { get; private set; }

    /// <summary>Race as per Employment Equity Act S.20 reporting. Data classification: confidential.</summary>
    public string Race { get; private set; }

    /// <summary>Declared disability status. Affects UIF disability eligibility. Data classification: confidential.</summary>
    public bool DisabilityStatus { get; private set; }

    /// <summary>Disability description. Only populated when <see cref="DisabilityStatus"/> is true. Data classification: restricted.</summary>
    public string? DisabilityDescription { get; private set; }

    /// <summary>Derived EEA category string (e.g. African_Female). For EE reporting.</summary>
    public string? EmploymentEquityCategory { get; private set; }

    // ── Employment ────────────────────────────────────────────────────────────

    /// <summary>Employment start date. Used for leave accrual and ETI eligibility.</summary>
    public DateOnly HireDate { get; }

    /// <summary>Current lifecycle state: Active, Suspended, or Terminated.</summary>
    public EmploymentStatus EmploymentStatus { get; private set; }

    /// <summary>Employee type: Permanent, PartTime, Contractor, or Intern.</summary>
    public EmployeeType EmployeeType { get; private set; }

    /// <summary>Primary department. Used for cost-centre allocation and leave routing.</summary>
    public string DepartmentId { get; private set; }

    /// <summary>FK to roles collection or system role constant. Highest-privilege role.</summary>
    public string RoleId { get; private set; }

    /// <summary>System role enum value. Never SaasAdmin for tenant employees.</summary>
    public string SystemRole { get; private set; }

    /// <summary>
    /// Employee ID of direct manager for primary department.
    /// Null for Director/HRManager (self-approve leave — PRD-15 §1.5).
    /// </summary>
    public string? ReportsToEmployeeId { get; private set; }

    /// <summary>SETA registration number for SDL purposes. Data classification: internal.</summary>
    public string? SetaRegistrationNumber { get; private set; }

    /// <summary>Reference to the primary active bank account record. Data classification: restricted.</summary>
    public string? BankAccountRef { get; private set; }

    // ── POPIA data status ─────────────────────────────────────────────────────

    /// <summary>Data lifecycle: active, archived, or legal_hold.</summary>
    public string DataStatus { get; private set; }

    /// <summary>Timestamp when data_status transitioned to archived. Null while active.</summary>
    public DateTimeOffset? ArchivedAt { get; private set; }

    /// <summary>Reason for archival. Null while active.</summary>
    public string? ArchiveReason { get; private set; }

    // ── Audit fields ──────────────────────────────────────────────────────────

    public DateTimeOffset CreatedAt { get; }
    public string CreatedBy { get; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public string UpdatedBy { get; private set; }
    public string SchemaVersion { get; } = "1.0";

    // ── Domain events ─────────────────────────────────────────────────────────

    private readonly List<object> _domainEvents = [];

    /// <summary>Pops accumulated domain events after the aggregate is persisted.</summary>
    public IReadOnlyList<object> PopDomainEvents()
    {
        var events = _domainEvents.ToList();
        _domainEvents.Clear();
        return events;
    }

    // ── Constructor (private — use factory) ───────────────────────────────────

    private Employee(
        string employeeId,
        string tenantId,
        string firebaseUid,
        string legalName,
        string nationalIdOrPassport,
        string? taxReference,
        DateOnly dateOfBirth,
        string personalPhoneNumber,
        string personalEmail,
        string? workEmail,
        string nationality,
        string gender,
        string race,
        bool disabilityStatus,
        string? disabilityDescription,
        DateOnly hireDate,
        EmployeeType employeeType,
        string departmentId,
        string roleId,
        string systemRole,
        string? reportsToEmployeeId,
        string actorId,
        DateTimeOffset now)
    {
        EmployeeId = employeeId;
        TenantId = tenantId;
        FirebaseUid = firebaseUid;
        LegalName = legalName;
        NationalIdOrPassport = nationalIdOrPassport;
        TaxReference = taxReference;
        DateOfBirth = dateOfBirth;
        PersonalPhoneNumber = personalPhoneNumber;
        PersonalEmail = personalEmail;
        WorkEmail = workEmail;
        Nationality = nationality;
        Gender = gender;
        Race = race;
        DisabilityStatus = disabilityStatus;
        DisabilityDescription = disabilityDescription;
        HireDate = hireDate;
        EmploymentStatus = EmploymentStatus.Active;
        EmployeeType = employeeType;
        DepartmentId = departmentId;
        RoleId = roleId;
        SystemRole = systemRole;
        ReportsToEmployeeId = reportsToEmployeeId;
        DataStatus = "active";
        CreatedAt = now;
        CreatedBy = actorId;
        UpdatedAt = now;
        UpdatedBy = actorId;
        // Derive EEA category for Employment Equity Act reporting
        EmploymentEquityCategory = string.IsNullOrWhiteSpace(race) || string.IsNullOrWhiteSpace(gender)
            ? null
            : $"{race}_{gender}";
    }

    // ── Factory ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new <see cref="Employee"/> in <see cref="EmploymentStatus.Active"/> status
    /// and raises <see cref="EmployeeCreatedEvent"/>.
    /// </summary>
    public static Result<Employee> Create(
        string employeeId,
        string tenantId,
        string firebaseUid,
        string legalName,
        string nationalIdOrPassport,
        string? taxReference,
        DateOnly dateOfBirth,
        string personalPhoneNumber,
        string personalEmail,
        string? workEmail,
        string nationality,
        string gender,
        string race,
        bool disabilityStatus,
        string? disabilityDescription,
        DateOnly hireDate,
        EmployeeType employeeType,
        string departmentId,
        string roleId,
        string systemRole,
        string? reportsToEmployeeId,
        string actorId,
        DateTimeOffset now)
    {
        // REQ-HR-001: Validate required fields at aggregate boundary.
        if (string.IsNullOrWhiteSpace(employeeId))
            return Result<Employee>.Failure(ZenoHrErrorCode.ValidationFailed, "EmployeeId is required.");
        if (string.IsNullOrWhiteSpace(tenantId))
            return Result<Employee>.Failure(ZenoHrErrorCode.ValidationFailed, "TenantId is required.");
        if (string.IsNullOrWhiteSpace(firebaseUid))
            return Result<Employee>.Failure(ZenoHrErrorCode.ValidationFailed, "FirebaseUid is required.");
        if (string.IsNullOrWhiteSpace(legalName))
            return Result<Employee>.Failure(ZenoHrErrorCode.ValidationFailed, "LegalName is required.");
        if (string.IsNullOrWhiteSpace(nationalIdOrPassport))
            return Result<Employee>.Failure(ZenoHrErrorCode.ValidationFailed, "NationalIdOrPassport is required.");
        if (string.IsNullOrWhiteSpace(personalPhoneNumber))
            return Result<Employee>.Failure(ZenoHrErrorCode.ValidationFailed, "PersonalPhoneNumber is required.");
        if (string.IsNullOrWhiteSpace(personalEmail))
            return Result<Employee>.Failure(ZenoHrErrorCode.ValidationFailed, "PersonalEmail is required.");
        if (string.IsNullOrWhiteSpace(nationality))
            return Result<Employee>.Failure(ZenoHrErrorCode.ValidationFailed, "Nationality is required.");
        if (string.IsNullOrWhiteSpace(gender))
            return Result<Employee>.Failure(ZenoHrErrorCode.ValidationFailed, "Gender is required.");
        if (string.IsNullOrWhiteSpace(race))
            return Result<Employee>.Failure(ZenoHrErrorCode.ValidationFailed, "Race is required.");
        if (string.IsNullOrWhiteSpace(departmentId))
            return Result<Employee>.Failure(ZenoHrErrorCode.ValidationFailed, "DepartmentId is required.");
        if (string.IsNullOrWhiteSpace(roleId))
            return Result<Employee>.Failure(ZenoHrErrorCode.ValidationFailed, "RoleId is required.");
        if (string.IsNullOrWhiteSpace(systemRole))
            return Result<Employee>.Failure(ZenoHrErrorCode.ValidationFailed, "SystemRole is required.");
        if (string.IsNullOrWhiteSpace(actorId))
            return Result<Employee>.Failure(ZenoHrErrorCode.ValidationFailed, "ActorId is required.");
        if (employeeType == EmployeeType.Unknown)
            return Result<Employee>.Failure(ZenoHrErrorCode.ValidationFailed, "EmployeeType must not be Unknown.");
        if (dateOfBirth >= DateOnly.FromDateTime(now.UtcDateTime))
            return Result<Employee>.Failure(ZenoHrErrorCode.ValueOutOfRange, "DateOfBirth must be in the past.");
        if (hireDate > DateOnly.FromDateTime(now.UtcDateTime).AddDays(30))
            return Result<Employee>.Failure(ZenoHrErrorCode.ValueOutOfRange, "HireDate cannot be more than 30 days in the future.");

        var emp = new Employee(
            employeeId, tenantId, firebaseUid, legalName, nationalIdOrPassport, taxReference,
            dateOfBirth, personalPhoneNumber, personalEmail, workEmail, nationality, gender,
            race, disabilityStatus, disabilityDescription, hireDate, employeeType,
            departmentId, roleId, systemRole, reportsToEmployeeId, actorId, now);

        emp._domainEvents.Add(new EmployeeCreatedEvent(employeeId, legalName, firebaseUid, departmentId, systemRole)
            { TenantId = tenantId, ActorId = actorId });

        return Result<Employee>.Success(emp);
    }

    // ── State machine transitions ─────────────────────────────────────────────

    /// <summary>
    /// Suspends an active employee. Removes them from future payroll runs until reactivated.
    /// </summary>
    public Result<Employee> Suspend(string reason, string actorId, DateTimeOffset now)
    {
        if (EmploymentStatus != EmploymentStatus.Active)
            return Result<Employee>.Failure(ZenoHrErrorCode.InvalidEmployeeState,
                $"Cannot suspend: employee is in {EmploymentStatus} status (expected Active).");
        if (string.IsNullOrWhiteSpace(reason))
            return Result<Employee>.Failure(ZenoHrErrorCode.ValidationFailed, "Suspension reason is required.");

        EmploymentStatus = EmploymentStatus.Suspended;
        UpdatedAt = now;
        UpdatedBy = actorId;

        _domainEvents.Add(new EmployeeSuspendedEvent(EmployeeId, reason) { TenantId = TenantId, ActorId = actorId });
        return Result<Employee>.Success(this);
    }

    /// <summary>
    /// Reactivates a suspended employee, returning them to <see cref="EmploymentStatus.Active"/>.
    /// </summary>
    public Result<Employee> Reactivate(string actorId, DateTimeOffset now)
    {
        if (EmploymentStatus != EmploymentStatus.Suspended)
            return Result<Employee>.Failure(ZenoHrErrorCode.InvalidEmployeeState,
                $"Cannot reactivate: employee is in {EmploymentStatus} status (expected Suspended).");

        EmploymentStatus = EmploymentStatus.Active;
        UpdatedAt = now;
        UpdatedBy = actorId;

        _domainEvents.Add(new EmployeeReactivatedEvent(EmployeeId) { TenantId = TenantId, ActorId = actorId });
        return Result<Employee>.Success(this);
    }

    /// <summary>
    /// Terminates employment. This is irreversible — terminated employees are never deleted (POPIA).
    /// </summary>
    public Result<Employee> Terminate(string terminationReasonCode, DateOnly effectiveDate, string actorId, DateTimeOffset now)
    {
        if (EmploymentStatus == EmploymentStatus.Terminated)
            return Result<Employee>.Failure(ZenoHrErrorCode.InvalidEmployeeState,
                "Employee is already terminated.");
        if (string.IsNullOrWhiteSpace(terminationReasonCode))
            return Result<Employee>.Failure(ZenoHrErrorCode.ValidationFailed, "Termination reason code is required.");

        EmploymentStatus = EmploymentStatus.Terminated;
        UpdatedAt = now;
        UpdatedBy = actorId;

        _domainEvents.Add(new EmployeeTerminatedEvent(EmployeeId, terminationReasonCode, effectiveDate)
            { TenantId = TenantId, ActorId = actorId });
        return Result<Employee>.Success(this);
    }

    // ── Profile updates ───────────────────────────────────────────────────────

    /// <summary>
    /// Updates mutable contact and personal fields. Raises <see cref="EmployeeUpdatedEvent"/>.
    /// Identity fields (EmployeeId, FirebaseUid, TenantId, DateOfBirth, HireDate) are immutable.
    /// </summary>
    public Result<Employee> UpdateProfile(
        string? legalName,
        string? personalPhoneNumber,
        string? personalEmail,
        string? workEmail,
        string? maritalStatus,
        string? taxReference,
        string? bankAccountRef,
        string actorId,
        DateTimeOffset now)
    {
        if (EmploymentStatus == EmploymentStatus.Terminated)
            return Result<Employee>.Failure(ZenoHrErrorCode.InvalidEmployeeState,
                "Cannot update profile of a terminated employee.");

        var changed = new List<string>();

        if (legalName is not null && legalName != LegalName) { LegalName = legalName; changed.Add(nameof(LegalName)); }
        if (personalPhoneNumber is not null && personalPhoneNumber != PersonalPhoneNumber) { PersonalPhoneNumber = personalPhoneNumber; changed.Add(nameof(PersonalPhoneNumber)); }
        if (personalEmail is not null && personalEmail != PersonalEmail) { PersonalEmail = personalEmail; changed.Add(nameof(PersonalEmail)); }
        if (workEmail != WorkEmail) { WorkEmail = workEmail; changed.Add(nameof(WorkEmail)); }
        if (maritalStatus != MaritalStatus) { MaritalStatus = maritalStatus; changed.Add(nameof(MaritalStatus)); }
        if (taxReference != TaxReference) { TaxReference = taxReference; changed.Add(nameof(TaxReference)); }
        if (bankAccountRef != BankAccountRef) { BankAccountRef = bankAccountRef; changed.Add(nameof(BankAccountRef)); }

        if (changed.Count == 0) return Result<Employee>.Success(this);

        UpdatedAt = now;
        UpdatedBy = actorId;

        _domainEvents.Add(new EmployeeUpdatedEvent(EmployeeId, changed) { TenantId = TenantId, ActorId = actorId });
        return Result<Employee>.Success(this);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Calculates age at a given date. Used for ETI eligibility (must be 18–29).
    /// </summary>
    public int AgeAt(DateOnly date) =>
        date.Year - DateOfBirth.Year - (date < DateOfBirth.AddYears(date.Year - DateOfBirth.Year) ? 1 : 0);

    /// <summary>Whether this employee is eligible for ETI at the given date (age 18–29 inclusive).</summary>
    public bool IsEtiAgeEligible(DateOnly date)
    {
        var age = AgeAt(date);
        return age >= 18 && age <= 29;
    }

    // ── Reconstitution (from Firestore) ───────────────────────────────────────

    /// <summary>
    /// Reconstitutes an <see cref="Employee"/> from raw Firestore fields.
    /// No domain events are raised — read-path operation only.
    /// </summary>
    public static Employee Reconstitute(
        string employeeId, string tenantId, string firebaseUid,
        string legalName, string nationalIdOrPassport, string? taxReference,
        DateOnly dateOfBirth, string personalPhoneNumber, string personalEmail,
        string? workEmail, string? maritalStatus, string nationality,
        string gender, string race, bool disabilityStatus, string? disabilityDescription,
        string? employmentEquityCategory, DateOnly hireDate,
        EmploymentStatus employmentStatus, EmployeeType employeeType,
        string departmentId, string roleId, string systemRole, string? reportsToEmployeeId,
        string? bankAccountRef, string? setaRegistrationNumber,
        string dataStatus, DateTimeOffset? archivedAt, string? archiveReason,
        DateTimeOffset createdAt, string createdBy, DateTimeOffset updatedAt, string updatedBy)
    {
        var emp = new Employee(
            employeeId, tenantId, firebaseUid, legalName, nationalIdOrPassport, taxReference,
            dateOfBirth, personalPhoneNumber, personalEmail, workEmail, nationality,
            gender, race, disabilityStatus, disabilityDescription, hireDate,
            employeeType, departmentId, roleId, systemRole, reportsToEmployeeId, createdBy, createdAt)
        {
            MaritalStatus = maritalStatus,
            EmploymentEquityCategory = employmentEquityCategory,
            BankAccountRef = bankAccountRef,
            SetaRegistrationNumber = setaRegistrationNumber,
            EmploymentStatus = employmentStatus,
            DataStatus = dataStatus,
            ArchivedAt = archivedAt,
            ArchiveReason = archiveReason,
            UpdatedAt = updatedAt,
            UpdatedBy = updatedBy,
        };
        return emp;
    }
}
