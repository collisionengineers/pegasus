# FRD-12: Operator experience

> Owner capabilities: UI · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [docs/design/README.md](../design/README.md)

## Purpose

This FRD owns how the staff interface behaves: the authenticated shell, its
routes and information architecture, the queues and search, the Case
workspace, the freshness and reconciliation rules, and the state, keyboard
and responsive contract every page carries. It serves the PRD's
Operations-first outcome — office-wide awareness of the work that needs a
person, with every count honest and every action reaching a named Core use
case. Visual, component, token and source/runtime rules are owned by
[design](../design/README.md); this document states behaviour only and
records no technical decision.

The shell described here is the Integrated Operations Workspace (`UI-16`).
It completes the design route that
[open decisions — later operator UI capabilities](../open-decisions.md#later-operator-ui-capabilities)
requires for the routes it activates; the design authority is
[design § Authenticated shell](../design/README.md#authenticated-shell).

## Behaviour

### Operator experience

The selected alpha direction is Operations-first. The UI must provide:

- an authenticated office-wide Work Centre with Europe/London day boundaries
  and Monday-to-Monday weeks;
- actionable receiving, requests, Triage, case, query, and exception queues;
- a Not-ready Missing filter with exact options `All`, `Instructions`,
  `Images`, and `Both missing` (§ Cases);
- clear counts that link to their exact filtered work and do not render stale
  zero placeholders;
- list/detail journeys for intake, source evidence, Triage, cases, documents,
  history, and exports;
- supporting-detail navigation from Intake or Case detail that neither
  commits nor discards the current form and returns to the same detail
  context, evidence selection, position, and unsaved edits;
- administration for authorised accounts, roles, principals, workflow
  configuration, mail settings, automation and AI settings, service health,
  action logs and reports;
- exact state labels mapped to Core decisions;
- loading, empty, current, stale, unavailable, partial, failed, validation,
  conflict, and access-denied states;
- keyboard, pointer, screen-reader, 200% zoom, forced-colour, and
  reduced-motion support;
- responsive use without hiding required evidence or actions.

Every actionable search result or queue row is a full-row keyboard-focusable
link or button with visible action affordance. At constrained desktop width,
a long Case/PO, Image Intake Reference, or U-reference moves to a labelled
second line instead of overlapping the received timestamp. Inbox and intake
rows always show received date above received time, and show the precise
processing outcome — such as `Case created`, `Image intake registered`,
`Associated with Case`, `Unidentified`, or `Blocked intake` — rather than a
generic `New`. One semantic action or state has one consistent icon across
Pegasus; no decorative or generated replacement icon is used.

Every drawn control maps to a named handler. A disabled control is permitted
only for a named, ticketed integration seam whose capability row in
[capabilities](../capabilities.md#capabilities) records it as a disabled
seam; an inert control is never rendered. Labels, values and controls carry
no explanatory copy
([design § No explanatory copy](../design/README.md#no-explanatory-copy-and-page-economy)).

### Shell and routes

Every authenticated page renders one shell: a persistent rail, a utility
bar, the workspace-tab strip and the page content. The rail carries, in
order, **Work Centre** (`/`), **Inbox** (`/Inbox`), **Upload** (`/Upload`),
**Cases** (`/Cases`), **Search** (`/Search`), **Operations**
(`/Operations`) and — for administrators only — **Administration**
(`/Admin`). Inbox, Cases and Operations carry a count; the Cases count is the
sum of Not ready, Review, With Engineer, Held, Triage and Unidentified. A
count is a page-queried figure: an absent count renders nothing, never `0`.
The current route is marked by more than colour. The rail foot shows the
freshness line and the signed-in account (name, role, account dialog with
session start, idle lock, sign out).

The utility bar carries the page freshness text, the global search input
(Enter or Ctrl K opens the command palette), the **Add** action (Upload
files, Create Case, Create upload request, Review Inbox) and notifications.
A skip link precedes the rail; toasts announce in a live region; every
dialog traps focus and inerts the page behind it.

| Route | Purpose | Replaces |
| --- | --- | --- |
| `/` | Work Centre — needs-attention work and the metric strip | Dashboard |
| `/Inbox`, `/Inbox/{id}` | Retained mail list and message ([FRD-08](frd-08-email-mailbox-and-background-processing.md)) | — |
| `/Upload`, `/Uploads/{token}` | Staff upload and the public upload request ([FRD-02](frd-02-intake-and-source-identity.md#upload-confirmation-surface)) | — |
| `/Cases` | Queues: workflow, pre-Case work and exceptions | Queues (`/Triage`) |
| `/Cases/{id}` | Case workspace | — |
| `/Cases/{id}/Assessment` | Engineer assessment | — |
| `/Search` | Advanced search (`UI-07`) | Cases list |
| `/Triage/{id}`, `/Unidentified/{id}` | Triage and Unidentified detail | — |
| `/Operations` | AI jobs, service health, attention, upload links, EVA handoffs | — |
| `/Admin`, `/Admin/{area}` | Administration areas | Administration index, Organisations, Access review, Roles, Automation Activity |

`/Triage` and `/Unidentified` are permanent redirects to `/Cases?tab=triage`
and `/Cases?tab=unidentified`, kept for existing links and bookmarks rather
than left dead. The `/VehicleImages` list route is removed; the vehicle-image
detail page remains the image record and is reached from Not-ready
Image-initiated rows, the Case Files section and upload outcomes. There is no
separate top-level Unidentified, Organisations, Access review, Roles or
Automation Activity entry.

### Work Centre

The Work Centre shows office-wide work: a metric strip of five counts —
Not ready, Review, Held, Unidentified, Blocked — each an exact link to its
Cases tab (`/Cases?tab=…`). Blocked links to `/Cases?tab=unidentified`,
where Blocked intake items are surfaced with their own state chip; there is
no separate Blocked tab. The Unidentified tab count, and the rail Cases sum,
count Unidentified items only; Blocked intake rows are listed in that tab
uncounted, with their own `Blocked intake` chip, so the two meanings stay
distinct. Then a two-pane needs-attention list and detail. A
needs-attention item is exactly one of these five kinds, each derived from
a Core query, never from fixture or placeholder data:

- **Case** — a chase that is due, a readiness blocker, or an outstanding
  requirement;
- **Held decision** — a Case on hold whose hold needs a decision;
- **Mail** — an Unidentified item;
- **Triage** — a Triage record without a finding;
- **External work** — retryable failed external work.

Failed AI jobs are not a needs-attention kind; they surface on Operations
(§ Operations).

Each item shows its kind and reference, title, priority, owner and due
value. Selecting an item shows the reason it needs attention (a label and
the Core-derived value only), the source, owner, last recorded outcome and
due facts, and the single next permitted action (Open Case, Open Triage,
Open Operations, Review source) plus copy-reference. `Blocked` is the exact
interface wording for the `Blocked intake` boundary and remains pre-case.

### Cases: queues and filters

`/Cases` is one page whose rail groups the queues, each with its own count:

| Group | Queues |
| --- | --- |
| Workflow | Not ready, Review, With Engineer, Complete |
| Pre-Case work | Triage |
| Exceptions | Held, Unidentified |

`?tab=` selects the queue. Filters are Principal (every queue) and, on Not
ready only, Missing — `All`, `Instructions`, `Images`, `Both missing` —
plus Clear. Each queue keeps its own row shape rather than being forced into
one column set: a Case row carries reference and registration, state,
claimant and principal, origin and received, due; an Image-initiated row
carries its VRM reference, registration, file count and custody; a Triage
row carries reference, registration, provider and assignee; an Unidentified
row carries the U-reference, kind, operator-meaningful handle (the original
filename, or the e-mail subject and sender — never an internal identifier),
received date and time and the canonical reason. A row links to its detail
and nothing else. Selecting a row shows a quick detail: for a Case its
origin, compact workflow position, outstanding requirements and current work
(due, Engineer, next action) with Open full Case; for other kinds the
definition list and the open action.

Not ready rows are either origin settled for the Image-initiated Case
lifecycle: a formal instructed Case, or an ImageIntake-backed projection
still awaiting one. Unidentified media kind (`Images` or `E-mails`) is
derived from the retained receipt's source channel and content type, not a
separate stored field. Terminal Cases other than Complete are excluded from
the Cases rail and appear in Search as `Closed · <outcome>`.

Unidentified detail shows the kind/received/reason facts, the retained file
or message by its operator-meaningful handle, one link to the underlying
retained material, chronological history, and the resolution form
(Destination: add to an existing Case, create a Case from an accepted
instruction, register an Image-initiated Case, or close with a reason).
Exact U-reference search returns both open and resolved items as a distinct
result type and never treats U<n> as a Case, Audit, or Image Intake
reference. Resolution is staff-authorised, antiforgery protected,
version-checked, idempotent by operation key, and requires a supported
destination and reason. A stale version is a non-destructive conflict; a
replay shows the original result. The permanent U-reference and origin
remain visible after resolution.

Triage detail carries the determinations (roadworthiness, repair outcome),
the source facts and notes; the existing server-side transitions remain
reachable where a handler exists ([FRD-03](frd-03-triage.md)).

### Search

`/Search` carries the `UI-07` filter set — Case/PO or image reference,
Registration, Claimant, Claim/provider reference, Principal, State,
Engineer, Received from/to, Origin — with Search and Clear. Results are one
table (Case/PO and provider reference, vehicle, claimant, principal, type,
state, due); pointer or keyboard intent on a row shows a selected-Case
preview (type, state, accident circumstances, provider reference, Engineer,
due, next action, outstanding requirements, Open Case, copy Case/PO) beside
the table, stacking after it at constrained width. Image-initiated Cases are
searchable by VRM reference or registration and use the named states
Awaiting instruction, Merged into Instruction-initiated Case, and
Staff-closed.

### Case workspace

`/Cases/{id}` shows an identity ribbon (Case/PO, registration, claimant,
principal, state), the presence strip, the action bar and six sections:
**Overview**, **Vehicle**, **Valuations**, **Inspection address**,
**Case Files**, **Notes**. A context column keeps the current position
(state, version, due, Engineer, edit authority) and the next action visible.

The action bar offers only actions the Core use cases permit for the current
state: Edit Case / Finish editing / Renew editing, or the holder and expiry
when another account holds the lease ([FRD-01](frd-01-case-identity-and-lifecycle.md#case-edit-authority-and-recovery));
Place on Hold / Release Hold; Create upload link; **Send to EVA** in Review
or **Download EVA package** once exported ([FRD-07](frd-07-eva-and-external-engineering-handoff.md));
**Report sent** in With Engineer, which confirms detected or linked Sent
evidence and enters post-report work — it never completes the Case and
never records a manual assertion ([FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md));
**Return to Engineer** in Complete; **Open Assessment** in With Engineer or
Complete only; Close Case when not Complete; Reopen Case when closed. Every
hold, release, close and reopen takes a reason. Editing shows a sticky bar
with the lease text, an unsaved marker, Discard and Save; saving in Review
warns first; a stale version shows the current and proposed values as a
non-destructive conflict.

- Overview: the workflow position (Not ready → Review → With Engineer →
  Complete, with Held as an exception badge), outstanding requirements
  (title, source, reason, resolve action), the edit form (claimant, provider
  reference, registration, make, model, accident circumstances) and the
  work, party and accident facts.
- Vehicle: registration, make, model, year, mileage and its source; vehicle
  checks (DVLA / DVSA-MOT refresh, `EXT-01`) and the vehicle-history
  narrative ([FRD-06](frd-06-vehicle-and-engineering-evidence.md#vehicle-data-and-mot-enrichment)).
- Valuations: each observation with source, date, time, mileage, retail and
  trade values, and Add valuation (`EXT-10`).
- Inspection address: recorded value, provider default, previous values,
  edit ([FRD-06](frd-06-vehicle-and-engineering-evidence.md#inspection-address)).
- Case Files: documents with custody state, preview and save-as; the
  retained vehicle-image gallery (`CASE-006`) whose lazy-loaded thumbnails
  expand to the full image with the original filename as the accessible
  name, served only by an authorised staff endpoint that returns the stored
  image media type inline — non-image material stays on the forced-download
  route; and linked correspondence with its actions
  ([FRD-08](frd-08-email-mailbox-and-background-processing.md#outbound-correspondence-evidence)).
- Notes: Case notes, business events, chase outcomes and AI events merged
  newest first, each with date, time and actor; Add Case note and Record
  chase while editing ([FRD-01](frd-01-case-identity-and-lifecycle.md#due-work-chasing-and-action-history)).

The case list and persistent identity area expose due/overdue state, while
the workspace keeps the missing-material reason, next chase, last recorded
outcome, and next permitted action together. Triage has no due/chaser
presentation. The Image-initiated record page remains the image record
(D1) and still renders its image gallery alongside preserved
filenames/group evidence, custody, and chronological merge/closure history;
staff closure is
a reasoned action, terminal records are read-only, and it is not a generic
Close control.

### Assessment

`/Cases/{id}/Assessment` opens for a Case in With Engineer or a later state,
never in Review, and is read-only once Complete. It carries the evidence
rail (instruction and images), the estimate set (`EXT-09`: named estimates
with source, repair days, rates, materials, other costs, a free VAT
percentage, lines and totals; one estimate is Current and drives the
report), Import estimate (`EXT-12`), Send to Claude (`AI-09`, disabled
without an Engineer's Value) and the report-draft generation and preview
([FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#report-draft-entry-point)).

### Operations

`/Operations` shows, with a partial-data notice when any query is not
current: the **AI Job List** (`AI-10`: kind, record, started by, created,
state, next action, Send Unidentified to AI); **Service health** (area,
service, state, latest evidence, dependency, retry); **Attention required**
(retryable external work with attempts, failure and Retry); **Active upload
links** (recipient, last activity, accepted, expiry, state, Withdraw); and
**EVA handoffs** (route, Engineer, state, result).

### Administration

`/Admin/{area}` carries eight areas for authorised accounts
([FRD-04](frd-04-parties-accounts-and-access.md)): **Staff accounts &
roles**, **Principals** (one area; an organisation is created inline by
Create Principal and is never a separate area), **Workflow configuration**,
**Mail settings**, **Automation & AI**, **Service health** (the Operations
table), **Action Logs** (the permanent action history with search, area,
actor, result and date filters) and **Reports** (the Engineer Report,
`MI-01`: per Engineer and period, queries received and reports). Every
consequential change — role, account state, principal credential, automation
stop/start — takes a reason and enters permanent history.

### Workspace tabs, command palette and keyboard

The tab strip holds the Work Centre plus one closable tab per open Case
record, at most four, evicting the least recently used. Tabs are a
browser-local convenience only: they never carry state, are never shared
between accounts or devices, and their absence changes nothing.

The command palette (Ctrl K, the global search field, the Open button)
finds Cases, references and routes by typing and opens the selection.

| Key | Action |
| --- | --- |
| Ctrl K | Command palette |
| Ctrl U | Upload |
| Ctrl N | Create Case |
| Ctrl S | Save while editing |
| F5 | Refresh — re-query, not a page reload |
| Arrow Up / Down | Move through a row list |
| Enter | Open the selected row |
| Escape | Close the open dialog |

A shortcut never bypasses a reason, confirmation or gate.

### Breakpoints

Content is centred at up to 1580px. Below 980px the rail becomes a
horizontal bar with icons only and the current route marked by a bottom
border. Below 760px every layout is a single column; two- and three-pane
pages stack in reading order with identity, state and action context kept.
No required evidence or action is hidden at any width or at 200% zoom.

### Display labels

Case states show their display labels; the Core lifecycle enum is
untouched. `ReportPreparation` and `PostReport` display as **With
Engineer**, `PostReportComplete` as **Complete**, and every other terminal
state as `Closed · <outcome>` in Search. The mapping is owned by
[FRD-01](frd-01-case-identity-and-lifecycle.md). `Audit`, `Triage`,
`Blocked intake` and `Unidentified` keep their settled meanings.

### Upload

Selected files render one row per file — name, size, and a per-file state
that is a spinner while the submission is in flight and a tick once the
response confirms the file is durably stored; a failed file states its
failure on the same row. Every row enters the in-flight state together,
since a single submission stores the whole batch and no finer per-file
signal exists during it — no row is ticked ahead of what the response
actually proved. No mechanics narration ("receipt", "submission group", or
similar internal vocabulary) appears on the Upload or status surfaces.

Once a file's processing resolves, the status surface shows a confirmation
outcome rather than a passive label: what already happened automatically
(reported, with a link to open it and, where relevant, the existing reversal
path — never re-offered as a choice), or the staff decision that is
genuinely open. Where it is open, the surface offers the decision itself:
the suggested action (create a case from what was uploaded, or — for a
just-registered vehicle-image case — the registration is reported with its
reference and link), **Add to an existing case**, and **Cancel**. Add to an
existing case opens a case search that suggests matching cases as the
operator types (keyboard-operable, with the active suggestion marked by more
than colour); choosing a case and confirming with a reason attaches the
uploaded material to it as an explicit staff decision. Cancel changes
nothing — the material stays retained with its state honestly shown. The
exact decision table and the attach contract are owned by
[FRD-02](frd-02-intake-and-source-identity.md#upload-confirmation-surface).
A grouped upload shows this per file; members of the same group can resolve
independently and are never collapsed into one group-wide outcome.

### Dashboard freshness and reconciliation

Every count and query exposes its last successful update time and current
refresh state. `0`, loading, current, stale-with-last-good-time, partial,
unavailable, and failed are distinct outcomes. A refresh never replaces a
last-good value with a false zero, merges partial data into an apparently
complete result, or implies that an external action succeeded.

Manual refresh reruns the same exact filtered query; it does not change
policy or create a business transition. Its caller, start/end time, sources,
and success/partial/failure result remain auditable in content-safe
telemetry. Reconciliation that accepts, rejects, links, or changes an
external business fact instead enters permanent business history with the
responsible actor, source/version, before/after values, time, and reason
where required.

`New cases today` counts every instructed Case created in the current
Europe/London calendar day, including a Case later closed that day. It
excludes Image-initiated Cases, Triage, Unidentified, and `Blocked intake`.
The Unidentified count is the exact count of open Unidentified items and
links to that queue. These are separate from `Due today`, `Sent to
Engineer`, and `Reports sent`. `Due by` and overdue/chaser work remain a
separate operational view from `New cases today`.

## States and transitions

Every surface renders exactly one of: loading, empty, current, stale (with
the last-good time), partial, unavailable, failed, validation, conflict, or
access denied. The UI never infers state from colour alone, never uses
decorative glyphs as unlabeled controls, and never presents draft, queued,
attempted, allocated, or configured work as completed, delivered, deployed,
or accepted. Workflow transitions are owned by
[FRD-01](frd-01-case-identity-and-lifecycle.md#case-identity-and-lifecycle);
the UI offers a transition only where its Core use case permits it for the
current state and account.

## Edge cases and fail-closed behaviour

- A count whose query has not run renders nothing; a failed query renders
  its failure, never `0`.
- An action bar for a state with no permitted action shows the state and no
  control.
- An integration without a composed caller shows its named disabled seam
  and nothing else; when the seam has no ticket the control is absent.
- A lost or expired edit lease surfaces the holder and expiry and disables
  Save; a stale version is a non-destructive conflict.
- Tabs and palette history that cannot be read are treated as empty; the
  page renders correctly with none.
- A redirect from a removed route keeps the query it was given.

## Acceptance evidence

Authenticated Web and real-browser tests prove: every rail route and its
count, both redirects, the removed `/VehicleImages` list, the Cases rail
groups and filters, the Work Centre kinds against Core queries, the
Assessment access rule, the tab limit and eviction, the keyboard map, axe
accessibility, focus behaviour and no document overflow at 1580, 1100 and
760px. Snapshot and catalogue checks are owned by
[design § Test UI](../design/README.md#test-ui). Deployment and live
acceptance remain separate evidence tiers
([engineering](../engineering.md#required-evidence-tiers)).

## Links

- Capabilities: `UI-01`–`UI-09`, `UI-11`, `UI-13`, `UI-16`, `UI-07`
  (Search), `AI-10`, `EXT-09`, `EXT-10`, `MI-01` in
  [capabilities](../capabilities.md#capabilities).
- Related FRDs: [FRD-01](frd-01-case-identity-and-lifecycle.md),
  [FRD-02](frd-02-intake-and-source-identity.md),
  [FRD-03](frd-03-triage.md),
  [FRD-04](frd-04-parties-accounts-and-access.md),
  [FRD-06](frd-06-vehicle-and-engineering-evidence.md),
  [FRD-07](frd-07-eva-and-external-engineering-handoff.md),
  [FRD-08](frd-08-email-mailbox-and-background-processing.md),
  [FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md).
- Design: [design](../design/README.md) owns the durable interaction,
  visual, component, and source/runtime rules.
- Technical constraints: [ADR-0029](../adr/0029-image-initiated-case-projection.md)
  (Image-initiated projection), [ADR-0031](../adr/0031-automation-actor-contract-without-eva-export-tools.md)
  (Automation Actor contract).
