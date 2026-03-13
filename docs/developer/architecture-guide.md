---
doc_id: DEV-ARCHITECTURE-GUIDE
version: 1.0.0
updated_on: 2026-03-13
---

# Architecture Guide

ZenoHR is built as a **modular monolith** using ASP.NET Core 10. This document explains the architectural patterns, module boundaries, communication mechanisms, and infrastructure decisions.

---

## Modular Monolith Pattern

ZenoHR uses a modular monolith architecture with **7 bounded contexts**, each implemented as a separate .NET project (assembly). This provides strong logical isolation while avoiding the operational complexity of microservices.

```
┌──────────────────────────────────────────────────────────────┐
│                     ZenoHR.Api                                │
│  (ASP.NET Core host — endpoints, middleware, pipeline)        │
├──────────┬───────────┬──────────┬──────────┬─────────────────┤
│ Employee │ Payroll   │ Leave    │ Time     │ Compliance      │
│ Module   │ Module    │ Module   │ Module   │ Module          │
├──────────┴───────────┴──────────┴──────────┴─────────────────┤
│           Audit Module              Risk Module               │
├──────────────────────────────────────────────────────────────┤
│                  ZenoHR.Domain (Shared Kernel)                │
│        MoneyZAR  ·  TaxYear  ·  Result<T>  ·  Enums          │
├──────────────────────────────────────────────────────────────┤
│               ZenoHR.Infrastructure                           │
│  Firestore Repos  ·  Firebase Auth  ·  PDF Gen  ·  Filing    │
└──────────────────────────────────────────────────────────────┘
```

### The 7 Bounded Contexts

| Module | Project | Responsibility |
|--------|---------|----------------|
| Employee | `ZenoHR.Module.Employee` | Employee lifecycle, profiles, contracts, departments |
| Payroll | `ZenoHR.Module.Payroll` | Payroll runs, PAYE/UIF/SDL/ETI calculations, payslips |
| Leave | `ZenoHR.Module.Leave` | Leave requests, balances, accruals, BCEA compliance |
| Time & Attendance | `ZenoHR.Module.TimeAttendance` | Clock entries, timesheets, flags |
| Compliance | `ZenoHR.Module.Compliance` | SARS filings (EMP201/EMP501), BCEA checks, POPIA controls |
| Audit | `ZenoHR.Module.Audit` | Hash-chained audit trail, evidence packs |
| Risk | `ZenoHR.Module.Risk` | Risk scoring, compliance dashboard insights |

---

## Vertical Slice Architecture

Within each module, code is organized by **feature** (vertical slice) rather than by technical layer (horizontal). Each feature contains everything needed to handle a specific use case: aggregate, commands, queries, and domain events.

```
ZenoHR.Module.Payroll/
├── Aggregates/
│   ├── PayrollRun.cs          # Aggregate root with state machine
│   └── PayrollAdjustment.cs   # Separate aggregate for post-finalization changes
├── Calculation/
│   ├── PayeCalculator.cs      # PAYE annual equivalent method
│   ├── UifCalculator.cs       # UIF ceiling-capped calculation
│   ├── SdlCalculator.cs       # SDL employer-only levy
│   └── EtiCalculator.cs       # ETI age/wage eligibility
├── Entities/
│   └── PayrollResult.cs       # Per-employee calculation result
└── Events/
    └── PayrollRunFinalizedEvent.cs  # MediatR domain event
```

---

## Module Communication

### Rule: MediatR Domain Events Only

Modules communicate **exclusively** through MediatR in-process domain events or shared kernel types. No module may directly read or write another module's Firestore collections.

```csharp
// CORRECT: Module publishes a domain event
public sealed record PayrollRunFinalizedEvent(
    string TenantId, string RunId, string Period) : INotification;

// Another module handles it
public sealed class CreateAuditOnPayrollFinalized
    : INotificationHandler<PayrollRunFinalizedEvent>
{
    public async Task Handle(PayrollRunFinalizedEvent e, CancellationToken ct)
    {
        // Audit module writes to its own collection
    }
}
```

```csharp
// WRONG: Module directly accesses another module's repository
// This violates module boundaries and is caught by Architecture Tests
var employees = await _employeeRepo.ListByTenantAsync(tenantId, ct); // from Payroll module
```

### Enforcement

The `ZenoHR.Architecture.Tests` project contains ArchUnit-style tests that verify module boundaries at compile time. These tests fail the build if any module directly references another module's internals.

See: `tests/ZenoHR.Architecture.Tests/`

---

## Shared Kernel: ZenoHR.Domain

The shared kernel contains types that all modules depend on. It has **zero** dependencies on any module or infrastructure project.

### Key Types

| Type | File | Purpose |
|------|------|---------|
| `MoneyZAR` | `src/ZenoHR.Domain/Common/MoneyZAR.cs` | Immutable value object for South African Rand. Always uses `decimal`. Serialized as string in Firestore. |
| `TaxYear` | `src/ZenoHR.Domain/Common/TaxYear.cs` | SA tax year (1 March to 28/29 February). |
| `Result<T>` | `src/ZenoHR.Domain/Errors/Result.cs` | Discriminated union for success/failure. Replaces exceptions for business logic errors. |
| `ZenoHrErrorCode` | `src/ZenoHR.Domain/Errors/ZenoHrErrorCode.cs` | Typed error code enum (1xxx-8xxx ranges). |
| `ZenoHrError` | `src/ZenoHR.Domain/Errors/ZenoHrError.cs` | Error record combining code + message. |
| `StatutoryRuleSet` | `src/ZenoHR.Domain/Common/StatutoryRuleSet.cs` | Configuration loaded from Firestore. Never hardcode tax rates. |

### Result<T> Pattern

All domain operations that can fail return `Result<T>` instead of throwing exceptions:

```csharp
// Domain method returns Result<T>
public Result<PayrollRun> MarkFiled(string actorId, DateTimeOffset now)
{
    if (Status != PayrollRunStatus.Finalized)
        return Result<PayrollRun>.Failure(
            ZenoHrErrorCode.PayrollRunInWrongState,
            "Only finalized runs can be marked as filed.");

    Status = PayrollRunStatus.Filed;
    FiledAt = now;
    return Result<PayrollRun>.Success(this);
}

// API endpoint checks result
var fileResult = run.MarkFiled(actorId, DateTimeOffset.UtcNow);
if (fileResult.IsFailure)
    return Results.BadRequest(fileResult.Error.Message);
```

### MoneyZAR Value Object

All monetary values in ZenoHR use `MoneyZAR`, which wraps `decimal`. Using `float` or `double` for money is classified as a **Sev-1 defect**.

```csharp
var salary = new MoneyZAR(45_000.00m);
var paye = new MoneyZAR(8_250.00m);
var net = salary - paye;  // MoneyZAR arithmetic

// Rounding per SARS rules
var annualPaye = calculatedTax.RoundToRand();   // Nearest rand, AwayFromZero
var periodPaye = (annualPaye / 12m).RoundToCent(); // Nearest cent

// Firestore serialization (string, not number)
string firestoreValue = salary.ToFirestoreString(); // "45000.00"
MoneyZAR restored = MoneyZAR.FromFirestoreString(firestoreValue);
```

---

## Infrastructure Layer

`ZenoHR.Infrastructure` contains all external system integrations. Domain and module projects have **no** dependency on infrastructure.

### Firestore Repositories

Each bounded context has its own repository classes in the infrastructure layer. Repositories enforce `tenant_id` scoping on every query.

```csharp
// Every query filters by tenant_id — cross-tenant access is a Sev-1 vulnerability
public async Task<Result<Employee>> GetByEmployeeIdAsync(
    string tenantId, string employeeId, CancellationToken ct)
{
    var docRef = _db.Collection("employees").Document(employeeId);
    var snapshot = await docRef.GetSnapshotAsync(ct);
    // Verify tenant_id matches before returning
}
```

Key infrastructure components:

| Component | Location | Purpose |
|-----------|----------|---------|
| Firestore repositories | `Infrastructure/Firestore/` | Data access for all collections |
| Firebase auth | `Api/Auth/FirebaseAuthExtensions.cs` | JWT validation middleware |
| PDF generation | `Infrastructure/Services/Payslip/` | QuestPDF-based payslip generation |
| Filing export | `Infrastructure/Services/Filing/` | EMP201/EMP501 CSV generation |
| Audit writer | `Infrastructure/Audit/` | Hash-chained audit event persistence |

---

## API Layer

ZenoHR uses **ASP.NET Core Minimal APIs** (not controllers). Endpoints are organized by module in `src/ZenoHR.Api/Endpoints/`.

### Endpoint Registration

Each module registers its endpoints via an extension method called from `Program.cs`:

```csharp
// In Program.cs
app.MapEmployeeEndpoints();   // src/ZenoHR.Api/Endpoints/EmployeeEndpoints.cs
app.MapLeaveEndpoints();      // src/ZenoHR.Api/Endpoints/LeaveEndpoints.cs
app.MapPayrollEndpoints();    // src/ZenoHR.Api/Endpoints/PayrollEndpoints.cs
```

### Middleware Pipeline Order

The middleware pipeline in `Program.cs` is ordered deliberately. The order matters for security and observability:

```
1.  CorrelationId          — Assigns X-Correlation-Id header for request tracing
2.  GlobalExceptionHandler — Outermost exception catch, returns ProblemDetails
3.  SecurityHeaders        — CSP, X-Frame-Options, X-Content-Type-Options, Referrer-Policy
4.  HSTS                   — HTTP Strict Transport Security (production only)
5.  HttpsRedirection       — Redirects HTTP to HTTPS
6.  CORS                   — Cross-Origin Resource Sharing policy
7.  Authentication         — Firebase JWT validation
8.  Authorization          — Role-based access control
9.  RateLimiter            — Per-tenant request rate limiting
10. SessionTimeout         — Idle session timeout enforcement
11. DepartmentScope        — Manager department scoping filter
```

See: `src/ZenoHR.Api/Program.cs`

---

## Web Layer: Blazor Server

The `ZenoHR.Web` project contains all Blazor Server components for the UI. It uses Server-Side Rendering (SSR) with real-time updates via SignalR.

### Key Patterns

- **All UI designs** are derived from HTML mockups in `docs/design/mockups/` (01-16 + shared.css)
- **Design tokens** (colors, typography, spacing) are defined in `docs/design/design-tokens.md`
- **RBAC**: Navigation items are entirely absent for roles without access (not greyed out)
- **Dark/Light mode**: Both themes are supported using CSS custom properties

### Component Organization

Blazor components follow the mockup structure:

| Mockup | Blazor Component Area |
|--------|----------------------|
| `02-dashboard.html` | Dashboard page with role-specific KPI widgets |
| `03-employees.html` | Employee list, detail, create/edit forms |
| `04-payroll.html` | Payroll run management |
| `05-leave.html` | Leave calendar and request management |

---

## Background Services

ZenoHR uses .NET `BackgroundService` with `PeriodicTimer` for scheduled tasks. All background services are SAST-timezone aware (UTC+2).

### Registered Services

| Service | File | Schedule | Purpose |
|---------|------|----------|---------|
| `NightlyAnalyticsService` | `BackgroundServices/NightlyAnalyticsService.cs` | 2:00 AM SAST daily | Compute analytics snapshots |
| `Emp201ReminderService` | `BackgroundServices/Emp201ReminderService.cs` | Daily check | EMP201 filing deadline reminders |
| `EtiExpiryAlertService` | `BackgroundServices/EtiExpiryAlertService.cs` | Daily check | ETI eligibility expiry alerts |
| `DataArchivalService` | `BackgroundServices/DataArchivalService.cs` | Nightly | POPIA data retention archival |
| `MonthlyAccessReviewService` | `BackgroundServices/MonthlyAccessReviewService.cs` | Monthly | POPIA access review triggers |

### PeriodicTimer Pattern

```csharp
public sealed class NightlyAnalyticsService : BackgroundService
{
    private static readonly TimeZoneInfo SastTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("South Africa Standard Time");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var nowSast = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, SastTimeZone);
            if (nowSast.Hour != 2) continue;  // Only run at 2 AM SAST
            // ... do work
        }
    }
}
```

Registration is centralized in `src/ZenoHR.Api/BackgroundServices/BackgroundServiceRegistration.cs`.

---

## Error Handling Strategy

ZenoHR uses a three-layer error handling approach:

### 1. Domain Errors: Result<T>

Business logic failures return `Result<T>` with typed `ZenoHrErrorCode` values. No exceptions are thrown for expected failures (validation errors, state machine violations, insufficient balance, etc.).

```csharp
if (Status != LeaveRequestStatus.Pending)
    return Result<LeaveRequest>.Failure(
        ZenoHrErrorCode.LeaveRequestAlreadyProcessed,
        "Cannot approve a request that is not in Pending status.");
```

### 2. Infrastructure Errors: Exceptions

Infrastructure failures (Firestore unavailable, network timeout, PDF generation crash) propagate as normal .NET exceptions. They are caught by the `GlobalExceptionMiddleware` and converted to `ProblemDetails` responses.

### 3. API Validation: ProblemDetails

API-layer validation uses `FluentValidation` at the boundary and returns standard RFC 7807 `ProblemDetails`:

```json
{
    "status": 400,
    "title": "Bad Request",
    "detail": "Invalid employee type: Intern"
}
```

### Error Code Ranges

| Range | Domain | Examples |
|-------|--------|----------|
| 1xxx | Validation | `RequiredFieldMissing`, `InvalidFormat`, `ValueOutOfRange` |
| 2xxx | Employee | `EmployeeNotFound`, `InvalidEmployeeState`, `ContractNotFound` |
| 3xxx | Payroll | `PayrollRunNotFound`, `PayslipInvariantViolation`, `PayrollRunAlreadyFinalized` |
| 4xxx | Leave | `InsufficientLeaveBalance`, `LeaveRequestAlreadyProcessed` |
| 5xxx | Compliance | `InvalidFilingPeriod`, `EfilingConnectionFailed` |
| 6xxx | Audit | `HashChainBroken`, `AuditEventImmutable` |
| 7xxx | Auth/RBAC | `Unauthorized`, `Forbidden`, `TenantNotFound` |
| 8xxx | Infrastructure | `FirestoreUnavailable`, `PdfGenerationFailed` |

See: `src/ZenoHR.Domain/Errors/ZenoHrErrorCode.cs`

---

## Testing Strategy

| Test Type | Framework | Project | Purpose |
|-----------|-----------|---------|---------|
| Unit tests | xUnit + FluentAssertions + NSubstitute | `tests/ZenoHR.Domain.Tests/` | Shared kernel, value objects |
| Domain tests | xUnit + FluentAssertions | `tests/ZenoHR.Module.*.Tests/` | Aggregate state machines, calculations |
| Property-based | FsCheck | `tests/ZenoHR.Module.Payroll.Tests/` | Payroll calculation edge cases |
| Integration | xUnit + Firestore emulator | `tests/ZenoHR.Integration.Tests/` | Repository read/write, queries |
| Architecture | ArchUnit-style | `tests/ZenoHR.Architecture.Tests/` | Module boundary enforcement |

### Coverage Targets

- **90%** line coverage for domain code
- **85%** branch coverage
- **100%** contract tests for API endpoints

### Test Naming Convention

```
MethodName_Scenario_ExpectedResult
```

Example: `MarkFiled_WhenRunIsFinalized_ReturnsSuccess`

---

## Key Reference Documents

| Document | Path |
|----------|------|
| Project conventions | `CLAUDE.md` |
| Firestore schema | `docs/schemas/firestore-collections.md` |
| Monetary precision | `docs/schemas/monetary-precision.md` |
| RBAC specification | `docs/prd/15_rbac_screen_access.md` |
| Payroll calculation spec | `docs/prd/16_payroll_calculation_spec.md` |
| UI mockups | `docs/design/mockups/` |
| Design tokens | `docs/design/design-tokens.md` |
