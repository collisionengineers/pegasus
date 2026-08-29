# CASE-027 post-implementation report

Branch `task/case-027-case-detail-views`, worktree
`../pegasus-worktrees/case-027-case-detail-views`, based on `origin/dev`
at `55e23b02`. Five commits, PR against `dev`. Not merged by this lane.

## What shipped

| File | Change |
| --- | --- |
| `Pages/Cases/Shared/_CaseVehicle.cshtml` (new) | The `?section=vehicle` body: vehicle facts, "Vehicle checks" with both refresh controls, the Experian D7 seam, the recorded lookup observations, and the accept/correct suggestion decisions. |
| `Pages/Cases/Shared/_CaseInspectionAddress.cshtml` (new) | The `?section=inspection-address` body: recorded value with provenance, the Principal default when one is recorded, the inspection mode, and the editor. |
| `Pages/Cases/Shared/_CaseFiles.cshtml` (new) | The `?section=case-files` body: the documents panel plus the instruction-photograph and vehicle-image galleries, moved out of `Details.cshtml`. |
| `Pages/Cases/Shared/_CaseDataHiddenFields.cshtml` (new) | All twenty `CaseEditableData` values as hidden inputs — one list for both edit forms. |
| `Pages/Cases/Shared/_CaseDocuments.cshtml` | Restyled onto the design system; per-file custody chip, Preview, Save as; Add evidence and Open Operations on the panel head; upload requests as a table with a Withdraw-link dialog; explanatory copy deleted. |
| `Pages/Cases/Details.cshtml` (lane E1) | Section dispatch narrowed to four `<partial>` lines (net −116 lines); the Open Assessment gate no longer renders an empty `data-condition`. |
| `Pages/Cases/Shared/_CaseWorkflow.cshtml` (lane E1) | Two hidden inputs: the Overview save was clearing the claimant's contact number and address. |
| `Presentation/OperatorLabels.cs` | One appended nested `static class CaseWorkspace`; nothing existing reordered or edited. |
| `docs/design/test-ui/catalogue.json` | `Cases/Vehicle`, `Cases/Custody`, `Cases/Tasks` reclassified `redirect` → `protocol` with an accurate reason. No snapshot regeneration. |
| `CaseVehicleWebTests.cs`, `CaseCustodyWebTests.cs`, `CaseTasksWebTests.cs`, `CaseDetailsWebTests.cs` | Nine new tests plus fixture support. No existing assertion weakened, skipped or removed. |

## Two defects the work uncovered, both fixed

1. **Silent data loss on the product's main edit form.** `SaveCase` writes all
   twenty `CaseEditableData` members unconditionally
   (`EfCaseDataStore.ApplyEditableData:346–365`); an omitted value is written
   as null and clears the confirmed field. `_CaseWorkflow.cshtml` posted
   eighteen, so every Overview save discarded `claimantContactNumber` and
   `claimantAddress`. Two hidden inputs, pinned by
   `OverviewEditorAlsoPostsTheClaimantContactNumberAndAddress`.
2. **An empty gate pill on every enabled gated control (PLAT-061).** Razor
   omits a plain HTML attribute whose expression is `false`; it does **not**
   omit one whose expression is `null`. The `data-condition="@(cond ? null :
   "…")"` idiom therefore leaves the attribute present and empty, and
   `.gated::after`'s unguarded `content: attr(data-condition)` paints an empty
   pill. Fixed on `Details.cshtml`'s Open Assessment gate and pinned by
   `NoWorkspaceGateEverRendersAnEmptyCondition`, which asserts across five
   sections and both access states. Four further call sites are reported
   below; the one-selector root fix is PLAT-061's.

## Rule 14 (D20 strict) — every capability with its production caller

| Capability | Production caller |
| --- | --- |
| Vehicle section route | `Pages/Cases/Details.cshtml:311–314` → `Cases/Shared/_CaseVehicle.cshtml` |
| Refresh DVLA / Refresh DVSA-MOT | `_CaseVehicle.cshtml:67–96` → `VehicleModel.OnPostRequestVehicleLookupAsync` (`Vehicle.cshtml.cs:21`) |
| Accept vehicle suggestion | `_CaseVehicle.cshtml:151–166` → `VehicleModel.OnPostAcceptVehicleSuggestionAsync` (`Vehicle.cshtml.cs:43`) |
| Correct vehicle suggestion | `_CaseVehicle.cshtml:167–207` → the same handler with `decision=Correct` |
| Recorded checks list | `_CaseVehicle.cshtml:108–143`, from `CaseDetails.VehicleEvidence` (`CaseQueries.cs:357`) |
| Inspection address section | `Details.cshtml:315–318` → `_CaseInspectionAddress.cshtml` |
| Inspection address editor | `_CaseInspectionAddress.cshtml:78–100` → `DetailsModel.OnPostSaveAsync` (`Details.cshtml.cs:357`) |
| Case Files section | `Details.cshtml:307–310` → `_CaseFiles.cshtml` → `_CaseDocuments.cshtml` |
| Preview / Save as | `_CaseDocuments.cshtml:96–108` → `DownloadModel.OnGetAsync` (`Documents/Download.cshtml.cs:16`) |
| Remove file · Third-party vehicle | `_CaseDocuments.cshtml:113–117` + `:131–161` → `CustodyModel.OnPostRemoveDocumentAsync` (`Custody.cshtml.cs:71`), `OnPostConfirmThirdPartyVehicleEvidenceAsync` (`:95`) |
| Create upload request · Withdraw link | `_CaseDocuments.cshtml:171–182`, `:206` + `:216–232` → `Custody.cshtml.cs:119`, `:170` |
| Image gallery and viewer | `_CaseFiles.cshtml:68, 90` → `Shared/_ImageGallery` → `Shared/_EvidenceViewer` (rendered by `Details.cshtml:369`) |
| Notes entries, Add note, Record chase | `Details.cshtml:303–306` → `Cases/Shared/_CaseHistory.cshtml:49, 67` → `TasksModel.OnPostAddNoteAsync` (`Tasks.cshtml.cs:33`), `OnPostRecordManualChaseAsync` (`:169`) |

**Named with no caller — stated, not claimed.**

- **Vehicle History (`narrative.history_check`)**. No handler writes it
  anywhere in `src/Pegasus.Web` — the Assessment page has no field-save
  handler either. Drawing the textarea would be an inert control, so it is
  absent. [[CASE-029]] names "Vehicle checks state list and Vehicle History
  wired" in its own Owns and supplies it.
- **Inspection address "Previous values" select**. `CaseDataFieldEntity`
  (`CaseDataEntities.cs:23–38`) holds one row per (field, kind) with no
  superseded-value history, so there is nothing to list. Needs a Core store
  change; not drawn.
- **Correspondence rows**. `MailWorkspaceScope` (`RetainedMail.cs:25`) has no
  case filter and `IRetainedMailQueries` (`:371`) exposes no case-scoped list,
  so there are no linked messages to draw. Not drawn. MAIL-026 (wave 4,
  correspondence actions) is the natural owner of the query and the
  Compose/Reply/Forward controls the ticket already defers to wave 4.

## Disabled seams drawn

| Seam | Where | Shape | Ticket |
| --- | --- | --- | --- |
| Experian | `_CaseVehicle.cshtml:101–105` | `<span class="gated" data-condition="Experian is not connected"><button type="button" class="btn" disabled aria-disabled="true">Run Experian check</button></span>` | ENG-001 (D7/D22) |

Drawn, never claimed. `data-condition` is always set (PLAT-061).

Not a seam: the two refresh controls render disabled with
`data-condition="No registration recorded"` only when the case records no
registration, and enable as soon as one is recorded — legitimate state under
D21's second row.

## Verification

| Command | Result |
| --- | --- |
| `dotnet build ./Pegasus.slnx --configuration Release -nodeReuse:false` | exit 0 — Build succeeded, **0 warnings, 0 errors, 0 CS-diagnostics** |
| `dotnet test … --filter "FullyQualifiedName~CaseDetailsWebTests"` | exit 0 — **53 passed, 0 failed, 0 skipped** (42 before this branch + 11 new results: 9 `[Fact]` plus a 2-case `[Theory]`) |
| `dotnet test … --filter "FullyQualifiedName~ImageViewingWebTests\|FullyQualifiedName~ImageIntakeWebTests"` | exit 0 — **5 passed, 0 failed** (the two suites that assert on `?section=case-files`) |
| `dotnet test ./tests/Pegasus.ArchitectureTests/… --no-build` | exit 0 — **100 passed, 0 failed** |

Not run by this lane: the full suite, the `Browser` category, and every
snapshot or catalogue script. `TestUiSnapshotTests` is inert without
`PEGASUS_TEST_UI_MODE` and was not driven.

Two failures occurred during development and were fixed in the source, never
in the assertion: the empty `data-condition` (a real defect, above) and a
wrong download-route shape in a new test (`/Cases/{caseId}/Documents/
{occurrenceId}/Download`, not a query-string form).

## Out-of-scope findings, not touched

1. `Pages/Triage/Details.cshtml:202` and
   `Pages/Cases/Assessment/Index.cshtml:203, 225, 250` (through
   `ImportCondition`, `SendToClaudeCondition`, `ReportDraftCondition`, all
   `string?`) carry the same `? null :` gate idiom and paint the same empty
   pill when enabled. The one-selector root fix is a `[data-condition]` guard
   on `.gated::after` in `wwwroot/css/site.css` — PLAT-029's file, PLAT-061's
   ticket.
2. `Cases/Custody?handler=RetryCustody` (`Custody.cshtml.cs:25`) is the only
   consumer of `IRetryCaseCustody` and has no UI caller; Operations retries
   through a different use case (`RetryExternalWork`). The four task-CRUD
   handlers (`Tasks.cshtml.cs:61, 89, 117, 143`) are likewise UI-less because
   the approved prototype draws no task surface. Deleting them removes the
   only consumer of five Core ports and their DI registrations — a
   cross-layer removal that belongs to UIIMP-009 (wave 5).
3. `_CaseWorkspaceNav.cshtml:10` still declares the six-section list inline
   rather than on `DetailsModel` — [[CASE-012]]'s recorded "one list per
   concept" breach, untouched as its scratch/salvage note directs.
4. Both edit forms render whenever edit authority is held, while Core refuses
   `SaveCase` once an Engineer is assigned or the case is past Review
   (`EfCaseDataStore.SaveAsync:191–197`). Risk accepted with a reason: gating
   only the new form would put one Core precondition in two places.
5. `"Not recorded"` is a literal in four view files. Recorded for the
   simplification wave; hoisting it while two sites are lane E1's would add a
   spelling rather than remove one.
