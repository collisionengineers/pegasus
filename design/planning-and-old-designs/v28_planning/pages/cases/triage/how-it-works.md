Read from the live source on 18 September 2026.

## What this page does not show

- No lede or explanatory sentence anywhere on the list or the record — a
  field is a label and a control or fact, nothing more.
- The list has no Principal filter of its own (unlike the Case-scope tabs):
  `Pages/Cases/Index.cshtml`'s filter bar only renders the Principal select
  when `IndexModel.ListsCases(Queue)` is true, and `triage` is not a
  Case-listing queue.
- The record's "Complete Triage" control is never omitted when a finding is
  missing — it renders disabled, with its enabling condition named on the
  control (`Available once a finding is recorded`), per a source comment
  citing `QdosTriageIntegrationTests.cs:216-221` and the merged Cases and
  Assessment pages, which gate the same way. It is not an inert control; it
  posts the same `complete` action once enabled.

## Source table

| File | Owns |
| --- | --- |
| `Pages/Cases/Index.cshtml` (tab `triage`) | The Triage list: rail counts, columns, quick-detail pane |
| `Pages/Cases/Index.cshtml.cs` | `TriageRow` (columns/cells/facts), `Columns` for `"triage"`, `Count`/`TriageCount` |
| `Pages/Triage/Index.cshtml.cs` | The permanent redirect to `/Cases?tab=triage` — never renders |
| `Pages/Triage/Details.cshtml` | The record: ribbon, Determinations/Source panels, Vehicle images, Exact response evidence, Chaser correspondence, Notes, all dialogs |
| `Pages/Triage/Details.cshtml.cs` | State-gated visibility (`mutable`, `canComplete`, `correction`), label helpers (`StateLabel`, `RoadworthinessLabel`, `AssessmentLabel`, `EventLabel`) |
| `Pegasus.Web.Presentation.OperatorLabels` | `TriageState`, `TriageReference`, `Principal`/`PrincipalNotKnown`, `SetPrincipal`, `SourceChannel` |
| `Pages/Shared/_ReasonDialog.cshtml` | Every reason-only transition dialog (Unassign, Await information, Complete, Cancel, Unlink case, Reopen) |
| `Pages/Shared/_StatusChip.cshtml` | The ribbon/list state chip's tone |
| `Pages/Shared/_ImageGallery.cshtml` | The Vehicle images gallery tiles |

## Behaviours

### The list is a tab, not a page

`/Triage` (`Pages/Triage/Index.cshtml`) is a bare `RedirectPermanent` to
`/Cases` with `?tab=triage` carried through from the old `?queue=` alias.
The actual list — columns `Reference, Registration, Provider, Received,
Assignee, State`, newest first, no Principal filter — is
`IndexModel.TriageRow` on the shared Cases queue page. Its quick-detail
panel shows `Reference, Registration, Provider, Assigned to, Opened` with
no separate State fact row (only Case and Triage rows carry a `Chip`, but
`RecordDetail` only promotes it to a `StateChip` fact for kinds the source
sets one for — Triage's `TriageRow` sets `Chip` but the quick-detail
rendering path for non-Case rows draws it only via the ribbon-style
`Notice`, which Triage rows do not set, so the panel shows facts only).

### One record, one determinations form, two purposes

The roadworthiness/repair-outcome/reason form posts `record_finding` with
no active finding, or `supersede_finding` against a specific
`supersedesFindingId` once one exists — the same three controls refilled,
never a second form. On a `Completed` record the identical form (relabelled
"Post-send correction" / "Record correction") supersedes the current
finding as the FRD-03 correction path; a `Cancelled` record must be
reopened first and shows the last finding as read-only facts instead.

### Setting the Principal is not a lifecycle action

`Set principal` has its own always-available dialog (no reason field,
mirroring Image Intake) outside the `mutable`-gated block, because a
completed or cancelled Triage's Principal remains correctable — recording
it is casework, not a workflow transition.

### Exact response evidence and chaser correspondence are independent panels

"Exact response evidence" only lists an approved-mailbox reply matched by
Core; the operator's job is to review the poll's candidates and link one
with a reason, or unlink the current one with a reason. "Chaser
correspondence" is the separate outbound side — composing and sending a
chaser e-mail with optional attachments — gated on an approved mailbox with
staff-send capability existing for the origin.

## Things the FRD does not settle

- The rail's disabled-with-a-named-condition pattern for "Complete Triage"
  is called out in the source as an open question against decision D7
  (whether a disabled control needs a named, ticketed integration seam) —
  "raised with the epic owner, not settled here."
