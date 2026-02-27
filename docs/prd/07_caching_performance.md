---
doc_id: PRD-07-CACHE
version: 1.0.0
owner: Performance Engineer
updated_on: 2026-02-18
applies_to:
  - Cache architecture and performance engineering
depends_on:
  - PRD-03-ARCH
  - PRD-04-API
requirements:
  - REQ-OPS-003
  - REQ-OPS-004
  - REQ-OPS-013
---

# Caching and Performance Specification

## Goals
1. Improve response time for read-heavy workflows without compromising legal correctness.
2. Prevent stale legal/tax data from influencing payroll or submission outputs.
3. Provide measurable performance budgets and verifiable cache behavior.

## Cache Classes
| Cache Class | Data Type | Use Cases | Source of Truth | Allowed Staleness |
|---|---|---|---|---|
| `CACHE-REF-TAX` | Statutory tax/rule reference data | Payroll previews, validations | `statutory_rule_sets` | Max 5 minutes for read paths; 0 for finalization |
| `CACHE-REF-HR` | Low-volatility reference data | Role catalogs, leave policy metadata | Firestore reference collections | Max 15 minutes |
| `CACHE-DERIVED-PREVIEW` | Derived payroll preview aggregates | Payroll draft dashboards | Derived from payroll calc outputs | Max 2 minutes |
| `CACHE-DASH-RISK` | Compliance risk dashboard aggregates | Dashboard rendering | Risk module outputs | Max 10 minutes |

## Strict No-Cache Paths
No cache may be used for:
1. Payroll finalization write path.
2. Compliance submission package final validation.
3. Tax table activation and rule version switch.
4. Evidence bundle integrity validation.

This enforces `REQ-OPS-004`.

## TTL and Invalidation Policy
| Cache Key Pattern | TTL | Invalidation Trigger | Fail Behavior |
|---|---|---|---|
| `tax:active:{date}` | 300 sec | `TaxTableVersionActivated` event | Bypass cache, direct read |
| `rule:set:{version}` | 300 sec | New signed ruleset activation | Block if direct read fails |
| `leave:policy:{policy_id}` | 900 sec | Policy publish event | Direct read fallback |
| `payroll:preview:{period}` | 120 sec | New calculation run or adjustment | Recompute on miss |
| `risk:dashboard:{period}` | 600 sec | Control status change event | Serve stale max 60 sec then refresh |

## Cache Invalidation Contract
1. Every mutable operation that changes cached data must emit invalidation event with:
- key namespace
- affected identifiers
- timestamp
- correlation ID
2. Invalidation must be at-least-once delivered.
3. Consumers must treat duplicate invalidation events as safe idempotent operations.

## Cache Consistency Rules
1. Read-through strategy for reference caches.
2. Derived caches are recomputed and never treated as authoritative records.
3. Finalization path performs direct source reads with checksum verification.
4. Cache entries store:
- source version
- cache creation timestamp
- checksum of source payload

## Cache Poisoning and Integrity Controls
1. Signed payload requirement for tax/rule data loaded into cache.
2. Schema validation before cache insert.
3. Namespace isolation per environment and tenant.
4. Rejection on checksum mismatch.
5. Audit logging for cache writes on legal reference namespaces.

## Performance Budgets
| Workflow | p50 | p95 | p99 | Hard Timeout |
|---|---|---|---|---|
| Employee profile lookup | 120 ms | 300 ms | 600 ms | 2 s |
| Payroll preview request | 400 ms | 1.2 s | 2.5 s | 8 s |
| Full payroll monthly run (<500 staff) | 4 min | 10 min | 15 min | 20 min |
| EMP201 package generation | 45 s | 2 min | 5 min | 8 min |
| Compliance dashboard load | 500 ms | 1.5 s | 3 s | 6 s |

## Throughput Targets
1. Interactive API:
- sustained: 50 req/s
- burst: 120 req/s for 1 minute
2. Background workers:
- payroll calc throughput: >=40 employees/sec at launch profile.

## SLOs and Error Budgets
1. API availability SLO: 99.9% monthly.
2. Cache correctness SLO:
- 0 legal-critical stale reads on finalization path.
3. Performance SLO:
- >=95% of payroll previews under p95 target.
4. Error budget policy:
- breach triggers release freeze on affected module until corrective action approved.

## Load and Resilience Test Requirements
1. Baseline load test before first production launch.
2. Monthly synthetic workload run for regressions.
3. Chaos tests for:
- cache node failure
- Firestore transient latency
- invalidation queue lag
4. Validate graceful degradation behavior in each scenario.

## Monitoring Metrics
- cache_hit_rate by namespace
- cache_stale_read_count
- cache_invalidation_lag_seconds
- finalization_direct_read_count
- payroll_run_duration_seconds
- report_generation_duration_seconds
- queue_depth and worker lag

## Evidence Artifacts
- `EV-CACHE-001`: cache config manifest and TTL policy snapshot.
- `EV-CACHE-002`: invalidation integration test report.
- `EV-CACHE-003`: stale-read and fallback behavior report.
- `EV-CACHE-004`: performance/load test report.
- `EV-CACHE-005`: cache security/integrity verification report.

## Implementation Constraints for Agents
1. Do not introduce cache in prohibited paths.
2. Do not serve legal/tax data without version metadata.
3. Do not suppress cache integrity failures; fail closed for compliance-critical flows.
