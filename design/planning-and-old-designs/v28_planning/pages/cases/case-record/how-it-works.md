Read from the live source on 18 September 2026.

## What this page does not show

- No page header, eyebrow or H1 outside the sticky ribbon: the ribbon's own
  `<h1>` (the Case reference) is the page's one heading
  (`Details.cshtml`, `<h1 class="ribbon-value">`).
- No separate "Assessment" page or ribbon: `Assessment/Index.cshtml` is a
  permanent redirect into this page's Estimate section (see below).
- No generic "Close" action outside the named, state-gated adverse
  disposition chooser — there is no terminally closed Case state at all
  (`CaseWorkspaceLabels` review-point-12 comment; `CONTEXT.md` Completed/Query).
- No Assessment-page-style per-field save buttons: every editable value in
  every section (Overview through Notes bar excepted) posts through the one
  `#case-edit-form` the Overview section renders, or an immediate lease-carrying
  post for actions that must survive outside that form (lookups, valuation
  cards, estimate lines, Glass's, tags).

## Source table

| File | What it owns |
| --- | --- |
| `Details.cshtml` | The sticky block (ribbon + section row), the `<article class="record case-record">` wrapper, the ten sections' dispatch loop, the aside (Figures, Next action), the viewer and dialogs hosts. |
| `Details.cshtml.cs` (+ `CaseMutationPageModel`) | `OnGetAsync`/`OnGetSectionAsync`, the estimate/valuation/report-generation handlers, `Section`/`SectionIsDeferred`, `AssessmentEditorValue`, `EditorTotals`, closure-outcome probing. |
| `Details.Frame.cs` | `IsEditing`, `ColleagueIsEditing`, `OwnLeaseHeldElsewhere`, `SectionIsEditable`, `SectionAvailability`, `SectionOffersEdit`, `StateChipText`, `CaseTypeChip`, `WorkingSetGlyph`, `CanCreateAudit`, `NextAction`, `RepairCostIncVat`, the Audit/Original Case links. |
| `Details.Files.cs` | The Files section's supporting projections (custody state, document/image lists). |
| `Details.Report.cs` | Report generation/delivery handlers (`GenerateReport`, `GenerateFeeNote`, `PrepareReportDelivery`, `SendPreparedReport`). |
| `Details.Valuation.cs` | `GetValuation`, `SaveValuation`, `ApplyValuation`, `PreviewValuation`, `StartMarketResearch`. |
| `Shared/_CaseOverview.cshtml` | The stepper, outstanding requirements, Lifecycle actions sub-panel, the Case/Principal/Claimant fact columns, the Notes band, the accident band, `#case-edit-form` itself. |
| `Shared/_CaseInspectionAddress.cshtml` | Inspection type/date, the Inspect-at chooser and recorded address, the Repairer and Storage sub-panels. |
| `Shared/_CaseVehicle.cshtml` | The vehicle identity grid, the DVLA/MOT lookup line and control, the Experian seam, mileage & condition, vehicle history. |
| `Shared/_CaseDamage.cshtml` | The Plan clicker SVG, the recorded-zones list, tyres & belts, unrelated damage, the derived narrative. |
| `Shared/_CaseValuation.cshtml` + `_CaseValuationLines.cshtml` | The guide-source entry cards, the calculator, the applied-history block, the printed calculation lines. |
| `Shared/_CaseEstimate.cshtml` | Head controls (Import, Glass's/Resume, Send to AI, More, Expand), the Glass's session line, Use/Duplicate/Discard, the header grid, the line grid, discount/VAT bars, the three work-lists and the rollup, plus its own Import/Compare/Delete/Send-to-AI dialogs. |
| `Shared/_CaseSettlement.cshtml` | The figures strip, the Decisions strip (with the Proposed column), the excess/betterment cells, costs/hire/delays, the seven salvage fields. |
| `Shared/_CaseReport.cshtml` + `_CaseReportImagePreparation.cshtml` | Generate report/fee note, the preview card and generation facts, delivery preparation and send, the report fields, images-in-report strip, and the preparation cards. |
| `Shared/_CaseFiles.cshtml` + `_CaseDocuments/_CaseImages/_CaseCorrespondence/_CaseImageTagPicker.cshtml` | The Documents/Images/Correspondence tabs, Add evidence, custody chip, each file row, the image grid with tag chips, and the tag picker disclosure. |
| `Shared/_CaseHistory.cshtml` | The Case note form, Record chase, and the one Notes timeline (operator notes and system events together). |
| `Shared/_CaseViewer.cshtml` | The full-screen viewer: title/tag/position, Rotate, Zoom, Crop, Download, In report, the filmstrip, and the crop-tool control set. |
| `Shared/_CaseDialogs.cshtml` | Every frame-level dialog: Hold/Release Hold, Mark completed, Return to Review/Engineer, Unlink report evidence, Correct principal, Archive, Close case, Hand to Engineer, EVA handoff, Mark report sent, Create audit. |
| `Shared/_CaseSectionHeadTools.cshtml` | The section-head Edit button, the one availability sentence, and the fold chevron — the one partial every section's head includes last. |
| `Shared/_EvaHandoff.cshtml` | Sign-off Engineer selection, the automatic-failure notice, Export EVA ZIP, Send via API — shared between the dialog and the standalone page. |
| `Shared/_ReadinessHiddenFields.cshtml` | The `instructionsComplete`/`imagesComplete`/`evidenceReference` envelope every readiness-gated transition form posts; carries no visible markup. |
| `Eva/Send.cshtml` | The real standalone `/Cases/{id}/Eva/Send` page: its own header, the last-submission summary, and `_EvaHandoff` again. |
| `Presentation/OperatorLabels.cs` (`CaseWorkspace`) | Section list/order, ribbon labels, Sign-off Engineer/EVA labels, the closure vocabulary, `AbsentValue`. |
| `Presentation/CaseWorkspaceLabels.cs` | Every other Case-record-only label: `Frame` (ribbon/Actions/section-head words), Damage, Settlement, Report, Estimate, Vehicle, Inspection, Valuation, Viewer, Files, ImageTags, GlassSession, ReportDelivery, ReportImages. |

## Behaviours found

### The page-wide edit session and per-section locking
One lease-carrying session covers the whole record. `DetailsModel.IsEditing`
(`Details.Frame.cs`) is true only when this browser holds
`LeaseToken`; `CanEditCaseData` additionally requires the Case not be
`PostReportComplete`/`Query` (`IsPostReportReadOnly`) and not archived.
`SectionIsEditable(key)` (`Details.Frame.cs`) splits sections into the five
Engineer sections (Damage, Valuation, Estimate, Settlement, Report — gated by
`CanEditEngineering`, itself `CanEditCaseData && AssessmentCanOpen &&
!AssessmentIsReadOnly`) and everything else (`CanEditCaseData`). A section
that cannot edit inside an open session carries the CSS class `is-locked`
(`.is-editing .is-locked .fc .fv{display:flex}` in `site.css`), which
reopens the read state even though the record itself is editing —
the "one geometry" read/edit contract (`.fc`'s `.fv`/`.fi` pair) never
needs a second markup path.

### Section availability and section-head Edit
`SectionAvailability(key)` (`Details.Frame.cs`) states one of two sentences:
a colleague's name plus "is editing" when `ColleagueIsEditing`, or
`CaseWorkspaceLabels.Frame.ReturnToEngineerToEdit` when this viewer's own
open session cannot edit the section because the Case is post-report
read-only. `SectionOffersEdit(key)` (`Details.Frame.cs`) draws the
section-head Edit pencil only outside any edit session, on a Case this
viewer could edit, and never for `files` or `notes` (their controls post
immediately instead).

### The Actions menu's exact membership
`Details.cshtml`'s `offersActionsMenu`/`canX` locals compute, per item:
`canHandToEngineer` (editing, Review, Engineer options exist),
`canSendToEva` (the Principal's `ReportGenerationPolicy` is `EvaZip` or
`EvaManualApi`, or `CanRetryAutomaticFailure`, and the state is Review,
Report preparation or Post report — offered **outside** an edit session too,
the one item that is), `canConfirmReportSent` (editing, Report preparation,
detected Sent evidence exists), `canMarkCompleted` (editing, Post report),
`canReturnToReview` (editing, Report preparation or Post report),
`canReturnToEngineer` (editing, Completed or Query), `canArchive` (editing,
terminal, not yet archived), `offersHoldGroup` (editing, not terminal),
`offersCreateAudit` (`CanCreateAudit`, `Details.Frame.cs`), and
`offersAdverseClosure` (editing, `AvailableClosureOutcomes.Count > 0`).
Close case is separated by a divider and rendered in red
(`btn--danger`) — Review point 12's explicit rule that an adverse
disposition is not a progression step.

### The lifecycle stepper and state chip
`_CaseOverview.cshtml` maps `CaseLifecycleState` to one of four stepper
stages (`0` Not ready/Held/closed, `1` Review, `2` Report preparation/Post
report, `3` Post-report complete/Query) — the display vocabulary folds two
Core states into one visible stage apiece. `StateChipText`
(`Details.Frame.cs`) appends `" · review on {date:d MMM}"` only when Held
and a `HoldReviewOn` date is set.

### Create audit
`CanCreateAudit` (`Details.Frame.cs`) requires: Case type Inspection + Audit,
no existing Audit Case link, no existing Original Case link (i.e. this Case
is not itself already an Audit), not Created in error, not archived, a
report has been generated (`HasGeneratedReport`), and the page-wide edit
session open. The dialog derives its proposed reference from
`AuditIdentity.Create` once an outcome is recorded (`ProposedAuditReference`).

### The Glass's slot and session line
`CanLaunchGlass`/`CanResumeGlass`/`CanCloseGlass` (grepped in
`Details.cshtml.cs`) gate one of Glass's/Resume in the Estimate head; the
session line (`_CaseEstimate.cshtml`) is shown while `GlassSessionElsewhere`
is set (another Case holds the account) or while this Case's own session
occupies the account or has failed, with a guarded Close (a required reason
plus an explicit "Glass's is closed and no estimate remains open"
confirmation) offered only while `CanCloseGlass`.

### PendingEstimateSources
`Model.PendingEstimateSources` (`Details.cshtml.cs`) lists retained
estimate-import files whose SHA-256 does not match any recorded estimate
version yet — the "not yet imported" state a Glass's Waiting session or a
plain upload can leave behind; `CompleteEstimateImport` turns one into a
Draft.

### The Actions-menu-vs-dialog duality for Send to EVA
`canSendToEva`'s control in the ribbon is a real `<a asp-page="/Cases/Eva/Send">`
carrying `data-dialog-open="eva-handoff-dialog"` — the no-script destination
and the enhancement trigger are the same element (`Details.cshtml`).

## Vehicle / Workflow / Tasks / Closure / Custody finding

All five (`Vehicle.cshtml`, `Workflow.cshtml`, `Tasks.cshtml`,
`Closure.cshtml`, `Custody.cshtml`) are **not** the Scroll no-script
fallback and **not** standalone reachable pages. Each `.cshtml` carries only
`@page "/Cases/{id:guid}/<Name>"` and `@model …Model` with no body markup,
and each `.cshtml.cs`'s `OnGet()` returns `NotFound()` outright — confirmed
by reading all five page models. They exist purely as POST-only mutation
endpoints (`CaseMutationPageModel` subclasses) that the Case record's own
forms and dialogs target by `asp-page`, and every handler redirects back to
`/Cases/Details`:

- `Vehicle.cshtml.cs` — `OnPostRequestVehicleLookupAsync` (the Vehicle
  section's DVLA/MOT lookup form).
- `Workflow.cshtml.cs` — Hold/Release Hold, ReturnToReview, AssignEngineer,
  AssignToMe, SetSignOffEngineer, CreateLinkedReplacement (Correct
  principal).
- `Tasks.cshtml.cs` — AddNote, RecordManualChase, LinkReportEvidence,
  UnlinkReportEvidence.
- `Closure.cshtml.cs` — RecordReportApproval, Close, ReturnToEngineer,
  Archive.
- `Custody.cshtml.cs` — RetryCustody, RemoveDocument, MarkAsOriginalReport,
  Tag/UntagImage, CreateImageTag.

The record's own Scroll no-script fallback is implemented entirely inside
`Details.cshtml`/`Details.cshtml.cs` via the `Section`/`?section=` query and
`OnGetSectionAsync` — a separate mechanism from these five pages, not
related to them.

`Assessment/Index.cshtml` is a **live redirect only**
(`RedirectPermanent($"/Cases/{id:D}?section=estimate")`) kept so old links
land on the Estimate section — the Engineer workbench it used to serve has
moved into this record (its own doc comment: "The Engineer workbench moved
into the Case record (D30)"). `Assessment/Suggestions.cshtml` carries no
`@page` directive at all, per its own header comment, and is out of this
lane's scope (`image_intake`/deferred-surfaces territory); it is not
captured here.

`Eva/Send.cshtml` **is** a real, independently `OnGetAsync`-routed page at
`/Cases/{id}/Eva/Send` with its own header, back link and last-submission
summary — the Actions menu's Send to EVA control both links to it directly
(the no-script path) and enhances it into `eva-handoff-dialog` (the
same `_EvaHandoff` partial either way). Captured as `cr-view="evasend"`.

## Things the FRD does not settle here

- The exact seam behind `AssessmentCanOpen`/`AssessmentIsReadOnly` (which
  Core policy or role rule decides when the Engineer sections are
  assessable) was not traced beyond `Details.cshtml.cs`'s two `??`
  fallbacks reading an injected `assessmentAccess` service. See the page
  README's Notes for how this capture approximates it.
- The exact per-outcome `CaseLifecycleRules.RequireClosureIsAllowed` gates
  behind `AvailableClosureOutcomes` live in Core and were not opened; this
  capture offers the whole adverse-closure group whenever the session is
  open and the Case is not already closed, rather than a traced per-state,
  per-outcome list.
