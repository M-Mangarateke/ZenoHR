---
doc_id: DEV-SECURITY-ARCHITECTURE
version: 1.0.0
updated_on: 2026-03-13
---

# Security Architecture

This document describes the security architecture of ZenoHR for developers implementing features that touch authentication, authorization, data protection, or compliance controls.

---

## Authentication Flow

ZenoHR uses **Firebase Authentication** as the identity provider. The authentication flow is:

```
1. User signs in via Firebase (email/password + MFA)
2. Firebase issues a JWT (ID token)
3. Client includes JWT in Authorization header: "Bearer <token>"
4. ASP.NET Core middleware validates the JWT against Firebase's public keys
5. Custom claims transformation enriches ClaimsPrincipal with ZenoHR claims
6. Endpoint handlers read claims from the ClaimsPrincipal
```

### Firebase JWT Validation

Configured in `src/ZenoHR.Api/Auth/FirebaseAuthExtensions.cs`. The middleware validates:

- **Issuer**: `https://securetoken.google.com/{projectId}`
- **Audience**: Firebase project ID
- **Signature**: Against Google's rotating public keys
- **Expiry**: Token must not be expired

### Claims Transformation

After Firebase JWT validation, `ZenoHrClaimsTransformation` (in `src/ZenoHR.Api/Auth/ZenoHrClaimsTransformation.cs`) maps Firebase custom claims to ZenoHR-specific claims:

| ZenoHR Claim | Source | Purpose |
|--------------|--------|---------|
| `tenant_id` | Firebase custom claim | Tenant isolation for every query |
| `system_role` | Firebase custom claim | RBAC role (Director, HRManager, Manager, Employee) |
| `employee_id` | Firebase custom claim | Self-access guarantee |
| `dept_id` | Firebase custom claim | Manager department scoping |
| `mfa_verified` | Firebase custom claim | MFA enforcement for privileged operations |

Claim name constants are in `src/ZenoHR.Api/Auth/ZenoHrClaimNames.cs`.

---

## Authorization: Role-Based Access Control

ZenoHR has 5 system roles defined in `docs/prd/15_rbac_screen_access.md`:

| Role | Scope | Description |
|------|-------|-------------|
| `SaasAdmin` | Platform (cross-tenant) | Platform operations only. Cannot read tenant data. |
| `Director` | Full tenant | All access. Can manage roles, departments, users. |
| `HRManager` | Full tenant | Identical to Director. Day-to-day HR operator. |
| `Manager` | Own department | Team leave/timesheet approval. No payroll/compliance/audit. |
| `Employee` | Own records | Own profile, payslips, leave requests. |

### Role Enforcement in Endpoints

Roles are enforced at the endpoint level using `RequireAuthorization()`:

```csharp
// Director/HRManager only
group.MapPost("/", CreateEmployeeAsync)
    .RequireAuthorization(policy => policy.RequireRole("Director", "HRManager"));

// Any authenticated user (role checked in handler)
group.MapGet("/", ListEmployeesAsync)
    .RequireAuthorization();
```

### Department Scoping

For Manager role, queries are automatically scoped to the manager's department(s). The `DepartmentScopeFilter` (in `src/ZenoHR.Api/Auth/DepartmentScopeFilter.cs`) and `DepartmentScopeMiddleware` enforce this:

```csharp
// Manager sees only their department's employees
if (systemRole == "Manager")
{
    var deptId = user.FindFirstValue(ZenoHrClaimNames.DeptId) ?? "";
    employees = await repo.ListByDepartmentAsync(tenantId, deptId, ct);
}
```

### Self-Access Guarantee

Every authenticated tenant user always has read access to:
- Their own employee document
- Their own payslips
- Their own leave requests/balances

This is enforced server-side by comparing `employee_id` from the JWT with the requested resource:

```csharp
// Employee self-access: always allowed regardless of role
var ownEmpId = user.FindFirstValue(ZenoHrClaimNames.EmployeeId);
if (id != ownEmpId && systemRole is not ("Director" or "HRManager" or "Manager"))
    return Results.Forbid();
```

---

## Middleware Pipeline Order

The middleware pipeline in `src/ZenoHR.Api/Program.cs` is ordered deliberately. Each piece must be in the correct position for security guarantees to hold:

```
 #  Middleware                      Purpose                                    Ref
── ──────────────────────────────── ────────────────────────────────────────── ────────
 1  CorrelationId                   Assign X-Correlation-Id for tracing        REQ-OPS-008
 2  GlobalExceptionHandler          Outermost exception catch → ProblemDetails REQ-OPS-008
 3  SecurityHeaders                 CSP, X-Frame-Options, nosniff, Referrer    REQ-SEC-003
 4  HSTS (production only)          HTTP Strict Transport Security, 1yr        VUL-023
 5  HttpsRedirection                Redirect HTTP → HTTPS                      —
 6  CORS                            Cross-Origin Resource Sharing              VUL-002
 7  Authentication                  Firebase JWT validation                    TASK-024
 8  Authorization                   Role-based policy enforcement              TASK-025
 9  RateLimiter                     Per-tenant request rate limiting            VUL-007
10  SessionTimeout                  Idle session timeout enforcement            VUL-013
11  DepartmentScopeEnforcement      Manager department scoping                 VUL-008
```

**Why this order matters**:
- Security headers (3) are set before any request processing, so even error responses have headers
- CORS (6) is before Authentication (7) so preflight requests work
- Authentication (7) is before Authorization (8) because you must know who the user is before checking permissions
- RateLimiter (9) is after Authorization (8) so rate limits are per-tenant (using tenant_id from JWT)
- DepartmentScope (11) is last because it needs all auth context to be resolved

---

## MFA Enforcement

Privileged operations require Multi-Factor Authentication. The `RequireMfaAttribute` (in `src/ZenoHR.Api/Auth/RequireMfaAttribute.cs`) checks the `mfa_verified` claim in the JWT.

### MFA-Protected Endpoints

| Endpoint | Reason |
|----------|--------|
| `PUT /api/payroll/runs/{id}/finalize` | Finalizing payroll is irreversible |
| `PUT /api/payroll/runs/{id}/file` | Filing with SARS is irreversible |

```csharp
group.MapPut("/runs/{id}/finalize", FinalizeRunAsync)
    .RequireAuthorization(ZenoHrPolicies.RequiresMfa);
```

This remediates **VUL-003**: MFA not enforced on privileged operations.

---

## Session Management

Session timeout is enforced by `SessionTimeoutMiddleware` (in `src/ZenoHR.Api/Auth/SessionTimeoutMiddleware.cs`) with `SessionActivityTracker` tracking last activity time.

| Session Type | Idle Timeout |
|-------------|-------------|
| Privileged (payroll, compliance, admin) | 15 minutes |
| Standard (all other operations) | 60 minutes |

This remediates **VUL-013**: No idle session timeout.

The `SessionPolicy` class (in `src/ZenoHR.Api/Auth/SessionPolicy.cs`) defines timeout durations.

---

## Data Protection

### Field-Level Encryption

Sensitive PII fields (national ID, tax reference, bank account) are stored encrypted in Firestore using AES-256 encryption. Decryption requires an explicit unmask request with a POPIA-approved purpose code.

### Firestore Managed Encryption

All Firestore data is encrypted at rest using Google-managed encryption keys. Data in transit uses TLS 1.2+.

### PII Masking

By default, PII fields are returned masked in API responses (e.g., `***5009087`). Full values are only returned through the dedicated unmask endpoint (`POST /api/employees/{id}/unmask`).

The unmask endpoint (in `src/ZenoHR.Api/Endpoints/UnmaskEndpoints.cs`) enforces:
- Director/HRManager role required
- Approved POPIA purpose code required
- Justification text required for `AUDIT_REVIEW` and `HR_INVESTIGATION` purposes
- Every unmask is recorded in the audit trail

---

## Audit Trail

ZenoHR maintains a **hash-chained, immutable audit trail** for all sensitive operations.

### Hash Chain

Every `AuditEvent` includes `previous_event_hash` — the SHA-256 hash of the prior event's canonical JSON. This creates a tamper-evident chain. Breaking the chain is classified as a **Sev-1 defect**.

```
Event N-1: { ..., hash: "abc123" }
Event N:   { ..., previous_event_hash: "abc123", hash: "def456" }
Event N+1: { ..., previous_event_hash: "def456", hash: "ghi789" }
```

### Audited Operations

All of the following are recorded in the audit trail:
- Employee creation, update, termination
- Payroll run creation, finalization, filing
- Leave request submission, approval, rejection
- PII unmask operations (with purpose code)
- Statutory rule set updates
- Role and permission changes

### Immutability

Audit events are write-once. No updates, no deletes. This is enforced by:
- Domain model: `AuditEvent` has no update methods
- Firestore security rules: deny update/delete on `audit_events` collection
- Error code `AuditEventImmutable` (6002) if attempted programmatically

---

## Rate Limiting

Three-tier rate limiting protects against DoS and brute-force attacks. Configured in `src/ZenoHR.Api/Security/RateLimitingExtensions.cs`.

| Policy | Limit | Window Type | Partition Key | Purpose |
|--------|-------|------------|---------------|---------|
| `general-api` | 100/min | Sliding (6 segments) | `tenant_id` or IP | General API protection |
| `auth-endpoints` | 10/5min | Fixed | IP address | Brute-force protection |
| `payroll-ops` | 20/min | Fixed | `tenant_id` or IP | Protect computation-heavy operations |

When exceeded, the API returns:

```json
HTTP/1.1 429 Too Many Requests
Retry-After: 60

{
    "status": 429,
    "title": "Too Many Requests",
    "detail": "Rate limit exceeded. Please retry after 60 seconds."
}
```

This remediates **VUL-007**: No rate limiting on API endpoints.

---

## CORS Policy

CORS is configured in `Program.cs` using `AddZenoHrCors()`. The policy:

- **Allowed origins**: Read from `Cors:AllowedOrigins` configuration (production domain only)
- **Allowed methods**: GET, POST, PUT, DELETE
- **Allowed headers**: `Authorization`, `Content-Type`, `X-Correlation-Id`
- **Credentials**: Allowed (for cookie-based auth fallback)

This remediates **VUL-002**: No CORS policy defined.

---

## Security Headers

The `UseZenoHrSecurityHeaders()` middleware (in `src/ZenoHR.Api/Middleware/SecurityHeadersExtensions.cs`) sets the following headers on every response:

| Header | Value | Purpose |
|--------|-------|---------|
| `Content-Security-Policy` | Restrictive CSP | Prevent XSS, restrict resource loading |
| `X-Frame-Options` | `DENY` | Prevent clickjacking |
| `X-Content-Type-Options` | `nosniff` | Prevent MIME type sniffing |
| `Referrer-Policy` | `strict-origin-when-cross-origin` | Limit referrer leakage |
| `Permissions-Policy` | Restrictive | Disable unused browser features |
| `Strict-Transport-Security` | `max-age=31536000; includeSubDomains` | Enforce HTTPS (production only) |

This remediates **VUL-001**: No security HTTP headers and **VUL-023**: Missing HSTS.

---

## Tenant Isolation

Tenant isolation is enforced at multiple layers:

### 1. JWT Claims

The `tenant_id` is always extracted from the JWT, never from the request body. An attacker cannot forge a different tenant_id without compromising the Firebase authentication system.

```csharp
// ALWAYS from JWT
var tenantId = user.FindFirstValue(ZenoHrClaimNames.TenantId)!;
```

### 2. Repository Queries

Every Firestore query filters by `tenant_id`:

```csharp
var employees = await repo.ListByTenantAsync(tenantId, ct);
```

### 3. Firestore Security Rules

Firestore security rules enforce that documents can only be read/written by users whose JWT `tenant_id` matches the document's `tenant_id` field.

### 4. Architecture Tests

The `ZenoHR.Architecture.Tests` project verifies that no repository method exists without a `tenantId` parameter.

---

## Known Open Vulnerabilities

The following Sev-1 vulnerabilities are tracked in `docs/security/vulnerability-register.md` and must be resolved before v1.0 release:

| ID | Finding | Status |
|----|---------|--------|
| VUL-004 | No incident response system | Open |
| VUL-005 | No POPIA breach register or notification workflow | Open |
| VUL-006 | No break-glass emergency access procedure | Open |

Previously resolved:

| ID | Finding | Resolution |
|----|---------|------------|
| VUL-001 | No security HTTP headers | `SecurityHeadersExtensions` middleware |
| VUL-002 | No CORS policy | `AddZenoHrCors()` configuration |
| VUL-003 | MFA not enforced on privileged operations | `RequireMfaAttribute` |
| VUL-007 | No rate limiting | `RateLimitingExtensions` |
| VUL-008 | Manager department scoping missing | `DepartmentScopeMiddleware` |
| VUL-013 | No idle session timeout | `SessionTimeoutMiddleware` |
| VUL-020 | PII unmask without purpose code | `UnmaskEndpoints` with POPIA enforcement |
| VUL-023 | Missing HSTS | HSTS middleware in pipeline |

---

## Security Checklist for New Features

When implementing any new feature, verify the following:

- [ ] `tenant_id` is extracted from JWT, never from request body
- [ ] `MoneyZAR` is used for all monetary values (no `float`/`double`)
- [ ] `CancellationToken` is propagated through the entire call chain
- [ ] No hardcoded secrets, API keys, or connection strings
- [ ] Rate limiting policy is applied to the endpoint group
- [ ] Role-based access is enforced (either at endpoint or in handler)
- [ ] Self-access guarantee is preserved (employees can view their own data)
- [ ] Sensitive operations are audited via the audit trail
- [ ] PII fields are masked by default in API responses
- [ ] Traceability comment present (`REQ-*`, `CTL-*`, or `VUL-*`)
