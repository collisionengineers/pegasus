# Proof — SIMPLI-010 (verified on merged `dev`)

## What landed

PR #387 (https://github.com/collisionengineers/pegasus/pull/387), merged into `dev` as **`5e59f933`** on 2026-08-17 12:10 UTC (merge commit; tree identical to the CI-tested head `1e5372ce`). 22 files, +33/−62: the `draft_ready` read alias deleted from `EfIntakeReceiptStore` (`DecisionCodes`, `ParseDecision`), `EfOperationsStore`, `EfCaseAcceptanceStore`, `IntakeContracts`; 14 fixture seeds → `case_created`; three test renames; design README / current-architecture / CONTEXT.md vocabulary; dead `_StatusChip` arm. Independent review: **PASS**, no blockers (`scratch-review`).

## Verification on `5e59f933` (ticket worktree detached at the merge commit; 2026-08-17 13:12–13:25 BST)

| Command | Result |
| --- | --- |
| `dotnet restore ./Pegasus.slnx --locked-mode` | up to date |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | Build succeeded — 0 warnings, 0 errors |
| `dotnet test tests/Pegasus.Core.Tests --configuration Release --no-build` | **572 passed** |
| `dotnet test tests/Pegasus.ArchitectureTests --configuration Release --no-build` | **94 passed** |
| `dotnet test tests/Pegasus.IntegrationTests --configuration Release --no-build` (full) | **530 passed, 16 skipped (corpus/profile-gated), 0 failed** — 546 total, 11m55s (covers all 12 fixture files, incl. the seven the PR's focused filter did not run) |
| CI on PR head `1e5372ce` (same tree) | pass: unit, browser, sql-integration (1)(2)(3), sql-integration-coverage, documentation, reference-data, changes; infrastructure skipped (no infra change) |
| `rg -n -i "draft.?ready" src tests docs CONTEXT.md --glob '!docs/design/system/**'` on the merged tree | no matches |

Logs: `verify-5e59f933.log`, `verify-5e59f933-integration-full.log` (session scratchpad).

## Ticket verification line — "case existence and retry/recovery state have one authoritative source"

- Case existence: every reader keys on `CurrentCaseId` / `CaseIntakeLinks` (`IntakeAllocation.cs:220`, `IntakeMcpTools.cs:200-207`, `Intake/Details.cshtml:14-16`, `Mail/Message.cshtml.cs:118-124`); `CaseCreated` is eligibility only and no longer has an alias that could re-enter through persistence.
- Retry/recovery: solely the allocation projection (`IntakeAllocationAttempts` → `IntakeAllocationProjectionStatus`).
- Production premise checked, not argued: read-only count 2026-08-17 — `IntakeReceipts WHERE Decision='draft_ready'` = **0** (research "Verified facts").

## Not claimed

No deployment, migration, repair, or cloud write. Follow-ups: [[INTK-002]] (one decision-code table), [[INTK-003]] (stale `dispatched` recovery), [[INTK-004]] (label/doc reconciliation).
