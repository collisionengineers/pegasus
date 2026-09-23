# Triage record — how it works today

Read from the live source on 23 September 2026 (origin/dev 446c3ce2f).

The Triage record is its own page at `/Triage/{id}`, separate from the Case
record. Paths are under `src/` unless stated.

## What this page does not show

- No sections, section row or tabs. The record is one scrolling page of
  panels under a page header and a ribbon.
- No Files view. The request's photographs appear under **Vehicle images**;
  the retained source opens through **Open message** or **Open file**. There
  is no list of retained sources and attachments.
- No **History** heading. The events-and-notes panel is titled **Notes**.
- No **Reply with outcome**. Nothing in `src/` carries that phrase; the
  outbound mail on this page is **Chaser correspondence**.
- No due date and no chase schedule on the page.
- No Case/PO. The reference is the Triage's own `T-` reference, or the
  registration when none is recorded.
- No sign that most controls need Edit Triage first. The forms are drawn
  whenever the record is open; posting without an edit scope returns "Select
  Edit Triage before changing this record."
- No Case search in **Link case**. The dialog asks for a "Case ID" and binds
  it as a GUID (`caseId`), not a reference.
- No Triage on the Case record. Nothing in `Pages/Cases/Details*` or
  `Pages/Cases/Shared/` names a Triage.

## Governing documentation

| Document | What it settles for this page |
| --- | --- |
| [FRD-03](../../../../../../docs/frd/frd-03-triage.md) | The `T-00001` reference; how a Triage starts; states Open, Awaiting information, Finding recorded, Completed, Cancelled; findings (Roadworthiness, Assessment, at least one); corrections as new findings; History and Files; Edit claims a Triage scope; assignee and Case link; cancel and reopen with a reason; optional Reply with outcome; automatic association with one Case. |
| [FRD-15](../../../../../../docs/frd/frd-15-work-centre-queues-and-search.md) | Triage detail: determinations, source facts, History, Files, Open message or Open file, Assign to me, Assign to Engineer dialog; the Triage row on `/Cases`; Triage as a Work Centre kind. |
| [FRD-14](../../../../../../docs/frd/frd-14-record-edit-leases.md) | Record edit scopes for Triage; a command touching a Triage and a Case checks both. |
| [FRD-12](../../../../../../docs/frd/frd-12-operator-experience.md) | `/Triage/{id}` is a record route; `/Triage` redirects to `/Cases?tab=triage`; Triage joins the working set. |
| [FRD-02](../../../../../../docs/frd/frd-02-intake-and-source-identity.md) | A Triage request with no registration is held in Unidentified; opening the Triage resolves that item. |
| [FRD-19](../../../../../../docs/frd/frd-19-image-led-intake-and-pairing.md) | Crop and tag on pre-Case images, including a Triage's. |
| [CONTEXT.md](../../../../../../CONTEXT.md) | Triage: a separate pre-Case assessment with a global T-reference; no Case/PO; Reply with outcome optional. |
| [Design authority](../../../../../../docs/design/README.md) | Triage and Unidentified are pre-Case records reached through the Cases rail, never Case states; a single-record screen other than the Case record is "header, identity ribbon, action bar, sections as tabs". |

## Source

| Layer | File | What it owns |
| --- | --- | --- |
| Web | `Pegasus.Web/Pages/Triage/Details.cshtml` | The page: header, ribbon, record bar, Determinations, Source, Vehicle images, Exact response evidence, Chaser correspondence, Notes, every dialog. |
| Web | `Pegasus.Web/Pages/Triage/Details.cshtml.cs` | `OnGetAsync`, `OnPostActionAsync` (every lifecycle and data action), `OnPostEditAsync`, `OnPostCancelEditAsync`, `OnPostHeartbeatEditAsync`, `OnPostReleaseScopeBeaconAsync`, `OnPostAssignToMeAsync`, `OnPostSendChaserAsync`, `OnPostReconcileChaserAsync`; labels for states, findings and history events. |
| Web | `Pegasus.Web/Pages/Triage/Index.cshtml.cs` | `/Triage`: a permanent redirect to `/Cases`. |
| Web | `Pegasus.Web/Presentation/OperatorLabels.cs` | `TriageState`, `TriageReference`, `Principal`, `PrincipalNotKnown`, `SetPrincipal`. |
| Web | `Pegasus.Web/Mcp/TriageMcpTools.cs` | The automation actor's Triage tools. |
| Core | `Pegasus.Core/Triage/TriageContracts.cs` | `TriageState`, `TriageRecord`, `TriageSummary`, `TriageDetail`, `TriageNotes`. |
| Core | `Pegasus.Core/Triage/TriageLifecycle.cs` | `CreateTriageFromIntake`, `TriageCasePairing`, the state-change use cases and `TriageLifecycleRules` (`CanAssignToSelf`, validation). |
| Core | `Pegasus.Core/Triage/TriageQueryUseCases.cs` | `ListTriage` (list and count), `ListTriagePage`. |
| Infrastructure | `Pegasus.Infrastructure/Persistence/EfTriageStore.cs` | The store: reference allocation, the established Principal, mutations under the edit scope, the completion guard, the list projection. |

## Behaviours

### Route, title and working set

`@page "/Triage/{id:guid}"`. The title is "Triage {reference}", where the
reference is `record.Reference` or, failing that, the registration.
`LoadAsync` registers a working-set record of kind `triage` at
`/Triage/{id}`. The rail marks Cases as current for any `/Triage` path
(`Shared/_Layout.cshtml`, `CurrentWhen`).

### Layout

1. **Page header**: eyebrow "Triage", `<h1>` the reference, **Back to Cases**
   (`/Cases?tab=triage`) and **Refresh**.
2. **Ribbon**: Triage (reference), Registration, Source (the source channel),
   Opened (office date), Assignee (name or "Unassigned"), and the state chip.
3. **Record bar**: Assign to me; Edit Triage, Take over, or Cancel while
   editing; Assign to Engineer or Reassign Engineer; Unassign.
4. **Determinations** (titled **Post-send correction** on a Completed record)
   beside **Source**.
5. **Vehicle images**, when the origin receipt has servable images.
6. **Exact response evidence**, when a response is linked or, while open,
   candidates exist.
7. **Chaser correspondence**, when the origin is a retained mailbox message.
8. **Notes**: the Add note form while open, then every history entry newest
   first: a meta line "Date {date}", "Time {time}", "ID {actor}", over
   "{event}: {reason}" (`DetailsModel.EventLabel`).

### Edit scope

The record opens read-only. **Edit Triage** claims an `EditScopeKind.Triage`
scope for this one record (`editScopes.ClaimAsync`). When another holder has
it, the button reads **Take over** and the claim is made with `takeOver`.
While editing, a hidden heartbeat form keeps the scope, a beacon releases it
as the page unloads, and **Cancel** releases it.

`OnPostActionAsync` refuses any action without an `editLeaseToken`. Every
state change and data change goes through it: assign, unassign, note,
set principal, await information, record and supersede a finding, link and
unlink a response, complete, cancel, reopen, link and unlink a Case. The
store removes the scope in the same transaction as each mutation
(`EfEditScopeStore.Complete` in `EfTriageStore`), so each change needs Edit
Triage again. A refused action releases the scope.

Three posts need no scope: **Assign to me**, **Send chaser** and **Reconcile
chaser status**. Send chaser checks the Triage version instead.

### State actions

While the record is open (not Completed or Cancelled):

- **Await information**, in Open or Finding recorded (reason dialog). Core
  allows the same two states (`AwaitTriageInformation`).
- **Complete Triage**, always drawn. It is disabled with the condition
  "Available once a finding is recorded" unless the state is Finding
  recorded; enabled, it opens a reason dialog.
- **Cancel Triage** (reason dialog, "Cancelled Triage can be reopened with a
  reason.").

On a Completed or Cancelled record only **Reopen** (reason) is offered.

Completion has a store-side guard: `EfTriageStore` refuses it unless exactly
one response evidence link exists ("Triage completion requires exactly one
replied Sent email evidence link.").

### Recording a finding

One form carries Roadworthiness (Not recorded, Roadworthy, Unroadworthy),
Repair outcome (Not recorded, Repairable, Total loss) and a required reason.
With no active finding it posts `record_finding` ("Save determinations").
With one active finding it posts `supersede_finding` with that finding's id,
the same controls refilled. On a Completed record the panel is titled
"Post-send correction" and its button reads "Record correction". More than
one active finding shows "Multiple active findings require reconciliation
before another finding can be recorded." A Cancelled record shows the last
finding as facts.

### Assignee

**Assign to me** shows while open when `TriageLifecycleRules.CanAssignToSelf`
(no assignee, not Completed or Cancelled) and the actor is Staff
(`NeedsAttentionPolicy.CanTake`). **Assign to Engineer** (or **Reassign
Engineer**) opens a compact dialog with an Engineer select and no reason; it
is drawn only when `EngineerChoices` is not empty. **Unassign** is a reason
dialog.

### Set principal

**Set principal** sits in the Source panel and its dialog is drawn in every
state, with a select whose empty option is "Not known" and the active
Principals' codes. It posts `set_principal` with no reason, through the same
edit-scope check. The Source panel's Principal reads the recorded
`PrincipalId`'s code (`TriageDetail.PrincipalCode`). At creation the store
records the Principal from the instruction draft's suggested code when it
resolves to exactly one active Principal, otherwise none
(`ResolveEstablishedPrincipalAsync`).

### Chaser correspondence

Shown when the Triage came from a retained mailbox message. It states the
latest chaser's status and, when that status is Unknown, offers **Reconcile
chaser status**. While open, with no chaser in flight and an approved
mailbox with staff-send capability for the origin, it offers a reply form:
To (prefilled from the original's reply-to), Cc, Subject ("Re: …"), Message
and the origin's attachments, then **Send chaser**. The send uses purpose
`TriageChaser` and the Triage as its context. Otherwise it states "The
existing correspondence operation must finish or be resolved before another
action." or "No approved mailbox with staff send capability is available for
this origin."

### Exact response evidence

Lists linked responses (time and reason). While open it offers **Unlink
response** (reason) for the first linked item, and a select of
approved-mailbox reply candidates with a reason and **Record and link exact
response**.

### Case link and unlink

The Source panel's **Case link** reads "Open the case" (to `/Cases/{id}`) or
"None". While open it offers **Link case** or **Unlink case**. Link case is
a dialog with "Case ID" and a reason; Unlink case is a reason dialog.
`ExecuteCaseAssociationAsync` reads the target Case, refuses while a live
Case edit lease is held (naming the holder), claims the Case lease itself,
runs `ILinkTriageCase` or `IUnlinkTriageCase` with both the Triage scope and
the Case lease, and releases the Case lease if the command did not consume
it. When the linked Case is unavailable or leased, the panel shows the
reason instead of the buttons.

After creation, `CreateTriageFromIntake` also runs `TriageCasePairing`,
which may link one Case automatically.

### What the automation actor can do

`TriageMcpTools` offers list, get, source download, edit begin, renew and
end, await information, record and supersede a finding, link and unlink a
response, complete, cancel, reopen, and link and unlink a Case. It has no
assign, note or set-principal tool.

### Every place that links to `/Triage/…`

| Where | What links | Source |
| --- | --- | --- |
| Cases list, Triage tab | Each row's reference, and the quick detail's **Open Triage** | `Pages/Cases/Index.cshtml.cs` `TriageRow` (`/Triage/{id}`), `RecordDetail` |
| Work Centre | The Today pane's **Open Triage** for a Triage item | `Pegasus.Core/Operations/OperationsSnapshot.cs` (`Route = /Triage/{id}`), `Presentation/NeedsAttentionPresentation.cs` (`ActionLabel`, `RecordPage`) |
| Vehicle-images record | **Open Triage** when a Triage came from the same receipt | `Pages/ImageIntake/Details.cshtml`, `ITriageQueries.GetByOriginReceiptAsync` |
| Unidentified record | **Open the Triage** on a resolved or closed item whose receipt opened one | `Pages/Unidentified/Details.cshtml` (Resolution panel) |
| Intake log | The Became reference for a receipt that became a Triage | `Presentation/OperatorLabels.cs` `IntakeLog.BecameHref`, `Pages/Administration/Logs.cshtml` |
| Working-set strip | The open Triage's tab | `Presentation/WorkingSetRecord.cs`, `wwwroot/js/site.js` (glyph `clipboard-list`) |
| Rail | Cases is current on `/Triage` paths | `Pages/Shared/_Layout.cshtml` |

The Inbox message page names a Triage outcome with a "Triage" chip and no
link (`Pages/Mail/Message.cshtml.cs`, `AttachmentOutcomeKind.Triage`).
`/Triage` itself redirects permanently to `/Cases`, adding `?tab=` only when
an old `?queue=` value is given (`Pages/Triage/Index.cshtml.cs`).

## Things the FRD does not settle

- Completion evidence. FRD-03 says sending a message is never required and
  neither composing nor sending is a gate; `EfTriageStore` refuses completion
  without exactly one linked response.
- Await information from Finding recorded. FRD-03's transition table lists
  only Open to Awaiting information; Core and the page also allow it from
  Finding recorded.
- Files and History. FRD-03 and FRD-15 describe a `Files` view and a
  `History` view; the page has neither heading.
- Reply with outcome. FRD-03 and FRD-15 describe it; nothing in the source
  offers it.
- The `/Triage` redirect. FRD-12 says `/Cases?tab=triage`; the source sends
  `/Cases`.
- How Link case should identify the Case. FRD-03 says staff may link with a
  reason; the page asks for the Case's GUID.
- Whether setting the Principal or adding a note should need Edit Triage.
  FRD-03 says every change rechecks the holder and token; the page draws both
  controls outside the scope.
- The Triage record's layout against the design authority's single-record
  rule (header, ribbon, action bar, sections as tabs).
