// REQ-HR-001, CTL-POPIA-005: Next of kin Firestore repository.
// Subcollection: employees/{emp_id}/next_of_kin.

using Microsoft.Extensions.Logging;
using Google.Cloud.Firestore;
using ZenoHR.Domain.Common;
using ZenoHR.Domain.Errors;
using ZenoHR.Module.Employee.Aggregates;

namespace ZenoHR.Infrastructure.Firestore;

/// <summary>
/// Repository for the <c>employees/{emp_id}/next_of_kin</c> subcollection.
/// CTL-POPIA-005: id_or_passport and phone_number are classified as restricted/confidential PII.
/// </summary>
public sealed class NextOfKinRepository
{
    private readonly FirestoreDb _db;

    public NextOfKinRepository(FirestoreDb db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    private CollectionReference NokCollection(string employeeId) =>
        _db.Collection("employees").Document(employeeId).Collection("next_of_kin");

    // ── Reads ─────────────────────────────────────────────────────────────────

    /// <summary>Returns all next-of-kin records for an employee.</summary>
    public async Task<IReadOnlyList<NextOfKin>> ListByEmployeeAsync(
        string tenantId, string employeeId, CancellationToken ct = default)
    {
        var snapshot = await NokCollection(employeeId)
            .WhereEqualTo("tenant_id", tenantId)
            .GetSnapshotAsync(ct);
        return snapshot.Documents.Select(FromSnapshot).ToList();
    }

    /// <summary>Gets a specific next-of-kin record by ID.</summary>
    public async Task<Result<NextOfKin>> GetByIdAsync(
        string tenantId, string employeeId, string nokId, CancellationToken ct = default)
    {
        var docRef = NokCollection(employeeId).Document(nokId);
        var snapshot = await docRef.GetSnapshotAsync(ct);

        if (!snapshot.Exists)
            return Result<NextOfKin>.Failure(ZenoHrErrorCode.NextOfKinNotFound, $"Next of kin {nokId} not found.");

        if (snapshot.TryGetValue<string>("tenant_id", out var snapshotTenantId)
            && !string.Equals(snapshotTenantId, tenantId, StringComparison.Ordinal))
            return Result<NextOfKin>.Failure(ZenoHrErrorCode.NextOfKinNotFound, $"Next of kin {nokId} not found.");

        return Result<NextOfKin>.Success(FromSnapshot(snapshot));
    }

    // ── Writes ────────────────────────────────────────────────────────────────

    /// <summary>Upserts a next-of-kin record.</summary>
    public async Task<Result> SaveAsync(
        string employeeId, NextOfKin nok, CancellationToken ct = default)
    {
        var docRef = NokCollection(employeeId).Document(nok.NokId);
        await docRef.SetAsync(ToDocument(nok), cancellationToken: ct);
        return Result.Success();
    }

    /// <summary>Deletes a next-of-kin record (POPIA right to erasure).</summary>
    public async Task<Result> DeleteAsync(
        string employeeId, string nokId, CancellationToken ct = default)
    {
        var docRef = NokCollection(employeeId).Document(nokId);
        await docRef.DeleteAsync(cancellationToken: ct);
        return Result.Success();
    }

    // ── Hydration ─────────────────────────────────────────────────────────────

    private static NextOfKin FromSnapshot(DocumentSnapshot s)
    {
        string? idOrPassport = null;
        s.TryGetValue("id_or_passport", out idOrPassport);

        string? email = null;
        s.TryGetValue("email", out email);

        return NextOfKin.Reconstitute(
            nokId: s.Id,
            tenantId: s.GetValue<string>("tenant_id"),
            employeeId: s.GetValue<string>("employee_id"),
            fullName: s.GetValue<string>("full_name"),
            relationship: ParseRelationship(s.GetValue<string>("relationship")),
            idOrPassport: idOrPassport,
            phoneNumber: s.GetValue<string>("phone_number"),
            email: email,
            isPrimaryBeneficiary: s.GetValue<bool>("is_primary_beneficiary"),
            createdAt: s.GetValue<Timestamp>("created_at").ToDateTimeOffset(),
            updatedAt: s.GetValue<Timestamp>("updated_at").ToDateTimeOffset());
    }

    private static Dictionary<string, object?> ToDocument(NextOfKin n) => new()
    {
        ["tenant_id"] = n.TenantId,
        ["nok_id"] = n.NokId,
        ["employee_id"] = n.EmployeeId,
        ["full_name"] = n.FullName,
        ["relationship"] = n.Relationship.ToString(),
        ["id_or_passport"] = n.IdOrPassport,
        ["phone_number"] = n.PhoneNumber,
        ["email"] = n.Email,
        ["is_primary_beneficiary"] = n.IsPrimaryBeneficiary,
        ["created_at"] = Timestamp.FromDateTimeOffset(n.CreatedAt),
        ["updated_at"] = Timestamp.FromDateTimeOffset(n.UpdatedAt),
        ["schema_version"] = n.SchemaVersion,
    };

    private static NokRelationship ParseRelationship(string v) => v switch
    {
        "Spouse" => NokRelationship.Spouse,
        "Child" => NokRelationship.Child,
        "Parent" => NokRelationship.Parent,
        "Sibling" => NokRelationship.Sibling,
        "Other" => NokRelationship.Other,
        _ => NokRelationship.Unknown,
    };
}
