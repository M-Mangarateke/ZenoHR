---
doc_id: PRD-15-RBAC
version: 1.2.0
replaces: PRD-05 Role Model section (7-role model superseded)
status: LOCKED
updated_on: 2026-02-20
req_refs: REQ-SEC-002, REQ-SEC-003, REQ-SEC-007
---

# ZenoHR — Role-Based Access Control & Screen Access

> **This is the single source of truth for all RBAC definitions.**
> Any code, test, or UI that references roles MUST trace back to this document.
> The 7-role model from PRD-05 is superseded by this document.

---

## 1. Role Architecture

### Design Principles
- Users see **only** what their role permits — restricted items do not appear in navigation or UI at all
- Access is enforced **server-side** (API + Firestore security rules) — UI hiding is cosmetic only
- The `SaasAdmin` role is platform-level and cross-tenant — it cannot access tenant data
- `Director` and `HRManager` have identical system access — the distinction is organizational only
- `Manager` roles are dynamically created by Director/HRManager and scoped to a department
- `Employee` roles are the base self-service tier
- Every tenant user (Director, HRManager, Manager, Employee) **is also an employee** — they have an `employees` document and receive payslips, leave, BCEA coverage (see Section 1.5)
- Every authenticated tenant user **always has self-access** to their own profile, payslips, and leave — regardless of system_role (see Section 1.6)
- A user may hold **multiple active role assignments** simultaneously — for dual roles or multi-department management (see Section 1.7)

### Role Hierarchy

```
SaasAdmin (Platform)
│
└── [Tenant Level]
    ├── Director (Superuser)        ← also an employee of the tenant
    ├── HRManager (Superuser)       ← also an employee of the tenant
    ├── Manager (Dept-scoped)       ← also an employee of the tenant
    └── Employee (Self-service)     ← base employment identity
```

---

## 1.5. Employment & Access Separation

**Core principle**: A person's *employment identity* and their *system access role* are separate concerns.

| Layer | Collection | Purpose |
|-------|-----------|---------|
| Employment identity | `employees` | Drives payroll, leave accruals, PAYE, UIF, BCEA compliance |
| Access identity | `user_role_assignments` | Drives UI visibility, API authorization, Firestore rules |

**Rules:**
- Every Director, HRManager, Manager, and Employee has an `employees` document — they appear in payroll runs, receive payslips, and accrue leave
- `SaasAdmin` is the **only exception** — a platform operator with no tenant employee record, no payslip, and no leave
- The `employees.department_id` field represents the person's **primary employment department** (used for payroll cost-centre allocation), not necessarily the department(s) they have management access over
- **Director and HRManager payroll department**: assigned to the auto-created `Executive` department at tenant onboarding. The `Executive` department is a system department (`is_system_dept: true`) created before the first Director employee record is provisioned. It cannot be deleted.
- **Director/HRManager leave approval**: Leave requests submitted by a Director or HRManager are **self-approved** — the `leave_balance` ledger is updated automatically and an AuditEvent is written (actor = requester). No secondary approver is required.
- Consequence: Directors and Managers appear in the employee directory and payroll runs like any other employee

---

## 1.6. Self-Access Guarantee

Regardless of `system_role`, every authenticated **tenant user** always has read access to their own data:

| Own data | Always accessible | Enforcement |
|----------|------------------|-------------|
| Own employee document (`/employees/{own_id}`) | Yes | Server-side: `firebase_uid == requesting_uid` |
| Own payslips (`/payroll/payslips?employee_id={own_id}`) | Yes | Server-side: `firebase_uid == requesting_uid` |
| Own leave requests and balance | Yes | Server-side: `firebase_uid == requesting_uid` |

**This is NOT a role permission.** It is a system-level guarantee enforced independently of the RBAC matrix.

Sensitive field self-access (own `national_id_or_passport`, `bank_account_ref`, `tax_reference`): the field IS accessible (self-access guarantee applies). The field remains masked on screen by default for shoulder-surfing protection. Clicking unmask writes an AuditEvent but **no purpose code is required** — POPIA Section 23 recognises a data subject accessing their own data as a legitimate lawful basis without justification. This contrasts with Director/HRManager unmasking another employee's data, which requires a purpose code (CTL-POPIA-002).

---

## 1.7. Multi-Role Assignment

A user may hold **multiple active `user_role_assignments`** simultaneously.

### Supported patterns

| Pattern | Example | How it works |
|---------|---------|-------------|
| Multi-department Manager | Person manages Finance AND Operations | Two Manager assignments, each with a different `department_id` |
| Dual role (Manager + Employee) | Finance Manager who also works warehouse shifts | Manager assignment (primary) + Employee assignment (secondary) |
| Promoted employee | Warehouse employee promoted to Warehouse Manager | Old Employee assignment revoked; new Manager assignment created |

### Resolution rules

1. **Effective `system_role`** for Blazor route guards and API authorization = **highest-privilege active assignment**
   - Priority order: `Director > HRManager > Manager > Employee`
   - Example: Manager assignment + Employee assignment → effective role = `Manager`

2. **Manager department scope** = **union of `department_id` values** across all active Manager assignments
   - Example: Finance Manager + Operations Manager → team queries use `WHERE department_id IN ["dept_finance", "dept_operations"]`
   - Combined team view: leave queue, timesheets, and employee list merged across all managed departments

3. **Payroll**: one payroll record per person per period — tied to `employees.department_id` (primary employment department), regardless of how many role assignments they hold

4. **Leave routing**: leave requests route to the Manager of `employees.department_id` (primary department), even if the employee is also a Manager of another department

5. **`is_primary: boolean`** on `user_role_assignments` marks the main assignment (highest privilege, used for audit attribution). Auto-management rules:
   - First assignment created for a user → `is_primary: true`
   - Each subsequent assignment → `is_primary: false`
   - When the primary assignment is revoked (`is_active: false`), the application auto-promotes the oldest remaining active assignment to `is_primary: true`
   - This is system-managed — Directors/HRManagers do not manually set `is_primary`

6. **Active assignment definition**: `is_active: true AND effective_from <= today AND (effective_to IS NULL OR effective_to >= today)`. The `is_active` field is set to `false` on revocation (assignments are never deleted — append-only per audit requirements). This enables efficient Firestore queries using a composite index on `is_active`.

7. **Expired assignment fallback**: If all assignments are inactive (all `is_active: false` or all `effective_to` in the past), the user is denied all access and redirected to `/unauthorized` with the message: "Your role assignment has expired. Contact your administrator." The user remains Firebase-authenticated but has zero system permissions.

### Example data — multi-department Manager

```json
// Assignment 1 — primary (Finance Manager)
{ "firebase_uid": "uid_abc", "role_id": "role_finance_manager", "system_role": "Manager",
  "department_id": "dept_finance", "is_primary": true }

// Assignment 2 — secondary (Operations Manager)
{ "firebase_uid": "uid_abc", "role_id": "role_ops_manager", "system_role": "Manager",
  "department_id": "dept_operations", "is_primary": false }
```

Effective role: `Manager`. Team scope: Finance + Operations combined.

### Example data — dual role (Manager + Employee)

```json
// Primary: Finance Manager
{ "firebase_uid": "uid_xyz", "role_id": "role_finance_manager", "system_role": "Manager",
  "department_id": "dept_finance", "is_primary": true }

// Secondary: Warehouse Employee
{ "firebase_uid": "uid_xyz", "role_id": "role_warehouse_employee", "system_role": "Employee",
  "department_id": "dept_warehouse", "is_primary": false }
```

Effective role: `Manager`. Payroll: processed under Finance (primary department). Leave: approved by whoever manages Finance.

---

## 2. Role Definitions

### Tier 0 — Platform Role

| Role | SystemRole Enum | Scope | Description |
|------|----------------|-------|-------------|
| `SaasAdmin` | `1` | Platform (cross-tenant) | Platform owner/operator. Separate `/admin/*` UI. Cannot read tenant data. Manages tenants, system config, health, seed data. |

### Tier 1 — Tenant Superusers

| Role | SystemRole Enum | Scope | Description |
|------|----------------|-------|-------------|
| `Director` | `2` | Full tenant | CEO/MD/Owner. All access. Creates roles, departments, manages users. |
| `HRManager` | `3` | Full tenant | Head of HR. All access identical to Director. Creates roles, departments, manages users. |

### Tier 2 — Department Managers (Dynamic)

| Role | SystemRole Enum | Scope | Description |
|------|----------------|-------|-------------|
| `Manager` | `4` | Department only (or multi-dept if multiple assignments) | Created by Director/HRManager. Department-scoped. Can: approve leave + timesheets for team, view team profiles, view team KPIs, view own payslip (self-access). Cannot: see other employees' payroll, compliance, audit, or data from non-managed departments. Also has an employee record (receives payslip, accrues leave). Manager's own employee record **is included** in their team view — they are an employee of the department they manage. |

Manager sub-types (examples, created at runtime):
- Finance Manager, Sales Manager, Operations Manager, Warehouse Manager, etc.

### Tier 3 — Employee (Base Self-Service)

| Role | SystemRole Enum | Scope | Description |
|------|----------------|-------|-------------|
| `Employee` | `5` | Own records only | Base self-service. Can: view own profile, own payslips, own leave balance, submit own leave requests. Cannot: access timesheets, compliance, audit, or other employees' data. |

Employee sub-types (examples, created at runtime):
- Permanent Employee, Part-Time Employee, Contractor, Intern

---

## 3. C# Role Enum

```csharp
// REQ-SEC-002: Role-based access with least-privilege defaults
public enum SystemRole
{
    Unknown = 0,
    SaasAdmin = 1,   // Platform owner only — cross-tenant
    Director = 2,    // Tenant superuser
    HRManager = 3,   // Tenant admin (identical access to Director)
    Manager = 4,     // Department-scoped (custom permission set from Firestore)
    Employee = 5     // Self-service only
}
```

---

## 4. Screen Access Matrix

### Screen Inventory

| ID | File | Blazor Route | Screen Name |
|----|------|-------------|-------------|
| S1 | 01-login.html | `/login` | Authentication |
| S2 | 02-dashboard.html | `/dashboard` | KPI Dashboard |
| S3 | 03-employees.html | `/employees` | Employee Management |
| S4 | 04-payroll.html | `/payroll` | Payroll Runs |
| S5 | 05-leave.html | `/leave` | Leave Management |
| S6 | 06-compliance.html | `/compliance` | Compliance Scoring |
| S7 | 07-timesheet.html | `/timesheets` | Time & Attendance |
| S8 | 08-audit.html | `/audit` | Audit Trail |
| S9 | 09-role-management.html | `/settings/roles` | Role Management |
| S10 | 10-settings.html | `/settings` | Settings (Role Mgmt, Departments, Users, Company, Archival) — Director/HRManager admin tabs only |
| S11 | 11-admin.html | `/admin` | SaasAdmin Platform UI |
| S12 | 12-analytics.html | `/analytics` | Company Analytics |
| S13 | 13-my-analytics.html | `/my-analytics` | My Analytics (Personal) |
| S14 | 14-clock-in.html | `/clock-in` | Clock In / Out (Employee self-service + Manager verification) |
| S15 | 15-security-ops.html | `/admin/security` | Security Operations Centre (OWASP coverage, vulnerabilities, incidents, POPIA controls) |
| S16 | 03-employees.html (self-view mode) | `/profile` | My Profile — merged employee record (read-only for self) + Account Settings (editable: photo, display name, theme, notifications, MFA) |

### Access Matrix

**Access levels**: Full, Read, Own (own data only), Team (department only), Approve (approve/reject only), None (route blocked, nav item hidden)

> **Self-access note**: Regardless of the matrix below, every tenant user always has Own-level access to their own employee document, own payslips, and own leave. See Section 1.6.

| Screen | SaasAdmin | Director | HRManager | Manager | Employee |
|--------|-----------|----------|-----------|---------|----------|
| S1 Login | All | All | All | All | All |
| S2 Dashboard | Platform widgets | All widgets | All widgets | Team widgets + Own | Own widgets |
| S3 Employees | None | Full | Full | Team / Read | Own profile |
| S4 Payroll | None | Full | Full | Own payslips only¹ | Own payslips only |
| S5 Leave | None | Full | Full | Team / Approve + Own requests | Own requests |
| S6 Compliance | None | Full | Full | None | None |
| S7 Timesheets | None | Read | Read | Team / Approve | None |
| S8 Audit | Platform read | Full | Full | None | None |
| S9 Role Mgmt | None | Full | Full | None | None |
| S10 Settings | None | Full (all 5 admin tabs) | Full (all 5 admin tabs) | None | None |
| S11 Admin UI | Full | None | None | None | None |
| S12 Analytics | None | Full | Full | Team-filtered² | None |
| S13 My Analytics | None | Full (own) | Full (own) | Full (own) | Full (own) |
| S14 Clock In/Out | None | Read all | Read all | Team status + Flag³ | Own clock-in/out |
| S15 Security Ops | Full | None | None | None | None |
| S16 My Profile | None | Own (Account Settings section) | Own (Account Settings section) | Own (Account Settings section) | Own (Account Settings section) |

¹ Managers receive payslips as employees but cannot access other employees' payroll data or payroll run management.
² Manager route `/analytics` is permitted but the API layer filters all queries to `scope IN ['department_<id>', ...]` using the JWT `dept_ids` claim. Payroll and compliance sub-maps are stripped from API responses for Manager-role callers. See REQ-ANA-002.
³ Manager view of `/clock-in` shows team clock status panel (today's in/out state per employee) and can create `timesheet_flags` records. Scope limited to managed department(s). Cannot modify employee clock entries directly.

---

## 5. Navigation Per Role

Navigation items are rendered conditionally — **absent entirely** for roles without access (not greyed out, not disabled — not rendered).

### SaasAdmin Navigation (`/admin/*`)
```
Admin Dashboard
Tenants
System Health
Seed Data Manager
Feature Flags
Platform Audit Log
Security Ops         → /admin/security
```

### Director / HRManager Navigation (identical)
```
── Main ──
Dashboard          → /dashboard
Employees          → /employees
Payroll            → /payroll
Leave              → /leave
Compliance         → /compliance
Timesheets         → /timesheets
Audit Trail        → /audit
── Settings ──
Role Management    → /settings/roles
Departments        → /settings/departments
Users              → /settings/users
Company Settings   → /settings/company
── Insights ──
Analytics          → /analytics
My Analytics       → /my-analytics
── Account ──
My Profile         → /profile     (merged: own employee record + account prefs)
```

### Manager Navigation
```
── Main ──
Dashboard          → /dashboard     (team widgets + own)
Employees          → /employees     (team members only)
Leave              → /leave         (team approval queue + own leave requests)
Timesheets         → /timesheets    (team approval queue)
Clock In / Out     → /clock-in      (team attendance status + flag suspected absences)
My Payslips        → /payroll/my-payslips
My Profile         → /profile       (merged: own employee record + account prefs)
── Insights ──
Team Analytics     → /analytics     (same route, server-side filtered to managed dept(s))
My Analytics       → /my-analytics
```

> Multi-department managers see a combined team view across all departments they manage.

### Employee Navigation
```
── Main ──
Dashboard          → /dashboard     (personal widgets)
My Profile         → /profile       (merged: own employee record + account prefs)
My Payslips        → /payroll/my-payslips
My Leave           → /leave/my-requests
Clock In / Out     → /clock-in      (employee self-service: clock in and clock out)
── Insights ──
My Analytics       → /my-analytics
```

---

## 6. Dashboard Widgets Per Role

Widgets are rendered conditionally — absent entirely for roles without access.

| Widget | SaasAdmin | Director | HRManager | Manager | Employee |
|--------|-----------|----------|-----------|---------|----------|
| Tenant health / uptime | Yes | No | No | No | No |
| Active tenant count | Yes | No | No | No | No |
| Headcount (all) | No | Yes | Yes | No | No |
| Team headcount | No | No | No | Yes | No |
| Payroll run status | No | Yes | Yes | No | No |
| Net payroll total (ZAR) | No | Yes | Yes | No | No |
| Leave balance summary | No | Yes | Yes | No | No |
| Team leave summary | No | No | No | Yes | No |
| Own leave balance | No | Yes² | Yes² | Yes¹ | Yes |
| Own next payslip date | No | Yes² | Yes² | Yes¹ | Yes |
| SARS compliance score | No | Yes | Yes | No | No |
| BCEA compliance score | No | Yes | Yes | No | No |
| POPIA compliance score | No | Yes | Yes | No | No |
| Pending approvals (all) | No | Yes | Yes | No | No |
| Pending approvals (team) | No | No | No | Yes | No |
| Recent audit events | No | Yes | Yes | No | No |
| Risk score | No | Yes | Yes | No | No |

¹ Via self-access guarantee (Section 1.6) — not a Manager permission token.
² Director/HRManager personal widgets appear alongside full-organisation widgets on the same dashboard (they are employees of the Executive department and accrue leave and receive payslips).

---

## 7. Dynamic RBAC: Role Management

Director and HRManager can create and manage roles via S9 (Role Management screen).

### Allowed Actions
1. **Create Manager role variant**: name it (e.g., "Finance Manager"), assign department scope, select from allowed permission set
2. **Create Employee type**: name it (e.g., "Contractor"), assign default department — no extra permissions
3. **Assign role to user** via Settings → User Management (S10)
4. **Revoke role from user** via S10

### Permission Tokens for Custom Manager Roles
Director/HRManager select permissions from this allowed set only:

| Token | Capability |
|-------|-----------|
| `leave.team.view` | View own team's leave requests |
| `leave.team.approve` | Approve / reject own team's leave |
| `timesheet.team.view` | View own team's timesheets |
| `timesheet.team.approve` | Approve own team's timesheets |
| `employee.team.view` | View profiles of own team members |
| `reports.team.view` | View team KPIs and headcount widgets on dashboard |

**Hardcoded denials** — cannot be granted to Manager or Employee roles by anyone:
- Any payroll access
- Any compliance access
- Any audit trail access
- Any role / user management access
- Any cross-department employee access

---

## 8. Field-Level Access Rules

### Salary Data Visibility

| Field | Director | HRManager | Manager | Employee |
|-------|----------|-----------|---------|----------|
| `gross_amount_zar` | Yes | Yes | No | Own payslip only |
| `net_amount_zar` | Yes | Yes | No | Own payslip only |
| `paye_amount_zar` | Yes | Yes | No | Own payslip only |
| `uif_amount_zar` | Yes | Yes | No | Own payslip only |
| `deduction_total_zar` | Yes | Yes | No | Own payslip only |

- Enforcement: API response shapes per role, not UI-only
- Manager receives own payslip data via the self-access guarantee (`firebase_uid == requesting_uid` check)
- Manager receives `403` on any payroll endpoint that isn't scoped to their own `employee_id` — cross-employee payroll data is never returned in a Manager-level response

### Restricted Field Masking (REQ-SEC-007)

Fields masked by default, require explicit unmasking action (logged as AuditEvent):

| Field | Masked Display | Who Can Unmask | Trigger |
|-------|---------------|----------------|---------|
| `national_id_or_passport` | `***-***-**** 0` | Director, HRManager | Click → purpose code required → AuditEvent |
| `tax_reference` | `*** / *** / ***` | Director, HRManager | Click → purpose code required → AuditEvent |
| `bank_account_ref` | `**** **** 1234` | Director, HRManager | Click → MFA challenge → AuditEvent |

AuditEvent written on unmask must include:
- `actor_id` (who unmasked)
- `field_name` (which field)
- `employee_id` (whose data)
- `purpose_code` (lawful basis — CTL-POPIA-002)
- `timestamp_utc`

### Manager Data Scope Enforcement

- All API queries for Manager role: server loads all active Manager assignments for the requesting user, then applies `WHERE department_id IN [dept1, dept2, ...]` filter (union of all managed departments)
- Single-department managers: effectively `WHERE department_id = dept1`
- Multi-department managers: combined result set across all managed departments — no switching required
- **Manager's own employee record is included in team queries** — they are an employee of the department they manage. Their own row appears in the team employee list.
- Accessing an employee outside all managed departments: returns `403` (not `404`) — information leakage prevention
- `reports_to_employee_id` chain used for leave and timesheet scoping within department(s)

---

## 9. Firestore Collections for RBAC

### `roles` collection
```json
{
  "tenant_id": "tenant_abc",
  "role_id": "role_finance_manager",
  "display_name": "Finance Manager",
  "base_system_role": "Manager",
  "department_id": "dept_finance",
  "permissions": ["leave.team.view", "leave.team.approve", "employee.team.view"],
  "created_by": "user_director_001",
  "created_at": "2026-02-20T00:00:00Z",
  "is_system_role": false
}
```

### `departments` collection
```json
{
  "tenant_id": "tenant_abc",
  "department_id": "dept_finance",
  "display_name": "Finance",
  "cost_centre_code": "CC-001",
  "head_employee_id": "emp_001",
  "created_at": "2026-02-20T00:00:00Z"
}
```

### `role_permissions` collection
```json
{
  "tenant_id": "tenant_abc",
  "role_id": "role_finance_manager",
  "permissions": ["leave.team.view", "leave.team.approve", "employee.team.view", "reports.team.view"],
  "version": 1,
  "updated_at": "2026-02-20T00:00:00Z",
  "updated_by": "user_director_001"
}
```

### `user_role_assignments` collection

A user may have **multiple active documents** with the same `firebase_uid` (one per role assignment). All active assignments are loaded at session time to determine effective role and department scope.

```json
{
  "tenant_id": "tenant_abc",
  "firebase_uid": "firebase_uid_abc123",
  "employee_id": "emp_456",
  "role_id": "role_finance_manager",
  "system_role": "Manager",
  "department_id": "dept_finance",
  "is_primary": true,
  "effective_from": "2026-03-01",
  "effective_to": null,
  "assigned_by": "user_director_001",
  "assigned_at": "2026-02-20T00:00:00Z"
}
```

`is_primary`: `true` for the main/highest-privilege assignment. `false` for secondary assignments (additional departments, secondary roles). Used for audit attribution and UI default context.

`is_active`: `true` = assignment is active; `false` = revoked. Assignments are **never deleted** (append-only audit requirement). Revocation sets `is_active: false` and populates `effective_to` + `revoked_by` + `revoked_at`.

**Invariants for `user_role_assignments`:**
- `department_id` must be **null** for `Director` and `HRManager` assignments (they are tenant-scoped, not department-scoped)
- `department_id` must be **non-null** for `Manager` assignments
- Exactly one active assignment per user must have `is_primary: true`
- If the primary assignment is revoked, the application auto-promotes the oldest remaining active assignment

### Fields added to `employees` collection

```json
{
  "department_id": "dept_finance",
  "role_id": "role_finance_manager",
  "system_role": "Manager",
  "employee_type": "Permanent",
  "reports_to_employee_id": "emp_001"
}
```

> `department_id` here is the **primary employment department** — used for payroll cost-centre allocation and leave approval routing. For multi-department managers, additional department access comes from secondary `user_role_assignments`, not this field.

---

## 10. Blazor Route Authorization

Every Blazor page component uses `[Authorize]` attribute with role constraints:

```csharp
// Dashboard — all authenticated users, widget rendering is role-conditional
[Authorize]

// Employee Management
[Authorize(Roles = "Director,HRManager,Manager,Employee")]

// Payroll
[Authorize(Roles = "Director,HRManager,Manager,Employee")] // Manager + Employee show own-payslip-only view (self-access guarantee)

// Leave
[Authorize(Roles = "Director,HRManager,Manager,Employee")]

// Compliance
[Authorize(Roles = "Director,HRManager")]

// Timesheets
[Authorize(Roles = "Director,HRManager,Manager")]

// Audit Trail
[Authorize(Roles = "Director,HRManager,SaasAdmin")]

// Settings (Roles, Depts, Users)
[Authorize(Roles = "Director,HRManager")]

// Admin UI
[Authorize(Roles = "SaasAdmin")]

// Company Analytics (Manager sees team-filtered view via server-side filtering)
// REQ-ANA-001, REQ-ANA-002
[Authorize(Roles = "Director,HRManager,Manager")]

// My Analytics — personal data only, all tenant users (self-access guarantee)
// REQ-ANA-003
[Authorize(Roles = "Director,HRManager,Manager,Employee")]
```

All unauthorized route access redirects to `/unauthorized` (not `/login`) — user is already authenticated, just lacks permission.

### Special Routing Cases

| Scenario | Behavior |
|----------|---------|
| SaasAdmin navigates to any tenant route (`/dashboard`, `/payroll`, etc.) | Redirect to `/admin` — they are authorized, just in the wrong context |
| All user assignments expired or `is_active: false` | Redirect to `/unauthorized` with message: "Your role assignment has expired. Contact your administrator." |
| Firebase-authenticated but no `user_role_assignments` document exists | Redirect to `/unauthorized` with message: "No role assigned. Contact your administrator." |

### JWT Token Claims Strategy

Firebase Authentication JWTs do not natively carry Firestore role data. ZenoHR uses **Firebase Custom Claims** to embed role information:

| Claim | Value | Set by |
|-------|-------|--------|
| `role` | Highest-privilege `system_role` string (e.g., `"Manager"`) | ASP.NET Core auth middleware on first request post-login |
| `dept_ids` | Array of managed department IDs (Manager-only; empty array for others) | Same middleware |
| `tenant_id` | Tenant identifier | Firebase Auth custom claim, set during onboarding |

**Claim population flow**: On first API request after Firebase sign-in, the ASP.NET Core middleware reads all active `user_role_assignments` for the UID from Firestore, computes effective role and dept scope, and writes custom claims back to Firebase Auth. Claims are refreshed when assignments change (revocation triggers a force-claim-refresh on next request).

---

## 11. Traceability

| Requirement | Implementation |
|-------------|---------------|
| REQ-SEC-002 | `SystemRole` enum, `[Authorize(Roles)]` on all pages |
| REQ-SEC-003 | Director/HRManager co-approve payroll finalization. **Exception**: Director/HRManager may co-approve payroll runs that include their own payslip — recusal is not required in the SA SME context. BCEA and SARS do not mandate salary-approval recusal. This is a documented governance exception. |
| REQ-SEC-007 | Field masking on `national_id_or_passport`, `tax_reference`, `bank_account_ref` |
| CTL-POPIA-002 | Purpose code required on unmask actions |
| CTL-POPIA-007 | Monthly access review — `user_role_assignments` effective dates enable this |
| TC-SEC-001 | Route guard tests: unauthorized roles redirected to `/unauthorized` |
| TC-SEC-007 | Field masking tests: restricted fields return masked values for unauthorized roles |
| TC-SEC-008 | Self-access guarantee: Manager and Employee can always retrieve own payslip even if role grants no payroll access |
| TC-SEC-009 | Multi-dept Manager: team query returns employees from all managed departments (union), not just primary |
