# ZenoHR — Claude Agent Context

> **Read this file first in every session.** It is the single source of truth for project conventions, architecture, and progress state.

---

## Project Overview

**ZenoHR** is a South African HR, payroll, and compliance platform built specifically for **Zenowethu (Pty) Ltd**. It automates payroll calculations (PAYE, UIF, SDL, ETI), leave management, timekeeping, SARS filing, and BCEA/POPIA compliance — all governed by South African labour law.

**Deployment company**: Zenowethu (Pty) Ltd
**Employee email domain**: @zenowethu.co.za (all employee and staff accounts use this domain)
**Primary operator role**: HR Manager — the day-to-day operator of ZenoHR. The Director retains identical system access and full authority over all functions. All user stories and workflows are designed with the HR Manager as the primary persona.
**Target users**: HR Manager (primary), Director (full authority), Managers (team-scoped), Employees (self-service), SaasAdmin (platform/IT admin).

---

## Agent Authority — Package & Tool Installation

**Agents are fully authorized to install any package, SDK, CLI tool, or dependency** that helps deliver the project objective without asking for permission first. This includes but is not limited to:

- **NuGet packages** — `dotnet add package <PackageName>` at any time
- **npm / Node.js packages** — for tooling (e.g., linters, static analysis)
- **Python packages** — `pip install` or `uv add` for tooling scripts
- **.NET SDK updates** — install newer .NET 10.x patch SDKs if required
- **Firebase CLI** — `npm install -g firebase-tools` for emulator and deployment
- **Azure CLI** — for Container Apps deployment (TASK-148)
- **GitHub CLI (`gh`)** — for CI/CD automation
- **Any other developer tooling** — if it unblocks implementation

**Authorization scope**: Installing packages to make the project compile, run tests, deploy, or satisfy a requirement is always pre-authorized. No confirmation needed.

**Constraints**: Do not install packages that introduce security vulnerabilities (check NuGet/npm advisory databases). Prefer stable releases over pre-release unless a specific pre-release version is required for .NET 10 compatibility.

---

## Tech Stack (Locked)

| Layer | Technology | Notes |
|-------|-----------|-------|
| Runtime | .NET 10 (LTS) | `<TargetFramework>net10.0</TargetFramework>` |
| Backend | ASP.NET Core Web API | Modular monolith, vertical slice |
| Frontend | Blazor Server | SSR, real-time via SignalR |
| Database | Google Cloud Firestore | Accessed via `Google.Cloud.Firestore` NuGet SDK |
| Auth | Firebase Authentication | OIDC/JWT, MFA, validated by ASP.NET Core middleware |
| Hosting | Azure Container Apps (SA North) | POPIA data residency compliance |
| PDF | QuestPDF | Payslips, evidence packs, reports |
| Events | MediatR (in-process) | Domain events within modular monolith |
| Background | .NET BackgroundService + Azure scheduled jobs | Cron triggers for filings, reminders |
| CI/CD | GitHub Actions | Build → test → SAST → deploy |
| Observability | OpenTelemetry SDK + Azure Monitor | Application Insights |
| Secrets | Azure Key Vault | All secrets, connection strings, API keys |
| Icons | Lucide Icons | 20px default, 1.5px stroke |
| Fonts | Inter (UI) + JetBrains Mono (monetary) | See `docs/design/design-tokens.md` |

---

## Solution Structure

```
ZenoHR/
├── src/
│   ├── ZenoHR.Api/                    # ASP.NET Core host, controllers, middleware
│   ├── ZenoHR.Web/                    # Blazor Server UI (pages, components)
│   ├── ZenoHR.Domain/                 # Shared kernel: MoneyZAR, TaxYear, enums, base types
│   ├── ZenoHR.Infrastructure/         # Firestore repos, Firebase auth, PDF gen, filing export
│   ├── ZenoHR.Module.Employee/        # Employee management bounded context
│   ├── ZenoHR.Module.TimeAttendance/  # Timesheets, clock entries
│   ├── ZenoHR.Module.Leave/           # Leave requests, balances, accruals
│   ├── ZenoHR.Module.Payroll/         # Payroll runs, calculations, payslips
│   ├── ZenoHR.Module.Compliance/      # SARS filings, BCEA checks, POPIA controls
│   ├── ZenoHR.Module.Audit/           # Audit trail, evidence packs, hash-chain
│   └── ZenoHR.Module.Risk/            # Risk scoring, dashboard insights
├── tests/
│   ├── ZenoHR.Domain.Tests/
│   ├── ZenoHR.Module.Payroll.Tests/   # Property-based tests for calculations
│   ├── ZenoHR.Module.Compliance.Tests/
│   ├── ZenoHR.Integration.Tests/      # Firestore emulator tests
│   └── ZenoHR.Architecture.Tests/     # ArchUnit-style boundary enforcement
├── docs/
│   ├── prd/                           # 17 PRD documents (00–17)
│   ├── seed-data/                     # Statutory configuration JSON files
│   ├── schemas/                       # Firestore schema, monetary precision
│   ├── design/
│   │   ├── brand/                     # Logos (Zenologo.png, Zenologo2.png)
│   │   ├── screenshots/               # Reference screenshots (10 PNGs)
│   │   ├── design-tokens.md           # Colors, typography, spacing (locked)
│   │   ├── mobile-guidelines.md       # Mobile responsive rules
│   │   └── mockups/                   # 16 HTML mockups + shared.css (source of truth for UI)
│   │       └── assets/                # Sample images (sarah.jpg, sarah.png)
│   └── progress/                      # Progress log, history, decisions
├── .mcp/
│   └── server.py                      # FastMCP context server (uv run .mcp/server.py)
├── .mcp.json                          # MCP server registration
└── CLAUDE.md                          # THIS FILE
```

---

## Critical Rules (Non-Negotiable)

### 1. No Hardcoded Statutory Values
All tax rates, thresholds, leave entitlements, and BCEA limits come from `StatutoryRuleSet` documents in Firestore (seeded from `docs/seed-data/*.json`). **Never** embed a tax bracket, UIF rate, or leave entitlement as a literal in code.

### 2. Decimal for Money — Always
All monetary values use `decimal` (`System.Decimal`) via the `MoneyZAR` value object. Using `float` or `double` for money is a **Sev-1 defect**. Firestore stores monetary values as **strings** to preserve precision. See `docs/schemas/monetary-precision.md`.

### 3. Immutable Finalized Records
Once a `PayrollRun`, `AuditEvent`, or `AccrualLedgerEntry` is finalized, it is **write-once**. No updates, no deletes. Corrections create new adjustment documents referencing the original. Firestore security rules enforce this.

### 4. Hash-Chained Audit Trail
Every `AuditEvent` includes `previous_event_hash` (SHA-256 of the prior event's canonical JSON). This creates a tamper-evident chain. Breaking the chain is a **Sev-1 defect**.

### 5. Tenant Isolation
Every root Firestore document has a `tenant_id` field. All queries filter by `tenant_id`. Firestore security rules enforce tenant scoping. Cross-tenant data access is a **Sev-1 security vulnerability**.

### 6. Module Boundaries
Modules communicate **only** via MediatR domain events or shared kernel types. No module may directly read/write another module's Firestore collections. The `ZenoHR.Architecture.Tests` project enforces this with ArchUnit-style tests.

### 7. Traceability
Every feature, test, and control must trace to a PRD requirement ID (`REQ-*`), control ID (`CTL-*`), or test ID (`TC-*`). Orphan code (not traceable to a requirement) should not exist.

---

## Coding Conventions

### C# Style
- **Nullable reference types**: Enabled globally (`<Nullable>enable</Nullable>`)
- **Implicit usings**: Enabled
- **Naming**: PascalCase for public members, _camelCase for private fields, camelCase for parameters
- **Classes**: `sealed` by default. Only unseal when inheritance is explicitly needed.
- **Records**: Use `record` or `record struct` for value objects and DTOs
- **Result pattern**: Use `Result<T>` for operations that can fail. No exceptions for business logic failures.
- **Enums**: Always include an `Unknown = 0` member for forward compatibility
- **Async**: All I/O operations are async. Use `CancellationToken` propagation.
- **Null checks**: Use `ArgumentNullException.ThrowIfNull()` at public API boundaries only

### Error Handling
- Domain errors → `Result<T>` with typed error codes (see error taxonomy in domain)
- Infrastructure errors → Let them propagate, caught by global exception handler
- Validation errors → `FluentValidation` at API boundary, return `ProblemDetails`

### Testing
- **Framework**: xUnit + FluentAssertions + NSubstitute
- **Property-based**: FsCheck for payroll calculations
- **Coverage targets**: 90% line (domain), 85% branch, 100% contract tests
- **Naming**: `MethodName_Scenario_ExpectedResult`
- **Firestore tests**: Use Firestore emulator, not production

---

## Payroll Calculation Rules

- **Pay periods**: Monthly (÷12) and Weekly (÷52)
- **PAYE method**: Annual equivalent — annualise → apply brackets → subtract rebates → floor at 0 → round annual → de-annualise → round period
- **Rounding**: Annual PAYE to nearest rand (AwayFromZero), period PAYE to nearest cent
- **UIF**: 1% employee + 1% employer, ceiling R17,712/month, max R177.12 each
- **SDL**: 1% employer-only, exempt if annual payroll < R500k
- **ETI**: Age 18-29, R2,500 min wage, R7,500 max remuneration, first/second 12-month tiers
- **Payslip invariant**: `net_pay == gross_pay - paye - uif_employee - pension_employee - medical_employee - other_deductions` (verified to the cent)

Full specifications: `docs/seed-data/sars-paye-2025-2026.json` and `docs/schemas/monetary-precision.md`

---

## Firestore Collections (Summary)

| Bounded Context | Root Collections |
|----------------|-----------------|
| Employee | `employees`, `employees/{id}/contracts` |
| Time & Attendance | `timesheets`, `clock_entries` |
| Leave | `leave_balances`, `employees/{id}/leave_requests`, `accrual_ledger` |
| Payroll | `payroll_runs`, `payroll_runs/{id}/results`, `payroll_runs/{id}/adjustments` |
| Compliance | `compliance_submissions`, `compliance_schedules` |
| Audit | `audit_events` |
| Risk | `risk_assessments`, `risk_snapshots` |
| Config | `statutory_rule_sets`, `company_settings` |

Full schema: `docs/schemas/firestore-collections.md`

---

## Progress Tracking

**Location**: `docs/progress/progress-log.json`

### Session Protocol
1. **On start**: Read `docs/progress/progress-log.json` → display current phase, active tasks, blockers
2. **During work**: Update task status as work progresses
3. **On decision**: Append to `docs/progress/decisions.jsonl`
4. **On task completion**: Mark completed, update next_actions
5. **On end**: Write final state with 1–3 concrete next_actions

### "What's Next?" Algorithm
1. Read `progress-log.json`
2. Filter tasks: `status != completed AND blockers == [] AND depends_on all completed`
3. Sort by priority
4. Return top 3 tasks with requirement links
5. Surface any unresolved blockers

---

## MCP Context Server

A local FastMCP server (`.mcp/server.py`) provides project context to every agent automatically.
**Always call `get_context()` first at the start of every session before any work.**

| Need | MCP Tool |
|------|----------|
| Full project state + CLAUDE.md | `get_context()` |
| Current progress log | `get_progress()` |
| Top 3 unblocked tasks | `get_next_tasks()` |
| Specific PRD document | `get_prd(number)` — 0–14 |
| Schema reference | `get_schema("firestore")` or `get_schema("monetary")` |
| Tax / BCEA statutory data | `get_seed_data("paye")`, `get_seed_data("bcea")`, etc. |
| UI mockup for a screen | `get_mockup("dashboard")`, `get_mockup("payroll")`, etc. |
| Design tokens + shared CSS | `get_design_tokens()` |
| Mark task complete | `update_task_status(task_id, "completed", notes)` |
| Log architectural decision | `log_decision(id, title, decision, rationale, req_ref)` |
| Check code traceability | `validate_traceability(code_snippet)` |
| RBAC role + screen access spec | `get_rbac()` |

**Server command**: `uv run .mcp/server.py` (configured in `.mcp.json`)

---

## Skill Invocation Rules (Mandatory — Not Optional)

These skills **must** be invoked at the specified triggers. Not suggestions — requirements.

| Trigger | Skill | When |
|---------|-------|------|
| Session start | `zenohr-progress-tracker` | Before any work — every session |
| Any feature implementation | `constraint-driven-development` | 4-persona check before writing code |
| Payroll / tax / BCEA / UIF calculations | `sa-compliance-engine` | Before any calculation logic |
| Firestore collections / repositories / queries | `dotnet-firestore-patterns` | Before any data access layer |
| Domain entities / aggregates / state machines | `zenohr-domain-expert` | Before designing domain models |
| Branch / CI / sprint / release decisions | `project-workflow-management` | For workflow choices |
| User correction received ("no, use X", "actually...") | `claude-reflect` | Auto-detect and queue learnings |
| Any Blazor UI component or page | `brand-guidelines` + design tokens | Before any UI code |

---

## Autonomous Agent Behaviors (Always — Without Being Asked)

### After completing any task:
1. Call MCP `update_task_status(task_id, "completed", brief_notes)` — never edit JSON directly
2. Call MCP `get_next_tasks()` and display the top 3 results to the user
3. If an architectural decision was made, call MCP `log_decision()`
4. **IMMEDIATELY stage all changed files for the completed phase/task** — `git add <specific-files>` — never `git add .` or `git add -A`. Do this right after marking the task complete, not at end of session.
5. Show the user a summary of staged files and **await approval before committing**
6. **Keep the working directory clean** — unstaged files from a completed phase are not acceptable. If a phase is done, its files must be staged before moving on.

### After implementing any production code:
1. Create test file(s) immediately in the same PR — never defer tests to later
2. Unit tests: `tests/ZenoHR.{Module}.Tests/{ClassName}Tests.cs`
3. Test naming: `MethodName_Scenario_ExpectedResult` (xUnit + FluentAssertions + NSubstitute)
4. Payroll calculations: add FsCheck property-based tests covering edge cases
5. Firestore repositories: add integration tests using the local emulator
6. Run all tests — fix failures before marking any task complete
7. Coverage targets: 90% line (domain), 85% branch

### Before writing any implementation code:
Apply the CDD 4-persona check (from `constraint-driven-development` skill):
1. **Architect** — Simplest approach? No premature abstractions?
2. **Reviewer** — Readable in 2 minutes by a new developer? No magic numbers?
3. **Designer** — Follows mockups in `docs/design/mockups/`? Accessible?
4. **Security Engineer** — `tenant_id` enforced? `MoneyZAR` used? `CancellationToken` propagated? No hardcoded secrets?

### Traceability on every new element:
Every class, public method, test, and API endpoint must have a `// REQ-XX-000` or `// CTL-XX-000` comment.
**Orphan code (no traceability reference) must not be committed.**

---

## UI Design Source of Truth

The canonical UI design is the **HTML mockups** in `docs/design/mockups/`:

| File | Screen |
|------|--------|
| `01-login.html` | Authentication / sign-in |
| `02-dashboard.html` | Main KPI dashboard |
| `03-employees.html` | Employee management |
| `04-payroll.html` | Payroll run management |
| `05-leave.html` | Leave management calendar |
| `06-compliance.html` | Compliance scoring (SARS, BCEA, POPIA) |
| `07-timesheet.html` | Time & attendance weekly view |
| `08-audit.html` | Audit trail with hash-chain |
| `09-role-management.html` | Role creation and permission management |
| `10-settings.html` | Settings: users, departments, company, security |
| `11-admin.html` | SaasAdmin platform operations console |
| `12-analytics.html` | Company Analytics — Director/HRManager (team-filtered for Manager) |
| `13-my-analytics.html` | My Analytics — personal earnings, leave, and tax summary for all tenant users |
| `14-clock-in.html` | Employee self-service Clock In / Out — employee view + manager team status panel |
| `15-security-ops.html` | SaasAdmin Security Operations Centre — OWASP coverage, vulnerabilities, incidents, POPIA controls |
| `16-payslip-template.html` | A4 Payslip — QuestPDF source of truth. All BCEA Section 33 mandatory fields. 9 sections. |
| `shared.css` | Design system — colors, typography, spacing |

All Blazor Server components **must faithfully implement these mockups** pixel-by-pixel.
Use MCP `get_mockup("screen-name")` or `get_design_tokens()` to retrieve them.

> `docs/design/google-stitch-prompts.md` is **archived** — do not use for implementation.

---

## Role-Based Access Control (RBAC)

Full specification: `docs/prd/15_rbac_screen_access.md` (PRD-15 — supersedes PRD-05 role model).
Use MCP `get_rbac()` to retrieve it during implementation.

### 5 System Roles (REQ-SEC-002)

| Role | Enum | Scope | Access Level |
|------|------|-------|-------------|
| `SaasAdmin` | 1 | Platform (cross-tenant) | Platform `/admin/*` UI only. Cannot read tenant data. |
| `Director` | 2 | Full tenant | All screens. Can create roles, departments, manage users. |
| `HRManager` | 3 | Full tenant | Identical to Director. |
| `Manager` | 4 | Own department only | Leave/timesheet approval, team headcount, team profiles. No payroll/compliance/audit. |
| `Employee` | 5 | Own records only | Own profile, payslips, leave requests. No timesheet access (HR enters). |

### Screen → Role Access (Quick Reference)

| Screen | SaasAdmin | Director/HRMgr | Manager | Employee |
|--------|-----------|----------------|---------|----------|
| `/dashboard` | Platform KPIs | All widgets | Team widgets | Own widgets |
| `/employees` | None | Full | Team / Read | Own profile |
| `/payroll` | None | Full | None | Own payslips |
| `/leave` | None | Full | Team / Approve | Own requests |
| `/compliance` | None | Full | None | None |
| `/timesheets` | None | Read | Team / Approve | None |
| `/audit` | Platform read | Full | None | None |
| `/settings/*` | None | Full | None | None |
| `/admin/*` | Full | None | None | None |

**Rule**: Navigation items are absent entirely for roles without access — not greyed out, not disabled.

### Self-Access Guarantee
Every authenticated tenant user always has read access to their own employee document, own payslips, and own leave — regardless of system_role. Enforced server-side (`firebase_uid == requesting_uid`). This is why Managers have "My Payslips" in nav.

### Employment & Access Separation
Every Director, HRManager, Manager, and Employee has an `employees` document. They appear in payroll runs and receive payslips. `system_role` is the access overlay only. `SaasAdmin` is the one exception (platform operator — no employee record).

### Multi-Role Assignments
- A user can have multiple active `user_role_assignments` (multi-dept managers, dual roles)
- Effective system_role = highest-privilege active assignment
- Multi-dept Manager team scope = union of all managed departments (combined view, not a switcher)
- One payroll record per person per period regardless of assignment count

### Dynamic RBAC
- Director/HRManager create custom Manager variants (e.g., "Finance Manager") via `/settings/roles`
- Custom Manager roles select from 6 permission tokens (leave, timesheet, employee team access only)
- Cannot grant payroll, compliance, audit, or cross-department access to any Manager/Employee role

---

## Key Reference Documents

| Document | Path |
|----------|------|
| PRD Manifest | `docs/prd/00_manifest.md` |
| PRD Gap Resolution | `docs/prd/14_gap_resolution.md` |
| PAYE Tax Tables | `docs/seed-data/sars-paye-2025-2026.json` |
| UIF/SDL Rates | `docs/seed-data/sars-uif-sdl.json` |
| ETI Rules | `docs/seed-data/sars-eti.json` |
| BCEA Working Time | `docs/seed-data/bcea-working-time.json` |
| BCEA Leave | `docs/seed-data/bcea-leave.json` |
| Firestore Schema | `docs/schemas/firestore-collections.md` |
| Monetary Precision | `docs/schemas/monetary-precision.md` |
| Design Tokens | `docs/design/design-tokens.md` |
| UI Mockups | `docs/design/mockups/` (01-login to 16-payslip-template + shared.css) |
| RBAC Specification | `docs/prd/15_rbac_screen_access.md` |
| PAYE Calculation Spec | `docs/prd/16_payroll_calculation_spec.md` (pseudocode, 12 sections, PRD-16) |
| Blazor Architecture | `docs/prd/17_blazor_component_patterns.md` (component structure, state, SignalR — PRD-17) |
| Navigation Flow | `docs/prd/18_navigation_flow.md` — 26 routes, 4 nav patterns, back-button rules, mobile nav (PRD-18) |
| Progress Log | `docs/progress/progress-log.json` |
| **Vulnerability Register** | `docs/security/vulnerability-register.md` — 28 findings, 6 Sev-1 open |
| **POPIA Control Status** | `docs/security/popia-control-status.md` — 15 controls, 13% implemented |
| **Traceability Index** | `docs/generated/traceability-index.json` — auto-generated by MCP |

---

## PRD Requirement Ranges

| Module | Requirements | Controls | Tests |
|--------|-------------|----------|-------|
| Employee | REQ-HR-001 to REQ-HR-006 | — | TC-HR-* |
| Payroll | REQ-HR-003, REQ-HR-004 | CTL-SARS-001 to CTL-SARS-010 | TC-PAY-* |
| Leave | REQ-HR-002 | CTL-BCEA-001 to CTL-BCEA-008 | TC-LEAVE-* |
| Compliance | REQ-COMP-001 to REQ-COMP-006 | CTL-POPIA-001 to CTL-POPIA-015 | TC-COMP-* |
| Security | REQ-SEC-001 to REQ-SEC-010 | CTL-SEC-001 to CTL-SEC-010 | TC-SEC-* |
| Operations | REQ-OPS-001 to REQ-OPS-009 | — | TC-OPS-* |

---

## Security Gap Tracking (Non-Negotiable)

**All security work MUST reference the Vulnerability Register and POPIA Control Status tracker.**

### Active Sev-1 Vulnerabilities (Production Blockers)
These 6 findings MUST be resolved before v1.0 release:

| ID | Finding | Phase Target |
|----|---------|-------------|
| VUL-001 | No security HTTP headers (CSP, HSTS, X-Frame-Options) | Phase 4 |
| VUL-002 | No CORS policy defined | Phase 4 |
| VUL-003 | MFA not enforced on privileged operations (payroll finalize, SARS approve) | Phase 4 |
| VUL-004 | No incident response system (anomaly detection, escalation, alerting) | Phase 5 |
| VUL-005 | No POPIA breach register or notification workflow (CTL-POPIA-011) | Phase 5 |
| VUL-006 | No break-glass emergency access procedure | Phase 5 |

**Use MCP `get_vulnerability_register()` before implementing any security-sensitive feature.**
**Use MCP `get_popia_status()` before implementing any compliance or data privacy feature.**

### POPIA Compliance Status
13% of 15 controls implemented. 8 controls not started. **Releasing without breach register (CTL-POPIA-011) violates POPIA Act §22 — criminal liability.**

### Security Skill Triggers (Additional)
| Trigger | Action |
|---------|--------|
| Implementing any API endpoint | Check VUL-001, VUL-002, VUL-007 (headers, CORS, rate limiting) |
| Implementing payroll finalize/approve | Check VUL-003 (MFA enforcement) |
| Implementing any data unmask feature | Check VUL-020 (purpose code), CTL-POPIA-002 |
| Implementing audit/logging | Check VUL-011 (metadata sanitization), VUL-014 (role changes logged) |
| Implementing compliance module | Check CTL-POPIA-001 through CTL-POPIA-015 in `popia-control-status.md` |

---

## Documentation Governance (Non-Negotiable)

### Documentation Version Control Rules

Every doc with a YAML frontmatter block **must** have `version` and `updated_on` fields.
When a doc is modified, call `bump_doc_version()` to increment version and timestamp.

```yaml
---
doc_id: PRD-XX-NAME
version: 1.0.0          # Bump on every change: patch=content fix, minor=new section, major=restructure
updated_on: YYYY-MM-DD  # Auto-set by bump_doc_version()
---
```

### New MCP Tools for Documentation

| Need | MCP Tool |
|------|----------|
| Security vulnerabilities + gaps | `get_vulnerability_register()` |
| POPIA control implementation status | `get_popia_status()` |
| Doc staleness report (all PRDs vs today) | `get_doc_staleness()` |
| Bump version + timestamp in any doc | `bump_doc_version(relative_path, summary, bump_type)` |
| Scan code → requirement cross-reference | `generate_traceability_index()` |

### Session Start Protocol (Extended)
1. Call `get_context()` — always
2. Call `get_doc_staleness()` — identify stale docs that need updating after recent code changes
3. Call `generate_traceability_index()` — if implementing code (refresh requirement index)
4. If working on security features: call `get_vulnerability_register()` + `get_popia_status()`

### After Completing Any Code Task (Documentation Protocol)
1. If the task implements or partially implements a requirement: call `bump_doc_version()` on the relevant PRD doc
2. If a new security finding is identified: add to `docs/security/vulnerability-register.md`
3. If a POPIA control status changes: update `docs/security/popia-control-status.md`
4. Call `generate_traceability_index()` after significant code additions to refresh the index

### Doc Freshness Thresholds
- **PRD docs**: Warn at >30 days, CI fails at >45 days without update
- **Security docs**: Warn at >35 days (monthly review mandatory)
- **Progress log**: Updated every session (MCP handles this automatically)
- **Decisions log**: Updated on every architectural decision (MCP handles this automatically)

### Local Audit Command
```powershell
powershell -ExecutionPolicy Bypass -File scripts/doc-audit.ps1
```
Run this before every release or when in doubt about documentation health.

### CI Documentation Gates
`.github/workflows/doc-update-gate.yml` enforces on every PR:
1. **Traceability check** — every C# file must have `REQ-*`/`CTL-*`/`TC-*` comment
2. **PRD staleness** — no PRD doc may be >45 days stale
3. **Security doc presence** — `vulnerability-register.md` and `popia-control-status.md` must exist
4. **Traceability index** — auto-generated and uploaded as CI artifact
