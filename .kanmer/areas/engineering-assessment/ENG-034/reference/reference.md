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

# Review record — ENG-034 (PR https://github.com/collisionengineers/pegasus/pull/668) — re-review

Reviewed head: `bd032ceb7cf0df5172de9a4c8940e08713034cbd` (branch
`task/eng-034-engineer-sections-move`, confirmed by `git rev-parse HEAD` in
the disposable review worktree `.worktrees/eng-034-review`). Head matches the
one the controller named; the branch did not move during review.

Reviewer models: the planned independent read by **gpt-5.6-terra xhigh** could
not run — `codex exec` again returned `ERROR: You've hit your usage limit …
try again at Sep 8th, 2026` (`CODEX_EXIT=1`, no output file), the same limit
that blocked the first round and the fix round. The independent read was
therefore performed by **Claude Opus** (this reviewer, who did not implement
the ticket), reading the fix-round diff, the whole moved handler surface
against its pre-move original
(`99c27e906^:src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml.cs`), the four
partials, the Core access policy, every changed test against its `origin/dev`
version, and the three committed Case Details snapshots.

Verdict: **REQUEST CHANGES** — one blocker (a second dropped `CanOpen`
authorization gate on the moved `SendToClaude` handler, of the same class as
round one's blocker, explicitly declined during the fix round), plus one
report nit. Round one's findings 1, 2, 3 and 4 are confirmed closed.

## Round-one findings — closed at this head?

| # | Earlier finding | Closed? | Evidence |
| --- | --- | --- | --- |
| 1 | `CanOpen` mutation gate relaxed to `access is null`; proving test deleted | **Yes** | `Details.cshtml.cs:1240` and `:1469` now read `access?.CanOpen != true` / `importAccess?.CanOpen != true`, byte-identical to the pre-move original at `old:917` and `old:1140`. `AssessmentCanOpen` (`:230`) is set at `:444` on the same line-for-line path as `AssessmentIsReadOnly`, fails closed to `false`, and is ANDed into `SelectedEstimateIsEditable` / `CanBeDuplicated` / `CanBeCurrent` (`:255`, `:261`, `:267`) and into the New-estimate and import branches of `_CaseEstimate.cshtml` (`:8`, `:22`, `:39`) so those controls are absent, not disabled. `AssessmentCopyWebTests.InaccessibleCaseCannotPostEstimateMutations` posts `?handler=SaveEstimate` with `canOpen: false` and asserts 404; `GuardEstimateEditAsync`'s `CanOpen` check is its first non-actor check, so no other 404 path can satisfy the test and it genuinely fails without the fix. |
| 2 | `ExtractedVehicleFactsTakePrecedenceOverLookupObservation` lost its negative half | **Yes** | `AssessmentVehiclePrefillWebTests.cs:88-89` restores `Assert.DoesNotContain("VOLKSWAGEN", …)` and `("GOLF", …)`. |
| 3 | XML doc comments and the `Unpriced` inline comment dropped by the move | **Yes** | Restored on `OnPostSaveEstimateAsync`, `OnPostEditLineAsync`, `OnPostDuplicateEstimateAsync`, `OnPostDiscardEstimateAsync`, `OnPostSetCurrentEstimateAsync`, `OnPostImportEstimateAsync`, and the "Carried forward only while the line still has no price" comment at `Details.cshtml.cs:1024-1027`. |
| 4 | `SpecificationLinesCaption` straight apostrophe | **Accepted risk** (unchanged, as dispositioned in round one). |
| 5 | Stale snapshot byte sizes in the report | **Partly** — see finding 2 below. |

The fix round's "no snapshot recapture needed" claim was independently
verified and **holds**: `grep -c` for `New estimate`, `Import estimate`,
`Save estimate`, `Delete estimate`, `Duplicate`, `Use estimate` and `Add line`
returns 0 in both `case-details--default.html` and
`case-details--conflict.html`, so ANDing `AssessmentCanOpen` onto conditions
that were already false changes no rendered byte, and CI will not report a
stale page.

## Findings and dispositions

| # | Severity | File:line | Finding | Disposition |
| --- | --- | --- | --- | --- |
| 1 | blocker | `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs:1621-1628` (`HasAssessmentAccessAsync`), called from `:938` in `OnPostSendToClaudeAsync`; `src/Pegasus.Web/Pages/Cases/Shared/_CaseEstimate.cshtml:48`, `:367`, `:378` | The move rewrote the pre-move `CanAccessAsync` — `(await getAssessmentAccess.ExecuteAsync(…))?.CanOpen == true` (`99c27e906^:…/Assessment/Index.cshtml.cs:1372-1378`) — into `HasAssessmentAccessAsync`, which returns `… is not null`. `OnPostSendToClaudeAsync` therefore now creates an `AiJobKind.Estimate` job for a case whose assessment workspace has not opened under D11, where the pre-move handler returned `NotFound()`. This is the identical defect class round one blocked on the estimate-mutation guards, and it is materially worse than it was pre-move: the old Assessment page also 404'd the whole GET when `!access.CanOpen` (`old:420-424`), so the handler check was a second line of defence; under D30 the Estimate section always renders, making the handler gate the *only* defence. `CanOpen == false && IsReadOnly == false` is not an exotic state — `AssessmentAccessPolicy` (`src/Pegasus.Core/Assessment/AssessmentWorkspace.cs:45-53`) makes it true of every case before `ReportPreparation`, i.e. the ordinary Review-stage case. The Send to Claude entry and its POST form are gated only on `!AssessmentIsReadOnly && SendToClaudeCondition is null`, and `SendToClaudeCondition` (`Details.cshtml.cs:563-576`) considers read-only, the AI toggle and `EngineerValue` but **not** `CanOpen` — so the control is UI-reachable on such a case whenever a confirmed Engineer's value exists, and craftable by any Engineer with a valid token otherwise. No test covers the refusal in either the `origin/dev` or the head version of `SendToAiIntegrationTests.cs`, which is why the whole suite is green. The fix round declined this explicitly ("`OnPostSendToClaudeAsync`/`HasAssessmentAccessAsync` was NOT touched — not named in the finding, out of this ticket's scope"); that is not a valid disposition — `SendToClaude` is named in the ticket body, the plan's Resolutions item 1 and the report's own list of the moved handler surface, and the plan's binding instruction is "move, do not rewrite". | **Fix.** Restore the `CanOpen` semantics on `HasAssessmentAccessAsync` (i.e. `?.CanOpen == true`, matching the pre-move helper), and add a test proving `OnPostSendToClaudeAsync` returns `404` with `canOpen: false` on the Case handler host — the same shape as the `InaccessibleCaseCannotPostEstimateMutations` retarget this round already landed. Also gate the Send to Claude entry and dialog on `AssessmentCanOpen`, or add `CanOpen` to `SendToClaudeCondition`, so the control is absent rather than a dead end. Returned to the implementing lane. |
| 2 | nit | `post-implementation-report.md` § Review round fixes → "Snapshot byte sizes restated at this reviewed head" | The restated sizes for `case-details--default.html` (69,470) and `case-details--conflict.html` (42,707) are correct at this head, but `case-details--unavailable.html` is restated as 24,390 bytes when the committed file is **24,694** bytes (`wc -c` in the review worktree). The other artifact facts hold: both Case pages begin `<!DOCTYPE html>`, carry `class="case-sticky"` once, expose exactly eleven `id="section-*"` hosts (damage, engineer-notes, estimate, files, inspection, notes, overview, report, settlement, valuation, vehicle) and contain no `<img src="#">`. | **Fix on the next report edit** (fold into the same round as finding 1). |

## What was verified and found correct

- Only owned paths changed. `git diff --stat origin/dev...HEAD` lists exactly
  the 21 files the ticket's `files.md` and the parallel-build policy allow —
  no `Pegasus.Core`, `Pegasus.Infrastructure`, `site.css`, `site.js`,
  `TestUiSnapshotTests.cs`, `ci.yml` or `scripts/*.ps1` edit. No migration is
  added, so `Test-MigrationGrants.ps1` is correctly not applicable. No package
  reference changed (the locked restore passed).
- `AssessmentCanOpen` is populated on exactly the path that populates
  `AssessmentIsReadOnly`, from the same single `getAssessmentAccess` call — no
  extra query — and defaults to `false`, so no path can report `true` when the
  restored backend guard would 404.
- `/Cases/{id}/Assessment` is a `RedirectPermanent` to
  `/Cases/{id}?section=estimate` with the exact 301 + `Location` assertion in
  `AssessmentCopyWebTests`; zero POST handlers remain on the retired page
  model.
- Every control drawn by the four partials names a handler that exists on
  `Details.cshtml.cs` with a `method="post"` form and antiforgery.
- `OnPostGenerateReportDraftAsync` is a faithful move — only the host-specific
  `TempData` key and redirect target changed — and its access check stays in
  Core (`AssessmentReportProjection.cs:434` checks `access?.CanOpen != true`),
  so report-draft policy is not duplicated in Web.
- No test lost an assertion this round; the checklist and the report otherwise
  match the diff, and the simplification pass names its two applied fixes with
  honest "not applicable" dispositions on the other three lenses.

## Commands run and exit codes (review worktree `.worktrees/eng-034-review`)

| Command | Exit | Result |
| --- | ---: | --- |
| `git rev-parse HEAD` | 0 | `bd032ceb7cf0df5172de9a4c8940e08713034cbd` — matches the reviewed head. |
| `dotnet restore ./Pegasus.slnx --locked-mode` | 0 | Locked restore passed. |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | 0 | 0 warnings, 0 errors. |
| `dotnet test ./tests/Pegasus.Core.Tests/… --configuration Release --no-build` | 0 | 1,240 passed. |
| `dotnet test ./tests/Pegasus.ArchitectureTests/… --configuration Release --no-build` | 0 | 100 passed. |
| `dotnet test ./tests/Pegasus.IntegrationTests/… --filter "…AssessmentCopyWebTests\|…AssessmentEstimateImportWebTests\|…AssessmentVehiclePrefillWebTests\|…CaseEngineerSectionsWebTests\|…AssessmentReportDraftWebTests\|…SendToAiIntegrationTests"` | 0 | 37 passed. |
| `codex exec -m gpt-5.6-terra …` (independent read) | 1 | Usage limit until Sep 8; no output. Read performed by Claude Opus instead. |

That scope covers the change: the Release build compiles every changed Razor
partial and page model, the Architecture tests prove the dependency direction
after moving a handler surface between composition-root pages, and the six
integration classes are the complete set of test files the diff touches plus
the new one. The suite passing while finding 1 stands is itself the evidence
that the dropped `SendToClaude` gate is uncovered. No snapshot recapture was
run or needed (see above). CI on the exact head was not gated on, because the
blocker returns the ticket to the implementing lane before merge.

# Review record — ENG-034 (PR https://github.com/collisionengineers/pegasus/pull/668) — re-review

Reviewed head: `6a2c3af779201144def500c964524902fc560d79` (branch
`task/eng-034-engineer-sections-move`, confirmed by `git rev-parse HEAD` in
the disposable review worktree `.worktrees/eng-034-review`, recreated from
`origin/task/eng-034-engineer-sections-move`; the previous round had left a
stale non-worktree directory at that path, which was removed first). The head
matches the one the controller named; the branch did not move during review.

Reviewer models: the planned independent read by **gpt-5.6-terra xhigh** again
could not run — `codex exec` returned `ERROR: You've hit your usage limit …
try again at Sep 8th, 2026` (`CODEX_EXIT=1`, no output file), the same limit
that blocked rounds one and two and both fix rounds. The independent read was
therefore performed by **Claude Opus** (this reviewer, who did not implement
the ticket): the round-three diff (`git diff bd032ceb7..HEAD`), the whole
moved handler surface against its pre-move original
(`99c27e906^:src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml.cs`), the
Core access and report-draft policies, the four partials, the changed tests
and the three committed Case Details snapshots.

Verdict: **REQUEST CHANGES** — round two's blocker is closed and its nit is
withdrawn, but one further finding of the same class (a control that renders
active and 404s) is open in this ticket's own owned files. Not merged.

## Round-two findings — closed at this head?

| # | Earlier finding | Closed? | Evidence |
| --- | --- | --- | --- |
| 1 | blocker — `HasAssessmentAccessAsync` dropped `CanOpen`, so `OnPostSendToClaudeAsync` created an `AiJobKind.Estimate` job for a case whose workspace had not opened; no test; control was a dead end | **Yes** | `Details.cshtml.cs:1628-1634` now reads `(await getAssessmentAccess.ExecuteAsync(new(caseId, actor), cancellationToken))?.CanOpen == true`, byte-equivalent to the pre-move `CanAccessAsync` (`old:1372-1378`); `grep` confirms exactly one caller (`:945`, in `OnPostSendToClaudeAsync`), so no other behaviour depended on the looser semantics. `EvaluateEngineerSectionConditionsAsync` (`:569-575`) sets `SendToClaudeCondition` to the existing `EngineerSections.NotAvailableForCase` when `!AssessmentCanOpen`, after the read-only branch, so `_CaseEstimate.cshtml:48-66` renders the gated, `disabled`/`aria-disabled` button instead of the dialog link, and `:367`'s dialog is not emitted. Ordering verified: `AssessmentCanOpen` is assigned at `:444`, before `LoadEngineerSectionsAsync` calls the evaluation at `:497`/`:509`. `SendToAiIntegrationTests.InaccessibleCaseCannotPostSendToClaude` composes `canOpen: false`, asserts the gated render and the absent dialog, then POSTs `?handler=SendToClaude&section=estimate` and asserts `404` — it fails without the fix, since the `CanOpen` check is the handler's first non-actor gate. `Compose` gained `canOpen` with a `true` default, so no existing case changed. No assertion was weakened or deleted this round (`git diff bd032ceb7..HEAD` touches only `Details.cshtml.cs` +11/-4 and `SendToAiIntegrationTests.cs` +39/-2). |
| 2 | nit — `case-details--unavailable.html` restated as 24,390 bytes when the file measured 24,694 | **Withdrawn — the lane's figure was right.** `core.autocrlf=true` in this repository: the committed blob is 24,390 bytes (`git show HEAD:… \| wc -c`, 304 lines) and the CRLF working copy is 24,694 (304 × 1). Round two measured the working copy, the lane measured the blob; both are correct readings and the discrepancy is a units difference, not a false report fact. The same holds for `case-details--default.html` (blob 68,319 / working copy 69,470) and `--conflict.html` (41,987 / 42,707). No change required. |

Round-one findings 1–4 remain closed (re-checked at this head: `:1247`
`access?.CanOpen != true`, `:1476` `importAccess?.CanOpen != true`, the
restored XML doc comments, the restored `DoesNotContain("VOLKSWAGEN"/"GOLF")`
assertions).

## Findings and dispositions

| # | Severity | File:line | Finding | Disposition |
| --- | --- | --- | --- | --- |
| 1 | should-fix (merge held) | `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs:585-589` (`ReportDraftCondition`), rendered by `src/Pegasus.Web/Pages/Cases/Shared/_CaseReport.cshtml:11-30` | The report-draft controls are the last pair on the Case page that still ignores `CanOpen`. `ReportDraftCondition` is `NotAvailableForCase` only when `ReportDraftPreparation is null`, and `NotReady` only when the preparation has reasons; `GetAssessmentWorkspace` (`src/Pegasus.Core/Assessment/AssessmentWorkspace.cs:126-142`) does **not** gate on `CanOpen`, and `AssessmentReportDraftPreparation.CanGenerate` is assessment completeness only (`AssessmentReportProjection.cs:395-401`, "Review-entry requirements are not repeated here"). So on a case with a complete assessment but `CanOpen == false` and `IsReadOnly == false` — the ordinary out-of-cycle state, `AssessmentAccessPolicy.CanOpen` requiring `LatestExportVersion >= LatestReviewVersion` — `_CaseReport` renders a submittable `GenerateReportDraft` form and a `PreviewReportDraft` link, and both 404: `GenerateCaseAssessmentReportDraft.ExecuteAsync` refuses at `AssessmentReportProjection.cs:434` with `access?.CanOpen != true`. That state is not hypothetical: `AssessmentReportDraftWebTests.CaseOutsideTheCurrentExportedReviewCycleCannotGenerateDirectly` (`:124-149`) constructs exactly it, loads the page and asserts the POST 404 — it simply never asserts what the page drew. Pre-move the Assessment page returned `NotFound()` for the whole GET when `!access.CanOpen` (`99c27e906^:…/Assessment/Index.cshtml.cs:422-424`), so the control could not render; D30 removed that page-level 404, which is what round one and round two closed for the estimate mutations and Send to Claude. This is the same defect class in the same PR's own owned file, and the same shape of fix. Backend policy is intact — this is a dead end, not a bypass — hence should-fix, not blocker. | **Fix.** Add `!AssessmentCanOpen` to `ReportDraftCondition` (reusing `EngineerSections.NotAvailableForCase`, exactly as the Send to Claude branch does — no new label), so the Generate form and the Preview link are replaced by the already-present gated control at `_CaseReport.cshtml:32-38`; and assert it in the existing `CaseOutsideTheCurrentExportedReviewCycleCannotGenerateDirectly` (`Assert.DoesNotContain("handler=\"GenerateReportDraft\"", …)` and `("Preview report draft", …)`, the same pair that test class already uses at `:110-111`). Returned to the implementing lane. No snapshot recapture is implied: neither `case-details--default.html` nor `--conflict.html` contains "Generate report draft" or "Preview report draft" (`grep -c` = 0), so no committed page's bytes move. |

## What was verified and found correct

- Only owned paths changed; the 21-file diff carries no `Pegasus.Core`,
  `Pegasus.Infrastructure`, `site.css`, `site.js`, `TestUiSnapshotTests.cs`,
  `ci.yml` or `scripts/*.ps1` edit, no migration (so
  `Test-MigrationGrants.ps1` is correctly not applicable) and no package
  change (the locked restore passed).
- Every mutation guard on the host now matches its pre-move original:
  `GuardEstimateEditAsync` (`:1247`), `OnPostImportEstimateAsync` (`:1476`)
  and `HasAssessmentAccessAsync` (`:1634`). No other `CanOpen`/`IsReadOnly`
  check was relaxed (`grep` over the whole file lists 18 sites, all
  accounted for).
- `AssessmentCanOpen` comes from the single `getAssessmentAccess` call that
  already fed `AssessmentIsReadOnly` (`:443-444`) — no second query — and
  fails closed to `false`.
- The new `!AssessmentCanOpen` branch reuses an existing label; nothing was
  added to `OperatorLabels.cs` this round, and the ENG-034 block delimiters
  (`:1482` … `:1599`) are intact.
- The two round-three code comments are code comments, not operator-facing
  copy; no explanatory copy was added to any partial.
- CI on the exact reviewed head is **green**: run `33959780644`,
  `headSha 6a2c3af779201144def500c964524902fc560d79`, `completed` /
  `success`, every job passing including `browser` and `test-ui`. The
  `LayoutIntegrityTests.TheCaseRecordLaysOutAndScrollsAtEveryWidth`
  failures at 1100px and 760px seen on the two earlier heads (`32de5bb7e`
  run `33955790118`, `bd032ceb7` run `33958578042`) did not reproduce here;
  the round-three diff changed no layout, so those two runs are recorded as
  flakes, not as a fixed defect. If that test fails again on a later head it
  is a real finding, not a retry candidate.

## Commands run and exit codes (review worktree `.worktrees/eng-034-review`)

| Command | Exit | Result |
| --- | ---: | --- |
| `git worktree add --detach .worktrees/eng-034-review origin/task/eng-034-engineer-sections-move` | `WT_EXIT=0` | `git rev-parse HEAD` = `6a2c3af77…` |
| `dotnet restore ./Pegasus.slnx --locked-mode` | `RESTORE_EXIT=0` | Locked restore passed. |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | `BUILD_EXIT=0` | 0 warnings, 0 errors. |
| `dotnet test ./tests/Pegasus.Core.Tests/… --configuration Release --no-build` | `CORE_EXIT=0` | 1,240 passed. |
| `dotnet test ./tests/Pegasus.ArchitectureTests/… --configuration Release --no-build` | `ARCH_EXIT=0` | 100 passed. |
| `dotnet test ./tests/Pegasus.IntegrationTests/… --filter "…AssessmentCopyWebTests\|…AssessmentEstimateImportWebTests\|…AssessmentVehiclePrefillWebTests\|…AssessmentReportDraftWebTests\|…SendToAiIntegrationTests\|…CaseEngineerSectionsWebTests\|…AssessmentReadinessSummaryBrowserTests" -- xUnit.MaxParallelThreads=2` | `INTEG_EXIT=0` | 39 passed (the six retargeted classes, the new class and the one changed browser class). |
| `gh run view 33959780644` | 0 | `success` on the reviewed head. |
| `codex exec -m gpt-5.6-terra -c model_reasoning_effort=xhigh …` | `CODEX_EXIT=1` | Usage limit until Sep 8; no review produced. Read performed by Claude Opus instead. |

That scope covers the change: the Release build compiles every changed Razor
partial and page model, the Architecture tests prove the dependency direction
after moving a handler surface between composition-root pages, and the seven
classes are the complete set of test files the diff touches plus the new one;
the full suite, the browser suite and the Test UI capture are CI's, and CI is
green on this exact head.
