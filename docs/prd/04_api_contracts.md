---
doc_id: PRD-04-API
version: 1.0.0
owner: API Lead
updated_on: 2026-02-18
applies_to:
  - External and internal API contracts
  - Event schemas and validation
depends_on:
  - PRD-02-DOMAIN
  - PRD-03-ARCH
requirements:
  - REQ-HR-003
  - REQ-COMP-003
  - REQ-COMP-005
  - REQ-SEC-006
---

# API and Contract Specification

## API Standards
1. Protocol: HTTPS only.
2. Payload: JSON UTF-8.
3. API versioning: URI version (`/api/v1/...`) + schema version in payload metadata.
4. Idempotency required for create/finalization endpoints using `Idempotency-Key` header.
5. All responses include `trace_id` and `timestamp_utc`.

## Common Error Model
```json
{
  "error_code": "COMPLIANCE_BLOCK",
  "message": "BCEA ordinary hours exceeded",
  "details": [
    {
      "control_id": "CTL-BCEA-001",
      "field": "timesheet.total_ordinary_hours",
      "rule_version": "BCEA-WORKTIME-2026.1"
    }
  ],
  "trace_id": "01H...",
  "timestamp_utc": "2026-02-18T12:00:00Z"
}
```

## Authentication and Authorization Contract
- Auth: OIDC/JWT bearer tokens.
- Required claims: `sub`, `role`, `tenant_id`, `session_mfa`, `scope`.
- Privileged operations require `session_mfa=true`.

## Public API Contracts

### 1. Payroll Calculation Interface
Endpoint:
- `POST /api/v1/payroll/runs`

Request:
```json
{
  "period": "2026-02",
  "run_type": "monthly",
  "employee_scope": "all",
  "rule_set_version": "STAT-RULES-2026.1",
  "tax_table_version": "SARS-TAX-2026.1",
  "initiated_by": "user-123"
}
```

Response:
```json
{
  "payroll_run_id": "pr_2026_02_001",
  "status": "Calculated",
  "employee_count": 312,
  "summary": {
    "gross_total": 8123456.90,
    "deduction_total": 2145678.22,
    "net_total": 5977778.68
  },
  "rule_version_checksum": "sha256:...",
  "trace_id": "..."
}
```

Validation rules:
1. Active `rule_set_version` and `tax_table_version` must exist and be signed.
2. Period must be open and not already finalized.
3. Employee records must include mandatory tax and bank fields.

### 2. Payroll Finalization
Endpoint:
- `POST /api/v1/payroll/runs/{payroll_run_id}/finalize`

Behavior:
1. Executes final compliance checks.
2. Requires dual authorization (`PayrollOfficer` + `ComplianceOfficer`).
3. Creates immutable finalized records and audit events.

### 3. Compliance Reporting Interface
Endpoints:
- `POST /api/v1/compliance/submissions/emp201`
- `POST /api/v1/compliance/submissions/emp501`
- `POST /api/v1/compliance/submissions/irp5-it3a`

Output contracts include:
- package identifier
- period
- validation outcome
- artifact references
- submission readiness status

Blocking conditions:
- Missing or invalid tax reference (`REQ-COMP-006`)
- Reconciliation mismatch above tolerance
- Incomplete employee certificate fields

### 4. Time and Attendance Interface
Endpoints:
- `POST /api/v1/time/entries`
- `POST /api/v1/time/weeks/{week_id}/approve`
- `GET /api/v1/time/weeks/{week_id}`

Rules:
1. Weekly ordinary-hour checks enforced before approval.
2. Overtime rules enforced at approval and payroll stages.
3. Approved week is immutable.

### 5. Leave Management Interface
Endpoints:
- `POST /api/v1/leave/requests`
- `POST /api/v1/leave/requests/{id}/approve`
- `POST /api/v1/leave/requests/{id}/reject`
- `GET /api/v1/leave/balances/{employee_id}`

Rules:
1. Leave balance and entitlement checks are mandatory.
2. Leave approvals write ledger records atomically.

### 6. Tax Table Provider Interface
Endpoints:
- `POST /api/v1/rules/tax-tables`
- `POST /api/v1/rules/tax-tables/{version}/activate`
- `GET /api/v1/rules/tax-tables/active?date=YYYY-MM-DD`

Schema fields:
- `version`
- `effective_from`
- `effective_to`
- `source_url`
- `source_published_date`
- `checksum`
- `signature`

Activation guardrails:
1. Signed artifact required.
2. Regression test pack must pass before activation.
3. Activation event emits cache invalidation events.

### 7. Audit Trail Schema Interface
Event schema:
```json
{
  "event_id": "aud_...",
  "actor_id": "user-123",
  "action": "PayrollFinalized",
  "entity_ref": "payroll_run:pr_2026_02_001",
  "before_hash": "sha256:...",
  "after_hash": "sha256:...",
  "timestamp_utc": "2026-02-18T12:00:00Z",
  "trace_id": "...",
  "schema_version": "1.0"
}
```

### 8. Risk Dashboard Interface
Endpoints:
- `GET /api/v1/risk/dashboard`
- `POST /api/v1/risk/simulate-fines`

Response includes:
- prioritized risk items
- legal due date
- control_id
- projected exposure range
- recommended action and owner

### 9. Termination Notice and Severance Interface
Endpoints:
- `POST /api/v1/terminations/calculate`
- `POST /api/v1/terminations/{termination_case_id}/approve`

Request fields:
- `employee_id`
- `effective_termination_date`
- `termination_reason_code`
- `policy_version`
- `rule_set_version`

Response fields:
- `notice_period_days`
- `notice_pay_zar`
- `severance_pay_zar`
- `compliance_status`
- `control_results` (includes `CTL-BCEA-006`)

Blocking conditions:
1. Missing policy/rule version for effective date.
2. Failed notice/severance compliance validation.
3. Missing required approval for settlement finalization.

## Event Contracts
Internal event bus topics:
- `payroll.calculated.v1`
- `payroll.finalized.v1`
- `compliance.package.generated.v1`
- `rules.tax_table.activated.v1`
- `security.compromise.reported.v1`

Each event payload includes:
- `event_id`
- `occurred_at_utc`
- `producer`
- `correlation_id`
- `schema_version`

## Idempotency Policy
1. Required for all POST operations that create state.
2. Duplicate idempotency key with same body returns prior result.
3. Duplicate key with different body returns conflict error.

## Backward Compatibility Policy
1. Additive changes allowed in minor versions.
2. Breaking changes require new major endpoint version.
3. Deprecated versions must provide minimum 2 release cycles notice.

## MCP Document Metadata Type
All markdown docs in this pack must expose:
```yaml
doc_id: string
version: semver
owner: string
updated_on: YYYY-MM-DD
applies_to: string[]
depends_on: string[]
requirements: string[]
```

## API Security Controls
1. Sensitive fields redacted in logs.
2. Schema validation required at ingress.
3. Replay protection on privileged endpoints.
4. Rate limiting and anomaly detection for abuse-prone endpoints.
