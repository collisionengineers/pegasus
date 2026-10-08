# FRD-15: Work Centre, queues and search

> Owner capabilities: TRI-08, UI-02 to UI-07, UI-18, UI-19 · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- The Work Centre (`/`) shows the whole office's work: five counts, the
  Activity figures, then one panel whose tabs hold the Needs attention ledger,
  New cases and AI jobs, each a table whose rows end in the next action and
  one Dismiss.
- Every count comes from a Core query. A failed read shows "unavailable",
  never `0`.
- `/Cases` is one page whose rail is one continuous list of queues, with no
  groups. Each queue keeps its own row shape.
- `/Search` is the advanced search.
- Due dates are calendar days at midnight Europe/London. Targets are
  settings; the day rules are not.

## Purpose

This document says how staff find the work that needs a person: the Work
Centre, the Cases queues, the pre-Case records, the Triage Case page, Search
and the freshness rules every count follows. Page shell and
navigation are owned by [FRD-12](frd-12-operator-experience.md). The Case
record is owned by [FRD-16](frd-16-case-record-workspace.md).

## Behaviour

### Work Centre

The Work Centre (`/`) shows office-wide work in one ledger (v30 design B,
25 September 2026; v32 design A, 5 October 2026). Its head reads "Updated
HH:MM", the page's one clock, with **Refresh** and **Create Case**; the utility bar's New case is omitted on this page and only
this page, so the action has one home. The page refreshes itself only while
its browser tab is visible: every five minutes, and when the tab regains focus
after 30 seconds away. It never refreshes while a dialog is open or a field has
focus. A refresh does not mark New cases as seen.

**Metrics.** Five counts in one strip, label and figure on one line, in this
order: Not ready, Review, Held, Unidentified and Triages. Each is an exact
link to its Cases tab (`/Cases?tab=…`) and counts everything regardless of
paging. Triages counts the Triage Cases that are Open, Awaiting information or
Finding recorded; Completed and Cancelled are not counted. A failed read shows
no figure and the section's unavailable notice, never `0`.

**Activity** (v32 A, operator 5 October 2026). Under the counts, a panel
headed Activity holds a table: the figures head the columns and the rows are
**Today** and **This week**. The figures are New cases, Sent to Engineer,
Reports sent, Completed and E-mails received, each counted for both rows
(operator 8 October 2026). Today runs from midnight Europe/London and This week
from Monday 00:00 Europe/London, both to the instant of the read. Every
figure is office-wide and plain text; New cases opens the New cases tab
when that tab is shown. The definitions:

| Figure | Counts |
| --- | --- |
| New cases | Cases created in the window, excluding Triage Cases, as the New cases list counts them |
| Sent to Engineer | Cases whose first entry into With Engineer (`First sent to Engineer`, [FRD-13](frd-13-case-lifecycle-and-workflow.md#assign-engineer)) falls in the window; a later return to With Engineer is not counted |
| Reports sent | Sent report e-mails, as the Engineer activity report MI-01 counts them ([FRD-17](frd-17-administration-workspace.md#management-reports)), so the two agree for the same week |
| Completed | Cases that entered Complete in the window, including one reopened since |
| E-mails received | Mailbox receipts; an upload is also a receipt and is not counted |

The figures are their own section with their own refresh state: a failed
read shows "Activity is unavailable." and no figure, never `0`. The panel is
drawn whether or not there is work to show. A dismissal changes no figure.

**Sections.** Below the strip one panel carries three tabs: **Needs
attention**, **New cases** and **AI jobs**, each with its count. A tab is
omitted when its section is empty: Needs attention when the Office has no
work and no filter is on (on Mine the section stays, so Office remains one
click away), New cases when nothing arrived in the window, AI jobs when no
job waits. A section that could not be read keeps its tab, shows a dash for
its count and its notice inside. When every section is empty the page reads
one line, "No work to show.", under the five zero counts. The chosen tab
travels in the address (`?tab=`), so Refresh, F5 and the background refresh
keep it.

**Needs attention** is one list. Each item is exactly one of these kinds.
Every kind comes from a Core query, never from fixture or placeholder data,
and has a due instant:

| Kind | Item | Due instant |
| --- | --- | --- |
| Case | A missing-material chase | The next chase time, else the end of its Due by date |
| Held | A held Case awaiting its decision | The end of the hold's Review on date, else Held decision target after the hold was placed |
| Review | A Case in Review | Review target after it entered Review |
| Unassigned | A Case in Review with no Engineer | Review target after it entered Review |
| Vehicle images paired | A pre-report Case its early vehicle images paired into, not changed by staff since ([FRD-19](frd-19-image-led-intake-and-pairing.md#age-and-chase-state)) | The pairing |
| Unidentified | An open Unidentified item | Unidentified target after it was received |
| Triage | A Triage Case without a finding | Triage target after it opened |
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
attention item. Where a failure blocks a person's work, the record says so
where it is used ("Lookup failed", "Storage not ready"), and a failed custody
job is retried in the Case's Custody page. Intake OCR and allocation retries
are on Administration Logs. Service health, Administration only, states the
external-work queue.

**Office, Mine, kinds and Find.** The section's toolbar carries the Office /
Mine switch and **Find in Needs attention**. Office is every item. Mine is
the items the signed-in person owns plus unowned items of kinds they can
take. Every staff role opens on Office, and the choice is remembered per
browser. **Kind chips** (Case, Held, Review, Unassigned, Vehicle images
paired, Unidentified, Triage, AI draft) filter the list, several at once. Each chip shows its count
over the whole scope before any filter. Find narrows the scoped list by a
case-insensitive match on the row's reference, title, detail (principal,
sender or instruction) and owner, before paging; Enter applies it and the
term travels with every address on the page. **Clear filters** appears when a
kind or a term is on and clears both. A filter that matches nothing keeps the
toolbar and says "No work matches these filters."

**The ledger.** A table with seven columns: Next action (the task, with its
kind beneath), Record / detail (the reference, with the subject beneath;
the reference links to the Case, or to the Unidentified item it names, and
an AI draft on the Unidentified queue has no link), Owner, Due, Received, the next action as a button (v32 item M) and
**Dismiss**, an icon-only control named "Dismiss {reference}" (v32 item B).
Owner is the person's name. A Held, Review, Unassigned, Vehicle images
paired or Triage row with an empty person slot says **Unassigned**. A Case
chase, an Unidentified item, or an AI draft with no person says **No owner**.
Find matches that word. Below 980px Owner and Received fold into the task
cell. Rows are grouped under **Overdue (n)**, **Due today
(n)** and **Later (n)**; a group with no rows is not drawn. Order is by due
instant, earliest first and undated last, then received, then reference. The
due text is in words ("2 days overdue", "Due today", "Due Fri", "Due 24 Sep")
coloured by group; Received is the age ("3 d ago"). There is no priority
chip. The list is paged at 50, reading "Page 1 of N · earliest due first"
with Previous and Next. An item leaves only when it no longer qualifies or
is dismissed; nothing is silently dropped.

**The open row.** Choosing a task opens the row in place, beneath it: the
kind, a chip only when Overdue (red, with how late) or Due today (amber), the
title, its facts (reference, subject, principal or sender or instruction,
owner, due, received) and a next action that does the action. Choosing the
open task again closes it; nothing opens by itself. Assign Engineer opens the
assignment dialog on the Work Centre. Review Case opens the Case, and so
does Open Case on a Vehicle images paired item. Open Triage opens the Triage
Case page. An AI draft offers its per-kind action. The open row
repeats no Dismiss; the row's own control is the one.

**New cases.** Every Case except a Triage Case created in the last 7 calendar
days, newest first, whatever created it: reference, registration, claimant,
principal and an arrival chip (Manual, E-mail, Principal API, Automation). A
"Since you last looked" divider marks what is new for this person. Opening the
Work Centre records the look. The section lists new Cases only: what the
Automation actor does to an existing Case is that Case's history, not a row
here (operator, 8 October 2026). The section is a table (v32 A): Case, Detail
(registration, claimant and principal), Arrival, Received and **Dismiss**; the
"Since you last looked" divider is a group row. The section is paged.

**AI jobs.** The office's unfinished AI jobs (Queued, Taken with its lease
expiry, Draft ready) and those that failed in the same 7 days, excluding
Market research, as a table (v32 A): Job, State, Instruction, Record, Started
(who and when), Note (a Taken job reads "Taken until HH:MM", v32 item K; a
failed job its reason), then the Draft ready action defined per kind in
[FRD-27](frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md#ai-job-list)
(Review estimate, Open query, Review, Complete job) and **Dismiss**. A failed
job shows its reason with Open Case. There is no Cancel
here; an Administrator stops a job on Administration AI jobs.

**Dismiss** (2 October 2026) takes a row off the Work Centre without
changing its record. A dismissal belongs to the record behind the row (the
Case, Unidentified item or AI job) and applies for everyone: every row of
that record that began at or before the dismissal leaves every tab, so a
Case dismissed from New cases also leaves Needs attention, and a Draft ready
job leaves both Needs attention and AI jobs. A row that begins after the
dismissal shows again:

| Row | Begins when |
| --- | --- |
| Case | Its chase fell due (each chase is a new row) |
| Held | The hold was placed |
| Review, Unassigned | The Case entered Review |
| Vehicle images paired | The images were paired |
| Unidentified | The item was opened or reopened |
| Triage | The Triage changed state, or opened |
| AI draft, AI job | The job entered the state it shows (taken, draft written, failed, or returned to the queue when its lease lapsed); a job released back to the queue keeps its creation time |
| New case | The Case was created |

Due dates play no part, so a target change never brings a dismissed row back.
The section counts, kind chips, groups and pages leave dismissed rows out;
the five metrics and the Activity figures count records and do not change.
There is no undo and no list of dismissed rows, and no notice: the row's
leaving is the answer. Every row of every section ends in the same
icon-only Dismiss (v32 item B); after a dismissal the page returns to the next
row of that list, or to the list's heading when none is left. The Automation
Actor dismisses a record the same way with `pegasus_work_centre_dismiss`
([FRD-10](frd-10-mcp-automation-and-actor-boundary.md#queue-and-intake-tools)).

### Cases: queues and filters

`/Cases` is one page. Its rail is one continuous list with no heading, group
labels or dividers (operator, issue 1046, 6 October 2026). Each queue carries
its own count, in this order: Not ready, Review, With Engineer, Query, Triage,
Awaiting instruction, Held, Unidentified. Every queue button looks the same.

`?tab=` selects the queue. Not ready contains only formal instructed Cases.
Triage lists the Triage Cases still being worked: Open, Awaiting information
and Finding recorded, the same states the Triages metric counts, and its
count is theirs. A Completed or Cancelled Triage Case leaves the queue and is
found through Search by its `t.` Case/PO or registration (operator, issue
1044, 7 October 2026). Triage Cases never appear in the Case queues.
A vehicle-images record still awaiting an instruction is listed under
Awaiting instruction, never in a Case queue
([FRD-19](frd-19-image-led-intake-and-pairing.md#image-initiated-case-projection)).
Completed and Query are reversible workflow states. Query has a queue;
Completed has none, and a Completed Case is found through Search's State
filter. Cancellation, rejection and Created in error are recorded
dispositions, not a Closed queue.

Filters are Principal (every queue) and, on Not ready only, Missing with the
exact options `All`, `Instructions`, `Images`, `Both missing`, plus Clear.

Each queue keeps its own row shape:

- a Case row: reference and registration, state, claimant and principal,
  origin and received, due, and who is editing it while a staff edit lease
  is live (the same column appears on Search results). An Inspection + Audit
  Case is one row, showing its Inspection's claimant, Principal and
  registration, whether or not it has an Audit;
- an Awaiting-instruction row: Image reference, registration, file count and
  custody;
- a Triage row: `t.` Case/PO, registration, Principal and assignee;
- an Unidentified row: the U-reference, kind, a handle the operator will
  recognise (the original filename, or the e-mail subject and sender, never
  an internal identifier), received date and time, and the canonical reason.

Awaiting-instruction rows select their quick detail. Every other row links
straight to its full detail. Selecting a row shows a quick detail. For a
Case that is its origin, compact workflow position, outstanding requirements
and current work, with Open full Case. Current work is the Case's Next action,
the step the Case record's aside names (a report blocker, Generate report,
Send report and so on), never the chase schedule's state; beside it stand
the Engineer and the due. A Case's due is one instant everywhere it shows, on
the list, the quick detail and Search: its Case chase due instant from the
table above, dated by the day it falls due (operator, 28 September 2026). For other
kinds it is the definition list and the open action, with Add to an existing
case on an Awaiting-instruction record. A Triage row's open action, **Open
Triage**, opens the Triage Case page at `/Cases/{id}`.

Unidentified media kind (`Images` or `E-mails`) is derived from the retained
receipt's source channel and content type, not stored separately. The
Unidentified tab shows **Open items** or, through its Show choice, **Closed
items**, each closed row reading "Closed · reason". It lists Unidentified
items only. There is no blocked row.

### Pre-Case records

**The Unidentified record** (`/Unidentified/{id}`) has a ribbon showing
reference, received, kind, source and state (Open, Closed or Resolved). An
item that could not be read says "Could not be read · {file kind}" with its
bounded detail. Its actions are:

- **Open file** (the retained original) and **Open message** (when it came
  by e-mail);
- **Request again**, a reply to that message;
- **Link to Case**, a dialog searching every Case and every Triage Case, in
  any state. Linking claims that Case's edit lease, or a Triage Case's edit
  scope;
- **Create case**, from its receipt;
- **Register images**, the registration prefilled from an agreeing reading,
  with a reason; on an upload group's item it registers the whole group;
- **Send Unidentified to AI**, while the item is open: queues one
  Unidentified-resolution AI job for that item
  ([FRD-27](frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md#ai-job-list));
- **Close with reason**, free text.

Where Core allows it the record also offers **Open the Triage**, which
creates a Triage Case under the receipt's Principal and is not offered when
that Principal is not established
([FRD-03](frd-03-triage.md#normal-workflow-and-completion-evidence)). It lists
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

**The vehicle-images record** is described in
[FRD-19](frd-19-image-led-intake-and-pairing.md#operator-surfaces).

### The Triage Case page

A Triage Case's only page is `/Cases/{id}`
([FRD-03](frd-03-triage.md#normal-workflow-and-completion-evidence)). It
keeps the Triage layout, headed by its `t.` Case/PO. It has no Edit step:
each action posts once and holds the record for its one save
([FRD-14](frd-14-record-edit-leases.md#record-edit-scopes)). It shows none
of the Case record's sections, and has no Set principal.

**Ribbon.** The `t.` Case/PO, registration, the claimant read from the
request (or Not recorded; operator, 7 October 2026), Principal, source, opened date
and time, the assignee, the linked Case (its Case/PO as a link, or None) and
the state chip. Then the state's next step as the one primary button, and one
**Actions** menu holding every other action the state permits, Cancel Triage
last in red (operator, 5 October 2026).

| State | Primary button | Actions menu |
| --- | --- | --- |
| Open | Record finding | Assign, Send chaser, Open file, Link case, Cancel Triage |
| Awaiting information | Record finding | Assign, Send chaser, Open file, Link case, Cancel Triage |
| Finding recorded | Complete Triage | Assign, Record finding (a correction), Send chaser, Open file, Link case, Cancel Triage |
| Completed | Reply with finding, until it is sent | Record correction, Open file, Reopen |
| Cancelled | Reopen | Open file |

Send chaser and Reply with finding appear only when a reply can be sent and
no send is in flight. Once the outcome reply is sent, by anyone, Reply with
finding is gone from the ribbon, the completion notice and the
Correspondence tab; only Reopen and a fresh completion offer it again
(operator, issue 1047, 7 October 2026). A failed or cancelled send leaves it
offered. Open file appears only when the request did not come
by e-mail. Assign appears while staff can be chosen. Link case becomes
Unlink case once a Case is linked. There is no Open message: the request
e-mail is the first row of the Correspondence tab.

**Tab row.** Under the ribbon, and staying with it as the page scrolls, are
the tabs **Images** (only when the request carried photographs), **Files** and
**Notes**, each with its count, and the finding read-only: Roadworthiness and
Repair outcome, or Not recorded. Without script the three panels follow one
another under their headings.

**Images** is the request's photographs at contact-sheet size, each opening
the image viewer. A Triage takes no crop and no tag.

**Files** shows the Box case folder's state chip, **Add evidence** (which
opens Upload for this Case,
[FRD-18](frd-18-manual-upload.md#upload-for-a-declared-case)) and More ›
Open in Box, over two tabs. **Documents** lists the documents with view and
download, or the empty state. **Correspondence** is the Case record's
Correspondence table: the request e-mail and every retained e-mail
associated with the Triage, each opened in the message dialog. Above it are
the latest send status (with Reconcile status when it is Unknown), the
in-flight notice when a send has not finished, and the Send chaser or Reply
with finding button. Exact response evidence, when there is any to show or
link, follows the table.

**Notes** merges durable events with append-only attributable notes in time
order. A correction is a new note. There is no note edit and no note delete.

**Record finding** opens one dialog: Roadworthiness, Repair outcome and the
required reason, then two tickboxes. **Complete Triage** completes the Triage
in the same post, as a second save on the version the finding left.
**Reply with finding**, offered only when a reply can be sent, opens the
composer on the reply once the Triage completes; ticking it ticks Complete
Triage. A repeated post replays both saves. With several active findings
the dialog is withheld and the page names the reconciliation needed.

On a Completed or Cancelled Triage the finding stays read-only. A correction
is still offered on Completed: **Record correction** opens the same fields in
a dialog, and the correction supersedes the finding
([FRD-03](frd-03-triage.md#normal-workflow-and-completion-evidence)).
Complete acts on the click and asks no reason. Cancel Triage, Reopen, Link
case and Unlink case keep their reason dialogs, as does the finding reason.
Each action shows its own notice. The completion notice opens Reply with
finding when a reply can be sent.

**Assign** opens one dialog. It lists eligible staff with the signed-in
person first as "Name (you)". Nothing is preselected. Its buttons are
Unassign (when assigned), Cancel and Assign. Neither asks a reason.

**Composer.** Send chaser and Reply with finding open the Inbox composer's
frame over the page: From (the approved mailbox), To, Cc, Subject, Message
and the request's own files as attachments. It is offered only when the
Triage came by e-mail and its approved mailbox may send. To answers the
request's Reply-To, or its From without one; a request a member of Collision
Engineers forwarded answers its original sender instead, and drops the
forward's own "FW:" from the subject
([FRD-21](frd-21-outbound-correspondence-and-sent-evidence.md#outbound-correspondence)).
Before Completed it sends the chaser FRD-03 offers. Once Completed it is
**Reply with finding**: the same To, the subject "Re: {original subject}" and
a body rendered from
the Triage outcome template
([FRD-17](frd-17-administration-workspace.md#e-mail-templates)), which staff
edit before Send. The server decides which it is. A refused send opens the
composer again on what was posted, with the reasons. The sent
correspondence attaches to the Triage and is never a completion gate.
Server-side transitions stay reachable where a handler exists.

### Search

`/Search` carries the `UI-07` filters: Case/PO or Image reference,
Registration, Claimant, Claim/Principal reference, Principal, State, Engineer,
Received from/to and Origin, with Search and Clear. Results are one table
(Case/PO and Our ref, vehicle, claimant, principal, type, state, due).
Pointer or keyboard intent on a row shows a selected-Case preview beside the
table (type, state, accident circumstances, Our ref, Engineer, due, next
action, outstanding requirements, Open Case, copy Case/PO). At constrained
width the preview stacks after the table. Vehicle-images records are
searchable by Image reference or registration and use the named states
Awaiting instruction, Merged into Instruction-initiated Case and
Staff-closed ([FRD-19](frd-19-image-led-intake-and-pairing.md#operator-surfaces)).

**Inspection + Audit.** When an Inspection + Audit Case with an Audit
matches, by either reference or any other filter, it is listed as two
entries: `{Case/PO}`, which opens the Case's Inspection view
(`?view=inspection`), and `a.{Case/PO}`, which opens its Audit view
(`?view=audit`). Each entry shows its own reference; both are the same Case.
Paging counts Cases, so a page may hold more entries than its size.

**Triage Cases.** Search finds a Triage Case by its `t.` Case/PO and by
registration. Its row shows the Triage state, never a Case state, and opens
the Triage Case page.

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

The Activity figures are defined under [Work Centre](#work-centre). The
Unidentified count is the exact count of open Unidentified items and links to
that queue. `Due by` and overdue or chaser work stay a separate operational
view from the Activity figures.

## States and transitions

Every surface renders exactly one of: loading, empty, current, stale (with
the last-good time), partial, unavailable, failed, validation, conflict, or
access denied. A queue offers a transition only where its Core use case
permits it ([FRD-13](frd-13-case-lifecycle-and-workflow.md)).

## Edge cases and fail-closed behaviour

- A count whose query has not run renders nothing. A failed query renders
  its failure, never `0`.
- A stale version on a pre-Case record or a Triage Case is a
  non-destructive conflict. A replay shows the original result.
- A Needs attention item with no due instant sorts last and leaves only
  when it no longer qualifies or is dismissed.
- An unreadable Unidentified item still shows its reference, origin and
  bounded detail.

## Acceptance evidence

Acceptance covers every rail route and its count, the `/Unidentified`
redirect, `/Triage` and `/Triage/{id}` answering Not found, the Cases rail
order and filters, the five Work Centre counts, the Activity figures and
their unavailable state, the Needs attention kinds
against Core queries, the Triage Case page in its read, edit, conflict,
Completed and Cancelled states, and the Search filters with the two entries
of an Inspection + Audit Case and Triage rows. Authenticated Web tests
cover server-owned behaviour; they do not prove client-side interaction or
visual correctness. Deployment and live acceptance are separate evidence
tiers ([engineering](../engineering.md#required-evidence-tiers)).

## Links

- Capabilities: `TRI-08`, `UI-02`–`UI-07`, `UI-18`, `UI-19` in
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
  [ADR-0029](../adr/0029-image-initiated-case-projection.md),
  [ADR-0056](../adr/0056-one-case-per-work-data-and-triage-case-type.md).
