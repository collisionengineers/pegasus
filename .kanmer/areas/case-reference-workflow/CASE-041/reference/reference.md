# Review record — CASE-041 (PR https://github.com/collisionengineers/pegasus/pull/664)

Reviewed head `7f03530715742dd21fb529eb32340f595a41bc09` (branch
`task/case-041-inspect-at-choices`), reviewed in the detached worktree
`.worktrees/case-041-review`. `git rev-parse HEAD` matched the head named in
the review request.

Reviewers: gpt-5.6-terra (effort xhigh) read the diff independently; Claude
Opus dispositioned every finding against the code, ran the verification
commands and gated on CI.

**Verdict: REQUEST CHANGES — one blocker.** CI on this exact head
(run `33931507432`) is `failure`: an existing browser test that CASE-041's own
label change breaks. Everything else in the diff is sound.

## Findings and dispositions

| # | Severity | Location | Finding | Disposition |
| --- | --- | --- | --- | --- |
| 1 | **blocker** | `tests/Pegasus.IntegrationTests/Browser/LayoutIntegrityTests.cs:249` | `InspectionAddressOutsideEditFormIsGuardedAndSaved` waits for `//dt[normalize-space(text())='Recorded value']/following-sibling::dd[1]`. `_CaseInspectionAddress.cshtml:48` renames that `<dt>` from `OperatorLabels.CaseWorkspace.RecordedInspectionAddress` ("Recorded value") to `.InspectAt` ("Inspect at"), so the locator never resolves and the test times out after 30 s. It fails both the `browser` job and the `test-ui` job's capture phase (1 failed / 124 passed in each). The lane's scoped filter covered only its own four classes, so the break was never run locally. | **Fix — returned to the implementer.** Point the locator at the new label and leave the assertion's strength unchanged (it must still assert the saved address appears in the read-mode value). `OperatorLabels.CaseWorkspace.RecordedInspectionAddress` is now dead — its only remaining reference is its own declaration — so remove it in the same change rather than leave an unused label constant. `Browser/LayoutIntegrityTests.cs` is a test file, not tooling, and the break is a direct consequence of this ticket's own rename, so editing it is in scope. |
| 2 | should-fix | `src/Pegasus.Web/wwwroot/js/site.js:655` | Manual-entry sentinel detection case-folds without trimming (`input.value.toLowerCase() === 'image based assessment'`) while Core trims and matches ordinally, and duplicates the Core constant as a JS literal. `" Image Based Assessment "` survives Manual entry; the save handler then normalizes it back to the sentinel and `CaseDataPolicy.InferInspectionMode` selects Image Based Assessment. | **Accept risk, with a note.** Core remains the single owner: `CaseDataPolicy.InferInspectionMode` and `ValidateInspection` decide the persisted mode, so the client can only surprise, never persist a wrong pairing, and the case needs an operator to hand-type a whitespace-padded sentinel. The clean fix — compare the trimmed input against the rendered Image Based Assessment option's own `data-address` instead of a duplicated literal — is recorded for [[UIIMP-014]]'s pass over this section. |
| 3 | should-fix | `src/Pegasus.Web/Pages/Cases/Shared/_CaseInspectionAddress.cshtml:71` | The static `<dt>Inspection</dt>` label is a literal in the partial rather than an `OperatorLabels` constant. | **Rejected — out of scope.** Verified against `origin/dev`: that line is untouched by this PR and pre-dates it. Rule 1 (scope is the brief) makes it a follow-up, not a commit on this branch. |
| 4 | should-fix | `tests/Pegasus.IntegrationTests/InspectionAddressChoicesPersistenceTests.cs:17` | The history test does not seed a blank or whitespace-only confirmed inspection address, so the query's `!string.IsNullOrWhiteSpace` blank-exclusion guard is untested. | **Rejected with reason.** The state is unreachable through the only write path: `CaseDataPolicy.Text` normalizes a blank to `null` and `EfCaseDataStore.SetConfirmed(..., null)` then deletes the field, so no blank confirmed value can exist to seed. The guard is defensive. The test does prove current-case exclusion, sentinel exclusion, case-insensitive de-duplication and newest-first ordering (`InspectionAddressChoicesPersistenceTests.cs:59-65`). |
| 5 | should-fix | `post-implementation-report/post-implementation-report.md` | The record is stale against the reviewed head: it names head `d5b1123c…`, migration `20260904183440`, snapshot sizes 65,562 / 40,380 bytes and 17 `id="section-"` hosts. | **Fixed here.** Measured on the reviewed head: head is `7f035307…`; the migration is `20260904233144_CaseInspectionAddressChoices`; `case-details--default.html` is **67,734 bytes**, `case-details--conflict.html` is **40,971 bytes**; both begin `<!DOCTYPE html>`, both carry exactly one `class="case-sticky"`, zero `<img src="#">`, and 16 `id="section-"` matches — **eleven section hosts** (overview, engineer-notes, inspection, vehicle, damage, valuation, estimate, settlement, report, files, notes) plus five `-title` ids. The drift is merge-prep regeneration after the report was written; the figures above supersede it. |
| 6 | should-fix | `docs/design/test-ui/pages/case-details--default.html` | The committed snapshot renders `<select id="inspection-address-choice" …>` with **zero `<option>` children**: only `CaseTasksWebTests.cs:123` substitutes `IInspectionAddressChoicesQueries`, so the snapshot scenario resolves the real EF adapter, gets `null`, and renders an empty control. The design artifact CI verifies therefore does not show the D33 feature at all. | **Deferred to [[UIIMP-014]].** Not a production defect — `GetAsync` returns `null` only when no case-data snapshot exists, and in that case `data is null` makes `mayEdit` false and the edit panel is not rendered, so the empty select is reachable only under the capture fake. UIIMP-014 owns the per-section catalogue states and is the right place to add a populated Inspect-at state. |

Nothing else was found. The ten review questions were otherwise clean: the 25
changed files are all owned paths and no tooling file is bent; every drawn
control is `form="case-edit-form"`-associated and reaches `OnPostSaveAsync`,
and the `site.js` binder mounts through CASE-038's idempotent
`window.pegasusMountBinders` hook and guards re-binding with
`select.dataset.inspectionAddressBound`; the new labels sit in a
`// CASE-041:` … `// End CASE-041.` delimited block in `OperatorLabels.cs` and
no explanatory copy was added; Core owns the choice ordering and the
availability rule (`InspectionAddressChoices.Resolve`,
`InspectionAddressChoice.IsAvailable`) with Infrastructure only looking values
up and Web only rendering; no changed existing assertion was weakened,
relaxed or skipped (the only edits to existing tests add fields and
assertions, and two doc comments drop a now-wrong count of "twenty" members);
the migration creates no table and produces no permission delta, drops and
re-adds `CK_CaseDataFields_FieldName` reversibly, and is the single new
migration with Designer and `PegasusDbContextModelSnapshot` in agreement and
in chronological position in `IntakePersistenceIntegrationTests.cs`; the
simplification-pass dispositions are honest — `CurrentText` is gone and
claimant, storage and repairer all read from the one `EfCaseDataStore.Map`
projection.

D48 is satisfied as written: `EfCaseDataStore` maps `RepairerAddress` as an
empty `CaseField` and `InspectionAddressChoice.IsAvailable` derives the
disabled state from the absent value, so INTK-058 enables the option without
a CASE-041 code change.

## Commands and exit codes (review worktree, head `7f035307`)

```
dotnet restore ./Pegasus.slnx --locked-mode                          RESTORE_EXIT=0
dotnet build ./Pegasus.slnx --configuration Release --no-restore     BUILD_EXIT=0   (0 warnings, 0 errors)
dotnet test ./tests/Pegasus.Core.Tests/... --no-build                CORE_EXIT=0    (1240 passed)
dotnet test ./tests/Pegasus.ArchitectureTests/... --no-build         ARCH_EXIT=0    (100 passed)
dotnet test ./tests/Pegasus.IntegrationTests/... --no-build
  --filter "FullyQualifiedName~InspectionAddressChoicesPersistenceTests
           |FullyQualifiedName~CaseDetailsWebTests
           |FullyQualifiedName~InspectionAddressChoiceBrowserTests"
  -- xUnit.MaxParallelThreads=2                                      INT_EXIT=0     (78 passed)
pwsh -NoProfile -File ./scripts/Test-MigrationGrants.ps1             GRANTS_EXIT=0  (93 migration files checked)
gh run list --branch task/case-041-inspect-at-choices                RUNLIST_EXIT=0
```

That scope covers the change because every type the diff touches is exercised
by it: `CaseDataPolicy.InferInspectionMode`/`Normalize` and
`InspectionAddressChoices.Resolve` by `Pegasus.Core.Tests`; the Core→
Infrastructure dependency direction by `Pegasus.ArchitectureTests`;
`EfCaseDataStore`, `InspectionAddressChoicesQueries` and the migration by
`InspectionAddressChoicesPersistenceTests`; `Details.cshtml.cs`,
`CaseMutationPageModel`, `AssessmentMcpTools` and the partial by
`CaseDetailsWebTests` (whose partial class includes `CaseTasksWebTests.cs`);
and the `site.js` binder by `InspectionAddressChoiceBrowserTests`. The scoped
filter is also exactly what made finding 1 escape: it does not include
`Browser/LayoutIntegrityTests`, which the label rename breaks. The snapshot
artifacts were opened and measured by hand rather than trusted to the gate.

## CI on the reviewed head

`gh run list --branch task/case-041-inspect-at-choices --limit 3
--json headSha,status,conclusion,databaseId` →
run `33931507432`, headSha `7f03530715742dd21fb529eb32340f595a41bc09`,
status `completed`, conclusion **`failure`**.

Jobs: `reference-data`, `local-development-scripts`, `documentation`,
`changes`, `unit`, `sql-integration (1..3)`, `sql-integration-coverage` all
`success`; `infrastructure` skipped; **`test-ui` and `browser` both
`failure`**, each on the single test in finding 1. Not a flake and not a
`changes`-job rerun candidate — a deterministic locator mismatch caused by
this diff.

## Outcome

Not merged. The ticket stays in Review for finding 1 to be applied and CI to
go green on the new head; findings 2–6 are dispositioned above and need no
change on this branch.
