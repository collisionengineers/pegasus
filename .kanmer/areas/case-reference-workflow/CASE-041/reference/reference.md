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

---

# Review record — CASE-041 (PR https://github.com/collisionengineers/pegasus/pull/664) — re-review

Reviewed head `42b38752a6ab38c4efe745cba87cc757a118ad7b` (branch
`task/case-041-inspect-at-choices`), in the detached worktree
`.worktrees/case-041-review`; `git rev-parse HEAD` matched the head named in
the review request.

Reviewers: gpt-5.6-terra (effort xhigh) read the whole `origin/dev...HEAD`
diff independently against the plan, checklist, owned paths, D33/D43–D50 and
the EPIC-012 Build policy; Claude Opus dispositioned every finding against the
code, ran the verification commands and gated on the CI run conclusion.

**Verdict: APPROVE.** The single blocker of the first review is closed at this
head, no regression was introduced by the fix, and CI on this exact head is
`success`.

## Blocker closure

`42b38752a` repoints
`tests/Pegasus.IntegrationTests/Browser/LayoutIntegrityTests.cs:249` from
`//dt[normalize-space(text())='Recorded value']` to
`//dt[normalize-space(text())='Inspect at']`, the label
`_CaseInspectionAddress.cshtml:48` now renders, and removes the dead
`OperatorLabels.CaseWorkspace.RecordedInspectionAddress` constant. Verified:

- The assertion is **unchanged** and not weakened — lines 250-253 still
  `Assert.Contains(inspectionAddress, await recordedAddress.InnerTextAsync(),
  StringComparison.Ordinal)`. The diff is one xpath string and one deleted
  constant, nothing else (`git show 42b38752a --stat`: 1 insertion,
  2 deletions across 2 files).
- A whole-tree search (`.cs`, `.cshtml`, `.html`, `.json`, excluding `obj/`)
  returns **zero** hits for `RecordedInspectionAddress` and zero for the old
  `Recorded value` label. Nothing is orphaned.
- Locally proved: the scoped integration filter, now including
  `LayoutIntegrityTests`, is **148 passed / 0 failed** (it was the failing
  class at the previous head).

## Findings and dispositions

The re-review returned no new functional finding. Its five entries are all
repeats of findings 2–6 of the first review; each was re-verified at this head
before disposition.

| # | Severity | Location | Finding | Disposition |
| --- | --- | --- | --- | --- |
| 1 | should-fix | `_CaseInspectionAddress.cshtml:71` | The static `<dt>Inspection</dt>` label is a literal rather than an `OperatorLabels` constant. | **Rejected — out of scope, unchanged.** Re-verified at this head: `git diff origin/dev...HEAD -- _CaseInspectionAddress.cshtml` contains no hunk touching that line; it pre-dates the PR. Rule 1 (scope is the brief) makes it a follow-up, not a commit on this branch. |
| 2 | should-fix | `InspectionAddressChoicesPersistenceTests.cs:28` | The `!string.IsNullOrWhiteSpace` blank guard at `InspectionAddressChoicesQueries.cs:45` is untested. | **Rejected with reason, unchanged.** The state is unreachable through the only write path: `CaseDataPolicy.Text` normalizes a blank to `null` and `EfCaseDataStore.SetConfirmed(..., null)` then deletes the field, so no blank confirmed value can be seeded except by bypassing the store. The guard is defensive. The reachable behaviour — current-case exclusion, sentinel exclusion, case-insensitive de-duplication, newest-first ordering — is proved. |
| 3 | should-fix | `site.js:655` | Manual entry clears only an untrimmed, case-folded sentinel; a hand-typed `" Image Based Assessment "` survives, and the server then trims it and infers Image Based Assessment. | **Accept risk, deferred to [[UIIMP-014]].** Core remains the single owner of the decision: `CaseDataPolicy.InferInspectionMode` trims and matches ordinally and `ValidateInspection` enforces the address/mode pairing, so the client can only surprise, never persist an invalid pairing. Reaching it needs an operator to hand-type a whitespace-padded sentinel. The clean fix — compare the trimmed input against the Image Based Assessment option's own `data-address` instead of a duplicated JS literal — is recorded for UIIMP-014's pass over this section. |
| 4 | should-fix | `docs/design/test-ui/pages/case-details--default.html:580` | The committed snapshot renders `<select id="inspection-address-choice">` with zero `<option>` children, so the design artifact does not show the D33 feature. | **Deferred to [[UIIMP-014]].** Re-verified it is not a production defect: `_CaseInspectionAddress.cshtml:10` makes `mayEdit` require `data is not null`, and `InspectionAddressChoicesQueries.GetAsync` returns `null` only when no case-data snapshot exists — precisely the case in which `data` is null and the edit panel is not rendered at all. The empty select is reachable only under the capture harness, which substitutes the case-details store but not `IInspectionAddressChoicesQueries`. Populating it means adding a capture state, and UIIMP-014 owns the per-section catalogue states. |
| 5 | should-fix | `post-implementation-report.md` | The report is stale against the reviewed head: pre-merge head `d5b1123c`, migration `20260904183440`, snapshot sizes 65,562 / 40,380 and 17 section hosts, and it contradicts its own later addendum naming `42b38752a`. | **Fixed here.** Measured by hand in the review worktree at this head and appended to the report as "Record correction at the final head": migration `20260904233144_CaseInspectionAddressChoices`; `case-details--default.html` **67,734 bytes**, `case-details--conflict.html` **40,971 bytes**; both begin `<!DOCTYPE html>`, both carry exactly one `class="case-sticky"`, zero `<img src="#">` and 16 distinct `id="section-…"` ids — the eleven section hosts plus five `-title` ids; `Test-MigrationGrants.ps1` checks 93 files. (The re-reviewer's 66,633 / 40,301 are the LF-normalized git blob sizes, not the working-tree files.) |

Nothing else was found, and the re-review confirmed independently that
`42b38752a` introduces no functional regression. Re-checked at this head:
every drawn control in the changed partial is `form="case-edit-form"`-
associated and reaches `OnPostSaveAsync`; no explanatory copy was added; the
new labels sit in the `// CASE-041:` … `// End CASE-041.` delimited block in
`OperatorLabels.cs` with no second label list; the 26 changed files are all
owned paths and no tooling file is bent; `Pegasus.Core` owns the D33 ordering
(`InspectionAddressChoices.Resolve`) and the availability rule
(`InspectionAddressChoice.IsAvailable`), with Infrastructure only looking
values up and Web only rendering; no existing assertion was weakened, relaxed,
skipped or deleted; the migration creates no table and produces no permission
delta, drops and re-adds `CK_CaseDataFields_FieldName` reversibly, and is the
single new migration with its Designer and `PegasusDbContextModelSnapshot` in
agreement and in chronological position in
`IntakePersistenceIntegrationTests.cs`; the simplification-pass dispositions
are honest (`CurrentText` is gone; claimant, storage and repairer all read
from the one `EfCaseDataStore.Map` projection).

D48 is satisfied as written: `EfCaseDataStore.Map` supplies `RepairerAddress`
as an empty `CaseField` and `InspectionAddressChoice.IsAvailable` derives the
disabled state from the absent value, so INTK-058 enables the option with no
CASE-041 code change.

One deliberate behaviour note, checked and accepted: `Details.cshtml.cs:568`
now saves `CaseDataPolicy.InferInspectionMode(inspectionAddress)` in place of
the posted `inspectionMode`, so the address/mode pairing is derived by Core
rather than trusted from the form. This is what makes the section correct with
JavaScript disabled, and it cannot violate `ValidateInspection` by
construction.

## Commands and exit codes (review worktree, head `42b38752a`)

```
git rev-parse HEAD                                                   = 42b38752a6ab38c4efe745cba87cc757a118ad7b
dotnet restore ./Pegasus.slnx --locked-mode                          RESTORE_EXIT=0
dotnet build ./Pegasus.slnx --configuration Release --no-restore     BUILD_EXIT=0   (0 warnings, 0 errors)
dotnet test ./tests/Pegasus.Core.Tests/... --no-build                CORE_EXIT=0    (1240 passed)
dotnet test ./tests/Pegasus.ArchitectureTests/... --no-build         ARCH_EXIT=0    (100 passed)
dotnet test ./tests/Pegasus.IntegrationTests/... --no-build
  --filter "FullyQualifiedName~InspectionAddressChoicesPersistenceTests
           |FullyQualifiedName~CaseDetailsWebTests
           |FullyQualifiedName~CaseTasksWebTests
           |FullyQualifiedName~InspectionAddressChoiceBrowserTests
           |FullyQualifiedName~LayoutIntegrityTests"
  -- xUnit.MaxParallelThreads=2                                      INT_EXIT=0     (148 passed)
pwsh -NoProfile -File ./scripts/Test-MigrationGrants.ps1             GRANTS_EXIT=0  (93 migration files checked)
```

That scope covers the change because every type the diff touches is exercised
by it: `CaseDataPolicy.Normalize`/`InferInspectionMode` and
`InspectionAddressChoices.Resolve` by `Pegasus.Core.Tests`; the Core→
Infrastructure dependency direction by `Pegasus.ArchitectureTests`;
`EfCaseDataStore`, `InspectionAddressChoicesQueries` and the migration by
`InspectionAddressChoicesPersistenceTests`; `Details.cshtml.cs`,
`CaseMutationPageModel`, `AssessmentMcpTools` and the partial by
`CaseDetailsWebTests` (whose partial class includes `CaseTasksWebTests.cs`);
the `site.js` binder by `InspectionAddressChoiceBrowserTests`; and the label
rename that broke the previous head by `Browser/LayoutIntegrityTests` — added
to the filter this round precisely because its absence let the blocker escape.
The snapshot artifacts were opened and measured by hand rather than trusted to
the gate.

## CI on the reviewed head

`gh run list --branch task/case-041-inspect-at-choices` → run
**`33933265382`**, headSha `42b38752a6ab38c4efe745cba87cc757a118ad7b`, status
`completed`, conclusion **`success`**. Jobs: `reference-data`,
`documentation`, `local-development-scripts`, `changes`, `test-ui`, `browser`,
`unit`, `sql-integration (1..3)`, `sql-integration-coverage` all `success`;
`infrastructure` skipped. `test-ui` and `browser` — the two jobs that failed
at the previous head — are green. No job was rerun.

## Outcome

Approved and merged to `dev`. CASE-041 moves to Verifying.
