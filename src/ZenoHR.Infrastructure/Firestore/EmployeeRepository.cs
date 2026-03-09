// REQ-HR-001, REQ-SEC-005, CTL-POPIA-001: Employee Firestore repository.
// Tenant isolation enforced on every read and query.
// POPIA: employees are never deleted — terminated records retained permanently.

using Microsoft.Extensions.Logging;
using Google.Cloud.Firestore;
using ZenoHR.Domain.Common;
using ZenoHR.Domain.Errors;
using ZenoHR.Module.Employee.Aggregates;

namespace ZenoHR.Infrastructure.Firestore;

/// <summary>
/// Firestore repository for the <c>employees</c> root collection.
/// Every read and write is scoped to <c>tenant_id</c> (REQ-SEC-005).
/// Employees are never deleted — POPIA data retention enforced (CTL-POPIA-001).
/// </summary>
public sealed class EmployeeRepository : BaseFirestoreRepository<Employee>
{
    public EmployeeRepository(FirestoreDb db, ILogger<EmployeeRepository> logger) : base(db, logger) { }

    protected override string CollectionName => "employees";
    protected override ZenoHrErrorCode NotFoundErrorCode => ZenoHrErrorCode.EmployeeNotFound;

    // ── Hydration ────────────────────────────────────────────────────────────

    protected override Employee FromSnapshot(DocumentSnapshot snapshot)
    {
        var employmentStatus = ParseEmploymentStatus(snapshot.GetValue<string>("employment_status"));
        var employeeType = ParseEmployeeType(snapshot.GetValue<string>("employee_type"));

        string? maritalStatus = null;
        snapshot.TryGetValue("marital_status", out maritalStatus);

        string? taxReference = null;
        snapshot.TryGetValue("tax_reference", out taxReference);

        string? workEmail = null;
        snapshot.TryGetValue("work_email", out workEmail);

        string? bankAccountRef = null;
        snapshot.TryGetValue("bank_account_ref", out bankAccountRef);

        string? reportsToEmployeeId = null;
        snapshot.TryGetValue("reports_to_employee_id", out reportsToEmployeeId);

        string? setaRegistrationNumber = null;
        snapshot.TryGetValue("seta_registration_number", out setaRegistrationNumber);

        string? disabilityDescription = null;
        snapshot.TryGetValue("disability_description", out disabilityDescription);

        string? employmentEquityCategory = null;
        snapshot.TryGetValue("employment_equity_category", out employmentEquityCategory);

        string? archiveReason = null;
        snapshot.TryGetValue("archive_reason", out archiveReason);

        DateTimeOffset? archivedAt = null;
        if (snapshot.TryGetValue<Timestamp>("archived_at", out var archivedAtTs))
            archivedAt = archivedAtTs.ToDateTimeOffset();

        return Employee.Reconstitute(
            employeeId: snapshot.Id,
            tenantId: snapshot.GetValue<string>("tenant_id"),
            firebaseUid: snapshot.GetValue<string>("firebase_uid"),
            legalName: snapshot.GetValue<string>("legal_name"),
            nationalIdOrPassport: snapshot.GetValue<string>("national_id_or_passport"),
            taxReference: taxReference,
            dateOfBirth: ToDateOnly(snapshot.GetValue<Timestamp>("date_of_birth")),
            personalPhoneNumber: snapshot.GetValue<string>("personal_phone_number"),
            personalEmail: snapshot.GetValue<string>("personal_email"),
            workEmail: workEmail,
            maritalStatus: maritalStatus,
            nationality: snapshot.GetValue<string>("nationality"),
            gender: snapshot.GetValue<string>("gender"),
            race: snapshot.GetValue<string>("race"),
            disabilityStatus: snapshot.GetValue<bool>("disability_status"),
            disabilityDescription: disabilityDescription,
            employmentEquityCategory: employmentEquityCategory,
            hireDate: ToDateOnly(snapshot.GetValue<Timestamp>("hire_date")),
            employmentStatus: employmentStatus,
            employeeType: employeeType,
            departmentId: snapshot.GetValue<string>("department_id"),
            roleId: snapshot.GetValue<string>("role_id"),
            systemRole: snapshot.GetValue<string>("system_role"),
            reportsToEmployeeId: reportsToEmployeeId,
            bankAccountRef: bankAccountRef,
            setaRegistrationNumber: setaRegistrationNumber,
            dataStatus: snapshot.GetValue<string>("data_status"),
            archivedAt: archivedAt,
            archiveReason: archiveReason,
            createdAt: snapshot.GetValue<Timestamp>("created_at").ToDateTimeOffset(),
            createdBy: snapshot.GetValue<string>("created_by"),
            updatedAt: snapshot.GetValue<Timestamp>("updated_at").ToDateTimeOffset(),
            updatedBy: snapshot.GetValue<string>("updated_by"));
    }

    // ── Serialisation ────────────────────────────────────────────────────────

    protected override Dictionary<string, object?> ToDocument(Employee emp) => new()
    {
        // REQ-SEC-005: tenant_id must be present on every document
        ["tenant_id"] = emp.TenantId,
        ["employee_id"] = emp.EmployeeId,
        ["firebase_uid"] = emp.FirebaseUid,
        ["legal_name"] = emp.LegalName,
        ["national_id_or_passport"] = emp.NationalIdOrPassport,
        ["tax_reference"] = emp.TaxReference,
        ["date_of_birth"] = ToTimestamp(emp.DateOfBirth),
        ["personal_phone_number"] = emp.PersonalPhoneNumber,
        ["personal_email"] = emp.PersonalEmail,
        ["work_email"] = emp.WorkEmail,
        ["marital_status"] = emp.MaritalStatus,
        ["nationality"] = emp.Nationality,
        ["gender"] = emp.Gender,
        ["race"] = emp.Race,
        ["disability_status"] = emp.DisabilityStatus,
        ["disability_description"] = emp.DisabilityDescription,
        ["employment_equity_category"] = emp.EmploymentEquityCategory,
        ["hire_date"] = ToTimestamp(emp.HireDate),
        // employment_status stored as lowercase enum string
        ["employment_status"] = emp.EmploymentStatus.ToString().ToLowerInvariant(),
        ["employee_type"] = emp.EmployeeType.ToString(),
        ["department_id"] = emp.DepartmentId,
        ["role_id"] = emp.RoleId,
        ["system_role"] = emp.SystemRole,
        ["reports_to_employee_id"] = emp.ReportsToEmployeeId,
        ["bank_account_ref"] = emp.BankAccountRef,
        ["seta_registration_number"] = emp.SetaRegistrationNumber,
        ["data_status"] = emp.DataStatus,
        ["archived_at"] = emp.ArchivedAt.HasValue ? Timestamp.FromDateTimeOffset(emp.ArchivedAt.Value) : (object?)null,
        ["archive_reason"] = emp.ArchiveReason,
        ["created_at"] = Timestamp.FromDateTimeOffset(emp.CreatedAt),
        ["updated_at"] = Timestamp.FromDateTimeOffset(emp.UpdatedAt),
        ["created_by"] = emp.CreatedBy,
        ["updated_by"] = emp.UpdatedBy,
        ["schema_version"] = emp.SchemaVersion,
        ["data_classification"] = "restricted",
    };

    // ── Public reads ─────────────────────────────────────────────────────────

    /// <summary>Gets an employee by their ID, verifying tenant ownership. REQ-HR-001</summary>
    public Task<Result<Employee>> GetByEmployeeIdAsync(
        string tenantId, string employeeId, CancellationToken ct = default)
        => GetByIdAsync(tenantId, employeeId, ct);

    /// <summary>
    /// Gets an employee by their Firebase UID within the given tenant.
    /// Used by auth middleware to resolve the employee record from the JWT claim.
    /// REQ-SEC-002
    /// </summary>
    public async Task<Result<Employee>> GetByFirebaseUidAsync(
        string tenantId, string firebaseUid, CancellationToken ct = default)
    {
        var query = TenantQuery(tenantId)
            .WhereEqualTo("firebase_uid", firebaseUid)
            .Limit(1);

        var results = await ExecuteQueryAsync(query, ct);
        return results.Count == 0
            ? Result<Employee>.Failure(ZenoHrErrorCode.EmployeeNotFound,
                $"No employee found with Firebase UID '{firebaseUid}' in tenant '{tenantId}'.")
            : Result<Employee>.Success(results[0]);
    }

    /// <summary>
    /// Lists all active employees for a tenant.
    /// Used to build the employee selector for payroll runs. REQ-HR-003
    /// </summary>
    public Task<IReadOnlyList<Employee>> ListActiveAsync(
        string tenantId, CancellationToken ct = default)
    {
        var query = TenantQuery(tenantId)
            .WhereEqualTo("employment_status", "active")
            .OrderBy("legal_name");
        return ExecuteQueryAsync(query, ct);
    }

    /// <summary>
    /// Lists all employees for a tenant (active, suspended, terminated).
    /// Ordered alphabetically for the employee management screen.
    /// REQ-HR-001
    /// </summary>
    public Task<IReadOnlyList<Employee>> ListByTenantAsync(
        string tenantId, CancellationToken ct = default)
    {
        var query = TenantQuery(tenantId).OrderBy("legal_name");
        return ExecuteQueryAsync(query, ct);
    }

    /// <summary>
    /// Lists active employees in a specific department.
    /// Used for Manager-scoped views (REQ-SEC-002) and department headcount.
    /// </summary>
    public Task<IReadOnlyList<Employee>> ListByDepartmentAsync(
        string tenantId, string departmentId, CancellationToken ct = default)
    {
        var query = TenantQuery(tenantId)
            .WhereEqualTo("department_id", departmentId)
            .WhereEqualTo("employment_status", "active")
            .OrderBy("legal_name");
        return ExecuteQueryAsync(query, ct);
    }

    // ── Writes ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Upserts an employee document. Creates or overwrites.
    /// POPIA: Never call with intent to delete — terminated employees remain. REQ-HR-001
    /// </summary>
    public Task<Result> SaveAsync(Employee employee, CancellationToken ct = default)
        => SetDocumentAsync(employee.EmployeeId, employee, ct);

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static DateOnly ToDateOnly(Timestamp ts) =>
        DateOnly.FromDateTime(ts.ToDateTime());

    private static Timestamp ToTimestamp(DateOnly date) =>
        Timestamp.FromDateTime(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

    private static EmploymentStatus ParseEmploymentStatus(string value) => value switch
    {
        "active" => EmploymentStatus.Active,
        "suspended" => EmploymentStatus.Suspended,
        "terminated" => EmploymentStatus.Terminated,
        _ => EmploymentStatus.Unknown,
    };

    private static EmployeeType ParseEmployeeType(string value) =>
        Enum.TryParse<EmployeeType>(value, out var result) ? result : EmployeeType.Unknown;
}
