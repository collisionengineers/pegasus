# Post-implementation report — CASE-042 (2026-09-04)

## What shipped

`/Cases?tab=awaiting` — the Awaiting instruction Pre-case queue (D38) for
unmerged image-initiated cases:

- `CaseStageCounts` gains `AwaitingInstruction`; `EfDashboardQueries
  .GetCaseStageCountsAsync` moves the unmerged image-intake addend out of
  `NotReady` into it. Count and row read share the same origin-receipt
  association predicate (R-7), including the linked-but-not-yet-merged state.
- `ImageIntakeSummary`/`EfImageIntakeStore.ProjectAsync` gain aggregate
  retained image count and source — CASE-032 (merged) shipped only the
  custody half, so CASE-042 carried these two remaining projection fields
  itself (plan Dependencies/R-2 escape), from already-persisted data; no
  migration.
- `/Cases` gains the `awaiting` tab (Pre-Case work group) with the inline
  literal label `"Awaiting instruction"` — no `OperatorLabels` edit, per the
  shipped per-kind-literal convention (R-4). Rows link through
  `Href(selected:)` so every row's quick detail is reachable without script
  (R-1). Rows/quick detail show reference·registration, image count·custody,
  Received (`RegisteredAtUtc`), Source, and Chase; no Vehicle column, no
  lifecycle chip.
- The quick view offers **Add to an existing case** only (D50). It reuses
  `UploadConfirmationPageModel` (no third handler copy) and renders a
  refused attach instead of swallowing it (R-9).
- `RailCountsPageFilter` includes the Awaiting count once in the Cases shell
  total; `LoadNotReadyAsync` returns formal Not ready Cases only, its dead
  image branch removed.
- FRD-12's four affected sentences (`:150, 162-167, 169-171`) are updated in
  the same PR (R-6).

## Files changed

- `src/Pegasus.Core/Operations/DashboardCounts.cs`
- `src/Pegasus.Infrastructure/Persistence/EfDashboardQueries.cs`
- `src/Pegasus.Core/ImageIntake/ImageIntakeContracts.cs`
- `src/Pegasus.Infrastructure/Persistence/EfImageIntakeStore.cs`
- `src/Pegasus.Web/Pages/Cases/Index.cshtml`
- `src/Pegasus.Web/Pages/Cases/Index.cshtml.cs`
- `src/Pegasus.Web/Presentation/RailCountsPageFilter.cs`
- `tests/Pegasus.IntegrationTests/TriageQueuesWebTests.cs`
- `tests/Pegasus.IntegrationTests/Browser/AccessibilityTests.cs`
- `docs/frd/frd-12-operator-experience.md`
- `docs/design/test-ui/pages/queues--default.html`,
  `docs/design/test-ui/pages/queues--empty.html`

Every changed file is within the plan's Expected files / Do-not-modify
scoping (verified with `git diff origin/dev...HEAD --stat` against that
list before opening the PR). No file outside it was touched. No migration.
No `OperatorLabels.cs`, `site.css`, `site.js`, `Pages/Shared/*`,
`Pages/Cases/Shared/*`, `Pages/Index.*`, `catalogue.json`, or
`TestUiSnapshotTests.cs` edit.

## Coordination and side effects for other lanes

- **UIIMP-014:** the tab key is settled as `?tab=awaiting`. The
  `queues--awaiting` populated/empty states, their `catalogue.json` rows,
  and the `TestUiSnapshotTests.cs` scenario stay UIIMP-014's — not added
  here. CASE-042 regenerated only the two pre-existing `/Cases` captures
  (`queues--default.html`, `queues--empty.html`) that its own page change
  invalidated.
- **UIIMP-008:** the Work Centre "Not ready" metric now reports formal Not
  ready Cases only (it reads `CaseStages.NotReady`, which no longer folds in
  unmerged image intakes). No edit made to `Pages/Index.*`; this is a value
  change only, noted for that lane.
- **CASE-032 projection gap:** CASE-032 (merged, PR #659) shipped
  `ImageIntakeSummary.Custody` but not an aggregate image count or a source
  field. Per the plan's R-2 escape, CASE-042 added both fields itself inside
  `EfImageIntakeStore.ProjectAsync`, computed from already-persisted rows
  (retained `IntakeAssets`/`IntakeReceipts` data and the existing source
  channel) — no schema change, no migration.
- **CASE-038 (frame, merged mid-implementation, PR #656):** `origin/dev`
  moved forward significantly during implementation (CASE-038, UIIMP-016,
  PLAT-073). `git merge --no-edit origin/dev` on the task branch was clean —
  no conflicts, including on the shared `frd-12-operator-experience.md` file
  both tickets edited (different sentences). All local checks were re-run
  after the merge and stayed green.

## Deviation recorded (packet contradiction)

Plan R-3 required `AwaitingInstruction` on `CaseStageCounts` to be a
**required** positional field with no default, while also asserting every
existing four-argument `CaseStageCounts` initialiser — in files this ticket
must not touch (`Pages/Index.cshtml.cs` — UIIMP-008,
`tests/Pegasus.Core.Tests/Operations/DashboardBoundaryTests.cs`,
`tests/Pegasus.IntegrationTests/QdosAllocationRecoveryTests.cs`) — keeps
compiling unchanged. Those two requirements are mutually exclusive in C#: a
required positional parameter before an optional one makes every
shorter-arity call site fail to compile. Resolution: `AwaitingInstruction`
carries a `= 0` default (the real `EfDashboardQueries` construction still
always passes it explicitly, so production behaviour is unaffected); the two
new `ImageIntakeSummary` fields are likewise appended with defaults for the
same reason. Recorded in the PR description for reviewer awareness; no
out-of-scope file was touched to work around it.

## Simplification pass

Recorded in `plan/plan.md` under "## Simplification pass (2026-09-04)":
two findings applied (dead task-await ceremony in `LoadNotReadyAsync`;
duplicated retained-image-count label in `ImageRow`), one accepted as
documented risk (the shared `ProjectAsync` projection now computes image
count for three lower-traffic callers besides the Awaiting queue — splitting
it would add a second query shape, which the plan explicitly rules out).

## Commands run and exit codes

- `dotnet restore ./Pegasus.slnx --locked-mode` — 0
- `dotnet build ./Pegasus.slnx --configuration Release --no-restore` — 0 (0 warnings, 0 errors), re-run after merge and after the simplification pass, both 0
- `dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build` — 0, 1225 passed (re-run after merge and after simplification, both 0/1225 passed)
- `dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build` — 0, 100 passed (re-run after merge and after simplification, both 0/100 passed)
- `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~TriageQueuesWebTests|FullyQualifiedName~AccessibilityTests" -- xUnit.MaxParallelThreads=2` — 0, 39 passed (re-run after merge and after simplification, both 0/39 passed)
- `pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -Scope queues -CaptureFilter "FullyQualifiedName~TriageQueuesWebTests|FullyQualifiedName~CasesIndexWebTests|FullyQualifiedName~TestUiFocusedRenderTests"` — 0 (browser sub-filter matched no tests under that class-name combination — capture came from the non-browser phase, 21 passed; snapshot-update phase 1 passed; only the two authorized files changed, confirmed with `git status --porcelain`)
- `pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture -Scope queues` — 0, 1 passed
- `pwsh -NoProfile -File ./scripts/Test-UiCatalogue.ps1` — 0 (54 routed sources, 59 prototypes, 0 broken local references)
- `git diff --check` (via Codex's own run) — 0

## Snapshot artifact facts

- `docs/design/test-ui/pages/queues--default.html` — 31,687 bytes, begins
  `<!doctype html>`, no `<img src="#">`, contains `Awaiting instruction`.
- `docs/design/test-ui/pages/queues--empty.html` — 29,803 bytes, begins
  `<!doctype html>`, no `<img src="#">`, contains `Awaiting instruction`.
- `docs/design/test-ui/index.html` was touched by the capture run but its
  diff was a no-op (line-ending only) — reverted before committing.

## PR

https://github.com/collisionengineers/pegasus/pull/663
(branch `task/case-042-awaiting-instruction-queue`, head
`<pending — set at commit; see "Review round fixes" below for the round-2
head>`)

## Review round fixes (2026-09-05)

PR review (round 1, at head `353f3da1b82ff8d0079c01ae791cc066f52aa0eb`)
returned two blockers and one should-fix; all three are fixed at the new
head, along with two additional small fixes riding the same commit. Full
findings, dispositions, and re-verification are recorded in `plan/plan.md`
under "## Review round fixes (2026-09-05)":

1. **BLOCKER** — `Index.cshtml.cs:289, 341-343`: a successful "Add to an
   existing case" redirected to `?tab=awaiting&selected=<intakeId>`, but the
   attached intake had left the Awaiting rows, so `OnGetAsync`'s stale-
   `selected` guard 404'd past the confirmation message. Fixed: the guard
   now 404s on every tab except `awaiting`, which instead drops the stale
   selection and falls back to the first remaining row.
2. **BLOCKER (test)** — `TriageQueuesWebTests.cs:508-512`: the success test
   never followed the redirect, so it masked finding 1. Fixed: it now
   follows `response.Headers.Location`, asserts 200, asserts the
   confirmation text, and asserts the row is gone.
3. **SHOULD-FIX** — `Index.cshtml:36`: the failure banner used a
   nonexistent `alert alert--error` class. Fixed: changed to
   `validation-summary`, the class the two upload pages it was copied from
   actually use.

Also in the same commit: corrected the stale "(Triage)" remark at
`Index.cshtml.cs:24-25` to name the Awaiting instruction tab; reworded the
simplification-pass finding 3 wording (`plan/plan.md`) to stop calling the
shared `ProjectAsync` image-count subquery "bounded" (`ListAsync(false, …)`
reads the Awaiting queue unpaged) while keeping the accepted-risk substance;
and added `maxlength="60"`/`"500"` to the Awaiting attach form's
`reference`/`reason` inputs, matching the sibling `UploadGroupStatus.cshtml`
form.

Re-verified at the new head: `dotnet build` (0 errors), `Pegasus.Core.Tests`
(1240 passed), `Pegasus.ArchitectureTests` (100 passed),
`TriageQueuesWebTests`/`AccessibilityTests` (39 passed, including the
strengthened success-redirect assertion), scoped
`Update-TestUiSnapshots.ps1` capture and `-Verify -SkipCapture`, and
`Test-UiCatalogue.ps1` — all green. No `queues--*` snapshot content changed
(neither the attach form's `maxlength` nor the failure banner's class
appear in the default/empty captures).

Branch `task/case-042-awaiting-instruction-queue`, PR
https://github.com/collisionengineers/pegasus/pull/663. New head recorded
on the ticket's `commits` list after push.
