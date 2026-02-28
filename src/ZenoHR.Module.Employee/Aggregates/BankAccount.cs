// REQ-HR-001, CTL-POPIA-005: Bank account subcollection entity.
// Subcollection: employees/{emp_id}/bank_accounts — encrypted at rest.
// Only one account may be is_primary = true per employee at any time.

using ZenoHR.Domain.Errors;

namespace ZenoHR.Module.Employee.Aggregates;

/// <summary>
/// Bank account record for an employee (subcollection of employees/{emp_id}/bank_accounts).
/// Account numbers are write-once; to change accounts, deactivate existing and create a new record.
/// CTL-POPIA-005: PII (account_number, account_holder_name) encrypted at rest.
/// </summary>
public sealed class BankAccount
{
    // ── Identity ──────────────────────────────────────────────────────────────

    public string BankAccountId { get; }
    public string TenantId { get; }
    public string EmployeeId { get; }

    // ── Account details ───────────────────────────────────────────────────────

    public string AccountHolderName { get; }
    public string BankName { get; }
    public string AccountNumber { get; }
    public string BranchCode { get; }
    public BankAccountType AccountType { get; }
    public bool IsPrimary { get; private set; }

    // ── Validity ──────────────────────────────────────────────────────────────

    public DateOnly EffectiveFrom { get; }
    public DateOnly? EffectiveTo { get; private set; }

    // ── Audit ─────────────────────────────────────────────────────────────────

    public DateTimeOffset CreatedAt { get; }
    public string CreatedBy { get; }
    public string SchemaVersion { get; } = "1.0";

    // ── Constructor ───────────────────────────────────────────────────────────

    private BankAccount(
        string bankAccountId, string tenantId, string employeeId,
        string accountHolderName, string bankName, string accountNumber,
        string branchCode, BankAccountType accountType, bool isPrimary,
        DateOnly effectiveFrom, DateTimeOffset createdAt, string createdBy)
    {
        BankAccountId = bankAccountId;
        TenantId = tenantId;
        EmployeeId = employeeId;
        AccountHolderName = accountHolderName;
        BankName = bankName;
        AccountNumber = accountNumber;
        BranchCode = branchCode;
        AccountType = accountType;
        IsPrimary = isPrimary;
        EffectiveFrom = effectiveFrom;
        CreatedAt = createdAt;
        CreatedBy = createdBy;
    }

    // ── Factory ───────────────────────────────────────────────────────────────

    public static Result<BankAccount> Create(
        string bankAccountId, string tenantId, string employeeId,
        string accountHolderName, string bankName, string accountNumber,
        string branchCode, BankAccountType accountType, bool isPrimary,
        DateOnly effectiveFrom, DateTimeOffset now, string createdBy)
    {
        if (string.IsNullOrWhiteSpace(bankAccountId))
            return Result<BankAccount>.Failure(ZenoHrErrorCode.ValidationFailed, "BankAccountId is required.");
        if (string.IsNullOrWhiteSpace(tenantId))
            return Result<BankAccount>.Failure(ZenoHrErrorCode.ValidationFailed, "TenantId is required.");
        if (string.IsNullOrWhiteSpace(employeeId))
            return Result<BankAccount>.Failure(ZenoHrErrorCode.ValidationFailed, "EmployeeId is required.");
        if (string.IsNullOrWhiteSpace(accountHolderName))
            return Result<BankAccount>.Failure(ZenoHrErrorCode.ValidationFailed, "AccountHolderName is required.");
        if (string.IsNullOrWhiteSpace(bankName))
            return Result<BankAccount>.Failure(ZenoHrErrorCode.ValidationFailed, "BankName is required.");
        if (string.IsNullOrWhiteSpace(accountNumber))
            return Result<BankAccount>.Failure(ZenoHrErrorCode.ValidationFailed, "AccountNumber is required.");
        if (string.IsNullOrWhiteSpace(branchCode) || branchCode.Length != 6)
            return Result<BankAccount>.Failure(ZenoHrErrorCode.ValidationFailed, "BranchCode must be 6 digits.");
        if (accountType == BankAccountType.Unknown)
            return Result<BankAccount>.Failure(ZenoHrErrorCode.ValidationFailed, "AccountType must not be Unknown.");

        return Result<BankAccount>.Success(new BankAccount(
            bankAccountId, tenantId, employeeId,
            accountHolderName, bankName, accountNumber,
            branchCode, accountType, isPrimary,
            effectiveFrom, now, createdBy));
    }

    // ── Mutations ─────────────────────────────────────────────────────────────

    /// <summary>Deactivates this account when superseded by a new primary account.</summary>
    public void Deactivate(DateOnly effectiveTo) => EffectiveTo = effectiveTo;

    /// <summary>Clears primary flag when another account is set as primary.</summary>
    public void ClearPrimary() => IsPrimary = false;

    // ── Reconstitution ────────────────────────────────────────────────────────

    public static BankAccount Reconstitute(
        string bankAccountId, string tenantId, string employeeId,
        string accountHolderName, string bankName, string accountNumber,
        string branchCode, BankAccountType accountType, bool isPrimary,
        DateOnly effectiveFrom, DateOnly? effectiveTo,
        DateTimeOffset createdAt, string createdBy)
    {
        var ba = new BankAccount(
            bankAccountId, tenantId, employeeId,
            accountHolderName, bankName, accountNumber,
            branchCode, accountType, isPrimary,
            effectiveFrom, createdAt, createdBy)
        {
            EffectiveTo = effectiveTo,
        };
        return ba;
    }
}

// ── Enums ─────────────────────────────────────────────────────────────────────

public enum BankAccountType
{
    Unknown = 0,
    Cheque = 1,
    Savings = 2,
    Transmission = 3,
}
