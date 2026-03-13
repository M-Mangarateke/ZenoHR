---
doc_id: DEV-API-REFERENCE
version: 1.0.0
updated_on: 2026-03-13
---

# API Reference

This document lists all ZenoHR API endpoints, their authentication requirements, role restrictions, request/response shapes, and rate limiting tiers.

All endpoints are defined as ASP.NET Core Minimal APIs in `src/ZenoHR.Api/Endpoints/`.

---

## Base URL

- **Local development**: `https://localhost:5001` or `http://localhost:5000`
- **Production**: `https://zenohr.zenowethu.co.za` (Azure Container Apps, SA North)

---

## Authentication

All endpoints (except health checks) require a valid Firebase JWT token in the `Authorization` header:

```
Authorization: Bearer <firebase-jwt-token>
```

The JWT must contain the following custom claims:

| Claim | Description |
|-------|-------------|
| `tenant_id` | Tenant identifier (mandatory on all requests) |
| `system_role` | User's role: `SaasAdmin`, `Director`, `HRManager`, `Manager`, `Employee` |
| `employee_id` | User's employee record ID (not present for `SaasAdmin`) |
| `dept_id` | Department ID (used for Manager scoping) |
| `mfa_verified` | Whether MFA has been completed for this session |

---

## Rate Limiting

Three rate limiting tiers are applied to endpoints:

| Policy | Limit | Window | Applied To |
|--------|-------|--------|------------|
| `general-api` | 100 requests | 1 minute (sliding) | Most endpoints |
| `auth-endpoints` | 10 requests | 5 minutes (fixed) | Authentication endpoints |
| `payroll-ops` | 20 requests | 1 minute (fixed) | Payroll operations |

When rate-limited, the API returns `429 Too Many Requests` with a `Retry-After: 60` header.

See: `src/ZenoHR.Api/Security/RateLimitingExtensions.cs`

---

## Error Responses

All error responses follow the RFC 7807 `ProblemDetails` format:

| HTTP Status | Meaning | When |
|-------------|---------|------|
| `400 Bad Request` | Validation error or business rule violation | Invalid input, wrong state |
| `401 Unauthorized` | Missing or invalid JWT | No token, expired token |
| `403 Forbidden` | Insufficient permissions | Wrong role, wrong tenant, not own record |
| `404 Not Found` | Resource does not exist | Employee, run, request not found |
| `422 Unprocessable Entity` | Semantic validation failure | Invalid purpose code (unmask) |
| `429 Too Many Requests` | Rate limit exceeded | Too many requests in window |
| `500 Internal Server Error` | Unexpected infrastructure failure | Firestore down, PDF crash |

---

## Health Check Endpoints

These endpoints are **anonymous** (no auth required) and are used by Azure Container Apps for liveness/readiness probes.

### GET /health

Liveness probe. Returns `200 OK` if the process is alive.

### GET /health/ready

Readiness probe. Checks Firestore connectivity. Returns `200 OK` if all dependencies are healthy.

---

## Employee Endpoints

**Source**: `src/ZenoHR.Api/Endpoints/EmployeeEndpoints.cs`
**Base path**: `/api/employees`
**Rate limiting**: `general-api`

### GET /api/employees

List employees. Results are scoped by role:
- **Director/HRManager**: All employees in tenant
- **Manager**: Employees in own department only
- **Employee**: Own record only

| Field | Details |
|-------|---------|
| Auth | Required |
| Roles | All authenticated users |
| Response | `200` — `EmployeeSummaryDto[]` |

**EmployeeSummaryDto**:
```json
{
    "employeeId": "emp_...",
    "legalName": "Zanele Dlamini",
    "personalEmail": "zanele@zenowethu.co.za",
    "departmentId": "dept_hr",
    "systemRole": "HRManager",
    "employmentStatus": "Active",
    "hireDate": "2024-01-15"
}
```

### GET /api/employees/{id}

Get employee by ID. Returns role-filtered DTO:
- **Director/HRManager**: `EmployeeFullDto` (all fields, PII masked)
- **Manager**: `EmployeeProfileDto` (no salary, tax, banking, national ID)
- **Employee**: `EmployeeSelfDto` (own record, minimal fields)

| Field | Details |
|-------|---------|
| Auth | Required |
| Roles | Self-access always allowed; others need Director/HRManager/Manager |
| Response | `200` — role-filtered DTO, `404` if not found, `403` if unauthorized |

### POST /api/employees

Create a new employee.

| Field | Details |
|-------|---------|
| Auth | Required |
| Roles | Director, HRManager |
| Body | `CreateEmployeeRequest` |
| Response | `201` — `EmployeeDetailDto`, `400` — validation error |

**CreateEmployeeRequest**:
```json
{
    "firebaseUid": "firebase_uid_here",
    "legalName": "Sipho Nkosi",
    "nationalIdOrPassport": "9203025008086",
    "taxReference": "1234567890",
    "dateOfBirth": "1992-03-02",
    "personalPhoneNumber": "+27821234567",
    "personalEmail": "sipho@zenowethu.co.za",
    "workEmail": "sipho.nkosi@zenowethu.co.za",
    "nationality": "South African",
    "gender": "Male",
    "race": "Black",
    "disabilityStatus": false,
    "disabilityDescription": null,
    "hireDate": "2024-06-01",
    "employeeType": "FullTime",
    "departmentId": "dept_finance",
    "roleId": "role_accountant",
    "systemRole": "Employee",
    "reportsToEmployeeId": "emp_manager_001"
}
```

### PUT /api/employees/{id}/profile

Update mutable profile fields. Employees can update their own profile. Director/HRManager can update any employee.

| Field | Details |
|-------|---------|
| Auth | Required |
| Roles | Self (own profile), Director, HRManager |
| Body | `UpdateProfileRequest` |
| Response | `200` — `EmployeeDetailDto`, `400`, `403`, `404` |

### PUT /api/employees/{id}/terminate

Terminate an employee's employment.

| Field | Details |
|-------|---------|
| Auth | Required |
| Roles | Director, HRManager |
| Body | `TerminateEmployeeRequest` — `{ "terminationReasonCode": "Resigned", "effectiveDate": "2026-03-31" }` |
| Response | `200`, `400`, `404` |

---

## Leave Endpoints

**Source**: `src/ZenoHR.Api/Endpoints/LeaveEndpoints.cs`
**Base path**: `/api/leave`
**Rate limiting**: `general-api`

### GET /api/leave/balances

Get leave balances. Query parameters: `employeeId` (optional), `cycleId` (optional).

| Field | Details |
|-------|---------|
| Auth | Required |
| Roles | Self-access default; Director/HRManager/Manager can query other employees |
| Response | `200` — `LeaveBalanceDto[]` |

**LeaveBalanceDto**:
```json
{
    "balanceId": "bal_...",
    "employeeId": "emp_...",
    "leaveType": "Annual",
    "cycleId": "2026",
    "accruedHours": 168.0,
    "consumedHours": 40.0,
    "adjustmentHours": 0.0,
    "availableHours": 128.0,
    "lastAccrualDate": "2026-03-01"
}
```

### GET /api/leave/requests

List leave requests. `employeeId` query parameter optional.

| Roles | Behavior |
|-------|----------|
| Director/HRManager | All employees or specified employee |
| Manager | Team employees |
| Employee | Own requests only |

### GET /api/leave/requests/{id}

Get a single leave request by ID.

### POST /api/leave/requests

Submit a new leave request (always for the authenticated user).

| Field | Details |
|-------|---------|
| Body | `SubmitLeaveRequestDto` |
| Response | `201` — `LeaveRequestDto` |

**SubmitLeaveRequestDto**:
```json
{
    "leaveType": "Annual",
    "startDate": "2026-04-01",
    "endDate": "2026-04-05",
    "totalHours": 40.0,
    "reasonCode": "vacation"
}
```

### PUT /api/leave/requests/{id}/approve

Approve a leave request. Consumes leave balance atomically.

| Field | Details |
|-------|---------|
| Roles | Director, HRManager, Manager |
| Response | `200`, `400` (wrong state / insufficient balance), `404` |

### PUT /api/leave/requests/{id}/reject

Reject a leave request with a reason.

| Field | Details |
|-------|---------|
| Roles | Director, HRManager, Manager |
| Body | `{ "rejectionReason": "Team capacity too low" }` |

### PUT /api/leave/requests/{id}/cancel

Cancel a leave request. Employees can cancel their own; Director/HRManager can cancel any.

---

## Clock / Time Attendance Endpoints

**Source**: `src/ZenoHR.Api/Endpoints/ClockEndpoints.cs`
**Base path**: `/api/clock`
**Rate limiting**: `general-api`

### POST /api/clock/in

Clock in for the current day. At most one open entry per day.

| Field | Details |
|-------|---------|
| Auth | Required (any authenticated user) |
| Response | `201` — `ClockEntryDto`, `400` if already clocked in |

### POST /api/clock/out/{entryId}

Clock out from an open clock entry. Employee can only clock out their own entry.

### GET /api/clock/today

Get today's clock status for the authenticated user.

### GET /api/clock/team

Get team clock status for today. Manager sees department, Director/HRManager sees all.

| Roles | Director, HRManager, Manager |

### POST /api/clock/flags

Create a timesheet flag for an employee (e.g., absent without notice).

| Field | Details |
|-------|---------|
| Roles | Director, HRManager, Manager |
| Body | `{ "employeeId": "emp_...", "flagDate": "2026-03-13", "reason": "AbsentNoNotice", "notes": "..." }` |

---

## Payroll Endpoints

**Source**: `src/ZenoHR.Api/Endpoints/PayrollEndpoints.cs`
**Base path**: `/api/payroll`
**Rate limiting**: `payroll-ops` (20 req/min)
**Roles**: Director, HRManager only (except payslip endpoints)

### GET /api/payroll/runs

List all payroll runs for the tenant, newest first.

### GET /api/payroll/runs/{id}

Get detailed payroll run information.

### POST /api/payroll/runs

Create and calculate a new payroll run.

**CreatePayrollRunRequest**:
```json
{
    "period": "2026-03",
    "runType": "Monthly",
    "employeeIds": ["emp_001", "emp_002"],
    "ruleSetVersion": "2025-2026",
    "isSdlExempt": false,
    "idempotencyKey": "optional-unique-key"
}
```

### PUT /api/payroll/runs/{id}/finalize

Finalize (lock) a payroll run. **Requires MFA** (VUL-003 remediation).

### PUT /api/payroll/runs/{id}/file

Mark a payroll run as filed after EMP201 download. **Requires MFA**.

### GET /api/payroll/runs/{id}/results

List per-employee payroll results for a run.

### GET /api/payroll/runs/{runId}/results/{employeeId}

Get a single employee's payroll result.

### GET /api/payroll/runs/{runId}/results/{employeeId}/payslip

Get payslip data as JSON. **Self-access**: employees can view their own payslip.

### GET /api/payroll/runs/{runId}/payslips/{employeeId}/pdf

Download payslip as PDF. **Self-access**: employees can download their own payslip.

| Field | Details |
|-------|---------|
| Response | `200` — `application/pdf` file, `403`, `404` |

### POST /api/payroll/adjustments

Create a post-finalization adjustment to a finalized or filed run.

**CreateAdjustmentRequest**:
```json
{
    "payrollRunId": "pr_...",
    "employeeId": "emp_...",
    "adjustmentType": "Correction",
    "reason": "Incorrect overtime hours",
    "amountZar": 1500.00,
    "affectedFields": ["overtime_pay", "gross_pay", "net_pay"]
}
```

### GET /api/payroll/adjustments

List adjustments. Query: `runId` or `employeeId` (one required).

### GET /api/payroll/runs/{runId}/emp201/csv

Download EMP201 CSV file for SARS eFiling upload.

### GET /api/payroll/runs/{runId}/emp201/report

Get EMP201 human-readable summary report.

---

## Compliance Endpoints

**Source**: `src/ZenoHR.Api/Endpoints/ComplianceEndpoints.cs`
**Base path**: `/api/compliance`
**Roles**: Director, HRManager only

### GET /api/compliance/submissions

List compliance submissions. Optional query: `period`.

### GET /api/compliance/submissions/{id}

Get a single compliance submission.

### POST /api/compliance/emp201/{period}

Generate EMP201 for a specific period (e.g., `202603`). Reads finalized payroll run data.

### POST /api/compliance/emp501/{taxYear}

Generate EMP501 annual reconciliation for a tax year (e.g., `2026`).

### GET /api/compliance/submissions/{id}/download

Download the generated filing file (CSV).

### GET /api/compliance/employees/missing-tax-reference

List employees missing valid SARS tax reference numbers (ITREG workflow).

### POST /api/compliance/itreg/generate

Generate SARS ITREG (income tax registration) export file.

| Body | `{ "employerPayeReference": "7234567890" }` |

---

## eFiling Endpoints

**Source**: `src/ZenoHR.Api/Endpoints/EFilingEndpoints.cs`
**Base path**: `/api/efiling`
**Roles**: Director, HRManager only

### POST /api/efiling/emp201/submit

Submit EMP201 to SARS eFiling system.

**Emp201SubmitRequest**:
```json
{
    "taxYear": 2026,
    "taxPeriod": 3,
    "emp201ContentBase64": "base64-encoded-csv-content"
}
```

### GET /api/efiling/submissions/{submissionId}/status

Query the current status of an eFiling submission.

### GET /api/efiling/submissions

List eFiling submission history. Optional query: `taxYear` (defaults to current year).

---

## Statutory Settings Endpoints

**Source**: `src/ZenoHR.Api/Endpoints/StatutoryEndpoints.cs`
**Base path**: `/api/settings/statutory`
**Roles**: Director, HRManager only

### GET /api/settings/statutory

List all seeded statutory rule sets (PAYE, UIF, SDL, ETI, BCEA).

### PUT /api/settings/statutory/{id}/rule-data

Partial update of editable fields in a statutory rule set. Field whitelist enforced by `StatutoryFieldPermissions`. All changes are audited.

**UpdateRuleDataRequest**:
```json
{
    "fields": {
        "data_status": "confirmed",
        "source_url": "https://www.sars.gov.za/..."
    }
}
```

---

## PII Unmask Endpoint

**Source**: `src/ZenoHR.Api/Endpoints/UnmaskEndpoints.cs`
**Roles**: Director, HRManager only

### POST /api/employees/{employeeId}/unmask

Unmask a sensitive PII field with POPIA purpose code enforcement.

**UnmaskRequest**:
```json
{
    "fieldName": "national_id",
    "purposeCode": "PAYROLL_PROCESSING",
    "justification": null
}
```

| Allowed Fields | `national_id`, `tax_reference`, `bank_account` |
| Allowed Purpose Codes | `PAYROLL_PROCESSING`, `TAX_FILING`, `AUDIT_REVIEW`, `HR_INVESTIGATION`, `LEGAL_COMPLIANCE` |

**Note**: `AUDIT_REVIEW` and `HR_INVESTIGATION` require a `justification` string.

| Response | `200` — `UnmaskResponse`, `403`, `404`, `422` (invalid purpose/field) |

**UnmaskResponse**:
```json
{
    "employeeId": "emp_...",
    "fieldName": "national_id",
    "value": "9203025008086",
    "purposeCode": "PAYROLL_PROCESSING",
    "auditEventId": "ae_..."
}
```

---

## Endpoint Summary Table

| Method | Path | Auth | Roles | Rate Limit |
|--------|------|------|-------|------------|
| GET | `/health` | No | Anonymous | None |
| GET | `/health/ready` | No | Anonymous | None |
| GET | `/api/employees` | Yes | All | general-api |
| GET | `/api/employees/{id}` | Yes | All (role-filtered) | general-api |
| POST | `/api/employees` | Yes | Director, HRManager | general-api |
| PUT | `/api/employees/{id}/profile` | Yes | Self, Director, HRManager | general-api |
| PUT | `/api/employees/{id}/terminate` | Yes | Director, HRManager | general-api |
| POST | `/api/employees/{id}/unmask` | Yes | Director, HRManager | general-api |
| GET | `/api/leave/balances` | Yes | All | general-api |
| GET | `/api/leave/requests` | Yes | All (role-scoped) | general-api |
| GET | `/api/leave/requests/{id}` | Yes | All (role-checked) | general-api |
| POST | `/api/leave/requests` | Yes | All | general-api |
| PUT | `/api/leave/requests/{id}/approve` | Yes | Director, HRManager, Manager | general-api |
| PUT | `/api/leave/requests/{id}/reject` | Yes | Director, HRManager, Manager | general-api |
| PUT | `/api/leave/requests/{id}/cancel` | Yes | Self, Director, HRManager | general-api |
| POST | `/api/clock/in` | Yes | All | general-api |
| POST | `/api/clock/out/{entryId}` | Yes | Self | general-api |
| GET | `/api/clock/today` | Yes | All | general-api |
| GET | `/api/clock/team` | Yes | Director, HRManager, Manager | general-api |
| POST | `/api/clock/flags` | Yes | Director, HRManager, Manager | general-api |
| GET | `/api/payroll/runs` | Yes | Director, HRManager | payroll-ops |
| GET | `/api/payroll/runs/{id}` | Yes | Director, HRManager | payroll-ops |
| POST | `/api/payroll/runs` | Yes | Director, HRManager | payroll-ops |
| PUT | `/api/payroll/runs/{id}/finalize` | Yes + MFA | Director, HRManager | payroll-ops |
| PUT | `/api/payroll/runs/{id}/file` | Yes + MFA | Director, HRManager | payroll-ops |
| GET | `/api/payroll/runs/{id}/results` | Yes | Director, HRManager | payroll-ops |
| GET | `/api/payroll/runs/{runId}/results/{employeeId}` | Yes | Director, HRManager | payroll-ops |
| GET | `/api/payroll/runs/{runId}/results/{employeeId}/payslip` | Yes | Self, Director, HRManager | general-api |
| GET | `/api/payroll/runs/{runId}/payslips/{employeeId}/pdf` | Yes | Self, Director, HRManager | general-api |
| POST | `/api/payroll/adjustments` | Yes | Director, HRManager | payroll-ops |
| GET | `/api/payroll/adjustments` | Yes | Director, HRManager | payroll-ops |
| GET | `/api/payroll/runs/{runId}/emp201/csv` | Yes | Director, HRManager | payroll-ops |
| GET | `/api/payroll/runs/{runId}/emp201/report` | Yes | Director, HRManager | payroll-ops |
| GET | `/api/compliance/submissions` | Yes | Director, HRManager | general-api |
| GET | `/api/compliance/submissions/{id}` | Yes | Director, HRManager | general-api |
| POST | `/api/compliance/emp201/{period}` | Yes | Director, HRManager | general-api |
| POST | `/api/compliance/emp501/{taxYear}` | Yes | Director, HRManager | general-api |
| GET | `/api/compliance/submissions/{id}/download` | Yes | Director, HRManager | general-api |
| GET | `/api/compliance/employees/missing-tax-reference` | Yes | Director, HRManager | general-api |
| POST | `/api/compliance/itreg/generate` | Yes | Director, HRManager | general-api |
| POST | `/api/efiling/emp201/submit` | Yes | Director, HRManager | general-api |
| GET | `/api/efiling/submissions/{id}/status` | Yes | Director, HRManager | general-api |
| GET | `/api/efiling/submissions` | Yes | Director, HRManager | general-api |
| GET | `/api/settings/statutory` | Yes | Director, HRManager | general-api |
| PUT | `/api/settings/statutory/{id}/rule-data` | Yes | Director, HRManager | general-api |
