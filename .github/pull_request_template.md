## Summary
<!-- Brief description of changes — what problem does this PR solve? -->

## Changes
<!-- Bulleted list of what was changed, added, or removed -->
-

## Type of Change
- [ ] Bug fix
- [ ] New feature
- [ ] Security fix
- [ ] Refactoring
- [ ] Documentation

## Traceability
<!-- REQ-OPS-008: Every change must trace to a requirement. Orphan code is not acceptable. -->
Requirements addressed: REQ-

<!-- List all requirement IDs, control IDs, or test case IDs this PR addresses: -->
<!-- e.g. REQ-HR-003, CTL-SARS-001, TC-PAY-012 -->

## Testing
- [ ] Unit tests added/updated (naming: `MethodName_Scenario_ExpectedResult`)
- [ ] Integration tests added/updated (if Firestore collections are touched)
- [ ] FsCheck property-based tests added (if payroll calculation logic is changed)
- [ ] Traceability comments (`// REQ-*` or `// CTL-*`) added to all new code
- [ ] `dotnet test` passes locally with 0 failures

## Security Checklist
<!-- REQ-SEC-010: All PRs must pass this checklist before review -->
- [ ] No secrets, API keys, or connection strings committed
- [ ] `tenant_id` enforced on all new Firestore queries
- [ ] `MoneyZAR` used for all monetary values (never `float` or `double`)
- [ ] `CancellationToken` propagated in all new async methods
- [ ] `[RequireMfa]` applied to any new privileged endpoints (payroll finalize, SARS approve)
- [ ] New API endpoints checked against VUL-001 (CSP/HSTS headers) and VUL-002 (CORS policy)

## Breaking Changes
<!-- Does this PR change any public API contracts, Firestore document shapes, or enum values? -->
- [ ] No breaking changes
- [ ] Yes — describe impact and migration path below:
