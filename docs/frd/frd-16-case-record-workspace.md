# FRD-16: Case record workspace

> Owner capabilities: UI-09, UI-15, UI-17, CASE-32, CASE-33, CASE-34, ENG-03, ENG-04, EXT-09, EXT-10, EXT-12, RPT-06 · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- A Case is one page at `/Cases/{id}` with ten sections. You scroll it, or
  switch to tabs. Every section can always be read.
- Editing is one page-wide session over one lease. Edit Case, Save and
  Cancel sit in the ribbon. Case Save, Estimate Save and Valuation Apply are
  separate commands that do not disturb each other.
- One Actions menu offers only what Core allows for the current state.
  Hand to Engineer is the only way out of Review.
- The Engineer sections (Damage, Valuation, Estimate, Settlement, Report)
  are editable in Not ready, Review and With Engineer, and read-only in Held,
  Completed and Query.
- Raw estimate import is a whole-page drop. It is pointer-only, and that is a
  recorded accessibility gap.

## Purpose

This document says how the Case record page behaves: its ribbon, edit
session, Actions menu, ten sections and the Engineer workbench. Lifecycle
rules are owned by [FRD-13](frd-13-case-lifecycle-and-workflow.md). Edit
leases are owned by [FRD-14](frd-14-record-edit-leases.md). Visual and
component rules are owned by [design](../design/README.md).

## Behaviour

### Case workspace

`/Cases/{id}` is one record page with no separate page header. Opening it
adds the Case to the working set.

**The ribbon** travels with the page as it scrolls. It shows:

- the Case/PO as the page heading, under "Case workspace · registration";
- claimant, principal and Engineer;
- chips for state, Case type (Audit, Inspection + Audit) and, while someone
  else holds the edit lease, "{name} is editing" with a lock. A held Case
  reads "Held · review on {date}" when the hold has a review date;
- the links between an Audit Case and its original Case;
- **Edit Case**, or while editing the Editing badge, **Cancel** and **Save**;
- the one **Actions** menu.

When a colleague holds the lease the ribbon shows who, and offers **Take
over**. Any staff member may take over; no reason is needed and the takeover
is recorded in history. Renew editing works without script. The rule is in
[FRD-14](frd-14-record-edit-leases.md#take-over).

**The section row** sits under the ribbon. It lists the section links, marks
the current section as you scroll, and carries **Refresh** and the
Scroll/Tabs switch. Scroll is the default in every state. A Tabs choice
lasts for the browser session and is painted by the server, as is each
section's folded state, which is remembered per browser. `?section=` jumps to
a section. Sections below the fold load lazily. Tabs hide inactive sections
without discarding loaded values. Scroll is the no-script fallback.

Every editable section head has its own **Edit**, which starts the one
page-wide edit session without moving the page. When the state does not
allow editing it shows one availability label instead. Each head also has a
fold chevron.

An aside beside the sections holds **Figures** (outcome and legal chips and
three figures) and **Next action** (AI drafts ready on the Case with their
per-kind action, and the next permitted action with a link to its section).
Below 1441px the aside folds into a strip above the sections.

Actions post in place and the record's parts refresh without navigation.
Unsaved changes in each editor are confirmed before Cancel, Refresh,
navigation or an immediate action.

**Edit session.** The whole record enters one edit mode over one lease
([FRD-14](frd-14-record-edit-leases.md#case-edit-lease)). Case Save, Estimate
Save and Valuation Apply are separate commands. Saving one keeps editing open
and keeps the other editors' pending values, including estimate rows and
staged image preparations. Only that command's confirmed save clears its
draft. Pending editors advance their Case version and lease only after the
same operator's confirmed command, with no Case change in between. A refusal
or unknown response keeps the proposed values and their original authority
for review. Ctrl S submits the active dirty editor. Selecting a tab also
updates the section that Refresh submits; after a refresh its active lazy
body loads.

While editing, each section shows its one edit form instead of its read
view, never both. The Overview and Inspection sections each show exactly one
panel per mode. The edit-mode Overview uses the label **Claim reference** for
the provider's claim number everywhere it appears. Our ref is the separate,
immutable Case reference. The Registration, Make and Model inputs live in
the Vehicle section's edit state, not on Overview.

While editing, the lease line shows its expiry, the working-set tab carries
the unsaved marker, and a stale version shows the current and proposed
values as a non-destructive conflict. A Case save needs no reason; its
history line names the changed fields. Holds, releases, corrections and a
return to engineering record a reason.

**Sections**, in order: **Overview**, **Inspection**, **Vehicle**, **Damage**,
**Valuation**, **Estimate**, **Settlement**, **Report**, **Files**, **Notes**.
Every section can always be read. The Engineer sections (Damage, Valuation,
Estimate, Settlement, Report) are editable in Not ready, Review and With
Engineer by staff with `PerformCasework`, and read-only in Held and after
completion. Adopting the Engineer's Value is an Engineer act.

### Actions menu

The one **Actions** menu offers only what the Core use cases permit for the
current state. Outside an edit session the menu appears only when Send to
EVA is available. The rules behind each action are in
[FRD-13](frd-13-case-lifecycle-and-workflow.md#actions).

- **Hand to Engineer**, in Review while editing. Its dialog selects an
  eligible Engineer or **Assign to me**. The one handoff assigns them and
  enters With Engineer. There is no reviewed checkbox and no separate start
  action ([FRD-13](frd-13-case-lifecycle-and-workflow.md#hand-to-engineer)).
- **Send to EVA**, when the Principal's report-generation policy offers it.
  Sending never changes the Case state
  ([FRD-07](frd-07-eva-and-external-engineering-handoff.md)).
- **Mark report sent**, in With Engineer. It confirms detected or linked
  Sent evidence and enters post-report work. It never completes the Case and
  never records a manual assertion
  ([FRD-21](frd-21-outbound-correspondence-and-sent-evidence.md#outbound-correspondence-evidence)).
- **Mark completed**
  ([FRD-13](frd-13-case-lifecycle-and-workflow.md#completed-and-query)).
- **Return to Review** or **Return to Engineer**, in Completed or Query when
  engineering changes are needed. Both record a reason.
- **Archive**, on a closed Case
  ([FRD-13](frd-13-case-lifecycle-and-workflow.md#archive)).
- **Place on Hold** (reason and optional Review on date) or **Release Hold**
  (reason).
- **Create upload link**.
- **Correct principal**, which records Created in error and creates the
  replacement Case
  ([FRD-01](frd-01-case-identity-and-lifecycle.md#principal-reference-organisation-and-case-party-identity)).
- **Create audit**, on an Inspection + Audit Case once a report has been
  generated ([FRD-01](frd-01-case-identity-and-lifecycle.md#case-types)).
- **Close case**, after a separator and in red. Its compact dialog offers
  only the outcomes Core permits and needs an outcome and a reason
  ([FRD-13](frd-13-case-lifecycle-and-workflow.md#close-case)).

### Overview

Overview shows the workflow position (Not ready → Review → With Engineer →
Completed ⇄ Query, with Held as an exception badge) and the outstanding
requirements. Each requirement is a named unmet item from the instruction-
or image-completeness set, with title, source, reason and resolve action.
There is never a percentage
([FRD-13](frd-13-case-lifecycle-and-workflow.md#readiness-and-review)). An
Audit with no original report at all, neither a filed report nor one kept
from the instruction email, also lists **Original report missing**, sourced
from Audit.

Then the Case, Principal and Claimant columns of cells. Identity cells read
with a lock while the rest edits. The Claim source is chosen from the active
Claim Source contacts. A Notes band shows the Principal's and the Claim
source's Notes on every Case, read-only and absent when the record has none
([FRD-04](frd-04-parties-accounts-and-access.md#contacts-administration)),
beside this Case's own Principal and Claim source notes. Then Accident
circumstances beside Notes from client.

### Inspection

Inspection shows the recorded inspect-at value with its fast-update choice:
Image Based Assessment, Claimant address, Repairer location, Storage
location, previous addresses used for this principal, Manual entry. An option
without a value is disabled. The read view shows one row per fact not
already implied by the row above it: Inspect at (the recorded address or
`Image Based Assessment`, with its mode chip only when the mode says
something the value does not), Principal default only when it differs from
the recorded value, Storage location, and Repairer
([FRD-06](frd-06-vehicle-and-engineering-evidence.md#inspection-address)).

### Vehicle

Vehicle shows registration, make, model, year, and one mileage with its
editable unit and provenance (Extracted · Lookup · Staff). One **Look up
DVLA & MOT** action (`EXT-01`) fills an empty Make, Model, Year or Mileage,
and a Vehicle type that staff have not confirmed. It never overwrites an
extracted or staff-entered value. There is no checks panel and no suggestion
table. Run Experian check stays the disabled seam. A labelled Vehicle history
area holds the history-check narrative as read-only text, editable in edit
mode ([FRD-06](frd-06-vehicle-and-engineering-evidence.md#vehicle-data-and-mot-enrichment)).
A **DVLA & MOT lookup** outcome line says what the lookup itself did (looked
up and current, or a failure reason), separately from whether it filled a
field.

### Damage

Damage shows the **Plan** clicker: a top-down silhouette drawn as the panels
over the 19 panel and 4 wheel zones, with Underside, Interior and Mechanical
chips, five graded severity fills with a legend, and numbered markers that
match the recorded-zones list. Hovering names a zone only while editable.
Below it sits the zone list with severity and note per zone, and the other
damage facts. The field set is owned by
[FRD-06](frd-06-vehicle-and-engineering-evidence.md#damage-record).

### Valuation

Valuation lists each entry with its source, date, time, mileage, retail and
trade values, and guide month (`EXT-10`). Sources are Glass's, Brego and Super
CAP guide cards, Cazana (disabled seam), Engineer's Value and AI market
research (automation only). While editing, each guide source is one card with
month, mileage, retail and trade boxes, **Get valuation** and **Save**. Get
valuation asks the connected provider for the Case's accepted registration
and mileage in that month and fills the boxes, with a notice while that
source has no provider. Save records the card. The same source and month
replaces the earlier card, and a typed figure saves the same way. A
**Valuation month** and **AI market research** above the cards create a
`MarketResearch` job and show a "Researching · {month}" card until it
completes; a re-run replaces the card
([FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#ai-job-list)).
Read mode shows only applied increases. The calculator applies presets and
custom lines through Core. Valuation sources are owned by
[FRD-06](frd-06-vehicle-and-engineering-evidence.md#valuation-sources).

### Estimate

Estimate carries the estimate set and raw estimate import. See
[Assessment](#assessment).

### Settlement

Settlement shows outcome, category, salvage value, excess, betterment,
claimant VAT registered, reserve, equity (derived), repair duration and
delays, report delay, storage per day, recovery, hire start and daily cost,
diminution and salvage logistics. Financial ratio lines are permitted. The
field meanings are owned by
[FRD-06](frd-06-vehicle-and-engineering-evidence.md#settlement).

### Report

**Report position** is the only report-composition control: one Close-up,
one Overview, the rest Supporting, with order, rotation and crop. An image's
own classification is its tags on the Files Images tab, not this section.
The section also shows the readiness list of named outstanding items, the
agreed fee and description lines with the fee note preview, and Generate /
Preview report draft
([FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#report-generation-entry-point)).
The report renders the sign-off Engineer tuple and the marked damage
diagram.

### Files

Files is one panel with two tabs. Both are rendered, so a no-script visit
shows the two lists one after the other under their own headings. The panel
header carries Add evidence, Open Box case folder (or the folder's own state
chip before custody is confirmed), Open Operations, and Create upload link
with its request table. Linked correspondence sits below both tabs
([FRD-21](frd-21-outbound-correspondence-and-sent-evidence.md#outbound-correspondence-evidence)).

**Documents** lists every live file as a row: filename, role, size, origin,
recorded time and custody-state chip, with Preview, Save as and, while
editing, delete. When an Audit lists **Original report missing**, each
non-image row also offers **Mark as original report** while editing. That
action assigns the Audit report role and clears the requirement.

**Images** is one grid of every image occurrence: the Case's own image
documents plus, for each vehicle-images record associated with the Case, its
photographs labelled by Image reference. Each tile shows:

- a lazy-loaded thumbnail that expands to the full image, with the original
  filename as the accessible name. It is served only by an authorised staff
  endpoint that returns the stored image media type inline. Non-image
  material stays on the forced-download route;
- its applied tag chips
  ([FRD-05](frd-05-documents-extraction-and-custody.md#image-tags)), and for
  a Case image a Tag picker naming every vocabulary entry plus New tag with
  a colour;
- Preview and, while the Case edit lease is held, Crop.

The Crop lease gate is the record's whole edit mode
([FRD-14](frd-14-record-edit-leases.md#case-edit-lease)). Crop is unavailable
once the Case reaches Completed or Query, and never on an archived Case. It
is not tied to With Engineer, so a Review-state Case shows Crop. Images open
in a full-screen viewer (title, tag, position, Rotate, Zoom, Download, In
report while editing, and a filmstrip). Crop happens on the viewer stage. A
crop is a stored rectangle: the tile and the report show the cropped region
and Download returns the original.

Images on pre-Case records (a vehicle-images record, a Triage or an
Unidentified item) carry the same crop, rotation and tags. Their rules are
in [FRD-19](frd-19-image-led-intake-and-pairing.md#operator-surfaces).

### Notes

Notes merges Case notes, business events, chase outcomes and AI events,
newest first, each with date, time and actor. **Add Case note** sits at the
top and needs no edit session. **Record chase** is a dialog, offered while a
chase is scheduled and the lease is held
([FRD-13](frd-13-case-lifecycle-and-workflow.md#due-work-and-chasing)). There
is no Case tasks panel.

The workspace keeps the missing-material reason, next chase, last recorded
outcome and next permitted action together. Triage has no due or chaser
presentation.

### Assessment

The Engineer workbench is the Damage, Valuation, Estimate, Settlement and
Report sections of the Case record. `/Cases/{id}/Assessment` is a permanent
redirect to `/Cases/{id}?section=estimate`. The sections can always be read
and are read-only in Completed. Report position lives on the Report section:
a distinct `Close-up` first, `Overview` second, optional supporting images in
explicit order, and non-destructive crops that leave the retained source and
its hash untouched.

The Estimate section carries the estimate set (`EXT-09`): named estimates
with source, repair days, the selected labour-rate-card snapshot, VAT
categories, lines and totals. One estimate is Current and drives the report.
Each version's card prices panel, paint and Specialist work-unit hours. The
VAT rule and the Use as Current gate are owned by
[FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#estimate-vat-on-the-rendered-report).
No comparison or savings figure is shown. In both read and edit modes, every
saved version with lines carries **Estimate PDF** in its actions row.
Previewing it does not save or discard pending edits. The section also
carries **Send to AI**, which creates an `AI-10` `Estimate` job
([AI Job List](frd-11-reports-correspondence-and-reviewed-proposals.md#ai-job-list)),
disabled without an Engineer's Value. Report-draft generation and preview sit
on the Report section
([FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#report-generation-entry-point)).

**Raw estimate import** (`EXT-12`) is a whole-page drop surface. One file is
imported immediately, with no confirmation step and no visible file picker.
Only registered parser types are accepted. An ambiguous file is refused, not
guessed. Provenance and replay rules are owned by
[FRD-06](frd-06-vehicle-and-engineering-evidence.md#canonical-repair-specifications).

The drop is pointer-only. That is a narrow accepted accessibility exception,
and a real gap: a keyboard-only operator cannot import a raw estimate file.
The same Core command is reachable without a pointer only through the MCP
`pegasus_estimate_import` tool
([FRD-10](frd-10-mcp-automation-and-actor-boundary.md#ai-job-and-estimate-tools)),
which is an automation boundary, not a staff keyboard route. Manual line
entry in the estimate editor stays keyboard-reachable but is a different
capability: it keeps no source file, hash or parser provenance.

## States and transitions

The page offers a transition only where its Core use case permits it for the
current state and account ([FRD-13](frd-13-case-lifecycle-and-workflow.md)).
Section editability by state:

| State | Overview, Inspection, Vehicle, Files, Notes | Engineer sections |
| --- | --- | --- |
| Not ready, Review, With Engineer | Editable under the lease | Editable with `PerformCasework` |
| Held | Read-only | Read-only |
| Completed, Query | Read-only (Return to Engineer to edit) | Read-only |

## Edge cases and fail-closed behaviour

- A lost or expired edit lease shows the holder and disables Save. A stale
  version is a non-destructive conflict showing current and proposed values.
- A refused or unknown save response keeps the proposed values for review.
- Unsaved changes are confirmed before Cancel, Refresh, navigation or an
  immediate action.
- An action bar for a state with no permitted action shows the state and no
  control.
- Crop is refused on an archived Case and in Completed or Query.
- An ambiguous raw estimate file is refused with its reason.

## Acceptance evidence

Acceptance covers the ten sections and the `?section=` jump, the
`/Cases/{id}/Assessment` redirect, the read-only rule in Completed, the
Actions menu per state, and the separate Save commands. Authenticated Web
tests cover server-owned behaviour; they do not prove client-side interaction
or visual correctness. The whole-page drop stays the one accepted
pointer-only exception. Deployment and live acceptance are separate evidence
tiers ([engineering](../engineering.md#required-evidence-tiers)).

## Links

- Capabilities: `UI-09`, `UI-15`, `UI-17`, `CASE-32`, `CASE-33`, `CASE-34`,
  `ENG-03`, `ENG-04`, `EXT-09`, `EXT-10`, `EXT-12`, `RPT-06` in
  [capabilities](../capabilities.md).
- Related FRDs: [FRD-01](frd-01-case-identity-and-lifecycle.md),
  [FRD-05](frd-05-documents-extraction-and-custody.md),
  [FRD-06](frd-06-vehicle-and-engineering-evidence.md),
  [FRD-07](frd-07-eva-and-external-engineering-handoff.md),
  [FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md),
  [FRD-12](frd-12-operator-experience.md),
  [FRD-13](frd-13-case-lifecycle-and-workflow.md),
  [FRD-14](frd-14-record-edit-leases.md),
  [FRD-19](frd-19-image-led-intake-and-pairing.md),
  [FRD-21](frd-21-outbound-correspondence-and-sent-evidence.md).
- Design: [design](../design/README.md).
- Technical constraints:
  [ADR-0031](../adr/0031-automation-actor-contract-without-eva-export-tools.md).
