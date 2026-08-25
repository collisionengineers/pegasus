# Plan — recover lost dispatched intake work

## Chosen approach

Extend the existing intake reconciliation path so an unleased `dispatched` row whose recorded publication is at least one minute old is conditionally returned to `pending`. The existing dispatcher then republishes the same staged-receipt identifier and the existing idempotent processing claim prevents duplicate evaluation or case allocation.

This reuses `IIntakeWorkStore`, `ReconcileStagedArtifacts`, the existing Worker reconciliation Function, the `(State, DueAtUtc)` index, the queue adapter, adjustable clock, and RecoveryTests. It adds no timer, queue, service, schema column, migration, or compatibility path.

## Governing docs

- `docs/frd/frd-02-intake-and-source-identity.md`: implement INTK-041's settled requirement that pending/unreceived publication, including unleased dispatched work, becomes eligible for idempotent recovery within one minute. Preserve commit-before-publish, stable identifier messages, Worker-only processing, bounded states, and duplicate no-op behaviour.
- ADR-0032 will be linked after INTK-041 merges. This ticket implements its slow reconciliation safety net, not immediate publication or Graph wake-up.

## Ordered steps

1. Wait for INTK-041 to merge to `dev`; confirm INTK-040 no longer holds overlapping unmerged changes, then create a fresh INTK-003 worktree from `origin/dev`.
2. Extend the existing store recovery contract to accept the stale-dispatch cutoff while keeping a single bounded recovery batch.
3. In EF, select oldest eligible expired leases and stale unleased `dispatched` rows using existing state/time fields, then use compare-before-update predicates so a concurrent claim wins.
4. Re-arm recovered dispatched work as `pending`, due now, with lease/failure fields clear and attempt count unchanged.
5. Keep `ReconcileStagedArtifacts` and its existing timer as the sole caller; expose a distinct recovered-publication count only if it is needed to prove the FRD stage/recovery timing without duplicating state vocabulary.
6. Update all interface fakes/decorators mechanically.
7. Add integration coverage for stale recovery, fresh exclusion, race safety, bounded fairness, redispatch, and process-once; retain enqueue-before-mark and duplicate-delivery tests.
8. Run focused tests, Release build/full relevant tests, simplification lenses, then report/commit/push/open the PR to `dev`.

## Proof

Focused SQL integration tests must show a one-minute-old unleased dispatched row becomes pending, a fresh row stays dispatched, concurrent processing is not overwritten, and redispatch results in one evaluation/outcome. Build and relevant suites prove all interface callers agree. Merged-dev verification repeats these checks; production observation belongs to DELIV-021.

## Risks and mitigations

- **Race with delayed delivery:** conditional update includes observed state/lease facts; processing claim wins.
- **Recovery starvation:** one oldest-first bounded candidate set covers both categories.
- **Attempt inflation:** recovery does not increment processing attempts.
- **Scope collision:** implementation waits for INTK-040 and INTK-041, then starts from refreshed `origin/dev`.
- **Accidental external-work generalization:** excluded; INTK-042 owns its relevant publication route.

## Simplification pass — 2026-08-25

- **Reuse:** kept the existing `IIntakeWorkStore`, reconciler timer, bounded priority queue, EF conditional update, dispatcher, and idempotent processor; no parallel route or new infrastructure.
- **Simplification:** renamed the widened lease-only API/result/log vocabulary to interrupted work/recovered work items so the single path remains truthful.
- **Efficiency:** retained one paged query and one `maximumItems` heap. Independent review found that stale dispatched rows were initially ranked by dispatch time rather than recovery eligibility; corrected ranking to `DueAtUtc + stale age` so expired leases cannot be starved.
- **Altitude:** the change stays at the durable work-store/reconciler boundary. It does not alter extraction, classification, allocation, schema, schedules, or deployment.
- **Disposition:** fairness finding applied with a mixed-state bounded-selection integration test. No other findings.
