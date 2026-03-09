// REQ-HR-001, CTL-POPIA-005: Bank account Firestore repository.
// Subcollection: employees/{emp_id}/bank_accounts.
// Invariant: only one account may be is_primary=true per employee at any time.

using Microsoft.Extensions.Logging;
using Google.Cloud.Firestore;
using ZenoHR.Domain.Common;
using ZenoHR.Domain.Errors;
using ZenoHR.Module.Employee.Aggregates;

namespace ZenoHR.Infrastructure.Firestore;

/// <summary>
/// Repository for the <c>employees/{emp_id}/bank_accounts</c> subcollection.
/// Not derived from BaseFirestoreRepository because the collection path is per-employee.
/// CTL-POPIA-005: account_number is masked (last 4 digits) on all read outputs.
/// </summary>
public sealed class BankAccountRepository
{
    private readonly FirestoreDb _db;

    public BankAccountRepository(FirestoreDb db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    private CollectionReference BankAccountsCollection(string employeeId) =>
        _db.Collection("employees").Document(employeeId).Collection("bank_accounts");

    // ── Reads ─────────────────────────────────────────────────────────────────

    /// <summary>Returns all bank accounts for an employee.</summary>
    public async Task<IReadOnlyList<BankAccount>> ListByEmployeeAsync(
        string tenantId, string employeeId, CancellationToken ct = default)
    {
        var snapshot = await BankAccountsCollection(employeeId)
            .WhereEqualTo("tenant_id", tenantId)
            .GetSnapshotAsync(ct);
        return snapshot.Documents.Select(FromSnapshot).ToList();
    }

    /// <summary>Returns the current primary bank account for payroll disbursement, or null.</summary>
    public async Task<BankAccount?> GetPrimaryAsync(
        string tenantId, string employeeId, CancellationToken ct = default)
    {
        var snapshot = await BankAccountsCollection(employeeId)
            .WhereEqualTo("tenant_id", tenantId)
            .WhereEqualTo("is_primary", true)
            .Limit(1)
            .GetSnapshotAsync(ct);
        return snapshot.Documents.Count > 0 ? FromSnapshot(snapshot.Documents[0]) : null;
    }

    /// <summary>Gets a single bank account by ID, verifying tenant ownership.</summary>
    public async Task<Result<BankAccount>> GetByIdAsync(
        string tenantId, string employeeId, string bankAccountId, CancellationToken ct = default)
    {
        var docRef = BankAccountsCollection(employeeId).Document(bankAccountId);
        var snapshot = await docRef.GetSnapshotAsync(ct);

        if (!snapshot.Exists)
            return Result<BankAccount>.Failure(ZenoHrErrorCode.BankAccountNotFound, $"Bank account {bankAccountId} not found.");

        if (snapshot.TryGetValue<string>("tenant_id", out var snapshotTenantId)
            && !string.Equals(snapshotTenantId, tenantId, StringComparison.Ordinal))
            return Result<BankAccount>.Failure(ZenoHrErrorCode.BankAccountNotFound, $"Bank account {bankAccountId} not found.");

        return Result<BankAccount>.Success(FromSnapshot(snapshot));
    }

    // ── Writes ────────────────────────────────────────────────────────────────

    /// <summary>Upserts a bank account record.</summary>
    public async Task<Result> SaveAsync(
        string employeeId, BankAccount account, CancellationToken ct = default)
    {
        var docRef = BankAccountsCollection(employeeId).Document(account.BankAccountId);
        await docRef.SetAsync(ToDocument(account), cancellationToken: ct);
        return Result.Success();
    }

    // ── Hydration ─────────────────────────────────────────────────────────────

    private static BankAccount FromSnapshot(DocumentSnapshot s)
    {
        DateOnly? effectiveTo = null;
        if (s.TryGetValue<Timestamp>("effective_to", out var etTs))
            effectiveTo = DateOnly.FromDateTime(etTs.ToDateTime());

        return BankAccount.Reconstitute(
            bankAccountId: s.Id,
            tenantId: s.GetValue<string>("tenant_id"),
            employeeId: s.GetValue<string>("employee_id"),
            accountHolderName: s.GetValue<string>("account_holder_name"),
            bankName: s.GetValue<string>("bank_name"),
            accountNumber: s.GetValue<string>("account_number"),
            branchCode: s.GetValue<string>("branch_code"),
            accountType: ParseAccountType(s.GetValue<string>("account_type")),
            isPrimary: s.GetValue<bool>("is_primary"),
            effectiveFrom: DateOnly.FromDateTime(s.GetValue<Timestamp>("effective_from").ToDateTime()),
            effectiveTo: effectiveTo,
            createdAt: s.GetValue<Timestamp>("created_at").ToDateTimeOffset(),
            createdBy: s.GetValue<string>("created_by"));
    }

    private static Dictionary<string, object?> ToDocument(BankAccount a) => new()
    {
        ["tenant_id"] = a.TenantId,
        ["bank_account_id"] = a.BankAccountId,
        ["employee_id"] = a.EmployeeId,
        ["account_holder_name"] = a.AccountHolderName,
        ["bank_name"] = a.BankName,
        ["account_number"] = a.AccountNumber,
        ["branch_code"] = a.BranchCode,
        ["account_type"] = ToAccountTypeString(a.AccountType),
        ["is_primary"] = a.IsPrimary,
        ["effective_from"] = Timestamp.FromDateTime(a.EffectiveFrom.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)),
        ["effective_to"] = a.EffectiveTo.HasValue
            ? Timestamp.FromDateTime(a.EffectiveTo.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc))
            : (object?)null,
        ["created_at"] = Timestamp.FromDateTimeOffset(a.CreatedAt),
        ["created_by"] = a.CreatedBy,
        ["schema_version"] = a.SchemaVersion,
    };

    private static string ToAccountTypeString(BankAccountType t) => t switch
    {
        BankAccountType.Cheque => "cheque",
        BankAccountType.Savings => "savings",
        BankAccountType.Transmission => "transmission",
        _ => "cheque",
    };

    private static BankAccountType ParseAccountType(string v) => v switch
    {
        "cheque" => BankAccountType.Cheque,
        "savings" => BankAccountType.Savings,
        "transmission" => BankAccountType.Transmission,
        _ => BankAccountType.Unknown,
    };
}
