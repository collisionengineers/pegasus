# Review record — ENG-034 (PR https://github.com/collisionengineers/pegasus/pull/668)

Reviewed head: `32de5bb7e08ebe7a9c575ed010300b22cf99831b`
(branch `task/eng-034-engineer-sections-move`, confirmed by
`git rev-parse HEAD` in the disposable review worktree
`.worktrees/eng-034-review`).

Reviewer models: the planned independent read by **gpt-5.6-terra xhigh** could
not run — `codex exec` returned `ERROR: You've hit your usage limit … try again
at Sep 8th, 2026` (`CODEX_EXIT=1`, no output file). The independent read was
therefore performed by **Claude Opus** (this reviewer, who did not implement
the ticket), reading the whole diff, the moved handler bodies against their
`origin/dev` originals, the four new partials, the label block, the catalogue
and index changes, every changed test file against its `origin/dev` version,
and the Core/Infrastructure guards behind the moved handlers.

Verdict: **REQUEST CHANGES** — one blocker (an authorization gate dropped and
the test that proved it deleted), one should-fix, three nits.

## Findings and dispositions

| # | Severity | File:line | Finding | Disposition |
| --- | --- | --- | --- | --- |
| 1 | blocker | `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs:1209`, `:1430`; `tests/Pegasus.IntegrationTests/AssessmentCopyWebTests.cs` (deleted `InaccessibleCaseCannotPostAssessmentChanges`) | The moved estimate-mutation guards changed from the original `access?.CanOpen != true → NotFound()` to `access is null → NotFound()`, so `GuardEstimateEditAsync` (Save, EditLine, Duplicate, Discard, SetCurrent) and `OnPostImportEstimateAsync` now admit a case whose assessment workspace has not opened under D11 (`AssessmentAccessPolicy.CanOpen`: state ∈ ReportPreparation/PostReport/PostReportComplete **and** a current-cycle export). Nothing below restores it: `SaveEstimate` (`src/Pegasus.Core/Assessment/Estimates.cs:387`) has no lifecycle check, `CaseLifecycleRules.ValidateMutation` (`src/Pegasus.Core/Lifecycle/CaseLifecycle.cs:415`) validates only case/version/actor/reason/lease, and `EfRepairSpecificationStore.Guard` (`:544`) checks version, lease and archive only. The surface is UI-reachable, not merely craftable: `_CaseEstimate.cshtml:30` and `:284` render the Import control **and its POST form** whenever the actor is an Engineer and the case is not Complete, regardless of whether the workspace opened. Under the same change the one test that proved the refusal was deleted rather than retargeted (repo rule 19). The plan's narrowing of D11 is about visibility only — "`CanOpen` cannot hide sections" — and authorises no change to the mutation gate. | **Fix.** Either restore the `CanOpen` condition on the mutating handlers and stop drawing the import/new-estimate controls when the workspace is unavailable, plus retarget (not delete) `InaccessibleCaseCannotPostAssessmentChanges` to the Case handler host; or, if opening estimate work before the workspace opens is intended, that is an operator/epic decision to record as a D-decision, with a test proving the new rule. Returned to the implementing lane. |
| 2 | should-fix | `tests/Pegasus.IntegrationTests/AssessmentVehiclePrefillWebTests.cs:59-86` | `ExtractedVehicleFactsTakePrecedenceOverLookupObservation` lost the negative half of its assertion (the `origin/dev` version asserted the lookup value `VOLKSWAGEN GOLF` was *not* rendered when confirmed facts exist). It now asserts only that FORD/FOCUS/40,000 appear, which any render containing both sources satisfies — the test no longer proves the precedence its name claims. | **Fix.** Restore an equivalent negative assertion against the Vehicle section's primary fields, or rename the test to what it now proves. |
| 3 | nit | `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs` (moved handler bodies) | The move dropped every XML doc comment carried by the moved handlers and the inline comment in `SaveEstimate` explaining why `Unpriced` is carried forward only while a line has no price (an `AssessmentPolicy` constraint that is not obvious from the code). The plan's instruction was "move, do not rewrite". | **Fix (cheap).** Restore the comments with the code they explain. |
| 4 | nit | `src/Pegasus.Web/Presentation/OperatorLabels.cs` (`EngineerSections.SpecificationLinesCaption`) | The moved caption changed `&#8217;` (’) to a straight `'`, so the rendered wording differs by one typographic character from the source markup (`Assessment/Index.cshtml:711` on `origin/dev`). | **Accept risk.** Cosmetic; note it if the design authority wants the typographic apostrophe. |
| 5 | nit | `post-implementation-report/post-implementation-report.md` § Snapshot artifact facts | The recorded byte sizes (66,113 / 41,777) predate the `origin/dev` merge in the reviewed head; the committed files are 69,470 and 42,707 bytes. Every other artifact fact was re-verified and holds. | **Fix on the next report edit.** Re-state the sizes at the reviewed head. |

## What was verified and found correct

- The nine named handlers plus lease claim/heartbeat/release exist on
  `Details.cshtml.cs` and **zero** remain on `Assessment/Index.cshtml.cs`
  (counted in both files against `origin/dev`); the lease handlers were
  deleted rather than duplicated, CASE-038's already being on the host.
- Every control drawn by the four partials names a handler that exists:
  `SaveEstimate`, `EditLine` (via `formaction`), `DuplicateEstimate`,
  `SetCurrentEstimate`, `DiscardEstimate`, `ImportEstimate`, `SendToClaude`,
  `GenerateReportDraft`, `PreviewReportDraft`.
- `/Cases/{id}/Assessment` is a `RedirectPermanent` to
  `/Cases/{id:D}?section=estimate`, asserted with both status and exact
  `Location`.
- Glass's and Audatex launch controls were removed, not moved (D21).
- No damage type (D45), no signatory tuple (D31), no image curation or crop
  entry (D46), no fee-note preview (D42), no staff-review wording (D44) — the
  new test asserts the last one directly.
- Complete renders mutation controls **absent**, proved by `DoesNotContain`
  on New estimate, Import estimate, Save estimate, Send to Claude and Generate
  report draft in `CaseEngineerSectionsWebTests`.
- Every operator-visible and accessible-name literal in the new markup comes
  from the one `// ENG-034: … // ENG-034 end.` block; no hard-coded string
  remains in the partials; no label in the block is unused; the moved
  condition sentences match their `origin/dev` wording exactly.
- No explanatory copy: the only prose is the delete-estimate consequence
  sentence (permitted) and the pre-existing report-draft readiness notice.
- No Core, persistence, CSS/JS or migration change; the diff stays inside the
  owned paths plus the two recorded deviations.
- Deviation (a), the one-row `docs/design/test-ui/index.html` move, is
  **accepted**: it is the mechanically generated companion of the owned
  `catalogue.json` row and `Test-UiCatalogue.ps1` is red without it.
  Deviation (b), regenerating `case-details--default.html` and
  `--conflict.html`, is **accepted**: they are the snapshots of the page this
  ticket changed.
- Both simplification-pass fixes are real in the diff (the shared
  `DetailsModel.AssessmentValue` helper reusing `CaseWorkspace.AbsentValue`;
  the six posted line-field collections materialized once before the
  `ReadEditorPost` loop).
- Snapshot artifacts opened: `case-details--default.html` 69,470 bytes and
  `case-details--conflict.html` 42,707 bytes, both beginning `<!DOCTYPE html>`,
  both with one `class="case-sticky"`, eleven distinct `id="section-*"` hosts
  (damage, engineer-notes, estimate, files, inspection, notes, overview,
  report, settlement, valuation, vehicle) and no `<img src="#">`;
  `case-assessment--default.html` confirmed deleted.

## Commands run in the review worktree (exit codes quoted)

| Command | Exit |
| --- | ---: |
| `git worktree add --detach .worktrees/eng-034-review origin/task/eng-034-engineer-sections-move` | 0 |
| `dotnet restore ./Pegasus.slnx --locked-mode` | `RESTORE_EXIT=0` |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | `BUILD_EXIT=0` (Build succeeded, 0 warnings) |
| `dotnet test ./tests/Pegasus.Core.Tests/… --configuration Release --no-build` | `CORE_EXIT=0` (1,240 passed) |
| `dotnet test ./tests/Pegasus.ArchitectureTests/… --configuration Release --no-build` | `ARCH_EXIT=0` (100 passed) |
| `dotnet test ./tests/Pegasus.IntegrationTests/… --filter "…AssessmentCopyWebTests\|…AssessmentEstimateImportWebTests\|…AssessmentVehiclePrefillWebTests\|…AssessmentReportDraftWebTests\|…SendToAiIntegrationTests\|…CaseEngineerSectionsWebTests"` | `INT_EXIT=0` (36 passed) |
| `codex exec -m gpt-5.6-terra …` (independent read) | `CODEX_EXIT=1` — usage limit, no review produced |

That scope covers the change: the diff touches only Web composition/
presentation, the six retargeted test classes plus one new one, and generated
Test UI artifacts; the changed types are exercised by exactly those classes,
and the browser class (`AssessmentReadinessSummaryBrowserTests`) plus the full
suite and the Test UI verify are left to CI, which is the merge gate.
`Test-MigrationGrants.ps1` is not applicable — no migration.

CI at the reviewed head (`repository-check`, run `33955790118`) was
`in_progress` at review time. **Not merged**: the blocker above must be
dispositioned by the implementing lane first.
