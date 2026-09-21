# FRD-15: Work Centre, queues, search and Operations

> Owner capabilities: TRI-08, UI-01 to UI-07, UI-18, UI-19 · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- The Work Centre (`/`) shows the whole office's work: five counts, one
  Needs attention list, a Today pane, New cases and AI jobs.
- Every count comes from a Core query. A failed read shows "unavailable",
  never `0`.
- `/Cases` is one page of queues grouped as Workflow, Pre-Case work and
  Exceptions. Each queue keeps its own row shape.
- `/Search` is the advanced search. `/Operations` shows AI jobs, retryable
  failures and EVA handoffs.
- Due dates are calendar days at midnight Europe/London. Targets are
  settings; the day rules are not.

## Purpose

This document says how staff find the work that needs a person: the Work
Centre, the Cases queues, specialised records, Search, Operations and the
freshness rules every count follows. Page shell and navigation are owned by
[FRD-12](frd-12-operator-experience.md). The Case record is owned by
[FRD-16](frd-16-case-record-workspace.md).

## Behaviour

### Work Centre

The Work Centre (`/`) shows office-wide work. Its head reads "Updated HH:MM"
with **Create Case** and **Refresh**. The page refreshes itself when its
browser tab regains focus after 30 seconds away, and every five minutes. It
never refreshes while a dialog is open or a field has focus. A refresh does
not mark New cases as seen.

**Metrics.** Five counts: Not ready, Review, Held, Triage, Unidentified. Each is an
exact link to its Cases tab (`/Cases?tab=…`) and counts everything regardless
of paging. A failed read shows its unavailable state, never `0`.

**Needs attention** is one list. Each item is exactly one of these kinds.
Every kind comes from a Core query, never from fixture or placeholder data,
and has a due instant:

| Kind | Item | Due instant |
| --- | --- | --- |
| Case | A missing-material chase | The next chase time, else the end of its Due by date |
| Held | A held Case awaiting its decision | The end of the hold's Review on date, else Held decision target after the hold was placed |
| Review | A Case in Review | Review target after it entered Review |
| Unassigned | A Case in Review with no Engineer | Review target after it entered Review |
| Unidentified | An open Unidentified item | Unidentified target after it was received |
| Triage | A Triage record without a finding | Triage target after it opened |
| AI draft | An AI job in Draft ready, except Market research | AI draft target after the draft was written |

The targets are workflow settings
([FRD-17](frd-17-administration-workspace.md#workflow-configuration)). These
rules are fixed, not settings:

- days are calendar days, never working days;
- the day boundary is midnight Europe/London;
- a target of 0 means due by the next midnight after the event;
- **Overdue** means due at or before now. **Due today** means due before the
  next midnight;
- a settings change applies from the next read and rewrites no recorded
  date.

Failed external work (custody, vehicle lookup, intake OCR) is never a Needs
attention item. It is on Operations, whose rail badge counts retryable
failed external work. Where a failure blocks a person's work, the record says
so where it is used ("Lookup failed", "Storage not ready").

**Office and Mine.** A switch above the list. Office is every item. Mine is
the items the signed-in person owns plus unowned items of kinds they can
take. Engineers open on Mine, everyone else on Office, and the choice is
remembered per browser. **Kind chips** (Case, Held, Review, Unassigned,
Unidentified, Triage, AI draft) filter the list, several at once. Each chip
shows its count over the whole scope before the filter. All kinds clears.

The list is grouped under **Overdue (n)**, **Due today (n)** and
**Later (n)**, each with its own empty state ("Nothing overdue"). Order is by
due instant, earliest first and undated last, then received, then reference.
A row shows its title, kind, reference and detail; its due text in words
("2 days overdue", "Due today", "Due Fri", "Due 24 Sep") coloured by group;
its owner; and "Received 3 d ago". There is no priority chip. The list is
paged at 50, reading "Page 1 of N · earliest due first" with Previous and
Next. Nothing is silently dropped.

**Today pane.** The selected item's kind and reference, title, a chip only
when Overdue (red, with how late) or Due today (amber), its facts, and a next
action that does the action. Assign Engineer opens the assignment dialog on
the Work Centre. Review Case opens the Case. Open Triage opens the Triage. An
AI draft offers its per-kind action. **Assign to me** is offered on an
Unassigned item to an Engineer, and on a Triage item without an assignee,
where Core would accept it.

**New cases.** Every Case created in the last 7 calendar days, newest first,
whatever created it: reference, registration, claimant, principal and an
arrival chip (Manual, E-mail, Provider API, Automation). A "Since you last
looked" divider marks what is new for this person. Opening the Work Centre
records the look. A change the Automation actor makes to an existing Case
appears as a "Changed by automation" row naming the change. The section is
paged.

**AI jobs.** The office's unfinished AI jobs (Queued, Taken with its lease
expiry, Draft ready) and those that failed in the same 7 days, excluding
Market research. Columns: kind and detail, record, started by, created,
state, and the Draft ready action defined per kind in
[FRD-27](frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md#ai-job-list)
(Review estimate, Open query, Review, Complete job). A failed job shows its
reason with Open Case. Cancel stays on Operations.

### Cases: queues and filters

`/Cases` is one page. Its rail groups the queues, each with its own count:

| Group | Queues |
| --- | --- |
| Workflow | Not ready, Review, With Engineer, Completed, Query, Triage |
| Pre-Case work | Awaiting instruction |
| Exceptions | Held, Unidentified |

`?tab=` selects the queue. Not ready contains only formal instructed Cases.
A vehicle-images record still awaiting an instruction is listed under
Awaiting instruction, beside Triage, never in a workflow queue
([FRD-19](frd-19-image-led-intake-and-pairing.md#image-initiated-case-projection)).
Completed and Query are reversible workflow states. Cancellation, rejection
and Created in error are recorded dispositions, not a Closed queue.

Filters are Principal (every queue) and, on Not ready only, Missing with the
exact options `All`, `Instructions`, `Images`, `Both missing`, plus Clear.

Each queue keeps its own row shape:

- a Case row: reference and registration, state, claimant and principal,
  origin and received, due, and who is editing it while a staff edit lease
  is live (the same column appears on Search results);
- an Awaiting-instruction row: Image reference, registration, file count and
  custody;
- a Triage row: reference, registration, provider and assignee;
- an Unidentified row: the U-reference, kind, a handle the operator will
  recognise (the original filename, or the e-mail subject and sender, never
  an internal identifier), received date and time, and the canonical reason.

Awaiting-instruction rows select their quick detail. Every other row links
straight to its full detail. Selecting a row shows a quick detail. For a
Case that is its origin, compact workflow position, outstanding requirements
and current work (due, Engineer, next action), with Open full Case. For other
kinds it is the definition list and the open action, with Add to an existing
case on an Awaiting-instruction record.

Unidentified media kind (`Images` or `E-mails`) is derived from the retained
receipt's source channel and content type, not stored separately. The
Unidentified tab shows **Open items** or, through its Show choice, **Closed
items**, each closed row reading "Closed · reason". It lists Unidentified
items only. There is no blocked row.

### Specialised records

**The Unidentified record** (`/Unidentified/{id}`) joins the working set. Its
ribbon shows reference, received, kind, source and state (Open, Closed or
Resolved). An item that could not be read says "Could not be read · {file
kind}" with its bounded detail. Its actions are:

- **Open file** (the retained original) and **Open message** (when it came
  by e-mail);
- **Request again**, a reply to that message;
- **Link to Case**, a dialog searching viable Cases. Linking claims that
  Case's edit lease;
- **Create case**, from its receipt;
- **Register images**, the registration prefilled from an agreeing reading,
  with a reason;
- **Close with reason**, free text.

Where Core allows it the record also offers **Open the Triage**, lists
registration readings with Dismiss (reason), and on a closed item shows the
outcome with **Reopen** (reason). An image on the item offers crop and tag
([FRD-19](frd-19-image-led-intake-and-pairing.md#operator-surfaces)). There is
no "Resolution" or "Resolve material" wording in the UI. An exact U-reference
search returns both open and resolved items as their own result type and
never treats U<n> as a Case, Audit or Image reference. Every action is
staff-authorised, antiforgery protected, idempotent by operation key and
version-checked where the record has a version, and requires its reason
where the action records one. A stale version is a non-destructive conflict.
A replay shows the original result. The permanent U-reference and origin
stay visible after closure or resolution.

A received file has no page of its own
([FRD-02](frd-02-intake-and-source-identity.md#received-file-history-and-technical-actions)).
Once linked, it reads **Linked to Case** in the Intake log and on its
message, whatever decision first proposed a Case.

**The Triage Case workspace** is canonical at `/Cases/{id}` and carries the determinations (roadworthiness, repair
outcome), the source facts, a `History` view that merges durable events with
append-only attributable notes in time order, and a `Files` view of the
retained sources, their attachments, staff Case uploads and the linked vehicle images with view
and download. A correction is a new note. There is no note edit or note
delete. Its retained source opens with **Open
message** when it came by e-mail, otherwise **Open file**. **Assign to me** is
offered where the Triage has no assignee and the operator may take it.
**Assign to Engineer** opens a compact dialog (engineer select, Assign and
Cancel). Assignment records no reason. The determination reason and other
meaningful Triage decisions keep their required reasons
([FRD-03](frd-03-triage.md#normal-workflow-and-completion-evidence)).
Completion records the decided outcome. Optional **Reply with outcome** opens
the email feature with an editable preset template. The sent correspondence
attaches to the Triage and is never a completion gate. Server-side
transitions stay reachable where a handler exists.

**The vehicle-images record** is described in
[FRD-19](frd-19-image-led-intake-and-pairing.md#operator-surfaces).

### Search

`/Search` carries the `UI-07` filters across every Case type, including Triage: Case/PO or Image reference,
Registration, Claimant, Claim/provider reference, Principal, State, Engineer,
Received from/to and Origin, with Search and Clear. Results are one table
(Case/PO and Our ref, vehicle, claimant, principal, type, state, due).
Pointer or keyboard intent on a row shows a selected-Case preview beside the
table (type, state, accident circumstances, Our ref, Engineer, due, next
action, outstanding requirements, Open Case, copy Case/PO). At constrained
width the preview stacks after the table. Vehicle-images records are
searchable by Image reference or registration and use the named states
Awaiting instruction, Merged into Instruction-initiated Case and
Staff-closed ([FRD-19](frd-19-image-led-intake-and-pairing.md#operator-surfaces)).

### Operations

`/Operations` shows these, with a partial-data notice when any query is not
current:

- the **AI Job List** (`AI-10`): kind, record, started by, created, state,
  next action, Send Unidentified to AI. Started by names the staff username
  or the Automation client name, resolved the same way Action logs does,
  never a raw subject identifier;
- **Attention required**: retryable external work with attempts, failure and
  Retry. For Administrators it also lists failed intake, each received file
  under its failure kind (Allocation failed, OCR failed, Processing failed),
  offering only its own action (Retry allocation, Retry OCR or
  Re-evaluate), each with a reason, through the Logs handlers
  ([FRD-02](frd-02-intake-and-source-identity.md#received-file-history-and-technical-actions));
- **EVA handoffs**: route, Engineer, state, result.

Operations is open to Engineers and Users. Its rail badge counts retryable
failed external work and is absent at zero. Service health is
Administration-only. Operations has no service health table; its one-line
partial-data notice links to Administration Service health.

### Dashboard freshness and reconciliation

Every count and query shows its last successful update time and current
refresh state. `0`, loading, current, stale with last-good time, partial,
unavailable and failed are distinct outcomes. A refresh never replaces a
last-good value with a false zero, never merges partial data into an
apparently complete result, and never implies that an external action
succeeded.

Manual refresh reruns the same exact filtered query. It changes no policy
and creates no business transition. Its caller, start and end time, sources
and result (success, partial, failure) stay auditable in content-safe
telemetry. Reconciliation that accepts, rejects, links or changes an external
business fact instead enters permanent business history with the responsible
actor, source and version, before and after values, time, and reason where
required.

`New cases today` counts every instructed Case created in the current
Europe/London calendar day, including one later completed, cancelled or
rejected that day. It excludes vehicle-images records, Triage and
Unidentified. The Unidentified count is the exact count of open Unidentified
items and links to that queue. These are separate from `Due today`, `Sent to
Engineer` and `Reports sent`. `Due by` and overdue or chaser work stay a
separate operational view from `New cases today`.

## States and transitions

Every surface renders exactly one of: loading, empty, current, stale (with
the last-good time), partial, unavailable, failed, validation, conflict, or
access denied. A queue offers a transition only where its Core use case
permits it ([FRD-13](frd-13-case-lifecycle-and-workflow.md)).

## Edge cases and fail-closed behaviour

- A count whose query has not run renders nothing. A failed query renders
  its failure, never `0`.
- A stale version on a pre-Case record is a non-destructive conflict. A
  replay shows the original result.
- A Needs attention item with no due instant sorts last and is never
  dropped.
- An unreadable Unidentified item still shows its reference, origin and
  bounded detail.

## Acceptance evidence

Acceptance covers every rail route and its count, the `/Triage` and
`/Unidentified` redirects, the Cases rail groups and filters, the Work Centre
kinds against Core queries, and the Search filters. Authenticated Web tests
cover server-owned behaviour; they do not prove client-side interaction or
visual correctness. Deployment and live acceptance are separate evidence
tiers ([engineering](../engineering.md#required-evidence-tiers)).

## Links

- Capabilities: `TRI-08`, `UI-01`–`UI-07`, `UI-18`, `UI-19` in
  [capabilities](../capabilities.md). `AI-10`
  stays with [FRD-27](frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md#ai-job-list).
- Related FRDs: [FRD-02](frd-02-intake-and-source-identity.md),
  [FRD-03](frd-03-triage.md),
  [FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md),
  [FRD-27](frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md),
  [FRD-12](frd-12-operator-experience.md),
  [FRD-13](frd-13-case-lifecycle-and-workflow.md),
  [FRD-16](frd-16-case-record-workspace.md),
  [FRD-17](frd-17-administration-workspace.md),
  [FRD-19](frd-19-image-led-intake-and-pairing.md).
- Design: [design](../design/README.md).
- Technical constraints:
  [ADR-0029](../adr/0029-image-initiated-case-projection.md).
