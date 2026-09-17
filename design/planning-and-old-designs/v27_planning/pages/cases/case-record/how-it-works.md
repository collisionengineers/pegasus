# Case record — how it works

Read from the live source on 16 September 2026 (`origin/dev` at `5765a527a`).
The baseline mockup in [`../../../current/`](../../../current/README.md) is this
page reproduced; the numbers in brackets are its screenshots.

## What the page does not show

- Who else has the record open: only a live edit lease surfaces ("E Mawdsley
  is editing"); readers are invisible to each other.
- When editing will next be available: a non-holder is told who is editing
  and never given a time (FRD-01).
- The AI job list, Operations failures and Action logs for this Case: they
  are on Operations and Administration. Only a Draft-ready job reaches the
  record, as a row on Next action.
- The Sent-item that proves Report sent: the Overview shows mailbox and time
  once linked; the item itself is on the Inbox message.
- Any percentage of completeness: outstanding requirements are named items
  (D23).
- The report PDF itself: Preview draft and the generated artifacts open in
  the page viewer or a new tab; the record shows the preview card and the
  generation facts.

## Governing documentation

| Document | What it settles for this page |
| --- | --- |
| [FRD-12 § Case workspace](../../../../../../docs/frd/frd-12-operator-experience.md#case-workspace) | the one record page, ribbon and section row, the ten sections and their contents, the Actions menu, edit mode and the aside |
| [FRD-12 § Assessment](../../../../../../docs/frd/frd-12-operator-experience.md#assessment) | the Engineer sections as sections of the record, Report position, the estimate set, raw estimate import as a whole-page drop |
| [FRD-12 § Record edit ownership](../../../../../../docs/frd/frd-12-operator-experience.md#record-edit-ownership) | one lease per record, who sees what while it is held |
| [FRD-01 § Case edit authority and recovery](../../../../../../docs/frd/frd-01-case-identity-and-lifecycle.md#case-edit-authority-and-recovery) | the server-owned expiring lease, no take-over, no forced merge, a save needs no reason |
| [FRD-01 § Workflow display labels and stage-bound actions](../../../../../../docs/frd/frd-01-case-identity-and-lifecycle.md#workflow-display-labels-and-stage-bound-actions) | Not ready · Review · With Engineer · Completed · Query, Held; which action is offered in which stage; Close case |
| [FRD-01 § Sign-off Engineer](../../../../../../docs/frd/frd-01-case-identity-and-lifecycle.md#sign-off-engineer) | the Sign-off Engineer field and its default |
| [FRD-06](../../../../../../docs/frd/frd-06-vehicle-and-engineering-evidence.md) | inspection address, vehicle lookup, damage record (23 regions, derived location and severity), valuation sources and the Apply order, settlement fields, canonical repair specifications, Glass's sessions |
| [FRD-11](../../../../../../docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md) | outcomes, report generation entry point, correction and finality, reviewed AI proposals on the decision fields, the AI job list |
| [FRD-05](../../../../../../docs/frd/frd-05-documents-extraction-and-custody.md) | custody states, image tags, the Audit Case's shared documents |
| [FRD-08 § Outbound correspondence](../../../../../../docs/frd/frd-08-email-mailbox-and-background-processing.md#outbound-correspondence) | Compose and the report send through an approved mailbox; Sent evidence |
| [ADR-0051](../../../../../../docs/adr/0051-linked-audit-case-identity-and-custody.md) | Create audit, the `a.`/`ap.` reference and the link between the two Cases |
| [docs/design/README.md](../../../../../../docs/design/README.md) | tokens, breakpoints, the capture rule |
| [CONTEXT.md](../../../../../../CONTEXT.md) | reserved terms |

## Source

| Layer | File | Owns |
| --- | --- | --- |
| Web | `Pages/Cases/Details.cshtml` | the frame: notices, ribbon, Actions menu, section row, stale bar, the ten sections, the aside |
| Web | `Pages/Cases/Details.cshtml.cs`, `Details.Frame.cs`, `Details.Files.cs`, `Details.Report.cs`, `Details.Valuation.cs` | every handler; `IsEditing`, `CanEditCaseData`, `CanEditEngineering`, `SectionAvailability`, `SectionOffersEdit`, `NextAction`, `CanCreateAudit`, `StateChipText` |
| Web | `Pages/Cases/Shared/_CaseSectionHeadTools.cshtml` | Edit, the availability sentence and the collapse chevron on every head |
| Web | `Pages/Cases/Shared/_CaseOverview.cshtml` … `_CaseHistory.cshtml` | one partial per section |
| Web | `Pages/Cases/Shared/_CaseDialogs.cshtml`, `_EvaHandoff.cshtml`, `Shared/_ReasonDialog.cshtml` | the frame's dialogs |
| Web | `Pages/Cases/Shared/_CaseViewer.cshtml`, `_CaseImageTagPicker.cshtml`, `_CaseReportImagePreparation.cshtml` | viewer, tag picker, preparation cards |
| Web | `Presentation/CaseWorkspaceLabels.cs`, `OperatorLabels.cs` | every label string; `Sections` (keys, labels, icons) |
| Web | `Presentation/DamagePlanGeometry.cs` | the Plan clicker's paths and markers |
| Web | `wwwroot/css/site.css`, `case-workspace.css`; `wwwroot/js/site.js`, `case-workspace.js` | the geometry (`.fc` read/edit cells, `.is-editing`, `.is-locked`, sticky block), the behaviours (menus, dialogs, collapse, tabs, lease heartbeat, fragments, viewer) |
| Core | `Assessment/AssessmentContracts.cs` | the vocabulary: field paths, types, codes, `DamageZones`, `DamageSeverities` |
| Core | `Cases/`, `Lifecycle/`, `Workflow/` | `CaseLifecycleRules`, `CaseEditAuthority`, `CreateAuditCase`, the workspace save |
| Core | `Reports/` | `CaseReportGenerationState`, `ReportRepairCosts`, delivery preparation |

## Behaviours

### One page, one lease

`/Cases/{id}` renders the whole record. Edit Case claims the record's one
lease and every section's controls appear in place of its values: the cell
keeps its position, so entering edit never moves the page (`.fc .fv` / `.fi`,
02). Save posts the one `#case-edit-form` every section's controls join;
Cancel releases the lease, asking first when something changed. A colleague's
lease renders no control anywhere; the chip and every section head name them
(09). In Completed and Query the button reads "Enable return": the session
exists only to offer Return to Engineer, and every section stays locked with
"Return the Case to the Engineer to edit" (07).

### Stage-bound actions

One Actions menu, built from Core's rules for the state (10): Hand to
Engineer in Review; Send to EVA in Review or With Engineer without a lease;
Mark report sent while detected Sent evidence exists; Mark completed in Post
report; Return to Review; Return to Engineer in Completed or Query; Archive
case once closed; then Place on Hold or Release Hold, Create upload link,
Correct principal and Create audit; then Close case in red. Outside a session
the menu shows only when Send to EVA is available.

### The Engineer sections

Damage, Valuation, Estimate, Settlement and Report edit only With Engineer
and only for an Engineer or Administrator; in every other session they read
with "Available With Engineer" in the head (04, 52). Case data (Overview,
Inspection, Vehicle) edits in any open session.

### Sections

- **Overview**: stepper, Held exception, outstanding requirements (Not ready
  and Held), Lifecycle actions, the Case / Principal / Claimant columns with
  locked identity cells, Case contact, the Notes band (record notes locked
  beside this Case's notes), Accident circumstances and Notes from client.
- **Inspection details**: type, date, Inspect at (the choice fills the address
  and the Principal default shows only while image based), Repairer with the
  directory link, Storage with per-day and recovery figures (engineering
  cells).
- **Vehicle**: the lookup line and Look up DVLA & MOT in the head, Experian as
  a gated seam; identity cells with provenance; the lookup's own rows; Mileage
  & condition; Vehicle history prose.
- **Damage**: the Plan clicker, derived Impact location and severity, the
  recorded zones list with per-zone severity and note, tyres and belts,
  material transfer, unrelated damage and deduction, the derived narrative.
- **Valuation**: Valuation month and AI market research above the cards;
  Glass's, Brego and Super CAP entry cards while editing; Cazana as a seam;
  the calculator (previous total loss, condition deduction, commercial VAT,
  value increases), Core's lines, Apply as Engineer's Value, the applied
  history.
- **Estimate**: tabs per version with route and state; the Glass's session
  line; Retained estimate sources; Use estimate / Duplicate / Discard; the
  editable Draft with the header grid, the phantom blank line, Add line,
  discounts, VAT categories with Overridden and Reset; the read summary line
  for other versions; three work-lists and the rollup.
- **Settlement**: the figures strip; the Decisions strip (Outcome, Engineer's
  Value read-only with a Valuation link, Salvage category and value for Total
  loss, Roadworthiness, the reason when Unroadworthy) with the Proposed column
  and Accept while an AI proposal awaits; Excess and Betterment; Costs, hire &
  delays with the derived storage charge and repair days; the Salvage panel.
- **Report**: Generate report or the "Report not ready" gate; More; the
  preview card and artifacts; generation facts and the stale notice; Reviewed
  recipients → Prepare delivery → Send prepared report; Sign-off Engineer,
  Report date, fee, comments, the three content switches, the valuation
  commentary text, the statement of truth; Images in report and, while
  editing, Report image preparation.
- **Files**: the custody chip, Add evidence, More; Documents, Images
  (tiles with tags, Tag picker and Crop while editing; placeholders while
  custody is pending; intake photograph groups) and Correspondence (Compose
  and the retained e-mails); Public upload requests.
- **Notes**: Add Case note without a lease; Record chase while a chase is
  scheduled; the merged timeline.

## Things the FRD does not settle

- FRD-12 § Case workspace describes Files as "one panel with two tabs"; the
  live section has three (Documents, Images, Correspondence with Compose and
  the query e-mails, CASE-009 / PR 753). The Correspondence tab belongs in
  FRD-12 when v27's FRD edits are written.
- The Case contact sub-panel (contact name, e-mail, phone — CASE-027) and the
  Public upload requests table on Files are not described in FRD-12's section
  list.
- "Enable return" as the ribbon's label in Completed and Query, and the
  read-only reason "Return the Case to the Engineer to edit", are page
  wording FRD-12 does not name.
- The Retained estimate sources sub-panel and "Complete import" are named in
  FRD-06 § Canonical repair specifications; FRD-12 § Assessment does not
  mention them.
- The stale bar under the section row (a generation went stale) and the
  Next action's readiness line ("… · 2 more") are page behaviours with no FRD
  sentence.
