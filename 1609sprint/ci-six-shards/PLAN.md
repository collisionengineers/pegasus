# Six-shard SQL CI plan

Snapshot: 16 September 2026. Approved by the operator in this conversation.
Temporary, operator-requested sprint handover; not a new permanent policy.
See [progress and evidence](PROGRESS.md) and the related
[performance PR plan](../performance-pr-764/PLAN.md).

## Aim and reason

Reduce CI's longest SQL integration job without dropping tests, weakening
assertions or merely increasing the timeout. The current three-shard run
reached 45 minutes on two runners while the third finished in 16m47s.
Allocation groups entire classes by enumerated test count, not elapsed time.
The 188-row CaseDetails class alone occupied 31m07s on its runner.

The agreed change is six SQL runners plus genuinely independent Case feature
test classes. Keep the existing 45-minute job limit and four concurrent test
classes within each runner. No duration-based scheduler is included.

## Implementation

### Independent feature classes

Replace the single partial `CaseDetailsWebTests` class, spread across 16 files,
with 15 concrete classes named after the existing feature test files. Each
class explicitly retains the `Category=SqlServer` trait.

The feature owners are AssetPreparation, Closure, Custody, DamageAndViewer,
Details, EditMode, EstimateHeader, RecordFrameV26, RecordGapsV26,
ReportApproval, Tasks, ValuationV26, Valuation, Vehicle and Workflow.

Redistribute the main file's tests by behavior, not arbitrary numeric batches:

| Behavior | Destination |
| --- | --- |
| Files, queries, upload requests, render leases and exports | Custody |
| Image preparation | AssetPreparation |
| Lifecycle and EVA | Workflow |
| Report evidence | ReportApproval |
| Manual chasing | Tasks |
| Inspection, contact details and readiness | RecordGapsV26 |
| Damage saving | DamageAndViewer |
| Edit authority, lease refusal, retained values and holder disclosure | EditMode |
| Adverse closing | Closure |
| Layout, navigation, generic sections, cross-section reads and history | Details |

Extract only genuinely shared helpers into the test-free
`internal static partial CaseWebTestSupport`. Keep feature-specific helpers
private. Each test retains its own store, factory, database, temporary
directory and disposable resources. Do not introduce inherited tests, a
shared mutable fixture or a shared collection that serializes the classes.
Preserve every method, assertion, theory argument and skip condition.

### Six-runner workflow

Update all three shard-count owners in `.github/workflows/ci.yml`: matrix
entries, shard execution and partition verification. Each uses six.
Preserve `Category!=Corpus`, the allocation algorithm, `fail-fast: false`,
per-shard artifacts, executed-count checking and exact partition checking.

Extend `scripts/Test-TestShard.ps1` to exercise three and six shards:
both directions of the snake allocation, reversed discovery order,
whole-class ownership, exactly-once coverage and empty assignments when
classes are fewer than runners. Update examples and the runbook explanation.

## Verification and acceptance

1. Review the complete source diff, especially shared helper access and
   parallel test isolation.
2. Freeze a commit. Acquire the canonical host slot before any local build,
   test or verification script; never overlap another task's host workload.
3. Run shard regression checks and an affected Release integration build.
4. Enumerate all six actual assignments and verify their union against the
   original 2,349 non-corpus rows. Compare the original 188 CaseDetails rows
   with their new class owners using method names and full theory arguments,
   not counts alone. There must be no missing, extra or duplicate cases.
5. Execute all 15 feature classes together in one focused local run so their
   new parallel scheduling is exercised. Retain TRX and failed attempts.
6. Check documentation links and applicable Markdown placement.
7. Push the reviewed candidate to the existing PR and require all six SQL
   jobs, partition verification and other required CI lanes to pass. Remote
   CI owns full-suite evidence; do not repeat the full suite locally.
8. Report each shard's duration and the longest job against the old run.
   If timeouts remain, investigate measured remaining work before increasing
   limits or changing allocation again.

## Boundaries and handover

Implementation belongs to `perf/first-use-and-image-cache` in
`C:/Users/Alex/Documents/GitHub/pegasus-worktrees/performance-next`, within
[PR #764](https://github.com/collisionengineers/pegasus/pull/764).
This is test/CI organization, not an application, database, dependency or
deployment change. No performance improvement is accepted until measured.

These sprint files were requested in the original checkout. Leave them
uncommitted unless separately requested: `1609sprint/` is outside the current
Markdown placement allowlist. Do not silently weaken that gate or move the
operator-requested files. Consolidate durable evidence into the PR/runbook
before retiring this temporary handover.
