# Post-implementation report — PLAT-070

PR: https://github.com/collisionengineers/pegasus/pull/649
Branch: `task/plat-070-remove-review-flags`
Head SHA: `8a749f53405535c234eda23206d24ae67ff5f891`

## Summary

Removed the staff instruction/image review readiness gate throughout
`Pegasus.Core`, `Pegasus.Infrastructure` and `Pegasus.Web` (D44), leaving
Not ready → Review decided by instruction/image completeness alone; Send to
EVA is the implicit review action. Recorded D45 (a damage zone records zone,
severity and note only — no damage type) in governing documents only, per
the ticket's scope (D45 is not an engineering change here).

## Files changed (four commits)

**Core** (`8e7d9919`): `CaseWorkflowContracts.cs`,
`DefaultCaseWorkflowConfiguration.cs`, `WorkflowConfigurationAdministration.cs`,
`CaseLifecycle.cs`, `CaseContracts.cs`, `CaseDataOperations.cs`, plus the six
Core test files the plan named.

**Infrastructure** (`609e1113`): `AdministrationPolicyEntities.cs`,
`AdministrationPolicyModelConfiguration.cs`, `EfWorkflowConfigurationStore.cs`,
migration `20260903153134_RemoveStaffReviewFlags` (+ Designer),
`PegasusDbContextModelSnapshot.cs`, plus eight integration test fixtures
trimmed to the reduced `CaseWorkflowConfiguration`/`CaseReadinessEvidence`
constructors.

**Web** (`21d59c89`): `Configuration.cshtml`/`.cshtml.cs`,
`CaseMutationPageModel.cs`, `Closure.cshtml.cs`, `Details.cshtml`/`.cshtml.cs`,
`_CaseWorkflow.cshtml`, `_ReadinessHiddenFields.cshtml`, `Workflow.cshtml.cs`,
`OperatorLabels.cs`, four integration web tests, three Test UI snapshots.

**Docs** (`12423adc`): `frd-01`, `frd-06`, `frd-12`,
`docs/design/README.md`. `EPIC-011/context.md` written directly to the
Kanmer board via `set_group_doc` (not part of the repo git diff).

## Commands and exit codes

- `dotnet restore ./Pegasus.slnx --locked-mode` → 0
- `dotnet build ./Pegasus.slnx --configuration Release --no-restore` → 0
  (0 warnings, 0 errors, final rebuild after the simplification pass)
- `dotnet test tests/Pegasus.Core.Tests --configuration Release --no-build`
  → 0 (1182/1182 passed; one earlier run hit a flaky, pre-existing,
  unrelated `RegexMatchTimeoutException` in
  `QdosInstructionExtractionPolicyTests` under parallel load — a file this
  ticket never touches — which passed both in isolation and on rerun)
- `dotnet test tests/Pegasus.ArchitectureTests --configuration Release --no-build`
  → 0 (100/100)
- `./scripts/Update-TestUiSnapshots.ps1` → 0 (browser phase 119/119, 7m58s;
  non-browser phase 295/295, 10m9s; snapshot-update phase 1/1)
- `./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture` → 0 (55s)
- `./scripts/Test-UiCatalogue.ps1` → 0 (54 routed sources, 58 prototypes, 0
  broken local references)
- `./scripts/Test-MigrationGrants.ps1` → 0 (88 migration files checked,
  every created table granted or exempted — no grant change needed for a
  column-only drop on an existing table)
- `./scripts/Test-DocumentationLinks.ps1` → 1: one broken link,
  `.opencode/skills/kanmer-setup/SKILL.md` → `../../../../docs/manual/greenfield.md`.
  Verified pre-existing and identical on `origin/dev` (`git diff origin/dev`
  for that file is empty); the file is untouched by this ticket and outside
  its owned paths. Not fixed here — reported as a pre-existing repository
  issue.
- `git grep -i "ReviewedByStaff|RequireStaffImageReview|RequireStaffInstructionReview|staff-reviewed"`
  → only the negative test assertions in `WorkflowConfigurationWebTests.cs`
  (asserting the strings are absent from the rendered page)

Full solution `dotnet test --filter "Category!=Corpus&Category!=Browser"`
was **not** run locally per the execution packet's explicit instruction
(~26 minutes; GitHub CI runs it sharded three ways on the PR).

## Deviations from the plan

1. **Resumed an interrupted implementation.** The worktree already held
   most of the plan's work uncommitted from a prior gpt-5.6-terra run whose
   `impl-summary.md` recorded a deliberate stop at a scope boundary. Each
   inherited change was verified against the plan and checklist before
   being kept; nothing was restarted from empty.
2. **Two test files outside the plan's step-5 file list needed a mechanical
   fix.** `AutomationMcpTestSupport.cs` and `DueChaserSweepPersistenceTests.cs`
   each construct `CaseReadinessEvidence` with the old 5-arg positional
   form; the plan's "no external caller outside owned files" verification
   for `CaseReadinessEvidence` missed them. Trimmed both to 3 args —
   identical treatment to the six sibling fixture files the plan did name,
   and required for the solution to compile after the owned Core contract
   change. Also trimmed two more 5-arg occurrences in the already-owned
   `CaseWorkflowPersistenceTests.cs` that the plan's file list did not call
   out individually.
3. **One inherited test edit violated a pre-existing Core invariant.**
   `AutomaticCaseReadinessTests.MissingEvidenceIsNotReady` set
   `InstructionConfirmedByStaff`/`ImagesConfirmedByStaff` to a literal
   `true` regardless of the completeness flags under test, which throws
   under `CaseDataPolicy.ValidateCompleteness`'s unchanged "confirmed
   implies complete" guard. Fixed by deriving the confirmed values from the
   completeness flags being tested.
4. **Migration needed scaffolding.** The interrupted run had not yet
   generated the EF migration; scaffolded via `dotnet ef migrations add`,
   renamed to `RemoveStaffReviewFlags`, and corrected the generated `Down`'s
   `defaultValue` from `false` to `true` to match the plan's requirement
   (columns were seeded `true`).
5. **Simplification pass surfaced one test-quality fix beyond its own two
   findings.** `WorkflowConfigurationWebTests.cs`'s blanket
   `Assert.DoesNotContain("<form", html)` false-failed against the shared
   layout's unrelated `<form class="utility-search">` search form (present
   on every page). Narrowed to `Assert.DoesNotMatch(ConfigurationFormRegex(), html)`
   — the file's own existing regex for the specific retired POST form —
   so the assertion tests what it claims to test without being weakened.

Full disposition table for the simplification pass, and the complete
resumed-execution note, are in `plan/plan.md`.

## Not done / follow-ups (reported, not silently expanded)

- `src/Pegasus.Web/Pages/Cases/Create.cshtml(.cs)` still binds/constructs
  `CaseCompleteness` with real `InstructionConfirmedByStaff`/
  `ImagesConfirmedByStaff` intake-time values and ~15 unowned test files
  still reference the two properties — a residual gap against D44's literal
  "no checkbox... anywhere" that the plan explicitly deferred as a
  follow-up ticket rather than expanding this one's scope (out of owned
  paths, not part of EPIC-012's Case-workspace redesign).
- The now-unused `automaticallyDefinitive` parameter on
  `CaseCompleteness.IsReadyForReview`/`CaseCompletenessPolicy.Evaluate`
  is flagged as a follow-up simplification (removing it touches
  `src/Pegasus.Core/Intake/AcceptIntake.cs`, an unowned caller).

## Review-fix pass (2026-09-03)

Applied review findings 1-7 (see `reference/reference.md` for the full record,
`plan/plan.md` → "Review response" for the per-finding disposition table).
Findings 1 and 2 were red CI on this branch's own tests (a stale migration
list; a stale readiness assertion pre-dating D44's own behaviour change) and
blocked the merge on their own; findings 3-7 were in owned paths. Findings 8
and 9 stay deferred to [[PLAT-072]] and finding 10 stays rejected — untouched
by this pass.

New head SHA: `8a749f53405535c234eda23206d24ae67ff5f891` (commit `8a749f53`).

Fixes applied via `codex exec -m gpt-5.6-sol -c model_reasoning_effort="medium"`
against a written fix packet, then verified by re-running the full delivery
commands directly (not just the two fast local proxies used earlier):

- `dotnet restore ./Pegasus.slnx --locked-mode` → 0
- `dotnet build ./Pegasus.slnx --configuration Release --no-restore` → 0 (0
  warnings, 0 errors)
- `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter
  "Category!=Corpus&Category!=Browser"` → 0, run in full this time (unlike the
  initial implementation pass, which deferred the SQL-backed integration
  suite to CI): `Pegasus.Core.Tests` 1182/1182, `Pegasus.ArchitectureTests`
  100/100, `Pegasus.IntegrationTests` 1106/1108 passed + 2 pre-existing skips,
  0 failed.
- `./scripts/Update-TestUiSnapshots.ps1` → 0 (browser phase 119/119 6m53s;
  non-browser phase 295/295 7m12s; snapshot-update phase 1/1). One real
  content diff: `docs/design/test-ui/pages/administration-configuration--default.html`
  loses the same dead `TempData["AdministrationStatus"]` notice block finding
  7 removed from the Razor. Every other snapshot `git status` reported
  modified was a CRLF/LF normalization no-op (confirmed empty diff per file)
  and was reverted, not committed.
- `./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture` → 0
- `./scripts/Test-UiCatalogue.ps1` → 0 (54 routed sources, 58 prototypes, 0
  broken local references)
- `./scripts/Test-MigrationGrants.ps1` → 0 (88 migration files checked; this
  pass added no migration, so no grant change)
- `git diff --check` → 0 (no whitespace errors)

Committed as `8a749f53` on `task/plat-070-remove-review-flags` and pushed to
`origin`. PR #649 unchanged in target/scope. Not merged — merge authority and
the next review pass are outside this task's stop condition.

### Decision needed outside this ticket (not actioned here)

The `documentation` CI lane is red for a reason unrelated to PLAT-070:
`.opencode/skills/kanmer-setup/SKILL.md` links to a non-existent
`docs/manual/greenfield.md` on `dev` itself (confirmed identical on
`origin/dev`), which will fail every PR in EPIC-012 until fixed. Outside
PLAT-070's owned paths; not fixed here. Whether it becomes a separate chore
ticket or an administrator push straight to `dev` needs a controller/
administrator decision.
