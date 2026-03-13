---
doc_id: DEV-GETTING-STARTED
version: 1.0.0
updated_on: 2026-03-13
---

# Getting Started with ZenoHR

This guide walks you through setting up a local development environment for the ZenoHR platform. By the end, you will be able to build the solution, run the API locally, and execute the test suite.

---

## Prerequisites

| Tool | Version | Purpose |
|------|---------|---------|
| [.NET 10 SDK](https://dotnet.microsoft.com/download) | 10.0.x LTS | Runtime and build toolchain |
| [Node.js](https://nodejs.org/) | 18+ LTS | Firebase CLI and tooling scripts |
| [Git](https://git-scm.com/) | 2.40+ | Source control |
| IDE | VS Code with C# Dev Kit **or** JetBrains Rider 2025+ | Development |
| [Firebase CLI](https://firebase.google.com/docs/cli) | Latest | Firestore emulator for local development |
| [PowerShell](https://learn.microsoft.com/powershell/) | 7+ | Build and test scripts |

### Verify installations

```bash
dotnet --version    # Should print 10.0.x
node --version      # Should print v18.x or later
git --version       # Should print 2.40+
firebase --version  # Should print 13.x+ (install via: npm install -g firebase-tools)
```

---

## Clone and Build

### 1. Clone the repository

```bash
git clone https://github.com/your-org/ZenoHR.git
cd ZenoHR
```

### 2. Restore NuGet packages

```bash
dotnet restore
```

### 3. Build the solution

```bash
dotnet build
```

Alternatively, use the project build script which provides a cleaner summary output:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/build2.ps1
```

### 4. Run tests

```bash
dotnet test
```

Or use the dedicated test script:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/run-tests.ps1
```

---

## Running Locally

### 1. Configure user secrets

ZenoHR uses .NET User Secrets for local configuration. You must set the Firebase project ID:

```bash
cd src/ZenoHR.Api
dotnet user-secrets init
dotnet user-secrets set "Firebase:ProjectId" "your-firebase-project-id"
```

### 2. Set up the Firestore emulator

The Firestore emulator allows local development without a live Google Cloud project.

```bash
# Install the emulator (one-time)
firebase init emulators

# Start the Firestore emulator
firebase emulators:start --only firestore
```

The emulator runs on `localhost:8080` by default. Configure your local app to point to it:

```bash
cd src/ZenoHR.Api
dotnet user-secrets set "Firestore:EmulatorHost" "localhost:8080"
dotnet user-secrets set "Firestore:ProjectId" "your-firebase-project-id"
```

### 3. Start the API

```bash
dotnet run --project src/ZenoHR.Api
```

The API starts on `https://localhost:5001` (HTTPS) and `http://localhost:5000` (HTTP) by default.

### 4. Verify the API is running

```bash
curl http://localhost:5000/health
# Should return 200 OK

curl http://localhost:5000/health/ready
# Should return 200 OK if Firestore is reachable
```

---

## Environment Variables

The following environment variables are used in production (Azure Container Apps). For local development, use .NET User Secrets instead.

| Variable | Description | Example |
|----------|-------------|---------|
| `Firebase:ProjectId` | Firebase project ID for JWT validation | `zenohr-prod-abc123` |
| `Firestore:ProjectId` | Google Cloud project for Firestore | `zenohr-prod-abc123` |
| `Firestore:EmulatorHost` | Firestore emulator (local only) | `localhost:8080` |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Azure Monitor telemetry export | `InstrumentationKey=...` |
| `Cors:AllowedOrigins` | Comma-separated allowed CORS origins | `https://zenohr.zenowethu.co.za` |
| `ASPNETCORE_ENVIRONMENT` | Runtime environment | `Development` or `Production` |

**Important**: Never commit real secrets to the repository. All production secrets are stored in Azure Key Vault.

---

## Project Structure Walkthrough

```
ZenoHR/
├── src/
│   ├── ZenoHR.Api/                    # ASP.NET Core host — endpoints, middleware, auth
│   │   ├── Endpoints/                 # Minimal API endpoint definitions
│   │   ├── Middleware/                # Correlation ID, exception handler, security headers
│   │   ├── Auth/                      # Firebase JWT validation, claims transformation, MFA
│   │   ├── Security/                  # Rate limiting, CORS configuration
│   │   ├── BackgroundServices/        # Scheduled jobs (analytics, reminders, archival)
│   │   └── Program.cs                 # Application entry point and middleware pipeline
│   │
│   ├── ZenoHR.Web/                    # Blazor Server UI — pages, components, layouts
│   ├── ZenoHR.Domain/                 # Shared kernel — MoneyZAR, TaxYear, Result<T>, enums
│   │   ├── Common/                    # Value objects: MoneyZAR, TaxYear, StatutoryRuleSet
│   │   └── Errors/                    # Result<T>, ZenoHrError, ZenoHrErrorCode enum
│   │
│   ├── ZenoHR.Infrastructure/         # Firestore repos, Firebase auth, PDF gen, filing
│   ├── ZenoHR.Module.Employee/        # Employee management bounded context
│   ├── ZenoHR.Module.TimeAttendance/  # Timesheets, clock entries
│   ├── ZenoHR.Module.Leave/           # Leave requests, balances, accruals
│   ├── ZenoHR.Module.Payroll/         # Payroll runs, PAYE/UIF/SDL/ETI calculations
│   ├── ZenoHR.Module.Compliance/      # SARS filings, BCEA checks, POPIA controls
│   ├── ZenoHR.Module.Audit/           # Audit trail, evidence packs, hash-chain
│   └── ZenoHR.Module.Risk/            # Risk scoring, dashboard insights
│
├── tests/
│   ├── ZenoHR.Domain.Tests/           # Unit tests for shared kernel
│   ├── ZenoHR.Module.Payroll.Tests/   # Property-based tests for payroll calculations
│   ├── ZenoHR.Module.Compliance.Tests/# Compliance module tests
│   ├── ZenoHR.Integration.Tests/      # Firestore emulator integration tests
│   └── ZenoHR.Architecture.Tests/     # ArchUnit-style boundary enforcement
│
├── docs/                              # All project documentation
│   ├── prd/                           # Product requirement documents (PRD-00 to PRD-18)
│   ├── seed-data/                     # Statutory configuration JSON files
│   ├── schemas/                       # Firestore schema, monetary precision rules
│   ├── design/                        # UI mockups, design tokens, brand assets
│   ├── security/                      # Vulnerability register, POPIA control status
│   └── developer/                     # Developer documentation (this folder)
│
├── scripts/                           # Build, test, and utility scripts
├── CLAUDE.md                          # Agent context file — project conventions and rules
└── .mcp/                              # MCP context server for AI agents
```

For a deeper understanding of the architecture, see [Architecture Guide](architecture-guide.md).

---

## Common Build Issues and Fixes

### File lock errors during build

**Symptom**: Build fails with "The process cannot access the file because it is being used by another process."

**Cause**: Windows Defender real-time scanning or zombie `VBCSCompiler.exe` / `dotnet.exe` processes holding file locks.

**Fix**:

```powershell
# Kill zombie compiler processes
Get-Process VBCSCompiler -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process dotnet -ErrorAction SilentlyContinue | Stop-Process -Force

# Clean and rebuild
dotnet clean
dotnet build
```

**Prevention**: Add the repository folder to Windows Defender exclusions:

```
Windows Security → Virus & threat protection → Manage settings →
Exclusions → Add exclusion → Folder → C:\Users\<you>\ZenoHR
```

### NuGet restore failures on .NET 10

**Symptom**: `error NU1102: Unable to find package X.Y.Z` or similar restore errors.

**Fix**: Ensure you have the latest .NET 10 SDK installed. Some packages require specific SDK patch versions:

```bash
dotnet --list-sdks
# If 10.0.x is not listed, download from https://dotnet.microsoft.com/download
```

### Port conflicts when running locally

**Symptom**: `System.IO.IOException: Failed to bind to address https://127.0.0.1:5001: address already in use.`

**Fix**: Kill the process using the port, or use a different port:

```bash
dotnet run --project src/ZenoHR.Api --urls "https://localhost:5011;http://localhost:5010"
```

### Firestore emulator not connecting

**Symptom**: Health check at `/health/ready` returns unhealthy.

**Fix**: Ensure the Firestore emulator is running and the `Firestore:EmulatorHost` user secret is set correctly:

```bash
# Verify emulator is running
firebase emulators:start --only firestore

# Verify user secret
cd src/ZenoHR.Api
dotnet user-secrets list
# Should show Firestore:EmulatorHost = localhost:8080
```

---

## Next Steps

- Read the [Architecture Guide](architecture-guide.md) to understand the modular monolith structure
- Read the [Coding Conventions](coding-conventions.md) for C# style and project rules
- Read the [API Reference](api-reference.md) for endpoint documentation
- Read the [Security Architecture](security-architecture.md) for auth and security patterns
- Read `CLAUDE.md` at the repository root for the full project context and critical rules
