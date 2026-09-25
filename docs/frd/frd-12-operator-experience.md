# FRD-12: Operator experience

> Owner capabilities: UI-13, UI-16 · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- Every signed-in page uses one shell: a rail, a utility bar and the page.
  There is no working set of open records.
- Every count is a real query. Nothing shows `0` for "not loaded".
- Every control does one named thing. Inert controls are never drawn.
- The page works by keyboard, at 200% zoom and on a narrow screen without
  hiding required evidence or actions.
- Where the work lives: the Work Centre and queues are in FRD-15, the Case
  record in FRD-16, Administration in FRD-17, the Inbox in FRD-20 and
  Upload in FRD-18.

## Purpose

This document owns the staff interface's shell: routes, navigation, the
command palette, the notification bell, keyboard shortcuts, breakpoints, and the
state and freshness contract every page carries. It serves the PRD's
Operations-first outcome: office-wide awareness of the work that needs a
person, with every count honest and every action reaching a named Core use
case. Visual, component and token rules are owned by
[design](../design/README.md). This document states behaviour only and
records no technical decision.

The shell is the Integrated Operations Workspace (`UI-16`). Its design
authority is [design § Authenticated shell](../design/README.md#authenticated-shell).

## Behaviour

### Operator experience

The direction is Operations-first. The UI must provide:

- an authenticated office-wide Work Centre with Europe/London day boundaries
  and Monday-to-Monday weeks
  ([FRD-15](frd-15-work-centre-queues-and-search.md#work-centre));
- actionable receiving, requests, Triage, case, query and exception queues
  ([FRD-15](frd-15-work-centre-queues-and-search.md#cases-queues-and-filters));
- clear counts that link to their exact filtered work and never show a
  stale zero placeholder;
- list and detail journeys for received mail, source evidence, Triage,
  cases, documents, history and exports;
- supporting-detail navigation from the Inbox, Upload or a Case that neither
  commits nor discards the current form, and returns to the same detail
  context, evidence selection, position and unsaved edits;
- administration for authorised accounts, roles, principals, workflow
  configuration, mail settings, automation and AI settings, service health,
  action logs and reports
  ([FRD-17](frd-17-administration-workspace.md#administration));
- exact state labels mapped to Core decisions;
- loading, empty, current, stale, unavailable, partial, failed, validation,
  conflict and access-denied states;
- keyboard, pointer, screen-reader, 200% zoom, forced-colour and
  reduced-motion support;
- responsive use without hiding required evidence or actions.

Screen-reader-compatible semantics are required behaviour. That does not by
itself claim interoperability with Narrator or any other screen reader,
complete WCAG conformance, subjective usability or operator acceptance.

Every actionable search result or queue row is a full-row keyboard-focusable
link or button with a visible action affordance. At constrained desktop
width, a long Case/PO, Image reference or U-reference moves to a labelled
second line instead of overlapping the received timestamp. Inbox and Intake
log rows always show received date above received time, and show the precise
processing outcome (`Case created`, `Vehicle images`, `Linked to Case`,
`Unidentified`, `Could not be read` or `Closed`), never a generic `New`. One
action or state has one icon across Pegasus. No decorative or generated
replacement icon is used.

Every drawn control maps to a named handler. A disabled control is allowed
only for a named integration seam whose row in
[capabilities](../capabilities.md) records it as a disabled seam. An inert
control is never rendered. Labels, values and controls carry no explanatory
copy ([design § No explanatory copy](../design/README.md#no-explanatory-copy-and-page-economy)).

### Shell and routes

Every authenticated page renders one shell: a persistent rail, a utility
bar and the page content.

**The rail** carries, in order, **Work Centre** (`/`), **Inbox** (`/Inbox`),
**Upload** (`/Upload`), **Cases** (`/Cases`), **Search** (`/Search`),
**Operations** (`/Operations`) and, for administrators only,
**Administration** (`/Administration`). Inbox, Cases and Operations carry a
count. The Cases count is the sum of Not ready, Review, With Engineer, Query,
Held, Triage and Unidentified. A count is a page-queried figure: an absent
count renders nothing, never `0`. The current route is marked by more than
colour. The rail foot shows the freshness line and the signed-in account
(name, role, and an account dialog with session start, idle lock and sign
out).

The rail foot also carries **Collapse**, which folds the rail to icons and
counts (labels become titles) and back. The choice is remembered per browser
and painted by the server on the next page. Below 980px, where the rail lies
down, there is no collapse.

**The utility bar** carries the page freshness text, the global search input
(Enter or Ctrl K opens the command palette), **New case**, and the bell.
On the Work Centre, whose header carries Create Case, New case is omitted so
the action has one home. Upload and Inbox keep their named navigation.

**Create Case** opens direct staff creation with the identity-critical Case
facts and no invented intake receipt or source provenance. When opened from
an Unidentified item or an upload decision, the same form uses that existing
receipt and the normal intake allocation path, not a second allocation route
([FRD-02](frd-02-intake-and-source-identity.md#ways-intake-starts)). A file
that cannot become a Case is refused with the reason and Open message / Open
file. The direct form's Case type also offers **Triage**, which asks only for
the Principal and the registration
([FRD-03](frd-03-triage.md#normal-workflow-and-completion-evidence)); the
form opened from a receipt does not offer it.

A skip link precedes the rail. Toasts announce in a live region. Every
dialog traps focus and inerts the page behind it. The navless frame is two
planes: a charcoal identity panel (the Pegasus mark, the product name and the
company) beside a white panel carrying the page's one card; below 980px the
identity panel becomes a strip above the card. Sign in reads "Sign in" with
Username, Password (with a Show / Hide control) and one Sign in button.
Access denied renders in the navless frame with the rest of the error family:
the area as the eyebrow, the refusal in one sentence and Return to Work
Centre.

**The bell** is the signed-in person's own notifications, never office-wide
work or queue counts. It shows the unread count, absent at zero. A
notification is raised for exactly these causes:

- an AI draft is ready on a Case: to the Case's Engineer, otherwise to
  whoever started the job. Market research never raises one;
- a Case is assigned to an Engineer: to that Engineer;
- a Case an Engineer is assigned to is edited by someone else, receives an
  e-mail, or receives a query: to that Engineer.

Nobody is notified of their own act. There is no e-mail notification. The
dialog lists notifications newest first with the Case reference,
registration, the cause in operator words, when, and Unread until opened.
Opening one marks it read and goes to its place. **Mark all read** marks
every one read. Notifications are kept for 30 days and then drop off. A
notification-query failure must not block the page unless it is a
cancellation or a failed authorisation. The dialog then shows
`Notifications unavailable.` with no placeholder or stale rows, and the
failure is logged.

**What's new** is the newest published release note
([FRD-17](frd-17-administration-workspace.md#release-notes)), opened once
for each person as a dialog on the next page they load after it is
published, with the note's title, when it was published, its version and
its body. **Got it** records the person's acknowledgement and the dialog
does not open again for that note; closing it without Got it leaves it to
open on the next page. The Account dialog's **Release notes** link and
`/ReleaseNotes` list every published note, newest first. A release-note
query failure must not block the page: nothing opens, and the failure is
logged.

**Report a problem** is in the Account dialog and on the error page: one
text box, What happened, and **Send**. The report retains the person's
complete submitted text and the state Pegasus captures: build, page and
method, UTC time, trace, person, Case reference on screen, recent acts and
browser errors. A report from the Error page includes the captured server
exception, inner causes and stack; other reports state when no server
exception was captured. Pegasus does not automatically collect document
content, images, mail or request bodies. The issue in the configured
repository contains that text and diagnostic snapshot
([ADR-0055](../adr/0055-github-issues-as-the-problem-report-sink.md)).
The person returns to the page they were on with `Reported as #<n>.` as
the confirmation, or `The report was kept but could not be sent.` when the
raise failed; an Administrator retries it from
[Problem reports](frd-17-administration-workspace.md#problem-reports).

| Route | Purpose | Replaces |
| --- | --- | --- |
| `/` | Work Centre ([FRD-15](frd-15-work-centre-queues-and-search.md#work-centre)) | Dashboard |
| `/Inbox`, `/Inbox/{id}` | Retained mail list and message ([FRD-20](frd-20-mailbox-workspace.md#inbox-scopes-and-filters)) | — |
| `/Upload` | Staff upload ([FRD-18](frd-18-manual-upload.md#staff-upload-page)) | — |
| `/Cases` | Queues: workflow (Triage included), pre-Case work and exceptions ([FRD-15](frd-15-work-centre-queues-and-search.md#cases-queues-and-filters)) | Queues; the Triage list |
| `/Cases/{id}` | Case record with section navigation; `?section=` jumps; once the Case has an Audit, `?view=inspection` or `?view=audit` chooses the view, the Audit view by default ([FRD-16](frd-16-case-record-workspace.md#case-workspace)). A Triage Case renders as the Triage Case page ([FRD-15](frd-15-work-centre-queues-and-search.md#the-triage-case-page)) | Case workspace side-nav sections; the Assessment page; the Triage record page |
| `/ReleaseNotes` | Published release notes, newest first; **Got it** posts here | — |
| `/ProblemReports` | **Send** on Report a problem posts here; it has no page of its own | — |
| `/Search` | Advanced search ([FRD-15](frd-15-work-centre-queues-and-search.md#search)) | Cases list |
| `/Unidentified/{id}`, `/VehicleImages/{id}` | Unidentified and vehicle-images records | The received-file page |
| `/Received/{id}/Source`, `/Received/{id}/Image`, `/Received/{id}/Asset/{assetId}` | Open file: the retained original, served to authorised staff only | — |
| `/Operations` | AI jobs, attention, EVA handoffs ([FRD-15](frd-15-work-centre-queues-and-search.md#operations)) | Operations Service health table |
| `/Administration`, `/Administration/...` | Administration areas ([FRD-17](frd-17-administration-workspace.md#administration)) | Separate Principal and Claim Source areas |
| `/Administration/Logs` | Action logs and Intake log tabs; `/Administration/ActionLogs` answers a permanent redirect keeping its query | Action Logs |

There is no received-file page. Its history is the Intake log, its technical
actions are on Operations and the Intake log, and its outcome is stated on
its message, its upload and the record it became
([FRD-02](frd-02-intake-and-source-identity.md#received-file-history-and-technical-actions)).

`/Unidentified` is a permanent redirect to `/Cases?tab=unidentified`, kept
for existing links and bookmarks. `/Triage` and `/Triage/{id}` are removed
and answer Not found, with no redirect. The Case record's other capability
routes (custody, tasks, vehicle, workflow, closure, document export and EVA
send) answer Not found for a Triage Case. The `/VehicleImages` list route is
removed. The vehicle-images detail page remains and is reached from
Awaiting-instruction rows, the Case Files section and upload outcomes. There
is no separate top-level Unidentified, Organisations, Staff accounts, Roles
or Automation Activity entry. The Engineer workbench is a set of sections on
the Case record, not a page of its own.

### Command palette and keyboard

There is no working set of open records and no strip of record tabs under
the utility bar, on any page (operator, 24 September 2026). Nothing replaces
its list of recently opened records: Search and the command palette remain.
A record's own state, such as the ribbon's Editing badge and "{name} is
editing", stays on the record.

The command palette (Ctrl K, the global search field, the Open button) finds
Cases, references and routes by typing and opens the selection.

| Key | Action |
| --- | --- |
| Ctrl K | Command palette |
| Ctrl U | Upload |
| Ctrl N | Create Case |
| Ctrl S | Save while editing |
| F5 | Refresh: re-query, not a page reload |
| Arrow Up / Down | Move through a row list |
| Enter | Open the selected row |
| Escape | Close the open dialog |

A shortcut never bypasses a reason, confirmation or gate.

Estimate import follows keyboard parity: Repair Spec provides a
keyboard-accessible **Import** action that opens the native file picker. File
drag and drop is also scoped to that section and uses the same import path
([FRD-16](frd-16-case-record-workspace.md#assessment)).

### Breakpoints

Content is centred at up to 1580px. Below 980px the rail becomes a
horizontal bar with icons only and the current route marked by a bottom
border. Below 760px every layout is a single column. Two- and three-pane
pages stack in reading order and keep identity, state and action context.
No required evidence or action is hidden at any width or at 200% zoom.

### Display labels

Case states and transition meanings are owned by
[FRD-13](frd-13-case-lifecycle-and-workflow.md#states-and-labels). Completed
and Query are reversible work states. No terminal Closed presentation is
permitted. `Audit`, `Triage` and `Unidentified` keep their settled meanings.
Screens never show the word "intake" except the Administrator's **Intake
log** tab. Vehicle-images records are called that, and their reference is
the Image reference.

## States and transitions

Every surface renders exactly one of: loading, empty, current, stale (with
the last-good time), partial, unavailable, failed, validation, conflict, or
access denied. The UI never infers state from colour alone, never uses
decorative glyphs as unlabelled controls, and never presents draft, queued,
attempted, allocated or configured work as completed, delivered, deployed or
accepted. Workflow transitions are owned by
[FRD-13](frd-13-case-lifecycle-and-workflow.md). The UI offers a transition
only where its Core use case permits it for the current state and account.

## Edge cases and fail-closed behaviour

Record edit ownership for Triage Cases and Image Intake is owned by
[FRD-14](frd-14-record-edit-leases.md#record-edit-scopes). Administration
settings use expected-version checks as defined in
[FRD-17](frd-17-administration-workspace.md#administration).

- A count whose query has not run renders nothing. A failed query renders
  its failure, never `0`.
- An action bar for a state with no permitted action shows the state and no
  control.
- An integration without a composed caller shows its named disabled seam
  and nothing else. When there is no seam the control is absent.
- A lost or expired edit lease shows the holder and disables Save. A stale
  version is a non-destructive conflict.
- A palette history that cannot be read is treated as empty. The page
  renders correctly with none.
- A redirect from a removed route keeps the query it was given.
- `/Triage`, `/Triage/{id}`, and a Case capability route addressed with a
  Triage Case's id, answer Not found.

## Acceptance evidence

Acceptance covers every rail route and its count, the `/Unidentified`
redirect, the removed `/Triage` routes and `/VehicleImages` list, the absence
of any record-tab strip, the keyboard table and the breakpoints.
Authenticated Web tests cover
server-owned behaviour; they do not prove client-side interaction or visual
correctness. Deployment and live acceptance are separate evidence tiers
([engineering](../engineering.md#required-evidence-tiers)).

## Links

- Capabilities: `UI-13`, `UI-16` in [capabilities](../capabilities.md).
- Related FRDs: [FRD-02](frd-02-intake-and-source-identity.md),
  [FRD-13](frd-13-case-lifecycle-and-workflow.md),
  [FRD-14](frd-14-record-edit-leases.md),
  [FRD-15](frd-15-work-centre-queues-and-search.md),
  [FRD-16](frd-16-case-record-workspace.md),
  [FRD-17](frd-17-administration-workspace.md),
  [FRD-18](frd-18-manual-upload.md),
  [FRD-20](frd-20-mailbox-workspace.md).
- Design: [design](../design/README.md) owns the durable interaction,
  visual, component and source/runtime rules.
- Technical constraints:
  [ADR-0053](../adr/0053-personal-staff-notification-store.md) (the bell),
  [ADR-0031](../adr/0031-automation-actor-contract-without-eva-export-tools.md)
  (Automation Actor contract).
