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
