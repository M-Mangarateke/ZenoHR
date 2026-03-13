---
doc_id: DEV-CODING-CONVENTIONS
version: 1.0.0
updated_on: 2026-03-13
---

# Coding Conventions

This document defines the coding standards for the ZenoHR codebase. All contributions must follow these conventions. They are enforced by code review, analyzer rules, and architecture tests.

---

## C# Style

### General Rules

- **Target framework**: `net10.0` (all projects)
- **Nullable reference types**: Enabled globally (`<Nullable>enable</Nullable>`)
- **Implicit usings**: Enabled (`<ImplicitUsings>enable</ImplicitUsings>`)
- **Classes**: `sealed` by default. Only unseal when inheritance is explicitly required and documented.
- **Records**: Use `record` or `record struct` for value objects and DTOs. Prefer records over classes for immutable data.

### Naming Conventions

| Element | Convention | Example |
|---------|-----------|---------|
| Public members | PascalCase | `EmployeeId`, `CalculatePaye()` |
| Private fields | _camelCase | `_logger`, `_tenantId` |
| Parameters | camelCase | `employeeId`, `cancellationToken` |
| Constants | PascalCase | `MaxRetryCount`, `DefaultTimeout` |
| Interfaces | IPascalCase | `IPayslipPdfGenerator` |
| Enums | PascalCase (singular) | `EmploymentStatus`, `LeaveType` |
| Enum members | PascalCase | `FullTime`, `Annual`, `Unknown` |
| Namespaces | Match folder structure | `ZenoHR.Module.Payroll.Calculation` |

### Enum Rules

Every enum **must** include an `Unknown = 0` member as the first value. This ensures forward compatibility when deserializing values from Firestore that may not yet be known to the code.

```csharp
public enum LeaveType
{
    Unknown = 0,       // Always first, always zero
    Annual = 1,
    Sick = 2,
    FamilyResponsibility = 3,
    Maternity = 4,
    Parental = 5,
}
```

---

## Money: Always Decimal via MoneyZAR

This is a **critical rule** (non-negotiable). All monetary values use `decimal` via the `MoneyZAR` value object. Using `float` or `double` for any monetary calculation, storage, or parameter is a **Sev-1 defect**.

```csharp
// CORRECT
var salary = new MoneyZAR(45_000.00m);
var deduction = new MoneyZAR(8_250.00m);
var net = salary - deduction;

// WRONG — Sev-1 defect
double salary = 45000.00;  // NEVER use double for money
float tax = 8250.0f;       // NEVER use float for money
```

### Rounding Rules

| Context | Method | Precision |
|---------|--------|-----------|
| Annual PAYE | `RoundToRand()` | 0 decimal places, AwayFromZero |
| Period PAYE, UIF, SDL, ETI | `RoundToCent()` | 2 decimal places, AwayFromZero |
| After rebate subtraction | `FloorAtZero()` | Clamp to R0.00 minimum |

### Firestore Serialization

MoneyZAR values are stored as **strings** in Firestore (not numbers) to preserve exact decimal precision. Firestore's `number` type is IEEE 754 double and loses precision for monetary values.

```csharp
string stored = salary.ToFirestoreString();        // "45000.00"
MoneyZAR restored = MoneyZAR.FromFirestoreString(stored);
```

See: `docs/schemas/monetary-precision.md`

---

## Async and Cancellation

All I/O operations must be async. Always propagate `CancellationToken` through the call chain.

```csharp
// CORRECT: async with CancellationToken
public async Task<Result<Employee>> GetByEmployeeIdAsync(
    string tenantId, string employeeId, CancellationToken ct)
{
    var snapshot = await _docRef.GetSnapshotAsync(ct);
    // ...
}

// WRONG: synchronous I/O
public Employee GetById(string tenantId, string employeeId)
{
    var snapshot = _docRef.GetSnapshotAsync(default).Result; // Blocks thread
}

// WRONG: missing CancellationToken
public async Task<Employee> GetByIdAsync(string tenantId, string id)
{
    await _docRef.GetSnapshotAsync(default); // Lost cancellation signal
}
```

### Null Checks

Use `ArgumentNullException.ThrowIfNull()` at **public API boundaries only** (constructors, public methods). Do not litter internal code with null checks.

```csharp
public NightlyAnalyticsService(ILogger<NightlyAnalyticsService> logger)
{
    ArgumentNullException.ThrowIfNull(logger);
    _logger = logger;
}
```

---

## Error Handling

### Domain Errors: Result<T>

Business logic failures return `Result<T>` with typed error codes. Never throw exceptions for expected failures.

```csharp
// Domain method
public Result<LeaveRequest> Approve(string approverId, DateTimeOffset now)
{
    if (Status != LeaveRequestStatus.Pending)
        return Result<LeaveRequest>.Failure(
            ZenoHrErrorCode.LeaveRequestAlreadyProcessed,
            $"Cannot approve leave request in status {Status}.");

    Status = LeaveRequestStatus.Approved;
    ApproverId = approverId;
    UpdatedAt = now;
    return Result<LeaveRequest>.Success(this);
}
```

### Error Code Ranges

| Range | Domain | Use For |
|-------|--------|---------|
| 0 | Unknown | Default/fallback |
| 1000-1999 | Validation | `RequiredFieldMissing`, `InvalidFormat`, `ValueOutOfRange`, `DuplicateValue` |
| 2000-2999 | Employee | `EmployeeNotFound`, `InvalidEmployeeState`, `ContractNotFound` |
| 3000-3999 | Payroll | `PayrollRunNotFound`, `PayslipInvariantViolation`, `PayrollRunAlreadyFinalized` |
| 4000-4999 | Leave | `InsufficientLeaveBalance`, `LeaveRequestAlreadyProcessed`, `SelfApprovalNotAllowed` |
| 5000-5999 | Compliance | `ComplianceSubmissionNotFound`, `InvalidFilingPeriod`, `EfilingConnectionFailed` |
| 6000-6999 | Audit | `HashChainBroken`, `AuditEventImmutable` |
| 7000-7999 | Auth/RBAC | `Unauthorized`, `Forbidden`, `TenantNotFound` |
| 8000-8999 | Infrastructure | `FirestoreUnavailable`, `PdfGenerationFailed` |

See: `src/ZenoHR.Domain/Errors/ZenoHrErrorCode.cs`

### Infrastructure Errors

Let infrastructure exceptions (Firestore unavailable, network timeout) propagate naturally. They are caught by `GlobalExceptionMiddleware` and converted to `ProblemDetails`.

### API Validation

Use FluentValidation at the API boundary. Return `ProblemDetails` for validation failures:

```csharp
if (!Enum.TryParse<PayFrequency>(req.RunType, ignoreCase: true, out var runType))
    return Results.BadRequest($"Invalid run type: {req.RunType}. Must be Monthly or Weekly.");
```

---

## No Hardcoded Statutory Values

This is a **critical rule** (non-negotiable). All tax rates, thresholds, leave entitlements, and BCEA limits come from `StatutoryRuleSet` documents in Firestore, seeded from `docs/seed-data/*.json`.

```csharp
// WRONG — hardcoded tax bracket
decimal paye = grossAnnual > 237_100m
    ? (grossAnnual - 237_100m) * 0.26m + 42_678m
    : grossAnnual * 0.18m;

// CORRECT — loaded from StatutoryRuleSet
var brackets = ruleSet.GetBrackets();
decimal paye = brackets.Calculate(grossAnnual);
```

Statutory data files:

| File | Content |
|------|---------|
| `docs/seed-data/sars-paye-2025-2026.json` | PAYE tax brackets, rebates, thresholds |
| `docs/seed-data/sars-uif-sdl.json` | UIF rates and ceilings, SDL rate |
| `docs/seed-data/sars-eti.json` | ETI eligibility, tiers, amounts |
| `docs/seed-data/bcea-working-time.json` | BCEA working time limits |
| `docs/seed-data/bcea-leave.json` | BCEA leave entitlements |

---

## Traceability

Every class, public method, test, and API endpoint **must** have a traceability comment referencing the requirement, control, or test case it implements.

```csharp
// REQ-HR-001: Employee aggregate — manages employee lifecycle
public sealed class Employee { }

// CTL-SARS-001: PAYE calculation uses statutory brackets from Firestore
public sealed class PayeCalculator { }

// TC-PAY-001: Verify PAYE calculation for Bracket 1
[Fact]
public void CalculatePaye_IncomeInBracket1_Returns18Percent() { }
```

### Traceability Prefixes

| Prefix | Meaning | Example |
|--------|---------|---------|
| `REQ-HR-*` | HR/Employee requirement | `REQ-HR-001` |
| `REQ-COMP-*` | Compliance requirement | `REQ-COMP-001` |
| `REQ-SEC-*` | Security requirement | `REQ-SEC-002` |
| `REQ-OPS-*` | Operations requirement | `REQ-OPS-003` |
| `CTL-SARS-*` | SARS tax control | `CTL-SARS-006` |
| `CTL-BCEA-*` | BCEA labour law control | `CTL-BCEA-003` |
| `CTL-POPIA-*` | POPIA data protection control | `CTL-POPIA-002` |
| `CTL-SEC-*` | Security control | `CTL-SEC-004` |
| `TC-*` | Test case | `TC-PAY-001` |
| `VUL-*` | Vulnerability remediation | `VUL-007` |
| `TASK-*` | Implementation task | `TASK-067` |

Orphan code (no traceability reference) must not be committed.

---

## Logging

Use `[LoggerMessage]` source generation for all log messages. This avoids string interpolation overhead and provides structured logging with sequential EventIds.

```csharp
public sealed partial class NightlyAnalyticsService : BackgroundService
{
    [LoggerMessage(Level = LogLevel.Information, EventId = 1001,
        Message = "NightlyAnalyticsService started. Target hour: {TargetHour} SAST.")]
    private static partial void LogServiceStarted(
        ILogger logger, int targetHour);

    [LoggerMessage(Level = LogLevel.Information, EventId = 1002,
        Message = "Analytics snapshot started for {Date}.")]
    private static partial void LogAnalyticsStarted(
        ILogger logger, string date);
}
```

### EventId Ranges by Module

| Module | EventId Range |
|--------|--------------|
| API / Middleware | 100-199 |
| Employee | 200-299 |
| Payroll | 300-399 |
| Leave | 400-499 |
| Compliance | 500-599 |
| Audit | 600-699 |
| Risk | 700-799 |
| Infrastructure | 800-899 |
| Background Services | 1000-1099 |

---

## Testing Conventions

### Test Naming

```
MethodName_Scenario_ExpectedResult
```

Examples:

```csharp
[Fact]
public void CalculatePaye_IncomeInBracket1_Returns18Percent() { }

[Fact]
public void Approve_WhenStatusIsPending_TransitionsToApproved() { }

[Fact]
public void CreateEmployee_MissingLegalName_ReturnsValidationError() { }
```

### Frameworks

| Library | Purpose |
|---------|---------|
| xUnit | Test runner |
| FluentAssertions | Assertion library |
| NSubstitute | Mocking framework |
| FsCheck | Property-based testing (payroll calculations) |

### FluentAssertions Style

```csharp
// Prefer FluentAssertions over xUnit Assert
result.IsSuccess.Should().BeTrue();
result.Value.Amount.Should().Be(45_000.00m);
employee.EmploymentStatus.Should().Be(EmploymentStatus.Active);
```

### Property-Based Tests for Payroll

All payroll calculations must have FsCheck property-based tests to verify invariants across random inputs:

```csharp
[Property]
public Property PayslipInvariant_NetEqualsGrossMinusDeductions(
    PositiveInt grossCents)
{
    var gross = new MoneyZAR(grossCents.Get / 100m);
    var result = calculator.Calculate(gross, ruleSet);

    return (result.NetPay.Amount ==
        result.GrossPay.Amount - result.TotalDeductions.Amount)
        .ToProperty();
}
```

---

## Tenant Isolation

Every Firestore root document has a `tenant_id` field. Every query must filter by `tenant_id`. Cross-tenant data access is a **Sev-1 security vulnerability**.

```csharp
// CORRECT: tenant_id always from JWT, never from request body
var tenantId = user.FindFirstValue(ZenoHrClaimNames.TenantId)!;
var employees = await repo.ListByTenantAsync(tenantId, ct);

// WRONG: tenant_id from request body — attacker can read other tenants
var tenantId = request.TenantId;  // NEVER do this
```

---

## Immutability Rules

Once finalized, certain records are **write-once**. No updates, no deletes. Corrections create new adjustment documents referencing the original.

| Record Type | Immutable After |
|-------------|----------------|
| `PayrollRun` | Status = Finalized |
| `AuditEvent` | Always (from creation) |
| `AccrualLedgerEntry` | Always (from creation) |
| `ComplianceSubmission` | Status = Submitted |

```csharp
// PayrollAdjustment: correction to a finalized run (new document, not update)
var adjustment = PayrollAdjustment.Create(
    adjustmentId: $"adj_{Guid.CreateVersion7()}",
    payrollRunId: originalRun.Id,  // References the immutable run
    // ...
);
```

---

## File Organization

| Element | Location Pattern |
|---------|-----------------|
| Domain aggregate | `src/ZenoHR.Module.{Module}/Aggregates/{Name}.cs` |
| Domain entity | `src/ZenoHR.Module.{Module}/Entities/{Name}.cs` |
| Domain event | `src/ZenoHR.Module.{Module}/Events/{Name}Event.cs` |
| Domain enum | `src/ZenoHR.Module.{Module}/{Name}.cs` or `src/ZenoHR.Domain/` |
| API endpoint | `src/ZenoHR.Api/Endpoints/{Module}Endpoints.cs` |
| Firestore repo | `src/ZenoHR.Infrastructure/Firestore/{Name}Repository.cs` |
| Unit test | `tests/ZenoHR.{Module}.Tests/{ClassName}Tests.cs` |
| Integration test | `tests/ZenoHR.Integration.Tests/{Feature}IntegrationTests.cs` |
