# Post-implementation report — TICK-053 / MAIL-11

## Outcome

Implemented MAIL-11 on `task/tick-053-mail-browse-search` and opened [PR #469](https://github.com/collisionengineers/pegasus/pull/469) against `dev`.

Retained Inbox search now filters in SQL before count/paging across the retained body, attachment filenames, and one receipt-owned projection of attachment text already produced by the canonical intake reader. Results disclose exact match locations and message detail discloses attachment searchability.

Deleted Items search is an explicit Core-authorized, GET-only Graph read against exact pollable approved mailboxes and each mailbox's resolved well-known `deleteditems` folder. It scans at most the 100 newest messages, passes MIME once through the existing `IIntakeSourceReader`, persists nothing, and reports unavailable/truncated states. No mailbox mutation, backfill, historical reconstruction, second parser, search store, feature framework, or deployment was added.

## Implementation

- Core: extended the retained-mail request/result contract, added typed match evidence and the bounded Deleted Items use case/port, and projects mailbox-reader content only through `IntakeSearchProjection`.
- Infrastructure: added receipt-owned `IntakeSearchDocuments`, atomic store/replace behavior, migration/model snapshot and runtime grants; extended the existing Graph client/source and approved-mailbox composition.
- Web: reused `/Inbox`, existing tabs/filterbar/paging/freshness conventions, preserved search scope through detail/back/section links, and rendered honest empty/unavailable/truncated/searchability evidence.
- Documentation: updated `docs/capabilities.md` and `docs/current-architecture.md` to the local implementation tier only. `docs/operations.md` is unchanged because nothing was deployed.
- Release checks: added the new grant-carrying migration to the existing Azure database-bootstrap permission matrix after CI's deployment-plan guard identified the required reconciliation.

## Commits

- `2d7c1421fcc4ff864cd9e114c904a3181c3fb4b9` — code, schema, UI and focused tests.
- `72f55b8a551336ffd46b8536aafdc334c1854f26` — exact local documentation.
- `93c06957` — database-bootstrap grant matrix reconciliation.

## Verification

- `dotnet restore Pegasus.slnx --locked-mode` — passed.
- `dotnet build Pegasus.slnx --configuration Release --no-restore` — passed, 0 warnings/errors.
- Focused `RetainedMailTests` — 25 passed.
- Focused migration/SQL/Graph/Web acceptance set — 51 MAIL-11-relevant tests passed.
- `CommittedMigrationCreatesTheSqlServerSchema` after inventory update — passed.
- `scripts/Test-MigrationGrants.ps1` — passed, 58 migrations checked.
- `scripts/Test-AzureDeploymentPlan.ps1 -Mode Local` — passed.
- Full Core suite — 763 passed.
- Full Architecture suite — 98 passed.

A first full Integration run found only the newly expected migration-name inventory and passed 799 other tests; the inventory was fixed and its exact rerun passed. Subsequent full runs overlapped other agents' LocalDB suites and repeatedly hit unrelated, pre-existing `QdosAllocationRecoveryTests.DistinctParallelRetriesResolveToOneCaseAggregate` deadlocks and one EVA SQL timeout. Even an exact selected allocation test deadlocked while other testhosts remained. The ticket-focused SQL/Graph/migration/Web set passed; CI is the clean full-suite authority. Fourteen corpus-dependent tests were intentionally skipped by the repository harness.

## Simplification and scope disposition

The dated four-lens pass is recorded in the plan. It extracted the pure projection from a temporary partial-class shape, reused the existing filterbar, removed unnecessary eager loads, kept page match grouping bounded to 25 rows, and confirmed the change stays in the existing Core/Infrastructure/Web boundaries. Actual hand-edited scope remained close to the estimate; EF's generated designer dominates the line count. No deferred capability or speculative abstraction was pulled in.

## Residual verification

Production tenant permission and real Deleted Items contents are not proven locally. After deployment, the already-approved authenticated read-only browse/search/thread journey remains the live acceptance step. No external or cloud write was performed.
