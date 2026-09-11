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
[open decisions — later operator UI capabilities](../open-decisions.md)
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

Screen-reader-compatible semantics remain required behavior. This requirement
does not itself claim interoperability with Narrator or any other screen reader,
complete WCAG conformance, subjective usability, or operator acceptance.

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
[capabilities](../capabilities.md) records it as a disabled
seam; an inert control is never rendered. Labels, values and controls carry
no explanatory copy
([design § No explanatory copy](../design/README.md#no-explanatory-copy-and-page-economy)).

### Shell and routes

Every authenticated page renders one shell: a persistent rail, a utility
bar, the workspace-tab strip and the page content. The rail carries, in
order, **Work Centre** (`/`), **Inbox** (`/Inbox`), **Upload** (`/Upload`),
**Cases** (`/Cases`), **Search** (`/Search`), **Operations**
(`/Operations`) and — for administrators only — **Administration**
(`/Administration`). Inbox, Cases and Operations carry a count; the Cases count is the
sum of Not ready, Review, With Engineer, Query, Held, Triage and Unidentified. A
count is a page-queried figure: an absent count renders nothing, never `0`.
The current route is marked by more than colour. The rail foot shows the
freshness line and the signed-in account (name, role, account dialog with
session start, idle lock, sign out).

The utility bar carries the page freshness text, the global search input
(Enter or Ctrl K opens the command palette), **New case** opening the direct
staff creation form, and notifications. Upload and Inbox retain their named
navigation; Case upload links remain contextual actions.
A notification-query failure must not block the page unless it represents
cancellation or failed authorization. The dialog shows `Notifications
unavailable.` without placeholder or stale rows, and the failure is logged.
**Create Case** opens direct staff creation with the identity-critical Case
facts and no fictional intake receipt or source provenance. When opened from a
received item, the same form uses that existing receipt and the normal intake
allocation path rather than creating a second allocation route (D26,
[FRD-02](frd-02-intake-and-source-identity.md#ways-intake-starts)).
A skip link precedes the rail; toasts announce in a live region; every
dialog traps focus and inerts the page behind it.

| Route | Purpose | Replaces |
| --- | --- | --- |
| `/` | Work Centre — needs-attention work and the metric strip | Dashboard |
| `/Inbox`, `/Inbox/{id}` | Retained mail list and message ([FRD-08](frd-08-email-mailbox-and-background-processing.md)) | — |
| `/Upload`, `/Uploads/{token}` | Staff upload and the public upload request ([FRD-02](frd-02-intake-and-source-identity.md#upload-confirmation-surface)); first successful file acceptance starts a fixed non-sliding 15-minute add/replace session, closed by explicit finalisation or expiry (D20) | — |
| `/Cases` | Queues: workflow, pre-Case work and exceptions | Queues (`/Triage`) |
| `/Cases/{id}` | Case record — one scrolling page of ten sections; `?section=` jumps (D29, D30) | Case workspace side-nav sections; the Assessment page |
| `/Cases/{id}/Assessment` | Permanent redirect to `/Cases/{id}?section=estimate` (D30) | Engineer assessment page |
| `/Search` | Advanced search (`UI-07`) | Cases list |
| `/Triage/{id}`, `/Unidentified/{id}` | Triage and Unidentified detail | — |
| `/Operations` | AI jobs, attention, upload links, EVA handoffs; a one-line partial-data notice links to Administration Service health (D37) | Operations Service health table |
| `/Administration`, `/Administration/...` | Administration areas, including Contacts and Principal settings | Separate Principal and Claim Source areas |

`/Triage` and `/Unidentified` are permanent redirects to `/Cases?tab=triage`
and `/Cases?tab=unidentified`, kept for existing links and bookmarks rather
than left dead. The `/VehicleImages` list route is removed; the vehicle-image
detail page remains the image record and is reached from Awaiting-instruction
Image-initiated rows, the Case Files section and upload outcomes. There is no
separate top-level Unidentified, Organisations, Staff accounts, Roles or
Automation Activity entry. `/Cases/{id}/Assessment` is a permanent redirect
to `/Cases/{id}?section=estimate` (D30): the Engineer workbench is a set of
sections on the Case record, not a page of its own.

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
| Workflow | Not ready, Review, With Engineer, Completed, Query |
| Pre-Case work | Triage, Awaiting instruction |
| Exceptions | Held, Unidentified |

Awaiting instruction lists the Image-initiated Cases still awaiting an
instruction; it is Pre-Case work beside Triage, never a workflow queue, and
its rows show reference, registration, image count, custody, received, source
and chase facts (D38). `?tab=` selects the queue. Image Intake detail shows
**Open Triage** when a Triage record shares the same origin receipt; there is
no "Open in Box" action, because no Box destination is exposed for Image
Intake material in this UI — a known limitation, not a designed omission.
Filters are Principal (every queue) and, on Not ready only, Missing — `All`,
`Instructions`, `Images`, `Both missing` — plus Clear. Each queue keeps its
own row shape rather than being forced into one column set: a Case row
carries reference and registration, state, claimant and principal, origin and
received, due; an Image-initiated row carries its VRM reference,
registration, file count and custody; a Triage
row carries reference, registration, provider and assignee; an Unidentified
row carries the U-reference, kind, operator-meaningful handle (the original
filename, or the e-mail subject and sender — never an internal identifier),
received date and time and the canonical reason. Awaiting-instruction rows
select their quick detail; every other row links directly to its full detail.
Selecting a row shows a quick detail: for a Case its
origin, compact workflow position, outstanding requirements and current work
(due, Engineer, next action) with Open full Case; for other kinds the
definition list and the open action, with Add to an existing case on an
Awaiting-instruction image record.

Not ready contains only formal instructed Cases; an ImageIntake-backed
projection still awaiting an instruction is listed under Awaiting instruction
(D38) with its origin visible. Unidentified media kind (`Images` or
`E-mails`) is
derived from the retained receipt's source channel and content type, not a
separate stored field. Completed and Query are reversible workflow states. Cancellation, rejection
and Created in error are recorded dispositions, not terminally Closed Cases.

Unidentified detail shows the kind/received/reason facts, the retained file
or message by its operator-meaningful handle, one link to the underlying
retained material — **View email** or **View file**, chosen by media kind —
and chronological history. **Link to Case** is the primary action, opening a
compact dialog; **Create Case**, **Register images** and **Close** remain as
named alternatives inside that same dialog (Destination: add to an existing
Case, create a Case from an accepted instruction, register an
Image-initiated Case, or close with a reason). There is no "Resolution" or
"Resolve material" framing in the UI; the dialog is titled with the item's
own U-reference. Exact U-reference search returns both open and resolved
items as a distinct result type and never treats U<n> as a Case, Audit, or
Image Intake reference. The underlying operation is staff-authorised,
antiforgery protected, version-checked, idempotent by operation key, and
requires a supported destination and reason. A stale version is a
non-destructive conflict; a replay shows the original result. The permanent
U-reference and origin remain visible after resolution.

A Received item (intake receipt) shows one effective state after
association: once linked, its Decision panel and list rows read **Linked to
Case** regardless of the decision that first proposed a Case; there is no
separate "Ready for case allocation" state once association exists. Before
association, the panel heading is **Decision** and shows the current decision
label and, where the receipt names an existing draft, the **Link to Case**
control.

Triage detail carries the determinations (roadworthiness, repair outcome),
the source facts, a `History` view that merges durable events with append-only
attributable notes in chronological order, and a `Files` view of the retained
sources, their attachments and the linked vehicle images with view and download
(D25). A correction is a new note; there is no note edit, no note delete and no
upload action on Triage. Its single retained source link reads **View email**
or **View file**, chosen by media kind, replacing a former generic "View
retained source". **Assign to Engineer** is a button opening a compact dialog
(engineer select, Assign/Cancel); assignment records no reason, while the
determination reason and other meaningful Triage decisions keep their
required reasons ([FRD-03](frd-03-triage.md#normal-workflow-and-completion-evidence)).
Completion records the decided outcome. Optional
**Reply with outcome** opens the email feature with an editable preset template;
the sent correspondence attaches to the Triage and is never a completion gate.
The existing server-side transitions remain
reachable where a handler exists ([FRD-03](frd-03-triage.md)).

### Search

`/Search` carries the `UI-07` filter set — Case/PO or image reference,
Registration, Claimant, Claim/provider reference, Principal, State,
Engineer, Received from/to, Origin — with Search and Clear. Results are one
table (Case/PO and Our ref, vehicle, claimant, principal, type,
state, due); pointer or keyboard intent on a row shows a selected-Case
preview (type, state, accident circumstances, Our ref, Engineer,
due, next action, outstanding requirements, Open Case, copy Case/PO) beside
the table, stacking after it at constrained width. Image-initiated Cases are
searchable by VRM reference or registration and use the named states
Awaiting instruction, Merged into Instruction-initiated Case, and
Staff-closed.

### Case workspace

`/Cases/{id}` is one scrolling page (D29) with no separate page header: three
rows travel together as the record scrolls — a sticky identity ribbon
(Case/PO, registration, claimant, principal, state, with Engineer and
Sign-off Engineer beside it — D31, plus small Back to Cases and Refresh icon
buttons at the ribbon's end), one sticky action row (Edit Case, or while
editing Cancel and Save with a small Editing badge, then the progression
action(s) permitted for the state, then More actions and Close case), and a
sticky section jump-nav whose current entry follows the scroll position. A
thin presence strip appears between the ribbon and the action row only while
another account holds the Case edit lease; the holder's own edit is shown by
the Editing badge, not a strip. `?section=` jumps to a section; sections
below the fold render lazily. The refined Scroll/Tabs choice uses the same
section hosts and one edit form. Tabs hide inactive sections without
discarding loaded values; Scroll remains the no-script fallback. The
sections, in order, are **Overview**,
**Inspection**, **Vehicle**, **Damage**, **Valuation**, **Estimate**,
**Settlement**, **Report**, **Files**, **Notes**. Every
section is always viewable; the Engineer sections — Damage, Valuation,
Estimate, Settlement, Report — are editable in With Engineer and read-only
in every other state (D30; the former D11 access rule is now this read-only
rule).
The whole record enters one edit mode over one lease
([FRD-01](frd-01-case-identity-and-lifecycle.md#case-edit-authority-and-recovery)).
While editing, each section renders its one edit form in place of its
read-only view — never both — so the Overview and Inspection sections each
show exactly one panel per mode; the edit-mode Overview form uses the label
**Claim reference** for the provider's claim number everywhere it appears
(Our ref is the separate, immutable Case reference), and the Registration,
Make and Model inputs move to the Vehicle section's own edit state rather
than appearing on Overview.

The action bar offers only actions the Core use cases permit for the current
state: Edit Case, one global Save/Cancel while editing, Renew editing when
needed without script, or the holder
when another account holds the lease ([FRD-01](frd-01-case-identity-and-lifecycle.md#case-edit-authority-and-recovery));
Place on Hold / Release Hold; Create upload link; **Hand to Engineer** in
Review while editing — its dialog selects an eligible Engineer and the one
handoff assigns them and enters With Engineer. Handoff is review; there is
no reviewed checkbox or separate Start report preparation action.
The Principal's report-generation policy determines EVA work: **EVA ZIP**
offers the export, **manual EVA API** offers Send via API, and **automatic EVA
API on Review** permits a staff retry only after its automatic delivery failed.
Pegasus generation has no EVA action. EVA never gates native engineering
([FRD-07](frd-07-eva-and-external-engineering-handoff.md));
**Report sent** in With Engineer, which confirms detected or linked Sent
evidence and enters post-report work — it never completes the Case and
never records a manual assertion ([FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md));
**Return to Engineer** in Completed or Query when engineering changes are needed.
Query receipt or attachment moves Completed to Query; replying returns it to
Completed without a separate reopen. **Close case** is the one
adverse-disposition action, visually separated from ordinary progression. Its
compact dialog offers only the outcomes Core currently permits for the
Case — `Provider cancelled` or `Collision Engineers rejected` — with a
required outcome and reason, and posts to the existing Closure handler; a
missing or unrecognised outcome is refused rather than defaulting. `Created in
error` (**Correct principal**) and `E-mail unlinked` keep their own dedicated
actions and are never offered by this dialog; `PostReportComplete` remains the
ordinary **Mark completed** progression, not part of Close case. Closing never
deletes a Case ([FRD-01](frd-01-case-identity-and-lifecycle.md#lifecycle-closure-and-correspondence)).
Holds, releases, corrections and a return to engineering record a reason. Editing shows a sticky bar
with the lease text, an unsaved marker, Discard and Save; saving in Review
warns first; a stale version shows the current and proposed values as a
non-destructive conflict.

- Overview: the workflow position (Not ready → Review → With Engineer →
  Completed ⇄ Query, with Held as an exception badge), outstanding requirements — the
  named unmet items of the versioned instruction- and image-completeness sets,
  each with title, source, reason and resolve action, and never a percentage
  (D23) — the edit form (claimant, Our ref, registration, make,
  model, accident circumstances) and the work, party and accident facts.
- Inspection: the recorded inspect-at value with its fast-update choice —
  Image Based Assessment, Claimant address, Repairer location, Storage
  location, previous addresses used for this principal, Manual entry; an
  option without a value is disabled. The read view shows one row per fact
  not already implied by the row above it: Inspect at (the recorded address
  or `Image Based Assessment`, with its mode chip shown only when the mode
  says something the value itself does not), Principal default shown only
  when it differs from the recorded value, Storage location, and Repairer
  (D33, [FRD-06](frd-06-vehicle-and-engineering-evidence.md#inspection-address)).
- Vehicle: registration, make, model, year, and one mileage with its
  provenance (Extracted · Lookup · Staff); one **Look up DVLA & MOT** action
  (`EXT-01`) whose looked-up values fill an empty Make, Model, Year, or
  Mileage directly and never overwrite an extracted or staff-entered value —
  no checks panel and no suggestion table; Run Experian check stays the
  disabled seam (D34 amended, 2026-09-11) — and a labelled Vehicle history
  area (the history-check narrative, read-only text, editable in edit mode,
  [FRD-06](frd-06-vehicle-and-engineering-evidence.md#vehicle-data-and-mot-enrichment)).
  A **DVLA & MOT lookup** outcome line states what the lookup itself did
  (looked up and current, or a failure reason), separately from whether it
  ever filled a field.
- Damage: the zone list with severity and note per zone; tyres and
  seat belts per corner, spare tyre and centre belt; unrelated damage with
  its deduction; paint or material transfer; impact location and severity
  shown as derived values (D39,
  [FRD-06](frd-06-vehicle-and-engineering-evidence.md#damage-record)).
- Valuation: each entry with source, date, time, mileage, retail and trade
  values, plus guide month per entry (`CASE-029`), and Add valuation
  (`EXT-10`); sources are Glass's valuation, Brego and Super CAP manual entries,
  Cazana (disabled seam), Engineer's Value and AI market research (automation
  only) (D40); requesting AI market research creates a `MarketResearch` job (D35,
  [FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#ai-job-list)).
- Estimate: the estimate set and raw estimate import (§ Assessment).
- Settlement: outcome, category, salvage value, excess, betterment, claimant
  VAT registered, reserve, equity (derived), repair duration and delays,
  report delay, storage per day, recovery, hire start and daily cost,
  diminution, salvage logistics; financial ratio lines are permitted (D41,
  [FRD-06](frd-06-vehicle-and-engineering-evidence.md#settlement)).
- Report: **Report position** (D19, `ENG-031`) — one Close-up, one Overview,
  the rest Supporting, with order, rotation and crop — is the only
  report-composition control; renamed from "Report images" because an
  image's own classification is now its tags (Files, Images tab), not this
  section. Also the readiness list of named outstanding items, the agreed
  fee and description lines with the fee note preview (D42), and Generate /
  Preview report draft
  ([FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#report-generation-entry-point));
  the report renders the sign-off Engineer tuple (D31) and the marked damage
  diagram (D39).
- Files: one panel with two tabs, both rendered so a no-script visit shows
  the two lists one after the other under their own headings. The panel
  header's Add evidence, Open Box case folder (or the folder's own state
  chip before custody is confirmed) and Open Operations actions, plus
  Create upload link and its request table, sit above both tabs; linked
  correspondence sits below them
  ([FRD-08](frd-08-email-mailbox-and-background-processing.md#outbound-correspondence-evidence)).
  - Documents: every live file as a row — filename, role, size, origin,
    recorded time and its custody-state chip, with Preview, Save as and,
    while editing, delete.
  - Images: one grid of every image occurrence — the Case's own image
    documents plus, for each image-intake associated with the Case, its
    photographs labelled by Image Intake Reference. Each tile shows a
    lazy-loaded thumbnail that expands to the full image with the original
    filename as the accessible name, served only by an authorised staff
    endpoint that returns the stored image media type inline (non-image
    material stays on the forced-download route); its applied tag chips
    (image tags, [FRD-05](frd-05-documents-extraction-and-custody.md#image-tags));
    a Tag picker naming every vocabulary entry plus New tag with a colour,
    for a Case image only; and Preview plus, while the Case edit lease is
    held, Crop. The lease gate is the same one the record's whole edit mode
    uses ([FRD-01](frd-01-case-identity-and-lifecycle.md#case-edit-authority-and-recovery)):
    unavailable once the Case reaches Completed or Query, and never on an
    archived Case, but no longer tied to With Engineer or report
    preparation — a Review-state Case now shows Crop. An image-intake
    photograph carries no tags and no Crop — it is a receipt, not yet a
    Case document.
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

The Engineer workbench is the Damage, Valuation, Estimate, Settlement and
Report sections of the Case record (D30); `/Cases/{id}/Assessment` is a
permanent redirect to `/Cases/{id}?section=estimate`. The sections are
always viewable and read-only in Completed. Report position lives on the
Report section: distinct `Close-up` first and `Overview` second, optional
supporting images in explicit order, and non-destructive crops that leave
the retained source and its hash untouched (D19). The Estimate section
carries the estimate set (`EXT-09`: named estimates with source, repair
days, the selected labour-rate-card snapshot, VAT categories, lines and
totals; one estimate is Current and drives the report). Each version's card
prices both panel and paint hours. Its own VAT percentage defaults to 20 and
applies to selected discounted Labour, Parts, Materials and Specialist
categories. Unknown repairer VAT blocks Use as Current until staff record an
explicit status or categories (D9, D17); no comparison or savings figure is
shown. It also carries Send to Claude, which creates an `AI-10`
[AI Job List](frd-11-reports-correspondence-and-reviewed-proposals.md#ai-job-list)
`Estimate` job (disabled without an Engineer's Value) rather than the
distinct, DevelopmentOffline-only `AI-09` transport; the report-draft
generation and preview sit on the Report section
([FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#report-generation-entry-point)).

Raw estimate import (`EXT-12`) is a whole-page drop surface. One file is
imported immediately, with no confirmation step and no visible file picker
(D16, 2026-09-01). Only currently registered
parser types are accepted; the provider and parser are auto-detected and an
ambiguous artifact is refused rather than guessed. The resulting Draft is named
by provider plus sequence, and the filename, source hash, provider/parser,
actor, channel and outcome are recorded. Dropping the same artifact on the same
Case again replays the existing Draft; a different artifact creates the next
immutable Draft
([FRD-06](frd-06-vehicle-and-engineering-evidence.md#canonical-repair-specifications)).
The drop is pointer-only, and D16 records that as a narrow accepted
accessibility exception rather than explaining it away: it is the only staff
route to raw-artifact import, so a keyboard-only operator cannot perform this
import. The same Core command is reachable without a pointer only through the
MCP `pegasus_estimate_import` tool
([FRD-10](frd-10-mcp-automation-and-actor-boundary.md#ai-job-and-estimate-tools)),
which is an automation boundary and not a staff keyboard route. Manual line
entry in the estimate editor stays keyboard-reachable but is a different
capability: it retains no source artifact, hash or parser provenance.

### Operations

`/Operations` shows, with a partial-data notice when any query is not
current: the **AI Job List** (`AI-10`: kind, record, started by, created,
state, next action, Send Unidentified to AI) — started by names the staff
username or the Automation client name by the same resolution Action Logs
uses, never a raw subject identifier; **Attention required**
(retryable external work with attempts, failure and Retry); **Active upload
links** (recipient, last activity, accepted, expiry, state, Withdraw); and
**EVA handoffs** (route, Engineer, state, result). Service health is
Administration-only; Operations carries no service health table, and its
one-line partial-data notice links to Administration Service health (D37).

### Administration

`/Administration` carries **Accounts**, **Contacts**, **Workflow
configuration**, **Mail settings**, **Valuation presets**, **Service health**,
**Action Logs**, **Reports** and **AI jobs**; Automation appears only when its
capability is composed. Contacts is the one directory for Principals, Claim
Sources, Repairers, Storage and Third Party Engineers. Principal-specific
settings are part of that Contact record; there are no separate Principal or
Claim Source areas. Every
consequential change — role, account state, principal credential, automation
stop/start — takes a reason and enters permanent history. The **Staff accounts
& roles** area carries a **Reset password** action beside the other account actions:
Pegasus generates a temporary password and visibly reveals it to the Administrator
in the protected confirmation UI (FRD-04 D15). The Administrator can convey it
to the user; staff accounts are not email-bound and no automatic email is sent.
The existing policy and non-reversible hashing apply and forced change is set
for the next sign-in. The stored secret cannot subsequently be retrieved (D15,
[FRD-04](frd-04-parties-accounts-and-access.md#staff-accounts)).

**Action Logs** names who acted rather than a raw identifier: a staff subject
resolves to their username, an unresolvable staff id (a deleted account)
reads **Former staff**, the Automation client renders as an **AI** chip with
its registered client name, and Worker-attributed work reads **Pegasus**. A
legacy security row recorded before the acting principal was captured carries
no actor kind and is labelled by its event type instead of a guessed user. An
**Actor type** filter (Staff / AI / Pegasus) narrows the list by this same
kind. An AI job row links through to the Case or Unidentified record it acted
on. Time values and the From/To period pickers display and accept only
whole minutes; recorded storage keeps its existing sub-second precision.

**Reports** shares one London period filter across MI-01 Engineer activity,
MI-02 Reports by Principal (per-Principal report counts by type) and MI-03
Turnaround (current holding age and instruction-to-produced/ready/sent
turnaround), each with its own totals and downloadable CSV. A section whose
query fails or returns invalid data renders an unavailable state rather than
a false zero.

Contacts accepts a telephone value of digits, spaces and an optional leading
`+` only (UK numbers are written with a leading `0` and internal spaces);
letters and other characters are rejected on both the field and the server.
The Contacts list filters live as the operator types, 300 milliseconds after
the last keystroke, alongside the existing Apply control for no-script use;
server-side filtering is unchanged. Where Automation is composed, each of its
scopes (`automation.cases`, `automation.intake`, `automation.documents`,
`automation.assessment`, `automation.mail`, `automation.jobs`) renders as a
plain label — Cases, Intake, Documents, Assessment, Mail, AI jobs — with the
underlying key kept only as a hover title.

**Workflow configuration** holds the versioned instruction- and
image-completeness rules as required/not-required items with exact blockers,
and the chase interval as one global whole-calendar-day value (1 to 365,
default 7, Europe/London), where `Held` preserves the remaining time (D23).
It has no staff instruction-review or image-review settings. The default view
shows current values; Edit claims the configuration, Save confirms a concise
reason, and Cancel discards changes. It also holds labour-rate-card administration: the
global versioned cards (name, panel-and-paint hourly rate, enabled state) that
every estimate version selects from, with disabling blocking future selection
without changing history (D17). It stays inside that area; no ninth area is
added.

**Valuation presets** adds and edits inline: a compact add row and in-row
editing, with no separate creation or edit dialog. Its existing five-minute
record-scoped edit lease still applies to an in-progress edit. **Remove** is
a soft, reasoned removal: the preset drops out of the list and out of new
selection, but a valuation already recorded against it keeps its own
snapshot, and that recorded addition is immune to any later removal, version
change or disabling of the preset it was recorded against — the calculation
basis carries the Case's recorded additions forward rather than
re-resolving them.

Review-gated transitions calculate completeness from persisted facts inside
the transaction. A submitted readiness claim or staff-confirmation checkbox is
not authority; those checkboxes are retired (CASE-046, PLAT-072).

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

One accepted exception to keyboard parity exists: the Case record's
whole-page raw-estimate drop is pointer-only (D16). It is recorded here rather
than left implicit, and it is a real gap — a keyboard-only operator cannot
import a raw estimate artifact. Every other staff action on the Case record
remains keyboard-reachable.

### Breakpoints

Content is centred at up to 1580px. Below 980px the rail becomes a
horizontal bar with icons only and the current route marked by a bottom
border. Below 760px every layout is a single column; two- and three-pane
pages stack in reading order with identity, state and action context kept.
No required evidence or action is hidden at any width or at 200% zoom.

### Display labels

Case states and transition meanings are owned by
[FRD-01](frd-01-case-identity-and-lifecycle.md). Completed and Query are
reversible work states; no terminal Closed presentation is permitted. `Audit`, `Triage`,
`Blocked intake` and `Unidentified` keep their settled meanings.

### Upload

Manual and public-link uploads follow the accepted 100 MiB-per-file, 20-file
and 200 MiB aggregate limits in
[FRD-02](frd-02-intake-and-source-identity.md#source-upload-limits). The
Provider API envelope stays 30 MB and is owned by
[FRD-09](frd-09-provider-and-intermediary-routes.md#provider-api-principal-and-contract-boundary).
The authenticated staff `/Upload` route is available only where durable
production intake and case custody exist; a production-local-only store is not
an accepted custody path, and without durable custody the route is absent
rather than offered.

Selected files render one row per file — name, size, and a per-file state
that is a spinner while the submission is in flight and a tick once the
response confirms the file is durably stored; a failed file states its
failure on the same row. Every row enters the in-flight state together,
since a single submission stores the whole batch and no finer per-file
signal exists during it — no row is ticked ahead of what the response
actually proved. No mechanics narration ("receipt", "submission group", or
similar internal vocabulary) appears on the Upload or status surfaces.

Once a manual upload's processing resolves, the status surface shows an
explicit destination decision rather than a passive label. Even one matching
Case requires staff confirmation: the screen offers viable existing Cases and
an editable new-Case proposal, whose acceptance is the only point at which a
formal Case/PO may be allocated. The proposal states that it creates nothing
until accepted; reject/cancel changes nothing and leaves the material retained.
**Add to an existing case** is keyboard-operable (the active suggestion is
marked by more than colour). Search failures are visibly
different from no matches, and a response for an earlier query cannot replace a
newer input. A grouped upload has one server-bound submission decision and
reports partial completion honestly. The exact decision table and attach
contract are owned by
[FRD-02](frd-02-intake-and-source-identity.md#upload-confirmation-surface).
A grouped upload shows one server-bound submission decision with the per-file
processing and outcome details beneath it (D20). Its target is shared, while
each member keeps its own reviewed receipt version and durable
result; a partial outcome is reported rather than represented as success.

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
Europe/London calendar day, including a Case later completed or given a cancellation/rejection disposition that day. It
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

### Record edit ownership

An existing mutable Triage item, Image Intake record, Contact, staff account,
valuation preset, approved mailbox, approved Outlook category, labour-rate card, or named configuration
opens read-only. Its explicit Edit action claims a five-minute, record-scoped
staff edit scope. The holder is named through the staff account where it can
be resolved, never by its internal subject identifier. Cancel releases the
scope without saving; Save checks the holder token and expected version inside
the mutation transaction, applies the requested change and removes the scope
in that same transaction. A stale, expired, revoked or other-holder request
does not change the record. New unsaved records have no persistent scope.

A holder is never blocked by their own record's scope. Leaving a page
releases its scope through a `pagehide` beacon; when that release was lost, a
re-entry from the same holder that finds their own scope has gone unbeaten
into its final heartbeat interval — four minutes of the five-minute scope,
with the 60-second heartbeat interval left — claims it again silently, without
a conflict. A hidden tab keeps beating and has its timers throttled, so a
shorter window rotated the token of a window that was still open. While that
scope is still being
renewed elsewhere — a live second window — the page instead shows "You are
editing this `<record>` in another window" and offers a **Take over** action:
a POST that reclaims the scope and rotates its token, so the other window's
next heartbeat finds its own token refused and disables its Save. A
colleague's live scope still shows only who holds it, with no take-over
control. The Case record's own edit lease uses its existing replay path
instead of this same-holder staleness rule: a return to a Case the operator
already holds simply resumes editing through the ordinary **Edit Case**
control, which replays the retained lease token for the same claim
operation; there is no separate "Recover editing" control.

Disabling an account or revoking its sessions clears that account's non-Case
edit scopes, so a token from the revoked session cannot later save an existing
record. The Case edit authority remains owned by its existing Case workflow;
commands that affect both a Triage item and a Case validate both scoped
authorities.

- A count whose query has not run renders nothing; a failed query renders
  its failure, never `0`.
- An action bar for a state with no permitted action shows the state and no
  control.
- An integration without a composed caller shows its named disabled seam
  and nothing else; when the seam has no ticket the control is absent.
- A lost or expired edit lease surfaces the holder and disables
  Save; a stale version is a non-destructive conflict.
- Tabs and palette history that cannot be read are treated as empty; the
  page renders correctly with none.
- A redirect from a removed route keeps the query it was given.

## Acceptance evidence

Acceptance covers every rail route and its count, both redirects,
the removed `/VehicleImages` list, the Cases rail
groups and filters, the Work Centre kinds against Core queries, the
`/Cases/{id}/Assessment` redirect and the read-only rule in Completed
(D30), the eleven Case record sections and the `?section=` jump (D29), the
tab limit and eviction. Authenticated Web tests cover server-owned behavior;
they do not establish client-side interaction or visual correctness.
The Case record whole-page drop remains the one
accepted pointer-only exception (D16); ordinary keyboard accessibility remains
required for every other action on that page. Deployment and live acceptance
remain separate evidence tiers
([engineering](../engineering.md#required-evidence-tiers)).

## Links

- Capabilities: `UI-01`–`UI-09`, `UI-11`, `UI-13`, `UI-16`–`UI-19`, `UI-07`
  (Search), `AI-10`, `AI-11`, `CASE-32`–`CASE-34`, `ENG-03`, `ENG-04`,
  `EXT-09`, `EXT-10`, `RPT-06`, `MI-01`, `MI-02`, `MI-03` in
  [capabilities](../capabilities.md).
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
