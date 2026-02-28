// REQ-HR-001, CTL-POPIA-005: Next of kin / beneficiary subcollection entity.
// Subcollection: employees/{emp_id}/next_of_kin — used for pension death benefit designation.

using ZenoHR.Domain.Errors;

namespace ZenoHR.Module.Employee.Aggregates;

/// <summary>
/// Next-of-kin / beneficiary record (subcollection of employees/{emp_id}/next_of_kin).
/// Legally distinct from emergency contacts — used for pension/provident fund death benefits.
/// CTL-POPIA-005: id_or_passport and phone_number are encrypted at rest.
/// </summary>
public sealed class NextOfKin
{
    // ── Identity ──────────────────────────────────────────────────────────────

    public string NokId { get; }
    public string TenantId { get; }
    public string EmployeeId { get; }

    // ── Person details ────────────────────────────────────────────────────────

    public string FullName { get; private set; }
    public NokRelationship Relationship { get; private set; }
    public string? IdOrPassport { get; private set; }
    public string PhoneNumber { get; private set; }
    public string? Email { get; private set; }
    public bool IsPrimaryBeneficiary { get; private set; }

    // ── Audit ─────────────────────────────────────────────────────────────────

    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public string SchemaVersion { get; } = "1.0";

    // ── Constructor ───────────────────────────────────────────────────────────

    private NextOfKin(
        string nokId, string tenantId, string employeeId,
        string fullName, NokRelationship relationship,
        string? idOrPassport, string phoneNumber, string? email,
        bool isPrimaryBeneficiary, DateTimeOffset now)
    {
        NokId = nokId;
        TenantId = tenantId;
        EmployeeId = employeeId;
        FullName = fullName;
        Relationship = relationship;
        IdOrPassport = idOrPassport;
        PhoneNumber = phoneNumber;
        Email = email;
        IsPrimaryBeneficiary = isPrimaryBeneficiary;
        CreatedAt = now;
        UpdatedAt = now;
    }

    // ── Factory ───────────────────────────────────────────────────────────────

    public static Result<NextOfKin> Create(
        string nokId, string tenantId, string employeeId,
        string fullName, NokRelationship relationship,
        string? idOrPassport, string phoneNumber, string? email,
        bool isPrimaryBeneficiary, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(nokId))
            return Result<NextOfKin>.Failure(ZenoHrErrorCode.ValidationFailed, "NokId is required.");
        if (string.IsNullOrWhiteSpace(tenantId))
            return Result<NextOfKin>.Failure(ZenoHrErrorCode.ValidationFailed, "TenantId is required.");
        if (string.IsNullOrWhiteSpace(employeeId))
            return Result<NextOfKin>.Failure(ZenoHrErrorCode.ValidationFailed, "EmployeeId is required.");
        if (string.IsNullOrWhiteSpace(fullName))
            return Result<NextOfKin>.Failure(ZenoHrErrorCode.ValidationFailed, "FullName is required.");
        if (relationship == NokRelationship.Unknown)
            return Result<NextOfKin>.Failure(ZenoHrErrorCode.ValidationFailed, "Relationship must not be Unknown.");
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return Result<NextOfKin>.Failure(ZenoHrErrorCode.ValidationFailed, "PhoneNumber is required.");

        return Result<NextOfKin>.Success(new NextOfKin(
            nokId, tenantId, employeeId, fullName, relationship,
            idOrPassport, phoneNumber, email, isPrimaryBeneficiary, now));
    }

    // ── Mutations ─────────────────────────────────────────────────────────────

    public void Update(
        string fullName, NokRelationship relationship,
        string? idOrPassport, string phoneNumber, string? email,
        bool isPrimaryBeneficiary, DateTimeOffset now)
    {
        FullName = fullName;
        Relationship = relationship;
        IdOrPassport = idOrPassport;
        PhoneNumber = phoneNumber;
        Email = email;
        IsPrimaryBeneficiary = isPrimaryBeneficiary;
        UpdatedAt = now;
    }

    // ── Reconstitution ────────────────────────────────────────────────────────

    public static NextOfKin Reconstitute(
        string nokId, string tenantId, string employeeId,
        string fullName, NokRelationship relationship,
        string? idOrPassport, string phoneNumber, string? email,
        bool isPrimaryBeneficiary, DateTimeOffset createdAt, DateTimeOffset updatedAt)
    {
        var nok = new NextOfKin(
            nokId, tenantId, employeeId, fullName, relationship,
            idOrPassport, phoneNumber, email, isPrimaryBeneficiary, createdAt)
        {
            UpdatedAt = updatedAt,
        };
        return nok;
    }
}

// ── Enums ─────────────────────────────────────────────────────────────────────

public enum NokRelationship
{
    Unknown = 0,
    Spouse = 1,
    Child = 2,
    Parent = 3,
    Sibling = 4,
    Other = 5,
}
