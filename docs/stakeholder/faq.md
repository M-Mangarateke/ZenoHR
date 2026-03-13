---
doc_id: STAKE-04-FAQ
version: 1.0.0
updated_on: 2026-03-13
owner: Director / HRManager
classification: Internal
applies_to: [Zenowethu (Pty) Ltd, ZenoHR Platform]
---

# ZenoHR -- Frequently Asked Questions

**For**: Directors, HR Managers, Managers, Employees, and Technical Staff
**Date**: 13 March 2026

---

## Table of Contents

- [Non-Technical Questions](#non-technical-questions)
- [Technical Questions](#technical-questions)

---

## Non-Technical Questions

### Q: What is ZenoHR?

**A**: ZenoHR is a custom-built HR, payroll, and compliance platform designed specifically for Zenowethu (Pty) Ltd. It handles everything from calculating employee pay and managing leave to filing with SARS and ensuring compliance with South African labour law (BCEA) and privacy law (POPIA). It replaces manual spreadsheets and generic HR tools with a single, integrated system built for South African requirements.

---

### Q: Is our employee data safe?

**A**: Yes. ZenoHR employs multiple layers of security to protect employee data:

- **Encryption**: All data is encrypted in transit (HTTPS/TLS) and at rest (AES-256). Sensitive fields like national ID numbers, tax references, and bank account numbers receive additional application-level encryption.
- **Access control**: Five-tier role-based access ensures each person sees only what they should. Firestore database security rules enforce access at the database level, providing a second line of defence even if application code had a flaw.
- **Audit trail**: Every action is recorded in a tamper-evident SHA-256 hash chain. If anyone alters a historical record, the hash chain breaks and the system detects it immediately.
- **Authentication**: Firebase Authentication with multi-factor authentication (MFA) required for privileged operations like finalising payroll or approving SARS filings.
- **Rate limiting**: Automated protection against brute-force attacks and denial-of-service attempts.
- **Security headers**: Industry-standard HTTP security headers (CSP, HSTS, X-Frame-Options) protect against common web vulnerabilities.
- **228+ security tests**: Automated tests continuously verify that security controls are working.

---

### Q: Where is our data stored?

**A**: All employee data is stored in **Azure South Africa North** (Johannesburg data centre) and **Google Cloud Firestore** configured for the **africa-south1** (Johannesburg) region. Employee personal information never leaves South Africa. This is a deliberate architectural decision to comply with POPIA's data residency requirements.

The only data that may traverse international networks is authentication tokens (login sessions), which do not contain personal information such as salaries, national IDs, or banking details.

---

### Q: Can we file with SARS automatically?

**A**: Not yet for electronic submission. ZenoHR generates all required SARS filing formats -- IRP5, EMP201, EMP501, EMP601, EMP701 -- in the correct format. Currently, you download the generated file and upload it manually to the SARS eFiling portal.

Automatic electronic filing requires SARS ISV (Independent Software Vendor) accreditation, which involves an application and approval process with SARS. This has been deferred to year two -- the application needs to prove viable before investing in the accreditation process. Manual filing is fully functional in the interim and many companies use this approach.

---

### Q: What happens if there is a data breach?

**A**: ZenoHR has an automated breach response procedure:

1. **Detection**: Anomaly detection monitors for suspicious patterns (multiple failed logins, unusual data exports, off-hours access) and raises alerts automatically.
2. **Containment**: Affected accounts are secured, tokens revoked, and in-progress operations frozen if needed.
3. **Assessment**: The scope and impact of the breach is assessed within 24 hours.
4. **Notification**: If the breach is likely to cause harm, the Information Regulator of South Africa is notified within 72 hours as required by POPIA Section 22. Affected employees are also notified.
5. **Remediation**: Root cause is identified and fixed. The breach register records all actions taken.
6. **Review**: A post-incident review is conducted and documented.

The 72-hour deadline is tracked automatically by the system, and overdue notifications are flagged to the Director and HR Manager.

---

### Q: Who can see what data?

**A**: ZenoHR uses strict role-based access:

| Role | What They Can See |
|------|------------------|
| **Director** | Everything -- all employees, payroll, compliance, audit trail, settings |
| **HR Manager** | Everything -- identical to Director (primary day-to-day operator) |
| **Manager** | Their team only -- team member profiles (no salary/tax/banking), team leave requests, team timesheets |
| **Employee** | Their own data only -- own profile, own payslips, own leave balances, own analytics |
| **SaasAdmin** | Platform operations only -- cannot see any tenant employee data |

Menu items are completely hidden for roles without access -- not greyed out, not clickable, simply absent. Sensitive fields (national ID, tax reference, bank account) are masked by default and require a documented business purpose to unmask.

---

### Q: How accurate are the payroll calculations?

**A**: ZenoHR's payroll engine is verified against the official SARS 2025/2026 tax tables using hundreds of automated tests, including property-based tests that check edge cases with random inputs. The system uses:

- The **annual equivalent method** for PAYE, as specified by SARS
- **Correct rounding**: annual PAYE to the nearest rand, period PAYE to the nearest cent
- **MoneyZAR**: a purpose-built money type that uses `decimal` arithmetic (never floating-point), ensuring cent-accurate calculations with no rounding drift
- **Payslip invariant**: net pay = gross pay - PAYE - UIF employee - pension employee - medical employee - other deductions, verified to the cent on every calculation

The UIF ceiling (R17,712/month), SDL threshold (R500,000 annual payroll), and ETI age/remuneration bands are all loaded from configuration -- never hardcoded -- so they can be updated when SARS publishes new rates without changing any code.

---

### Q: Can employees access the system on mobile?

**A**: Yes. ZenoHR is fully responsive and works on mobile phones and tablets:

- **Mobile phones**: A bottom navigation bar provides quick access to the most relevant features for each role. Tables convert to card lists for easy reading on small screens.
- **Tablets**: A compact sidebar with icon-only navigation (expands on hover/tap) provides the full feature set.
- **No app required**: ZenoHR runs in your mobile web browser. No app store download needed -- just navigate to the ZenoHR URL.

All features available on desktop are also available on mobile. Nothing is desktop-only.

---

### Q: Is there a dark mode?

**A**: Yes. Click the sun/moon toggle icon in the top navigation bar to switch between light and dark themes. Your preference is saved automatically and persists across sessions and devices. The theme switches instantly with no page flash.

---

### Q: What if we need to add custom roles?

**A**: Directors and HR Managers can create custom Manager variants through the **Settings > Roles** page. Custom roles select from six permission tokens:

1. Leave approval
2. Timesheet approval
3. Employee team view
4. Team headcount view
5. Team leave calendar
6. Team profile access

For example, you could create a "Finance Manager" role with leave approval and team profile access, but not timesheet approval.

**Limitation**: Custom roles cannot grant payroll access, compliance access, audit trail access, or cross-department access. These are reserved for Director and HR Manager roles only. This is a security boundary that cannot be overridden.

---

### Q: How much does ZenoHR cost to run?

**A**: ZenoHR has no per-user or per-employee licence fees. Costs are limited to cloud infrastructure:

- **Azure Container Apps**: Consumption-based pricing (pay for what you use)
- **Google Cloud Firestore**: Per-operation pricing (scales with database reads/writes)
- **Firebase Authentication**: Free tier covers up to 50,000 monthly active users
- **Azure Key Vault, Monitor**: Nominal per-operation costs

Compared to commercial HR software that typically charges R50--R200 per employee per month, ZenoHR's infrastructure costs are significantly lower and do not scale linearly with headcount. Zenowethu owns the entire codebase with no vendor lock-in.

---

### Q: What reports can ZenoHR generate?

**A**: ZenoHR generates a variety of reports:

- **Payslips**: Branded A4 PDF with all BCEA Section 33 mandatory fields
- **Tax certificates**: IRP5/IT3(a) for each employee
- **SARS filings**: EMP201, EMP501, EMP601, EMP701 in SARS-required format
- **Evidence packs**: PDF bundles of audit trail and compliance records for inspections
- **Analytics**: Headcount trends, payroll costs, salary distribution, leave heatmaps
- **Compliance reports**: SARS, BCEA, and POPIA compliance scoring

---

### Q: What happens to data when an employee leaves the company?

**A**: When an employee is terminated:

1. Their status is changed to "Terminated" -- the record is not deleted
2. Their system access is revoked immediately
3. Their data enters the retention lifecycle:
   - Payroll records: retained for minimum 3 years (BCEA) up to 7 years (SARS)
   - Employment records: retained for 3 years after termination
   - Audit trail: retained for 7 years
4. After the retention period, data is anonymised or securely destroyed with a record in the audit trail
5. BCEA Section 31 requires payroll records to be kept for 3 years -- ZenoHR enforces this automatically

---

### Q: Can we use ZenoHR for multiple companies?

**A**: ZenoHR is built with multi-tenant architecture. Each company (tenant) has completely isolated data -- one company cannot see another company's information. Currently, ZenoHR is configured for Zenowethu (Pty) Ltd. The architecture supports additional tenants if needed in the future.

---

## Technical Questions

### Q: What tech stack is ZenoHR built on?

**A**:

| Layer | Technology |
|-------|-----------|
| Runtime | .NET 10 (Long-Term Support) |
| Backend | ASP.NET Core Web API |
| Frontend | Blazor Server (server-side rendering with real-time SignalR) |
| Database | Google Cloud Firestore |
| Authentication | Firebase Authentication (OIDC/JWT with MFA) |
| Hosting | Azure Container Apps (South Africa North) |
| PDF generation | QuestPDF |
| In-process events | MediatR |
| Background jobs | .NET BackgroundService + Azure scheduled jobs |
| CI/CD | GitHub Actions |
| Observability | OpenTelemetry SDK + Azure Monitor (Application Insights) |
| Secrets | Azure Key Vault |
| Icons | Lucide Icons (20px, 1.5px stroke) |
| Fonts | Inter (UI) + JetBrains Mono (monetary values) |

---

### Q: How is the codebase structured?

**A**: ZenoHR follows a **modular monolith** architecture with 7 bounded contexts:

| Module | Responsibility |
|--------|---------------|
| `ZenoHR.Module.Employee` | Employee records, contracts, onboarding |
| `ZenoHR.Module.Payroll` | Payroll runs, calculations, payslips |
| `ZenoHR.Module.Leave` | Leave requests, balances, accruals |
| `ZenoHR.Module.TimeAttendance` | Timesheets, clock entries |
| `ZenoHR.Module.Compliance` | SARS filings, BCEA checks, POPIA controls |
| `ZenoHR.Module.Audit` | Audit trail, evidence packs, hash chain |
| `ZenoHR.Module.Risk` | Risk scoring, dashboard insights |

Each module is independent and communicates with other modules only through MediatR domain events or shared kernel types (`ZenoHR.Domain`). No module directly reads another module's database collections. Architecture boundary tests (`ZenoHR.Architecture.Tests`) enforce this automatically.

The shared kernel (`ZenoHR.Domain`) contains common types: `MoneyZAR` (money value object), `TaxYear`, enums, and base types used across modules.

---

### Q: How are payroll calculations validated?

**A**: Payroll calculations are validated at multiple levels:

1. **Unit tests**: Standard xUnit tests verify individual calculations (PAYE for specific salary amounts, UIF at ceiling, ETI for qualifying employees).
2. **Property-based tests**: FsCheck generates random salary inputs and verifies that invariants hold:
   - Net pay always equals gross minus deductions (to the cent)
   - PAYE is never negative
   - UIF never exceeds the ceiling
   - ETI only applies to qualifying employees
3. **Contract tests**: Verify that the annual equivalent method produces correct results against SARS published examples.
4. **Integration tests**: End-to-end payroll runs through the Firestore emulator verify the complete pipeline.
5. **Payslip invariant**: Every payslip is checked: `net_pay == gross_pay - paye - uif_employee - pension_employee - medical_employee - other_deductions` to the cent.

The `MoneyZAR` value object uses `System.Decimal` (not `float` or `double`) to prevent floating-point rounding errors. Using `float` or `double` for money is classified as a Sev-1 defect.

---

### Q: How does the audit trail work?

**A**: The audit trail uses a **SHA-256 hash chain**:

1. Each `AuditEvent` contains a `previous_event_hash` field -- the SHA-256 hash of the previous event's canonical JSON.
2. When a new event is written, the system computes the hash of the previous event and includes it in the new event.
3. This creates a linked chain: Event N references Event N-1, which references Event N-2, and so on.
4. If anyone modifies a historical event, its hash changes, which means the next event's `previous_event_hash` no longer matches -- the chain is broken.
5. The system can verify chain integrity by re-computing hashes from the beginning.

**Result**: The audit trail is tamper-evident. Any modification to historical records is immediately detectable. Combined with Firestore security rules that prevent updates and deletes on finalised records, the audit trail is effectively immutable.

Audit events are scoped by `tenant_id` -- each tenant has its own independent hash chain.

---

### Q: How is tenant isolation enforced?

**A**: Tenant isolation is enforced at three levels:

1. **Application level**: Every Firestore document has a `tenant_id` field. All queries include a `tenant_id` filter. The `BaseFirestoreRepository` validates `tenant_id` on every read operation.
2. **Database level**: Firestore security rules enforce that users can only read/write documents where `tenant_id` matches their authenticated tenant claim. Even if application code had a bug that omitted the filter, the database would reject the query.
3. **API level**: JWT tokens include the user's `tenant_id` claim. The API extracts this from the token and passes it to all repository calls. There is no mechanism for a user to query data with a different `tenant_id`.

Cross-tenant data access is classified as a **Sev-1 security vulnerability**. Architecture tests verify that all repository methods accept and enforce `tenant_id`.

---

### Q: What CI/CD pipeline is used?

**A**: ZenoHR uses **GitHub Actions** with the following pipeline:

1. **Build**: Compile the .NET 10 solution
2. **Test**: Run all unit tests, property-based tests, and architecture tests
3. **Coverage**: Enforce minimum coverage (90% line for domain, 85% branch)
4. **SAST**: Static analysis security testing
5. **Trivy scan**: Container image vulnerability scanning (fail on HIGH/CRITICAL CVEs)
6. **Documentation gate**: Verify traceability comments, PRD freshness, security doc presence
7. **Deploy**: Push to Azure Container Apps (SA North) on successful pipeline

Every pull request must pass all gates before merging. Security-critical code changes require additional review.

---

### Q: How do I add a new feature?

**A**: Follow the **vertical slice** pattern:

1. Identify the requirement (`REQ-*`) from the PRD documentation
2. Create the domain model in the appropriate module (e.g., `ZenoHR.Module.Payroll`)
3. Create the Firestore repository in `ZenoHR.Infrastructure`
4. Create the API endpoint in `ZenoHR.Api`
5. Create the Blazor component in `ZenoHR.Web`
6. Add traceability comments (`// REQ-XX-000`) to every class and public method
7. Write tests: unit tests, integration tests (Firestore emulator), and architecture boundary tests
8. Ensure the feature follows the CDD 4-persona check:
   - **Architect**: Simplest approach? No premature abstractions?
   - **Reviewer**: Readable in 2 minutes by a new developer?
   - **Designer**: Follows mockups? Accessible?
   - **Security Engineer**: `tenant_id` enforced? `MoneyZAR` used? No hardcoded secrets?

Refer to `docs/prd/17_blazor_component_patterns.md` for Blazor-specific patterns.

---

### Q: How is POPIA compliance maintained?

**A**: POPIA compliance is maintained through 15 controls tracked in `docs/security/popia-control-status.md`:

| Control | Description | Status |
|---------|------------|--------|
| CTL-POPIA-001 | Lawful processing basis | Partial |
| CTL-POPIA-002 | Purpose limitation (unmask validation) | Partial |
| CTL-POPIA-003 | Tenant data isolation | Implemented |
| CTL-POPIA-004 | Information quality (input validation) | Partial |
| CTL-POPIA-005 | Data subject notice versioning | Documented |
| CTL-POPIA-006 | Security safeguards (auth + encryption) | Partial |
| CTL-POPIA-007 | Access reviews | Documented |
| CTL-POPIA-008 | Breach detection and anomaly monitoring | Implemented |
| CTL-POPIA-009 | Data subject access requests | Implemented |
| CTL-POPIA-010 | Correction of personal information | Implemented |
| CTL-POPIA-011 | Breach register and notification | Partial |
| CTL-POPIA-012 | Compromise response and containment | Implemented |
| CTL-POPIA-013 | Cross-border transfer governance | Partial |
| CTL-POPIA-014 | Data processor inventory | Partial |
| CTL-POPIA-015 | Employee data retention policy | Implemented |

Each control maps to specific POPIA Act sections and is backed by code, tests, and evidence files. The compliance dashboard in ZenoHR shows real-time control status. 228+ automated security tests verify that controls remain effective.

---

### Q: What is MoneyZAR and why does it matter?

**A**: `MoneyZAR` is a custom value object in `ZenoHR.Domain` that wraps `System.Decimal` for all monetary calculations. It exists because:

- **`float` and `double` lose precision**: `0.1 + 0.2 = 0.30000000000000004` in floating-point arithmetic. This is unacceptable for payroll.
- **`decimal` is exact**: `0.1m + 0.2m = 0.3m` in decimal arithmetic. No rounding drift.
- **Firestore storage**: Monetary values are stored as **strings** in Firestore to preserve precision across serialisation boundaries. `MoneyZAR` handles the conversion.
- **Rounding rules**: Annual PAYE is rounded to the nearest rand (MidpointRounding.AwayFromZero). Period PAYE is rounded to the nearest cent. `MoneyZAR` encapsulates these rules.

Using `float` or `double` for money anywhere in ZenoHR is classified as a **Sev-1 defect** and will fail code review.

---

### Q: How do statutory rates get updated when SARS publishes new tables?

**A**: All statutory rates (PAYE brackets, rebates, medical credits, UIF ceiling, SDL threshold, ETI bands) are stored as `StatutoryRuleSet` documents in Firestore, seeded from JSON files in `docs/seed-data/`:

- `sars-paye-2025-2026.json` -- PAYE tax tables
- `sars-uif-sdl.json` -- UIF and SDL rates
- `sars-eti.json` -- ETI qualification and calculation rules
- `bcea-leave.json` -- Leave entitlements
- `bcea-working-time.json` -- Working time limits

When SARS publishes new rates for the next tax year:
1. Create a new JSON seed file with the updated rates
2. Seed it to Firestore as a new `StatutoryRuleSet` with the new tax year effective dates
3. The payroll engine automatically uses the correct rule set based on the payroll period date
4. No code changes required -- rates are configuration, not code

**Critical rule**: Statutory values are never hardcoded in application code. This is a non-negotiable architectural constraint.

---

### Q: What monitoring and alerting is in place?

**A**: ZenoHR uses OpenTelemetry with Azure Monitor (Application Insights):

- **Custom metrics**: 4 application-specific metrics tracked
- **Health checks**: `/health` endpoint for automated liveness monitoring
- **Log redaction**: PII is automatically stripped from all telemetry (national IDs, tax references, bank accounts) by the `LogRedactionProcessor`
- **Anomaly detection**: Background service monitors for suspicious access patterns
- **Alerting**: Configured through Azure Monitor alert rules

All telemetry respects POPIA -- no personal information appears in logs or monitoring dashboards.

---

### Q: How do I run the project locally?

**A**: Prerequisites:
- .NET 10 SDK
- Node.js (for Firebase emulator)
- Firebase CLI (`npm install -g firebase-tools`)
- Google Cloud Firestore emulator

Steps:
1. Clone the repository
2. Run `dotnet restore` to install NuGet packages
3. Start the Firestore emulator for local development
4. Run `dotnet run --project src/ZenoHR.Api` to start the API
5. Run tests: `powershell -Command "& 'scripts/run-tests.ps1'"`
6. Build only: `powershell -Command "& 'scripts/build2.ps1'"`

---

*For additional questions, contact the development team at admin@zenowethu.co.za or refer to the full PRD documentation in `docs/prd/`.*
