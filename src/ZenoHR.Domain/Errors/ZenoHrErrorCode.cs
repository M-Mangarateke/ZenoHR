// REQ-OPS-001: ZenoHrErrorCode — typed error taxonomy for all domain failures.
// Every Result<T>.Failure carries one of these codes — no stringly-typed errors.
// Ranges: 0=unknown, 1xxx=validation, 2xxx=employee, 3xxx=payroll, 4xxx=leave,
//         5xxx=compliance, 6xxx=audit, 7xxx=auth, 8xxx=infrastructure.

namespace ZenoHR.Domain.Errors;

/// <summary>
/// Typed error codes for all ZenoHR domain failures.
/// Used as the primary key for error handling, retries, and UI error messages.
/// </summary>
public enum ZenoHrErrorCode
{
    Unknown = 0,

    // ── 1xxx — Validation ────────────────────────────────────────────────────
    ValidationFailed = 1000,
    RequiredFieldMissing = 1001,
    InvalidFormat = 1002,
    ValueOutOfRange = 1003,
    DuplicateValue = 1004,

    // ── 2xxx — Employee ──────────────────────────────────────────────────────
    EmployeeNotFound = 2000,
    EmployeeAlreadyExists = 2001,
    InvalidEmployeeState = 2002,
    ContractNotFound = 2003,
    ContractAlreadyActive = 2004,
    BankAccountNotFound = 2005,
    NextOfKinNotFound = 2006,
    BenefitNotFound = 2007,

    // ── 3xxx — Payroll ───────────────────────────────────────────────────────
    PayrollRunNotFound = 3000,
    PayrollRunAlreadyFinalized = 3001,
    PayslipInvariantViolation = 3002,
    InvalidPayFrequency = 3003,
    PayrollCalculationFailed = 3004,
    StatutoryRuleSetNotFound = 3005,
    PayrollRunInWrongState = 3006,
    PayrollResultNotFound = 3007,
    PayrollAdjustmentNotFound = 3008,

    // ── 4xxx — Leave ─────────────────────────────────────────────────────────
    LeaveRequestNotFound = 4000,
    InsufficientLeaveBalance = 4001,
    LeaveRequestAlreadyProcessed = 4002,
    InvalidLeaveType = 4003,
    LeaveOverlapsExistingRequest = 4004,
    SelfApprovalNotAllowed = 4005,
    LeaveBalanceNotFound = 4006,

    // ── 5xxx — Compliance ────────────────────────────────────────────────────
    ComplianceSubmissionNotFound = 5000,
    FilingDeadlineExceeded = 5001,
    InvalidFilingPeriod = 5002,
    EfilingConnectionFailed = 5003,
    ComplianceCheckFailed = 5004,
    InvalidBreachStatusTransition = 5005,
    BreachNotInRequiredStatus = 5006,

    // ── 6xxx — Audit ─────────────────────────────────────────────────────────
    HashChainBroken = 6000,
    AuditEventNotFound = 6001,
    AuditEventImmutable = 6002,

    // ── 7xxx — Auth / RBAC ───────────────────────────────────────────────────
    Unauthorized = 7000,
    Forbidden = 7001,
    TenantNotFound = 7002,
    UserNotFound = 7003,
    InvalidToken = 7004,

    // ── 8xxx — Infrastructure ────────────────────────────────────────────────
    FirestoreUnavailable = 8000,
    FirestoreWriteConflict = 8001,
    PdfGenerationFailed = 8002,
    EmailDeliveryFailed = 8003,
    BlobStorageUnavailable = 8004,
}
