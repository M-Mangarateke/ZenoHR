<p align="center">
  <img src="docs/design/brand/Zenologo.png" alt="ZenoHR" width="280" />
</p>

<h3 align="center">South African HR, Payroll & Compliance Platform</h3>

<p align="center">
  Built for <strong>Zenowethu (Pty) Ltd</strong> — automating payroll, leave, timekeeping, SARS filing, and BCEA/POPIA compliance.
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet" alt=".NET 10" />
  <img src="https://img.shields.io/badge/Blazor-Server-512BD4?logo=blazor" alt="Blazor Server" />
  <img src="https://img.shields.io/badge/Firestore-Database-FFCA28?logo=firebase" alt="Firestore" />
  <img src="https://img.shields.io/badge/Azure-Container%20Apps-0078D4?logo=microsoftazure" alt="Azure" />
  <img src="https://img.shields.io/badge/License-Proprietary-red" alt="License" />
</p>

---

## What is ZenoHR?

ZenoHR is a purpose-built HR platform for South African businesses. It handles:

- **Payroll** — PAYE, UIF, SDL, ETI calculations verified against SARS tables (monthly & weekly)
- **Leave Management** — All 5 BCEA leave types with accrual tracking and approval workflows
- **Time & Attendance** — Clock in/out, timesheets, overtime tracking
- **SARS Compliance** — IRP5, EMP201, EMP501, EMP601, EMP701, ITREG generation
- **POPIA Compliance** — 15 privacy controls, breach notification, data subject rights
- **Audit Trail** — SHA-256 hash-chained, tamper-evident event log
- **Analytics** — Company-wide and personal dashboards

---

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Runtime | .NET 10 (LTS) |
| Backend | ASP.NET Core Web API |
| Frontend | Blazor Server (SSR + SignalR) |
| Database | Google Cloud Firestore |
| Auth | Firebase Authentication (OIDC/JWT, MFA) |
| Hosting | Azure Container Apps (SA North) |
| PDF | QuestPDF |
| Events | MediatR |
| IaC | Bicep (Azure) |
| CI/CD | GitHub Actions |
| Monitoring | OpenTelemetry + Azure Monitor |

---

## Project Structure

```
ZenoHR/
├── src/
│   ├── ZenoHR.Api/                    # ASP.NET Core host, controllers, middleware
│   ├── ZenoHR.Web/                    # Blazor Server UI (pages, components, tours)
│   ├── ZenoHR.Domain/                 # Shared kernel: MoneyZAR, TaxYear, enums
│   ├── ZenoHR.Infrastructure/         # Firestore repos, Firebase auth, PDF, encryption
│   ├── ZenoHR.Module.Employee/        # Employee management bounded context
│   ├── ZenoHR.Module.TimeAttendance/  # Timesheets, clock entries
│   ├── ZenoHR.Module.Leave/           # Leave requests, balances, accruals
│   ├── ZenoHR.Module.Payroll/         # Payroll runs, calculations, payslips
│   ├── ZenoHR.Module.Compliance/      # SARS filings, BCEA checks, POPIA controls
│   ├── ZenoHR.Module.Audit/           # Audit trail, evidence packs, hash-chain
│   └── ZenoHR.Module.Risk/            # Risk scoring, dashboard insights
├── tests/
│   ├── ZenoHR.Domain.Tests/           # Domain unit tests
│   ├── ZenoHR.Module.Payroll.Tests/   # Payroll calculation + property-based tests
│   ├── ZenoHR.Module.Compliance.Tests/# POPIA, SARS, security tests
│   ├── ZenoHR.Module.*.Tests/         # Per-module test projects
│   ├── ZenoHR.Integration.Tests/      # Firestore emulator integration tests
│   └── ZenoHR.Architecture.Tests/     # Module boundary enforcement (ArchUnit)
├── docs/
│   ├── prd/                           # 17 Product Requirement Documents
│   ├── schemas/                       # Firestore schema, monetary precision spec
│   ├── seed-data/                     # Statutory config (PAYE, UIF, SDL, ETI, BCEA)
│   ├── design/                        # UI mockups, design tokens, brand assets
│   ├── security/                      # Vulnerability register, POPIA control status
│   ├── deployment/                    # Deployment strategy, go-live checklist
│   ├── developer/                     # Developer guides (6 docs)
│   ├── stakeholder/                   # Executive summary, user guide, FAQ, audit prep
│   └── progress/                      # Progress log, decision log
├── infra/                             # Azure Bicep IaC (Container Apps, Key Vault, VNet)
├── scripts/                           # Build, test, and audit scripts
├── .github/                           # CI/CD workflows, PR template, CODEOWNERS
├── .mcp/                              # MCP context server for AI-assisted development
├── Dockerfile                         # Multi-stage container build
├── ZenoHR.sln                         # Visual Studio solution
└── ZenoHR.slnx                        # Compact XML solution format
```

---

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Firebase CLI](https://firebase.google.com/docs/cli) (for Firestore emulator)
- [Node.js 18+](https://nodejs.org/) (for Firestore security rules tests)
- [Azure CLI](https://docs.microsoft.com/cli/azure/) (for deployment only)

### Build

```bash
dotnet restore ZenoHR.slnx
dotnet build ZenoHR.slnx
```

Or use the build script:

```powershell
.\scripts\build2.ps1
```

### Run Tests

```bash
# Unit tests (no emulator needed)
dotnet test ZenoHR.slnx --filter "Category!=Integration"

# All tests (requires Firestore emulator on localhost:8080)
dotnet test ZenoHR.slnx
```

Or use the test scripts:

```powershell
.\scripts\run-tests.ps1                 # All tests
.\scripts\run-integration-tests.ps1     # Integration tests with emulator
```

### Run Locally

```bash
# Start Firestore emulator
firebase emulators:start --only firestore

# Run the application
dotnet run --project src/ZenoHR.Api
```

The app will be available at `https://localhost:5001`.

---

## Architecture

ZenoHR is a **modular monolith** with 7 bounded contexts communicating via MediatR domain events:

```
┌─────────────────────────────────────────────────────────┐
│                    ZenoHR.Api                           │
│              (ASP.NET Core Host)                        │
├─────────────────────────────────────────────────────────┤
│  ZenoHR.Web (Blazor Server)                             │
├─────────┬──────────┬────────┬───────────┬───────┬───────┤
│Employee │  Leave   │Payroll │Compliance │ Audit │ Risk  │
│ Module  │  Module  │ Module │  Module   │Module │Module │
├─────────┴──────────┴────────┴───────────┴───────┴───────┤
│              ZenoHR.Domain (Shared Kernel)              │
│         MoneyZAR · TaxYear · Enums · Base Types         │
├─────────────────────────────────────────────────────────┤
│            ZenoHR.Infrastructure                        │
│    Firestore · Firebase Auth · PDF · Encryption         │
└─────────────────────────────────────────────────────────┘
```

### Key Design Decisions

- **`MoneyZAR`** — All monetary values use `decimal` via a value object. `float`/`double` for money is forbidden.
- **Immutable records** — Finalized payroll runs, audit events, and accrual entries are write-once.
- **Hash-chained audit** — Every audit event includes `previous_event_hash` (SHA-256) for tamper evidence.
- **Tenant isolation** — Every document has `tenant_id`; all queries filter by it. Enforced at Firestore rules level.
- **No hardcoded statutory values** — Tax rates, thresholds, and entitlements load from `StatutoryRuleSet` documents.

---

## Roles & Access

| Role | Scope | Access |
|------|-------|--------|
| **SaasAdmin** | Platform | Admin console, security ops. No tenant data access. |
| **Director** | Full tenant | All screens and functions. |
| **HRManager** | Full tenant | Day-to-day operations (primary user persona). |
| **Manager** | Own department | Team leave/timesheet approval. No payroll/compliance. |
| **Employee** | Own records | Profile, payslips, leave requests. |

---

## Documentation

| Audience | Location | Contents |
|----------|----------|----------|
| **Developers** | [`docs/developer/`](docs/developer/) | Getting started, architecture, coding conventions, API reference (44 endpoints), security architecture, troubleshooting |
| **Stakeholders** | [`docs/stakeholder/`](docs/stakeholder/) | Executive summary, user guide, FAQ, POPIA compliance report, audit preparation guide |
| **Operations** | [`docs/deployment/`](docs/deployment/) | Deployment strategy, go-live checklist (60+ steps) |
| **Infrastructure** | [`infra/`](infra/) | Azure Bicep templates with deployment guide |
| **Design** | [`docs/design/`](docs/design/) | 16 UI mockups, design tokens, brand guidelines |
| **Requirements** | [`docs/prd/`](docs/prd/) | 17 PRD documents covering all features |
| **Security** | [`docs/security/`](docs/security/) | Vulnerability register, POPIA control status |

---

## SA Compliance

### SARS

| Filing | Status |
|--------|--------|
| PAYE calculation (2025/2026 tax year) | Implemented — verified against SARS tables |
| UIF (1% + 1%, R17,712 ceiling) | Implemented |
| SDL (1% employer, R500k threshold) | Implemented |
| ETI (youth employment incentive) | Implemented |
| IRP5/IT3a tax certificates | Implemented |
| EMP201 monthly return | Implemented (stub eFiling client — ISV accreditation deferred) |
| EMP501/EMP601/EMP701 reconciliation | Implemented |

### BCEA

All 5 leave types (annual, sick, family responsibility, maternity, parental), working time limits, overtime rules, and notice periods are enforced.

### POPIA

15/15 controls implemented: lawful basis, data quality, unmask audit, notice versioning, access reviews, anomaly detection, subject access requests, correction requests, breach notification, incident response, break-glass, session management, field encryption (AES-256), data retention enforcement, and data processor registry.

---

## Scripts

| Script | Purpose |
|--------|---------|
| `scripts/build2.ps1` | Build with error summary |
| `scripts/build.ps1` | Full verbose build |
| `scripts/run-tests.ps1` | Run all tests |
| `scripts/run-integration-tests.ps1` | Integration tests with Firestore emulator |
| `scripts/doc-audit.ps1` | Documentation health check |
| `scripts/validate-traceability.py` | Code-to-requirement traceability scan |

---

## Deployment

ZenoHR deploys to **Azure Container Apps** in the **South Africa North** region (POPIA data residency).

```
GitHub push → CI (build + test + Trivy scan) → GHCR → Azure Container Apps
```

See [`docs/deployment/deployment-strategy.md`](docs/deployment/deployment-strategy.md) for full details and [`docs/deployment/go-live-checklist.md`](docs/deployment/go-live-checklist.md) for the 60-step launch plan.

---

## License

Proprietary — Zenowethu (Pty) Ltd. All rights reserved.
