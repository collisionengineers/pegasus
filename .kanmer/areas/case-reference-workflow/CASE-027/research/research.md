# CASE-027 research — the four Case workspace section views

Read: `AGENTS.md`, `docs/design/README.md` (§Voice, §No explanatory copy,
§Absent versus disabled, §Component map, §Case workspace), the three refs
(`frd-12`, `frd-01`, `frd-05`), `EPIC-011/context.md` §1.8, `waves.md`,
and all four decision documents (D15–D26). [[CASE-012]]'s ticket, both
post-implementation reports and `scratch/salvage.md`.

## Premises verified by a read-only check (all against the worktree at `55e23b02`)

| # | Premise | How checked | Result |
| --- | --- | --- | --- |
| 1 | `Vehicle.cshtml`, `Custody.cshtml`, `Tasks.cshtml` are POST-only shells | `cat`, `grep -n "OnGet\|OnPost"` | **Verified.** Each `.cshtml` is 2 lines (`@page` + `@model`); the models expose only `OnPost*` handlers. A GET renders an empty 200. |
| 2 | `Details.cshtml:405` links "Open vehicle record" to `/Cases/{id}/Vehicle` | read `Details.cshtml` 398–411 | **Verified — and it is a defect.** That route renders nothing (premise 1). The Vehicle section is today a panel whose only control leads to a blank page. |
| 3 | `IRequestVehicleLookup` and `IAcceptVehicleSuggestion` have no production caller | `grep -rl` over `src/Pegasus.Web/**/*.cshtml` for both handler names | **Verified: zero callers.** `git show 2204117a^:…/_CaseWorkflow.cshtml` shows PR #599 removed the three forms that called them. Rule 14 / D20 breach inherited by this lane. |
| 4 | `CaseDetails.VehicleEvidence` is populated for the workspace | `CaseQueries.cs:357` (`VehicleEvidence = vehicleEvidence`), `IVehicleEvidenceQueries` registered `DependencyInjection.cs:243` | **Verified.** Confirmed values, latest observation, observation list and confirmation history are all already on the page model's `Model.Case`. No new query or DI change needed. |
| 5 | `narrative.history_check` has no write handler anywhere in Web | `grep -rn "HistoryCheck\|history_check" src/Pegasus.Web/` → no hits; `grep -n "OnPost" Assessment/Index.cshtml.cs` → no field-save handler | **Verified.** A "Vehicle History" textarea here would be an inert control. [[CASE-029]] names "Vehicle checks state list and Vehicle History wired" in its own Owns. |
| 6 | "Previous values" for the inspection address has no backing store | `CaseDataEntities.cs:23–38` — `CaseDataFieldEntity` is one row per (field, kind); no superseded/history column. `EfCaseDataStore.SetConfirmed` overwrites | **Verified: no Core query exists.** Rendering the select would be fabrication. |
| 7 | Correspondence rows for a case have no backing query | `MailWorkspaceScope` (`RetainedMail.cs:25`) has no `CaseId` filter; `IRetainedMailQueries` (:371) exposes list-by-scope, count, get-by-id, mailboxes, poll health only | **Verified: no case-scoped mail query.** `RetainedMailSummary` carries `CaseId` but nothing filters on it. |
| 8 | `SaveCase` clears any editable value the form omits | `EfCaseDataStore.ApplyEditableData` :346–365 calls `SetConfirmed` for all 20 `CaseEditableData` members unconditionally | **Verified.** |
| 9 | The Overview edit form omits two of those 20 | `_CaseWorkflow.cshtml` :166–184 posts 12 hidden + 6 visible = 18; `claimantContactNumber` and `claimantAddress` are absent | **Verified — silent data loss** on every Overview save. See the plan's disposition. |
| 10 | The model binder takes the *first* posted value for a duplicated name | the repository's own documented convention: `_CaseWorkflow.cshtml:136` ("trailing hidden false") and `CaseMutationPageModel.cs:462` ("the model binder reads the first entry") | **Verified as an existing convention**, so a visible input placed before a hidden block of current values wins. |
| 11 | The evidence viewer already provides Rotate view + Save as | `Pages/Shared/_EvidenceViewer.cshtml:20–26` (`data-evidence-download` "Save as", `data-rotate` "Rotate view") driven by `[data-evidence-set]`/`[data-evidence-item]` | **Verified.** Nothing new to build for §1.8's viewer clause; `_ImageGallery` already emits the trigger attributes. |
| 12 | `CaseFiles.Live` returns only custody-**Confirmed** versions | `DocumentContracts.cs:91–109` | **Verified.** A per-row custody chip is honest but constant; the case-level folder state is the varying one. |
| 13 | `_CaseHistory.cshtml` already delivers the Notes view this ticket describes | read the file: `panel`/`notes-list`/`note-entry`, Date + Clock + `ActorDisplayName`, `Tasks/AddNote`, `Tasks/RecordManualChase` | **Verified.** Notes needs no change in this lane; it is E1's file and is already on the design system. |
| 14 | `Custody.cshtml`, `Tasks.cshtml`, `Vehicle.cshtml` carry a false `redirect` classification | `docs/design/test-ui/catalogue.json` — reason "Compatibility route redirects to the canonical case detail surface." | **Verified.** [[CASE-012]] round 2 fixed the identical text on `Workflow.cshtml`/`Closure.cshtml` to `protocol` and handed these three to this lane by name. |
| 15 | PR #615 (CASE-012 round 3) is merged | `gh pr view 615` → `MERGED` 2026-08-29T09:15:14Z | **Verified.** `CaseDetailsWebTests.cs` has no open PR against it. |
| 16 | `IRetryCaseCustody` and the four case-task ports have no UI caller | `grep -rl` for the handler names in `*.cshtml`; `grep -rl` for the interfaces in `src/` | **Verified.** `Cases/Custody?handler=RetryCustody` and `Cases/Tasks` Create/Assign/Complete/Cancel are the only consumers and nothing renders them. Operations retries through a different Core use case (`RetryExternalWork`), not `IRetryCaseCustody`. |
| 17 | Every design-system class this port needs already exists | `grep -n` over `wwwroot/css/site.css` for `panel`, `panel-head`, `panel-body`, `checks-grid`, `lookup-card`, `document-list`, `document-row`, `document-icon`, `gallery`, `viewer-stage`, `fact-grid`, `fact`, `definition-list`, `definition`, `status`/`status--*`, `gated`, `prov`, `blockhead`, `table-wrap`, `notes-list`, `empty` | **Verified — no new CSS.** `site.css` is PLAT-029's file and is not touched. |

## Premises assumed (not checked)

- The Test UI snapshot corpus under `docs/design/test-ui/pages/` will be
  regenerated by the orchestrator once per merge; this lane does not run
  `Update-TestUiSnapshots.ps1` (`decisions-2026-08-29.md`, "Two shared files").
- No lane currently in flight edits `Pages/Cases/**` — taken from the
  orchestrator's own in-flight list (PLAT-025/026/027, PLAT-049, ENG-027,
  DELIV-034, INTK-047, DELIV-036). Not independently checkable from here.
- Visual behaviour at 1580/1100/760 is inherited from the existing
  design-system classes; no browser walk is run in this lane.

## What the frame already provides ([[CASE-012]], PRs #599 and #615)

`Details.cshtml` owns the page header, identity ribbon, presence strip, action
bar, sticky edit bar, `_CaseWorkspaceNav`, the context column and every
lifecycle dialog. Section selection is `DetailsModel.Section`
(`Details.cshtml.cs:68`), and `Details.cshtml:303–427` is a five-way branch
that renders each section's body. `case-files` already renders
`_CaseDocuments` plus two galleries inline; `notes` renders `_CaseHistory`;
`vehicle` renders the dead-link panel of premise 2; `valuations` and
`inspection-address` render a bare `panel-head` with no body.

[[CASE-012]]'s own report assigns the `_CaseVehicle` and `_CaseFiles` partials
to "lane E2" by name, and `_CaseWorkspaceNav.cshtml:3–5` says "the non-Overview
sections are owned by later lanes (CASE-027, wave 4); the link is the frame's
part and the section body is delivered by theirs."

## What §1.8 requires, against what exists

| Contract clause | Backing available today | Decision |
| --- | --- | --- |
| Vehicle: Registration, Make, Model, Year, Mileage, Mileage source | `Data.Vehicle.*` (case data, with provenance) + `VehicleEvidence.LatestObservation.Vehicle.ManufactureYear` for Year | Render; Year only when an observation carries it |
| Vehicle checks: Refresh DVLA / Refresh DVSA-MOT | `Cases/Vehicle?handler=RequestVehicleLookup` — one handler, contract says "the same lookup" | Render both, both posting that handler |
| Vehicle checks: Run Experian check | none — uncomposed integration, ENG-001 | D7/D22 disabled seam with `data-condition` |
| Vehicle checks: state list | `VehicleEvidence.Observations` (outcome, provider, retrieved-at, failure) | Render with `_StatusChip` |
| Vehicle: accept/correct the suggestion | `Cases/Vehicle?handler=AcceptVehicleSuggestion` — orphaned since PR #599 | Restore (premise 3); this is the only surface for the port |
| Vehicle History textarea | no write handler (premise 5) | **Absent**, reported; [[CASE-029]] owns wiring it |
| Inspection address: recorded value, provider default | `Data.Inspection.Address` slots + `CaseDataSourceKind.ProviderSetting` | Render |
| Inspection address: previous values select | no store (premise 6) | **Absent**, reported |
| Inspection address: Edit → input + Cancel/Save | `Cases/Details?handler=Save` | Render, posting all 20 editable values |
| Case Files: document rows, custody chip, Preview, Save as | `Details.Documents` + `Cases/Documents/Download` (`inline=true` / plain) | Render |
| Case Files: Add evidence → `/Upload`, Open Operations | existing routes | Render |
| Case Files: image gallery + viewer with rotate | `_ImageGallery` + `_EvidenceViewer` (premise 11) | Reuse unchanged |
| Case Files: upload requests | `Details.RequestUploadLinks` + `Cases/Custody` create/revoke | Render |
| Case Files: correspondence rows | no case-scoped query (premise 7) | **Absent**, reported |
| Notes | `_CaseHistory` already delivers it (premise 13) | No change |
