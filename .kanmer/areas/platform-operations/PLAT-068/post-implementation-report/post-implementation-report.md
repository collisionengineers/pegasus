# Post-implementation report — PLAT-068

PR: https://github.com/collisionengineers/pegasus/pull/655 (`dev` ←
`task/plat-068-sign-off-account`), head `a1f5b947c85ceee6ceef14a0318eb4dcdd49ac19`.

## Resumed-lane provenance

This ticket's implementing lease had expired (`claim_expires_at`
2026-09-03T20:11:32Z, discovered ~20:59Z). The worktree
(`.worktrees/plat-068`, branch `task/plat-068-sign-off-account`) already held
uncommitted work from an interrupted Codex run. The lease was re-taken with
the identical branch/worktree values (never a second worktree). The prior
run's own summary
(`scratchpad/build/PLAT-068/impl-summary.md`) was read first; its claims were
independently verified rather than trusted, per the packet's instruction.

That earlier run had:

- implemented Steps 1–3 in full (Core contract, EF persistence/query,
  Accounts page dialog and table column, labels);
- found and fixed a genuine defect: an existing default holder bypassed the
  eligibility check because the eligibility rule was only applied when the
  account was *not already* the default, letting a disabled/role-stripped
  default retain the designation; fixed by applying the rule whenever the
  requested result is default, plus a new integration test;
- regenerated the Accounts Test UI snapshot (Step 4) and run every command
  in the plan's verification list, all exit 0.

I independently reran every local check myself (not trusting the prior run's
reported numbers) after re-taking the lease: restore, build, Core tests
(1188 passed), Architecture tests (100 passed), migration grants (88
checked), snapshot verify (`-SkipCapture`), and the UI catalogue check —
all exit 0. I then fast-forward-merged `origin/dev` into the branch (3
unrelated commits it had picked up since the ticket's branch point:
KANMER-011 fix, an upload-custody fix, and PLAT-067 release-state docs) to
keep the eventual PR diff scoped to this ticket's 18 files only; the merge
was a clean fast-forward with no conflicts.

## Files changed

- `src/Pegasus.Core/Identity/StaffAccountAdministration.cs` — sign-off
  contract, `SignOffSignaturePolicy`, `SignOffEngineerEligibility`.
- `src/Pegasus.Infrastructure/Persistence/EfStaffAccountAdministration.cs`,
  `EfStaffAccountQueries.cs`, `PegasusDbContext.cs`, `DependencyInjection.cs`
  — persistence, audit, query, registration.
- `src/Pegasus.Infrastructure/Persistence/Migrations/20260903135604_StaffAccountSignOff.cs`
  (+ `.Designer.cs`) and `PegasusDbContextModelSnapshot.cs` — one additive
  migration, no `GRANT` (Web already has table-level `AspNetUsers`
  `SELECT, INSERT, UPDATE`; Worker has no `AspNetUsers` grant or caller
  here).
- `src/Pegasus.Web/Pages/Administration/Accounts/Index.cshtml`,
  `Index.cshtml.cs` — Settings control on Engineer rows only, sign-off
  dialog, table column.
- `src/Pegasus.Web/Presentation/OperatorLabels.cs` — additive
  `StaffAccounts` constants only.
- `tests/Pegasus.Core.Tests/Identity/{ActorDisplayNamesTests,IdentityUseCaseTests}.cs`,
  `Intake/RetainedMailTests.cs`, `Operations/DashboardBoundaryTests.cs`,
  `Reports/EngineerActivityReportTests.cs`,
  `Triage/GetTriageDisplayNameTests.cs`, `Workflow/CaseEditAuthorityTests.cs`
  — the eight `IStaffAccountQueries` fake updates plus Core normalizer
  coverage.
- `tests/Pegasus.IntegrationTests/StaffAccountsAndRolesWebTests.cs` — web
  handler coverage and a Core-level integration test proving replay-safety,
  conflict, default transfer, retained-but-ineligible state, digest-only
  history, and the database filtered-unique-index invariant.
- `docs/design/test-ui/pages/administration-accounts--default.html` —
  regenerated; `catalogue.json` unchanged.

All changes are inside PLAT-068's owned paths; `git status --porcelain`
after every commit shows nothing outside them.

## Commands and exit codes

Pre-simplification (own re-run, not the prior run's numbers):

- `dotnet restore ./Pegasus.slnx --locked-mode` — exit 0.
- `dotnet build ./Pegasus.slnx --configuration Release --no-restore` — exit
  0, 0 warnings, 0 errors.
- `dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj
  --configuration Release --no-build` — exit 0, 1188 passed.
- `dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj
  --configuration Release --no-build` — exit 0, 100 passed.
- `./scripts/Test-MigrationGrants.ps1` — exit 0, 88 migrations checked.
- `./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture` — exit 0, 1
  snapshot verification passed.
- `./scripts/Test-UiCatalogue.ps1` — exit 0, 54 routed sources, 58
  prototypes, 0 broken local references.

After the dev merge, rebuild/retest to confirm no regression: build exit 0,
Core tests exit 0 (1188 passed), Architecture tests exit 0 (100 passed).

Simplification pass (below), then post-fix:

- `dotnet build ./Pegasus.slnx --configuration Release --no-restore` — first
  attempt exit 1 (stale MSBuild node PID 24072 held
  `Pegasus.Infrastructure.dll` locked from a prior background run;
  confirmed via `Get-CimInstance Win32_Process -Filter 'ProcessId = 24072'`
  as a reusable `dotnet.exe`/`MSBuild.dll` node, then `Stop-Process -Id
  24072 -Force`). Clean rerun: exit 0, 0 warnings, 0 errors.
- `dotnet test ./tests/Pegasus.Core.Tests/...` — exit 0, 1188 passed.
- `dotnet test ./tests/Pegasus.ArchitectureTests/...` — exit 0, 100 passed.

No routed page or partial was touched by the simplification fixes, so the
Test UI snapshot/catalogue scripts were not re-run after it.

The prohibited full solution filter and standalone `Pegasus.IntegrationTests`
project were not run locally; GitHub CI runs that suite sharded on the PR.

## Simplification pass

Recorded in `plan/plan.md` under "Simplification pass (2026-09-03)". Three
findings from an independent Codex low-effort pass: one rejected (collapsing
the two `SignOffEngineerEligibility.IsEligible` overloads — the API/test
churn across 4 call sites and 5 assertions isn't worth avoiding a
one-element array allocation per row on a low-traffic admin page), two
applied (removed a redundant defensive signature copy in
`EfStaffAccountAdministration`; simplified an already-proven eligibility
branch in `OperatorLabels.SignOffState`). Both fixes verified with a
post-fix build + Core + Architecture test run (all exit 0, same pass
counts).

## Deviations from the plan

None in substance. The plan's own "Simplification pass (2026-09-02)"
placeholder ("To be recorded by the implementer before the PR opens") is
now filled per the process; no other plan section changed. The plan
document was briefly overwritten with a placeholder by my own tool-call
error during this session and immediately restored verbatim (plus the
simplification section) from the content this session had just read —
verified by content and doc `version` in the same tool round-trip; nothing
in `plan/plan.md`'s substance was lost.

## Verification claim boundary

Per the plan's cross-lane contract, this ticket supplies and proves the
sign-off profile/query seam only (Accounts page is its production caller).
It does not claim assessment-report renderer delivery — CASE-040 and
DOCS-017 own the later selection, projection-source wiring, and renderer
integration that discharge the ticket body's "Renderer reads the sign-off
tuple" verification line.

## Review fix (2026-09-03)

PR review returned one required fix: append
`"20260903135604_StaffAccountSignOff"` to the committed-migration list in
`tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs`, after
the existing tail entry
`"20260829212237_GrantProviderSubmissionAcceptRecovery"` (line 117). All
other cross-model findings were dispositioned without a code change — see
"PR review" in `plan/plan.md`.

Applied via `codex exec -m gpt-5.6-sol -c model_reasoning_effort="medium"`,
scoped by a fix packet to that one file/one line; diff verified to touch
only that line before committing.

Delivery commands re-run in full afterward:

- `dotnet restore ./Pegasus.slnx --locked-mode` — exit 0.
- `dotnet build ./Pegasus.slnx --configuration Release --no-restore` — exit
  0, 0 warnings, 0 errors.
- `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter
  "Category!=Corpus&Category!=Browser"` — exit 0: Core.Tests 1188 passed;
  ArchitectureTests 100 passed; IntegrationTests 1114 passed, 2 skipped, 0
  failed (16m37s).

Commit `a94fffd545d3f979e6d1a5bf9b82cbc9f013a894`
("test(integration): add StaffAccountSignOff to committed migration list"),
pushed to `task/plat-068-sign-off-account`
(`a1f5b947..a94fffd5`). New PR head SHA:
`a94fffd545d3f979e6d1a5bf9b82cbc9f013a894`. Ticket not merged; left in
Review for the reviewer.

## Merge-base note (2026-09-03, discovered while applying the review fix)

`gh pr view 655` now reports `mergeable: CONFLICTING` /
`mergeStateStatus: DIRTY` against `dev`. Root cause confirmed with a
disposable detached worktree merge check (`git merge --no-commit --no-ff
origin/dev`, aborted and removed, no branch/worktree state left behind):
`dev` has since gained PLAT-070's own migration
`20260903153134_RemoveStaffReviewFlags` (PR #649), which lands on the exact
same line of the committed-migration list this fix just edited — a genuine
content conflict, not a stale GitHub computation. This is the scenario the
plan's Step 2 already anticipated ("If another migration lands first: `git
merge --no-edit origin/dev`, regenerate this one migration after the new
tail; never a second migration") but resolving it is outside this review
fix's scope (append one migration-list entry only) and is left for the
reviewer/next lane to action before merge.

## Review round 2 fixes (2026-09-03)

Applied the three findings recorded in `reference/reference.md`'s round-2
review (verdict REQUEST CHANGES, head `a94fffd5`). Worked directly in
`.worktrees/plat-068` on `task/plat-068-sign-off-account`; no Codex delegate.

### Finding 1 (blocker) — merge conflict / migration ordering

`git fetch origin && git merge --no-edit origin/dev` pulled in PLAT-070's
`20260903153134_RemoveStaffReviewFlags`, ENG-035's
`20260903110926_ExtendAssessmentVocabulary`, and DOCS-017 (#651). One real
conflict, in `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs`'s
committed-migration list (both sides edited the same line); resolved by
listing all three tail entries in chronological order
(`ExtendAssessmentVocabulary`, `RemoveStaffReviewFlags`, then this ticket's
migration). `PegasusDbContextModelSnapshot.cs` and `OperatorLabels.cs`
auto-merged cleanly, as the round-2 review had already found.

Regenerated the migration so it lands after `dev`'s new tail:

- Deleted `20260903135604_StaffAccountSignOff.cs`/`.Designer.cs`.
- A first `dotnet ef migrations add StaffAccountSignOff` (project
  `src/Pegasus.Infrastructure/Pegasus.Infrastructure.csproj`, startup
  `src/Pegasus.Web/Pegasus.Web.csproj`, `--output-dir Persistence/Migrations`)
  produced an **empty** migration, because the post-merge model snapshot
  already carried this ticket's `AspNetUsers` sign-off columns (the
  merge's clean auto-merge of the snapshot had already unioned both
  branches' model changes) — EF saw no diff between the current model and
  the snapshot once the old migration file was gone.
- Reset `PegasusDbContextModelSnapshot.cs` to `origin/dev`'s tip (the
  pre-this-ticket baseline: PLAT-070 + ENG-035 applied, this ticket's
  columns not yet applied) and reran the same `dotnet ef migrations add`
  command. This produced the correct migration —
  **`20260903225331_StaffAccountSignOff`** — with the identical `Up`/`Down`
  operations as the original (six `AddColumn` calls plus the filtered
  unique `IX_AspNetUsers_IsDefaultSignOffEngineer` index, same types/lengths).
  The regenerated `PegasusDbContextModelSnapshot.cs` came back byte-identical
  (`git diff` empty) to the merge-resolved snapshot, confirming the merge's
  auto-resolution had been correct.
- Updated the committed-migration list's last entry to
  `"20260903225331_StaffAccountSignOff"`.
- `./scripts/Test-MigrationGrants.ps1` — exit 0, 90 migration files checked,
  every created table granted or exempted; this migration carries no `GRANT`
  (adds columns only, no new table), unchanged from the original.

New migration id/position: **`20260903225331_StaffAccountSignOff`**, last in
the chronological list, immediately after
`20260903153134_RemoveStaffReviewFlags`.

### Finding 2 (should-fix) — oversized-signature test fixture

`tests/Pegasus.Core.Tests/Identity/IdentityUseCaseTests.cs`: the oversized
case in `SignOffSignaturePolicyRejectsInvalidUploads` was
`new byte[SignOffSignaturePolicy.MaximumBytes + 1]` — all zeros, so it was
rejected on the PNG magic-byte check alone and never proved the 1 MiB limit.
Added `OversizedPngSignature()` (copies the existing `Png()` header onto an
oversized zero-filled array) and used it in place of the raw array, so the
case now exercises the size branch specifically.

### Finding 3 (should-fix) — unused `IsEligible` overload

`src/Pegasus.Core/Identity/StaffAccountAdministration.cs`:
`SignOffEngineerEligibility` had a `bool hasSignature` overload with no
caller besides the `byte[]` overload's own delegation to it — confirmed by
grep: all four production call sites (`EfStaffAccountQueries.cs:127,155`,
`EfStaffAccountAdministration.cs:336`) and all five test assertions in
`IdentityUseCaseTests.cs` pass `byte[]?`. The plan's earlier simplification-pass
rejection had claimed collapsing the overloads "would drop the roles-collection
overload the Core eligibility tests call directly," which was factually wrong
(the tests call the `byte[]` overload). Folded the bool overload's body into
the `byte[]` overload and removed the bool overload; behaviour unchanged.

### Verification (exit codes)

- `dotnet restore ./Pegasus.slnx --locked-mode` — exit 0.
- `dotnet build ./Pegasus.slnx --configuration Release --no-restore` — exit 0,
  0 warnings, 0 errors.
- `dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj
  --configuration Release --no-build` — exit 0.
- `dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj
  --configuration Release --no-build` — exit 0.
- `./scripts/Test-MigrationGrants.ps1` — exit 0, 90 migration files checked.
- The merge touched routed Razor pages from `dev` (`Administration/Configuration.cshtml`,
  `Cases/Details.cshtml`, `Cases/Shared/_CaseWorkflow.cshtml`,
  `Cases/Shared/_ReadinessHiddenFields.cshtml`) though none of them are
  PLAT-068's own files, so ran the Test UI chain per the instruction:
  `./scripts/Update-TestUiSnapshots.ps1` — exit 0 (browser capture 120
  passed, non-browser capture 296 passed); resulting snapshot tree was
  byte-identical to what was already committed (`git diff` empty; the
  `git status` "M" markers were a `core.autocrlf` line-ending artifact only,
  discarded with `git checkout --`, nothing to commit).
  `./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture` — exit 0.
  `./scripts/Test-UiCatalogue.ps1` — exit 0.
- Integration suite intentionally not run locally per instruction; GitHub CI
  runs it sharded on the PR.

### Commits and push

- `5de84095` fix(migrations): regenerate StaffAccountSignOff after PLAT-070's tail
- `0d642673` fix(tests): make the oversized signature fixture start with PNG bytes
- `7a1efab7` refactor(core): drop the unused bool overload of IsEligible

Pushed `task/plat-068-sign-off-account` (`a94fffd5..7a1efab7`, via merge
commit `be764751`). New head SHA: **`7a1efab7`**. Ticket left in Review;
not merged, per instruction.

### Unresolved

None. All three round-2 findings are fixed; findings 4 and 5 from the round-2
review were already dispositioned (accept risk / accept) and needed no code
change.
