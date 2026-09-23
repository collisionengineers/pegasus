# Case record — how it works today

Read from the live source on 23 September 2026 (origin/dev 446c3ce2f).

This covers only what the v29 round touches: the ribbon, the section row and
its Scroll/Tabs switch, section order, the Actions menu, the Report section's
generation and sent evidence, the post-report states, and what an Audit Case
adds. Paths are under `src/Pegasus.Web/` unless stated.

## What this page does not show

- No control that changes the Case type. The ribbon chip and the Overview's
  Case type cell only read it (`DetailsModel.CaseTypeChip`;
  `Shared/_CaseOverview.cshtml`, a `.ro` cell).
- No Case type chip on a plain Inspection: `CaseTypeChip` returns null for
  `CaseType.Inspection`.
- No sign, outside an edit session, that Create audit exists.
  `CanCreateAudit` requires `IsEditing`, so the item and its dialog are not
  rendered until Edit Case is pressed.
- No audit work on an Inspection + Audit Case itself. The audit happens on the
  separate Audit Case that Create audit makes; the Inspection + Audit Case
  only links to it.
- No Original report section, link or `?section=original-report` target on a
  Case that is not of type Audit. `DetailsModel.Section` turns that key back
  into Overview.
- No Scroll/Tabs switch without script. It is rendered `hidden` and
  `case-workspace.js` unhides it.
- No report approval control. `ClosureModel.OnPostRecordReportApprovalAsync`
  exists (`Pages/Cases/Closure.cshtml.cs`), but no form in
  `Pages/Cases/**/*.cshtml` posts to it.
- No linked Triage. Nothing in `Pages/Cases/Details*` or
  `Pages/Cases/Shared/` names a Triage.

## Governing documentation

| Document | What it settles for this page |
| --- | --- |
| [FRD-16](../../../../../../docs/frd/frd-16-case-record-workspace.md) | One page at `/Cases/{id}`; the ribbon (state and Case type chips, the links between an Audit Case and its original); the section row with Refresh and the Scroll/Tabs switch; Scroll the default, a Tabs choice kept for the browser session and painted by the server; section order with Original report on an Audit Case only; the Actions menu and its members; Original report missing and Mark as original report. |
| [FRD-01](../../../../../../docs/frd/frd-01-case-identity-and-lifecycle.md) | The three Case types and their references; standalone Audit and Original report missing; Inspection + Audit and Create audit (Actions menu, inside an edit session, once a report has been generated); a second Create audit is refused. |
| [FRD-13](../../../../../../docs/frd/frd-13-case-lifecycle-and-workflow.md) | States and labels (With Engineer is two internal states); Mark report sent needs exact Sent evidence; Mark completed; Return to Engineer; Close case; report approval names one report file and its approver. |
| [FRD-14](../../../../../../docs/frd/frd-14-record-edit-leases.md) | The Case edit lease behind the page-wide edit session. |
| [FRD-21](../../../../../../docs/frd/frd-21-outbound-correspondence-and-sent-evidence.md) | What counts as Sent evidence for Mark report sent. |
| [FRD-11](../../../../../../docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md) | Report generation and delivery. |
| [ADR-0051](../../../../../../docs/adr/0051-linked-audit-case-identity-and-custody.md) | Create audit creates a Case: one sequence, two records, shared file bytes, the `a.` Box subfolder under the original's folder, a permanent two-way link. |
| [CONTEXT.md](../../../../../../CONTEXT.md) | Audit; Inspection + Audit ("an Inspection Case and a linked Audit Case"); Completed and Query. |
| [Design authority](../../../../../../docs/design/README.md) | The Case record frame: 56px ribbon, 40px section row, Scroll/Tabs display modes over the same section hosts and one edit form, the Actions list with Close case last and red. |

## Source

| Layer | File | What it owns |
| --- | --- | --- |
| Web | `Pages/Cases/Details.cshtml` | The sticky block (ribbon, section row, stale bar), the Actions menu locals (`canX`, `offersX`) and markup, the section dispatch loop, the aside. |
| Web | `Pages/Cases/Details.Frame.cs` | `AuditCase`, `OriginalCase`, `HasGeneratedReport`, `IsEditing`, `CanCreateAudit`, `ProposedAuditReference`, `IsAuditCase`, `SectionIsShown`, `IsNestedSection`, `SectionLinkKey`, `CaseTypeChip`, `StateChipText`, `NextAction`, `DescribeFrameAsync`, `OnPostCreateAuditAsync`. |
| Web | `Pages/Cases/Details.cshtml.cs` | `Section` (`?section=`), `ActiveTabClass`, `CollapsedClass`, `SectionIsDeferred` and `LazySectionViews`, `OutstandingRequirements`, `OriginalReportMissing`, `IsPostReportReadOnly`, `CanEditCaseData`, `CanEditEngineering`, the report handlers (`OnPostGenerateReportAsync` and siblings, `OnPostPrepareReportDeliveryAsync`, `OnPostSendPreparedReportAsync`). |
| Web | `Pages/Cases/Details.Report.cs` | Report read helpers: `ReportTitle`, `ReportDeliveryFileName`, `ReportDeliveryMessage`, `FeeNote`, `AssessmentDisplay`. |
| Web | `Pages/Cases/Shared/_CaseDialogs.cshtml` | Every Actions dialog, including Mark report sent and Create audit. |
| Web | `Pages/Cases/Shared/_CaseReport.cshtml` | The Report section: head controls, preview card, generation facts, delivery. |
| Web | `Pages/Cases/Shared/_CaseOverview.cshtml` | Outstanding requirements, the Case type and Report sent cells. |
| Web | `Pages/Cases/Shared/_CaseOriginalReport.cshtml` | The Original report section. |
| Web | `Pages/Cases/Shared/_CaseDocuments.cshtml` | Mark as original report on each document row. |
| Web | `Pages/Cases/Shared/_CaseHistory.cshtml` | How history lines, including Create audit's and Mark as original report's, read on Notes. |
| Web | `Pages/Cases/Closure.cshtml.cs`, `Tasks.cshtml.cs`, `Custody.cshtml.cs`, `Workflow.cshtml.cs` | POST-only endpoints the menu's dialogs target: Complete, ReturnToEngineer, Close, Archive, RecordReportApproval; LinkReportEvidence, UnlinkReportEvidence; MarkAsOriginalReport; Hold, ReleaseHold, ReturnToReview, AssignEngineer, AssignToMe, CreateLinkedReplacement. |
| Web | `Presentation/CaseWorkspaceLabels.cs` (`Frame`) | Scroll, Tabs, Refresh, Audit case, Original case, Replacement case, Create audit, Mark report sent, Enable return, "Return the Case to the Engineer to edit". |
| Web | `Presentation/OperatorLabels.cs` | `CaseWorkspace.Sections` (order and labels), `CaseStage`, `CaseTypeName`, `MarkAsOriginalReport`. |
| Web | `Presentation/ShellPreferences.cs` | `CaseLayoutCookie` (`pegasus-case-layout`) and `CaseLayout(request)`. |
| Web | `wwwroot/js/case-workspace.js` (frame block) | Scroll/Tabs, lazy section bodies, jumps and the scroll spy, in-place swaps. |
| Web | `wwwroot/js/site.js` (`pegasusPreferences`) | Writes the layout cookie. |
| Web | `wwwroot/css/site.css` | `.record[data-layout="tabs"] .record-section:not(.is-active){display:none}`. |
| Core | `Lifecycle/CreateAuditCase.cs` | `CreateAuditCase`, `AuditCaseRefusal` and its messages, `AuditCasePolicy`, `ICaseAuditLinkQueries`, `ICaseReportGeneratedQueries`. |
| Core | `Cases/CaseContracts.cs` | `CaseType` (Inspection, Audit, InspectionAndAudit), `AuditIdentity.Create`. |
| Core | `Documents/MarkAsOriginalReport.cs` | `OriginalReportPolicy.RequireEligible`. |
| Core | `Assessment/AssessmentWorkspace.cs`, `Assessment/AssessmentPolicy.cs` | `AssessmentAccessPolicy` and `IsWritableState`: when the Engineer sections open and edit. |
| Infrastructure | `Persistence/EfCreateAuditCaseStore.cs` | Creates the linked Audit Case; answers the link and "report generated" queries. |
| Infrastructure | `Persistence/EfCaseWorkflowStore.cs` | Linking Sent evidence moves Report preparation to Post report; Return to Engineer moves to Report preparation. |
| Infrastructure | `Persistence/EfDocumentCustodyStore.cs` | Mark as original report. |

## Behaviours

### The ribbon

The ribbon's heading is `workflow.Identity.Reference` in an `<h1>`, under
"Case workspace · {registration}". Claimant, Principal and Engineer follow.
The chips are, in order: the state chip (`StateChipText`, which adds
" · review on {d MMM}" to Held when a review date is set), the Case type
chip, the outcome, the roadworthiness, the repairs-to-value share, Archived,
and "{name} is editing" (`Details.cshtml`).

The Case type chip is `CaseTypeChip`: "Audit" for `CaseType.Audit`,
"Inspection + Audit" for `CaseType.InspectionAndAudit`, nothing for
Inspection. It is navy and plain.

### Audit case and Original case links

`DescribeFrameAsync` reads `AuditCase` from
`auditLinks.GetAuditCaseAsync(caseId)`: the Case whose `AuditOfCaseId` is
this Case (`EfCreateAuditCaseStore.GetAuditCaseAsync`). It reads
`OriginalCase` only when this Case's type is Audit, from
`GetOriginalCaseAsync`. The ribbon's actions area then renders:

- "Audit case {reference}" linking to the Audit Case (`data-audit-link`);
- "Original case {reference}" linking back (`data-original-link`);
- separately, "Original case" and "Replacement case" (no reference) from
  `workflow.OriginalCaseId` and `workflow.ReplacementCaseId`, the pair that
  Correct principal records (`EfLinkedCaseReplacementStore`).

Both "Original case" buttons use the one label `Frame.OriginalCase`. A
standalone Audit Case has no `AuditOfCaseId`, so it shows no Original case
link.

### The section row

The nav lists `OperatorLabels.CaseWorkspace.Sections` filtered by
`SectionIsShown`: Damage and Valuation are left out (`IsNestedSection`; the
Vehicle link speaks for them) and Original report is left out unless
`IsAuditCase`. Each link is `/Cases/{id}?section={key}#section-{key}`. The
link marked `aria-current` is `SectionLinkKey` (a nested key marks Vehicle).

Refresh is a GET form carrying the current section in a hidden
`data-case-section-field`, so it reloads onto the section being read.

### The Scroll/Tabs switch

**Where it is rendered.** In `Details.cshtml`, inside `.section-tools` after
Refresh: a `div.layout-switch[data-case-layout-switch]` with the `hidden`
attribute and two buttons, `data-case-layout="scroll"` (`aria-pressed="true"`)
and `data-case-layout="tabs"`, labelled `Frame.Scroll` and `Frame.Tabs`.

**First paint.** The record's `<article>` carries
`data-layout="@ShellPreferences.CaseLayout(Request)"`. `CaseLayout` returns
`tabs` only when the cookie `ShellPreferences.CaseLayoutCookie`
(`pegasus-case-layout`) equals `tabs`; anything else reads `scroll`.
`DetailsModel.ActiveTabClass(key)` adds `is-active` to the host of the
addressed section (`Section`) when the layout is Tabs, so the first paint
shows that section. `site.css` hides every other `.record-section` under
`data-layout="tabs"`.

**Script.** At start-up `case-workspace.js` calls `readLayout()` and then
`setLayout(layout, false)`, which unhides the switch and sets
`aria-pressed`. In Tabs, `applyTabState` turns the nav into a `tablist`:
each link becomes a `tab` with `aria-selected`, `aria-controls` naming every
host it owns (Vehicle owns Damage and Valuation) and a roving `tabindex`;
each host becomes a `tabpanel` and only the active one carries `is-active`.
`selectTab` mounts the active tab's lazy placeholders and scrolls to the top.
Arrow Left and Right, Home and End move between tabs. In Scroll,
`applyScrollState` removes those roles, lazy bodies mount within two and a
half viewport heights, and the scroll spy marks the link in view. After an
in-place swap the record keeps its layout and re-applies the tab state.

**What Tabs does.** It hides the inactive sections with CSS; the section
hosts, their loaded values and the one edit form are the same as in Scroll.
Selecting a tab writes its key into every `[data-case-section-field]`
(Refresh, Edit Case, Cancel, Save), so the next reload returns to it. With
`?section=` in Scroll, the script jumps to that section after load unless it
is Overview.

**Persistence.** Pressing either button calls `setLayout(value, true)`, which
calls `saveLayout()`: `window.pegasusPreferences.write('pegasus-case-layout',
layout)` with no max-age. `site.js` states that a missing max-age writes a
session cookie, and `ShellPreferences` documents the cookie as a session
cookie. The choice therefore lasts for the browser session and applies to
every Case.

The Report section has its own Report and Fee tab pair (`data-report-tabs`).
It is unrelated to the layout switch.

### Section order

`OperatorLabels.CaseWorkspace.Sections` is the one list, in order: Case
details (`overview`), Claim, Original report, Inspection details, Vehicle,
Damage, Valuation, Repair Spec (`estimate`), Decisions (`settlement`),
Report, Files, Notes. `Details.cshtml` skips `original-report` unless
`IsAuditCase` (`CaseType.Audit`, which covers a standalone Audit and an Audit
made by Create audit). Damage and Valuation render inside the Vehicle
section's flow with `data-section-parent="vehicle"`.

Vehicle, Valuation, Files and Notes are `LazySectionViews`: unless addressed,
their bodies are fetched from `/Cases/{id}/Section?section={key}` as the
reader approaches them. While editing, only Files stays deferred
(`SectionIsDeferred`).

### The Actions menu

The menu (`details.menu[data-case-actions]`) renders when any item below is
available. Outside an edit session only Send to EVA can be, so the menu is
absent otherwise. Conditions are the locals at the top of `Details.cshtml`;
"editing" is `IsEditing`, "editing Case data" also requires
`!IsPostReportReadOnly`.

| Item | Offered when | Dialog and target |
| --- | --- | --- |
| Hand to Engineer | Editing Case data, Review, and `EvaHandoff` has Engineer options | `case-handoff-dialog` → `Workflow` AssignEngineer; Assign to me when `CanAssignToMe` |
| Send to EVA | Principal policy `EvaZip` or `EvaManualApi`, or `CanRetryAutomaticFailure`; state Review, Report preparation or Post report. No edit session needed | Link to `/Cases/Eva/Send`, enhanced into `eva-handoff-dialog` |
| Mark report sent | Editing, Report preparation, `AvailableReportSentEvidence` not empty | `case-report-sent-dialog` → `Tasks` LinkReportEvidence |
| Mark completed | Editing, Post report | `case-complete-dialog` → `Closure` Complete (reason) |
| Return to Review | Editing Case data, Report preparation or Post report | `case-return-review-dialog` → `Workflow` ReturnToReview (reason) |
| Return to Engineer | Editing, Completed or Query | `case-return-engineer-dialog` → `Closure` ReturnToEngineer (reason) |
| Archive case | Editing, a terminal state, not archived | `case-archive-dialog` → `Closure` Archive (reason) |
| Unlink report evidence | Editing Case data, Sent evidence linked, not Held, not terminal | `case-unlink-evidence-dialog` → `Tasks` UnlinkReportEvidence (reason) |
| Place on Hold or Release Hold | Editing Case data, not terminal | `case-hold-dialog` (reason, optional Review on) or `case-release-hold-dialog` (reason) |
| Correct principal | Same as the Hold group | `case-correct-principal-dialog` → `Workflow` CreateLinkedReplacement |
| Create audit | `CanCreateAudit` | `case-create-audit-dialog` → `Details` CreateAudit |
| Close case | Editing, `AvailableClosureOutcomes` not empty | `case-close-dialog` → `Closure` Close; after a separator, `btn--danger` |

A separator sits before the Hold and Create audit group only when one of
Hand to Engineer, Send to EVA, Mark report sent, Mark completed, Return to
Review, Return to Engineer or Archive is present.

### Create audit

`CanCreateAudit` (`Details.Frame.cs`) is true when the Case type is
`InspectionAndAudit`, `AuditCase` and `OriginalCase` are both null, the state
is not Created in error, the Case is not archived, `HasGeneratedReport` is
true (any generation with a confirmed artifact,
`EfCreateAuditCaseStore.HasGeneratedReportAsync`) and `IsEditing`. It does
not look at the recorded outcome; Core does. `OnPostCreateAuditAsync` runs
`ICreateAuditCase`, then redirects to the new Audit Case with
"Case {a.reference} was created." A refusal returns to this Case with Core's
message. The dialog, the checks and what the store copies are in
[Create audit](dialogs/create-audit/how-it-works.md).

### Report generation

The Report section's head (`_CaseReport.cshtml`) shows **Generate report**
when the section edits (`SectionIsEditable("report")`, which is
`CanEditEngineering`), the assessment is writable and has a draft
preparation, and either an unconfirmed report artifact is being retried or no
confirmed report exists and `ReportDraftCondition` is null. Otherwise, while
editing with a condition, it shows the gated label "Report not ready". The
More menu holds Preview draft, Preview Repair Spec, Preview images, Generate
Repair Spec, Generate images, Generate fee note and Include fee note.

The body shows the not-ready notice with every missing requirement, the
preview card (`ReportTitle`, for example "Total Loss Report — {registration}",
and "Generated {d MMMM yyyy HH:mm} · State {Pending, Confirmed or Stale}",
the enum name printed as it is, or "No generation yet."), a Download link for
the confirmed report, a Generated and State definition list, and the stale
notice. A stale generation also raises a stale bar in the
sticky block with "Open Report".

`GenerateArtifactAsync` (`Details.cshtml.cs`) calls `IGenerateCaseReport`
and redirects to the Report section with "The report was generated.",
"Report not ready: …", a pending message or a failure. Generating a report
does not move the Case: the only write of `PostReport` is in
`EfCaseWorkflowStore.ApplyReportEvidenceLink`.

### Delivery, Mark report sent and sent evidence

**Prepare delivery** shows while the section edits, the generation is
Confirmed, its report artifact is confirmed and nothing is prepared yet:
To and Cc recipients (with the Case's address book), attach choices (the
report always), the file name and the covering message. Once prepared, the
section lists the recipients, attachments, file name and message with **Send
prepared report**, which posts without a lease.

**Mark report sent** is an Actions item in Report preparation while editing,
when `details.AvailableReportSentEvidence` is not empty. Its dialog lists
each detected item (Mailbox, Sent time) with a required reason and "Confirm
detected evidence", posting `Tasks` LinkReportEvidence. The store accepts a
link only from Report preparation ("Report-Sent evidence can be linked only
from Report preparation.") and sets `ReportSentEvidenceId` and the state
`PostReport` (`EfCaseWorkflowStore.ApplyReportEvidenceLink`).

Once evidence is linked, the Overview's Case card shows **Report sent**:
"{mailbox} · {office time}". The Next action aside reads "Mark completed".
The state chip still reads "With Engineer": `OperatorLabels.CaseStage` maps
both `ReportPreparation` and `PostReport` to it.

### What post-report looks like

- **Post report** (chip "With Engineer"): Mark completed, Return to Review,
  Unlink report evidence, Hold and Correct principal are on offer while
  editing. The Engineer sections still edit
  (`AssessmentPolicy.IsWritableState` includes `PostReport`).
- **Completed and Query** (`IsPostReportReadOnly`): the ribbon's edit button
  reads **Enable return** instead of Edit Case. Inside that session
  `CanEditCaseData` is false, no section head offers Edit
  (`SectionOffersEdit`), and sections that cannot edit state "Return the Case
  to the Engineer to edit" (`SectionAvailability`). The Actions menu offers
  Return to Engineer, which moves the Case to Report preparation
  (`EfCaseWorkflowStore.ReturnToEngineerAsync`), and Close case where Core
  allows an outcome. The Engineer sections open read-only in Completed
  (`AssessmentAccessPolicy.CanOpen`) and do not open in Query.
- A reply sent to a query on a Query Case moves it back to Completed
  (`EfStaffMailSendStore`).

### What an Audit Case adds

**Original report section.** On an Audit Case, `_CaseOriginalReport.cshtml`
renders four assessment cells under `original_report.*`: assessor, date,
roadworthiness and outcome. The assessor input offers
`ThirdPartyReportProfiles.KnownIssuers` as completions; any name may be
typed. The cells are Case data (`SectionIsEditable("original-report")` falls
to `CanEditCaseData`). A source comment records that pre-filling them from a
filed report's extraction is "not yet wired".

**Original report missing.** `OriginalReportMissing` is true when the Case
type is Audit, the Case was not made by Create audit (`AuditOfCaseId` is
null), no standalone-Audit evidence was kept at intake
(`StandaloneAuditEvidenceId` is null), and no current file carries the
`AuditReport` role. It is then the first Outstanding requirement, titled
"Original report missing" with source "Audit". An Audit Case made by Create
audit never shows it.

**Mark as original report.** While `CanEditCaseData` and
`OriginalReportMissing`, every document row under Files → Documents offers
**Mark as original report**, posting `Custody` MarkAsOriginalReport. Core
refuses it for a Case that is not an Audit, a closed Case, or an image
(`OriginalReportPolicy.RequireEligible`). The store refuses a second,
different document ("A different document is already marked as the original
report."), sets the occurrence's role to `AuditReport`, and writes the
history line "Original report: {file name}", which Notes prints as the
line itself. The confirmation reads "The original report was recorded."

### Case type words on this page

The ribbon chip says "Inspection + Audit" (`CaseTypeChip`). The Overview's
Case type cell uses `OperatorLabels.CaseTypeName`, which says "Inspection and
audit". The Create case form says "Inspection and Audit" (see
[Create case](../create/how-it-works.md)).

## Things the FRD does not settle

- Whether a recorded outcome is required before Create audit. FRD-01 and
  ADR-0051 give the `a.` reference "whatever the assessment outcome"; Core
  refuses without one (`AuditCaseRefusal.NoRecordedOutcome`), and the Web
  gate offers the item without checking.
- What a reader outside an edit session is told about Create audit. FRD-01
  and FRD-16 place it inside an edit session; nothing says whether it is
  visible, disabled or absent before Edit Case.
- Return to Review. FRD-16's Actions list offers Return to Review or Return
  to Engineer "in Completed or Query". The source offers Return to Review in
  Report preparation and Post report, and Return to Engineer in Completed and
  Query.
- Where report approval is taken. FRD-13 says report approval names one
  report file and its approver; the handler exists but the page has no
  control for it.
- The Case type's wording. The FRDs write "Inspection + Audit"; the Overview
  and Create case use "Inspection and audit" and "Inspection and Audit".
- Whether the Scroll/Tabs choice is meant to be per browser session for every
  Case (as built) or per Case; FRD-16 says only "lasts for the browser
  session".
