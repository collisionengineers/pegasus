# FRD-16: Case record workspace

> Owner capabilities: UI-09, UI-15, UI-17 · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- A Case is one page at `/Cases/{id}` with ten sections. You scroll it, or
  switch to tabs. Every section can always be read.
- Editing is one page-wide session over one lease. Edit Case, Save and
  Cancel sit in the ribbon. The ribbon Save is the one save: it records the
  Case fields, the Repair Spec and the valuation calculation together.
- One Actions menu offers only what Core allows for the current state.
  Hand to Engineer is the only way out of Review.
- The Engineer sections (Damage, Valuation, Estimate, Settlement, Report)
  are editable by every enabled staff role in Not ready, Review and With
  Engineer, and read-only in Held,
  Completed and Query.
- Staff with Case edit rights can import an estimate on the Repair Spec
  section using its keyboard-accessible Import action or a section-scoped
  file drop. The imported spec is the one in use at once.
- Once an Inspection + Audit Case has its Audit, a Views card heads the
  aside. The Audit view is the default; the Inspection view is read-only.

## Purpose

This document says how the Case record page behaves: its ribbon, edit
session, Actions menu, ten sections and the Engineer workbench. Lifecycle
rules are owned by [FRD-13](frd-13-case-lifecycle-and-workflow.md). Edit
leases are owned by [FRD-14](frd-14-record-edit-leases.md). Visual and
component rules are owned by [design](../design/README.md).

## Behaviour

### Case workspace

`/Cases/{id}` is one record page with no separate page header. A Triage
Case at the same route renders as the Triage Case page instead
([FRD-15](frd-15-work-centre-queues-and-search.md#the-triage-case-page)).

**The ribbon** travels with the page as it scrolls. It shows:

- the Case/PO as the page heading, under "Case workspace · registration";
- claimant, principal and Engineer;
- chips for state, Case type (Audit, Inspection + Audit) and, while someone
  else holds the edit lease, "{name} is editing" with a lock. A held Case
  reads "Held · review on {date}" when the hold has a review date;
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
fold chevron, and every headed card inside a section has its own, remembered
per browser like the sections.

The ribbon's chips carry the state, the Case type and, once recorded, the
Engineer's decisions: the outcome (with the salvage category on a total
loss), the roadworthiness, and the repair cost as a share of the Engineer's
value (green under 66 %, amber to 79 %, red from 80 %). The Overview opens
straight on the Case, Principal and Claimant cards: the Case card carries the
derived **Matter line** (the incident's nature, the claimant and the incident
date) and, once a report has been sent, when and from which mailbox. There is
no workflow strip and no lifecycle panel; Return to Review, Unlink report
evidence and Archive are items of the one Actions menu.

An aside beside the sections holds **Figures** (three figures) and **Next action** (AI drafts ready on the Case with their
per-kind action, and the next permitted action with a link to its section;
With Engineer, while the report is not ready, its first blocker and how many
follow, linking to the section that clears that blocker).
Once the Case has an Audit, the **Views** card heads the aside
([Inspection and Audit views](#inspection-and-audit-views)).
Below 1441px the aside folds into a strip above the sections.

Actions post in place and the record's parts refresh without navigation.
Unsaved changes are confirmed before Refresh, navigation or an immediate
action.

**Edit session.** The whole record enters one edit mode over one lease
([FRD-14](frd-14-record-edit-leases.md#case-edit-lease)). There is one Save
(operator, 23 September 2026): the ribbon Save records the Case fields, the
Repair Spec, the guide cards and the valuation calculation as they are, in
one command under one version, then ends edit mode and releases the lease.
The Repair Spec and the Valuation calculator have no save of their own. A
refusal refuses the whole save and keeps every proposed value on the page
with its original authority for review. Ctrl S saves the same way and keeps
editing open, as does a save the page makes first so an action can carry on
from it (Apply or Remove scaling, and the unsaved-changes question's Save).
Pressing a section's Edit enters edit mode in place: the section stays where
it was on the screen. Selecting a tab also updates the section that Refresh
submits; after a refresh its active lazy body loads.

Reading and editing show the same fields in the same places (operator, 23
September 2026): every value is a box, greyed where it cannot be edited and a
white control where it can, and entering edit changes only which boxes are
controls. The Overview and Inspection sections each show exactly one panel
per mode. Each value carries its source tag in its label line in both modes
(Extracted, AI, E-mail, Lookup, Principal, Automatic); a staff value carries
none ([FRD-23](frd-23-case-draft-fields-provenance-and-global-checks.md#field-provenance-and-value-kinds)).
A control opens holding the value its box shows. A value keeps its tag
until staff change it: a Save that reposts it unchanged leaves its
provenance alone (operator, 25 September 2026). The edit-mode Overview uses the label **Claim reference** for
the provider's claim number everywhere it appears. Our ref is the separate,
immutable Case reference. The Registration, Make and Model inputs live in
the Vehicle section's edit state, not on Overview.

While editing, the lease line shows its expiry, and a stale version shows the
current and proposed values as a non-destructive conflict. A Case save needs
no reason; its history line names the changed fields. Holds, releases,
corrections and a return to engineering record a reason.

**Sections**, in order: **Case details**, **Claim**, **Original report** (an
Audit Case only), **Inspection details**, **Vehicle** (with **Damage** and
**Valuation** inside it, each its own foldable panel under the Vehicle link),
**Repair Spec**, **Decisions**, **Report**, **Files**, **Notes**.
Every section can always be read. The Engineer sections (Damage, Valuation,
Repair Spec, Decisions, Report) are editable by every enabled staff role in Not
ready, Review and With Engineer under the normal edit authority, and read-only
in Held and after completion. Adopting the Engineer's Value is a human
staff act: the Save adopts it when the valuation calculation changed.

### Inspection and Audit views

An Inspection + Audit Case whose Audit has been created
([FRD-13](frd-13-case-lifecycle-and-workflow.md#create-audit)) has two
views of the same record: the **Audit** view and the **Inspection** view.
Every other Case, including one whose Audit is not yet created, a standalone
Audit and a Triage Case, has one view and no Views card; a `view` value in
its address is ignored.

**The Views card** heads the aside, above Figures, and exists only once the
Audit exists. It holds two rows: "Inspection · {Case/PO}" with a "Sent"
chip, and "Audit · a.{Case/PO}" with the Case's state chip. The current view
reads plain and the other is a link. `?view=audit` and `?view=inspection`
address the two views; the Audit view
is the default, and a write returns to it. The ribbon is unchanged: its
heading stays the Case/PO. The Scroll/Tabs switch is unchanged and works in
both views.

**The Audit view** is the Case as it is worked: every section reads and
edits the Audit's values, and the Actions menu, Next action and the report
readiness follow the Audit.

**The Inspection view** shows the Inspection's values and its sent report,
read-only. No section offers Edit; instead each editable section head shows
the one availability label **Read-only · Audit created**. Files and Notes,
which both views share, carry no such label: their actions that need no Case
edit lease, such as Add evidence, previews and downloads, stay, and every
change that needs the lease is made in the Audit view. A staff member who
holds the edit lease and opens the Inspection view keeps the ribbon's
editing controls, but every section there stays read-only. The Next action
links to the Audit view.

### Actions menu

The one **Actions** menu offers only what the Core use cases permit for the
current state. Outside an edit session the menu appears only when Send to
EVA is available. The rules behind each action are in
[FRD-13](frd-13-case-lifecycle-and-workflow.md#actions).

- **Hand to Engineer**, in Review while editing. Its dialog selects an
  eligible enabled staff account or **Assign to me**. The one handoff assigns them and
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
- **Correct principal**, which records Created in error and creates the
  replacement Case
  ([FRD-01](frd-01-case-identity-and-lifecycle.md#principal-reference-organisation-and-case-party-identity)).
- **Create audit**, after Correct principal, on an Inspection + Audit Case
  once its Inspection report is sent
  ([FRD-13](frd-13-case-lifecycle-and-workflow.md#create-audit)). Its
  compact dialog states the Case, the Audit reference and the Engineer, asks
  for no reason and has no outcome. It posts in place and lands on the Audit
  view with no success notice; a refusal shows its reason.
- **Close case**, after a separator and in red. Its compact dialog offers
  only the outcomes Core permits and needs an outcome and a reason
  ([FRD-13](frd-13-case-lifecycle-and-workflow.md#close-case)).

### Overview

Overview opens on the outstanding requirements; the workflow position is
the ribbon's state chip, so there is no strip of stages. Each requirement is a named unmet item from the instruction-
or image-completeness set, with title, source, reason and resolve action.
There is never a percentage
([FRD-13](frd-13-case-lifecycle-and-workflow.md#readiness-and-review)). An
Audit with no original report at all, neither a filed report nor one kept
from the instruction email, also lists **Original report missing**, sourced
from Audit.

Then the Case and Principal cards, each folding and staying folded per
browser; the Sign-off Engineer is decided on the Case card. Identity cells (Case type, Our ref,
Received, Principal) stay greyed while the rest edits, with no padlock; the Case card carries the derived Matter line and, once a
report has been sent, when and from which mailbox. The Claim source is chosen from the active
Claim Source contacts. A Notes band shows the Principal's and the Claim
source's Notes on every Case, read-only and absent when the record has none
([FRD-04](frd-04-parties-accounts-and-access.md#contacts-administration)),
beside this Case's own Principal and Claim source notes. Then Accident
circumstances beside Notes from client.

### Claim

The claimant's cells four across — name, contact, address, VAT status — and,
beside them, the Engineer's decision on the claimant's VAT registration,
which the Decisions section no longer repeats.

### Original report

On an Audit Case only: who wrote the original report (the assessors Core's
third-party report profiles know are offered, any other is typed), its date,
its roadworthiness and its repairable status. They are Case data, edited in
the page-wide session (v28 P51, ruled 20 September 2026).

A filed original report fills them (#840). Pegasus reads the report file
itself, never the e-mail it arrived in:

- when a standalone Audit is accepted, from the report retained at intake;
- when staff **Mark as original report**, from that document.

A filled cell is tagged **Extracted** until staff change it. A fill lands
only on a cell staff have not recorded and never clears one, so a
staff-entered value is never overwritten. A cell stays blank for staff when
the report prints no value for it, prints two different values, or prints a
word the cell's list does not hold. Roadworthiness reads a printed Yes/No or
Roadworthy/Unroadworthy. Repairable status reads a printed Repairable,
Repair or Total loss; the report never fills Cash in lieu or Contract repair.

Repairable status alone falls back to the Audit's intake verdict — the
report's literal repairable or total-loss wording, or the Provider API's
declared verdict — when the report prints no outcome or cannot be read. When
the report and the verdict disagree, the cell stays blank.

### Inspection

Inspection shows the recorded inspect-at value with its fast-update choice:
Image Based Assessment, Claimant address, Repairer location, Storage
location, previous addresses used for this principal, Manual entry. An option
without a value is disabled. Reading and editing show one row per fact
not already implied by the row above it: Inspection type (the mode, read
once), Inspection date (the date the report says the damage was assessed,
[FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#report-readiness)),
Inspect at, the recorded address or `Image Based Assessment` with its
source tag, Principal default only when it differs from the recorded value,
Storage location, and Repairer
([FRD-06](frd-06-vehicle-and-engineering-evidence.md#inspection-address)).

### Vehicle

Vehicle shows registration, make, model, year, and one mileage with its
editable unit, each with its source tag (a staff value untagged). VIN,
Vehicle type and Body type follow, edited in place like the registration. The
facts only the DVLA/DVSA lookup records (engine, fuel, colour, tax and MOT
expiry) are always drawn, read-only with the Lookup tag, and read Not
recorded until a lookup answers; no Save writes them, and a Save that changes
the registration clears them until the new vehicle is looked up
([FRD-06](frd-06-vehicle-and-engineering-evidence.md#vehicle-data-and-mot-enrichment)).
Transmission keeps its place among them and is edited in place, picked from
Manual, Automatic, Semi-automatic, CVT or Unknown, because no approved lookup
returns it (operator, 24 September 2026). One **Look up DVLA & MOT** action
(`EXT-01`) fills an empty Make, Model, Year or Mileage and a Vehicle type
that staff have not recorded, and records the lookup's own facts. It never
overwrites an extracted or staff-entered value. There is no checks panel and
no suggestion table. Run Experian check stays the disabled seam. A labelled
Vehicle history area holds the history-check narrative as read-only text,
editable in edit mode
([FRD-06](frd-06-vehicle-and-engineering-evidence.md#vehicle-data-and-mot-enrichment)).
A **DVLA & MOT lookup** outcome line says what the lookup itself did (looked
up and current, or a failure reason), separately from whether it filled a
field.

### Damage

Damage shows the **plan**: a top-down silhouette drawn as the panels, with
one numbered disc per recorded damage, Underside, Interior and Mechanical
chips, and five graded severity fills with a legend. While editable, dashed
band guides show the eight areas, pressing and dragging on the vehicle sizes
a disc, dragging a disc moves it, the readout names the area under the
pointer, and Reset returns the damage to the values held when the edit
opened. A disc stays as it was drawn, names the areas it touches, is never
wider than half the vehicle and is clipped to its body, so it never covers
the page around the plan. Beside it sits the recorded-areas list numbered
like the discs, with the areas, severity and note per damage, and the other
damage facts; the list rows and the chips stand in the same places in read
and edit mode, greyed when they cannot be edited. The **Incident narrative**
is read-only: it is the report's Nature of Incident wording, the Engineer's
own from Report wording or else the sentence composed from the recorded
damage
([FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#report-wording-blocks)).
The field set is owned by
[FRD-24](frd-24-engineer-findings-damage-valuation-and-settlement.md#damage-record).

### Valuation

Valuation lists each entry with its source, date, time, retail and trade
values, and guide month, with the mileage an Engineer's Value or AI market
research entry carries (`EXT-10`). Sources are Glass's, Brego, Super
CAP, CAP and Cazana guide cards, Engineer's Value and AI market research
(automation only). Read and edit show the same cards: each guide source is
one card with month, retail and trade boxes holding that source's
latest recorded figures (no mileage: the Case's own is used, operator, 24
September 2026), greyed while reading; any box may be blank and is
saved as entered. While editing, the boxes are
inputs that belong to the Case form, and the card has **Get valuation**,
which asks the connected provider for the Case's accepted registration and
mileage in that month and fills the boxes in place, without redrawing the
page; while the source has no working provider the card shows "{Source}
valuation is unavailable. Contact an administrator or report a problem."
The card has no Save of its own (23 September 2026): the ribbon Save records
every card whose figures changed, a card left blank or unchanged records
nothing, and the same source and month replaces the earlier card; a typed
figure saves the same way. A **Valuation month** and **AI market research**
above the cards create a `MarketResearch` job and show a
"Researching · {month}" card until it completes; a re-run replaces the card
([FRD-27](frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md#ai-job-list)).
Read and edit list the same value increases, every active preset with a tick
on each the latest adoption applied, and the calculator opens on that
applied selection. The calculator has no Apply of its own (operator, 23
September 2026): the ribbon Save adopts the Engineer's Value it shows when
the calculation changed since the page opened — a different basis card, the
basis card's retail or trade, or any of its controls — and an unchanged
calculation adopts nothing. The adoption records the basis card's retail and
trade with the Engineer's Value
([FRD-24](frd-24-engineer-findings-damage-valuation-and-settlement.md#valuation-sources)).
The calculator applies presets and custom lines through Core. Valuation
sources are owned by
[FRD-24](frd-24-engineer-findings-damage-valuation-and-settlement.md#valuation-sources).

### Glass's window and Case edits

Launch and Resume open the provider window from the staff gesture and save
pending Case edits through the existing keep-edit Save first. Only a confirmed
save continues with the freshly rendered authority. Validation failure, a
conflict, lost response or new edits during the save leaves the draft intact
and makes no provider request. A blocked popup gives an actionable refusal.

The same-origin launch handoff refreshes only the Glass's launch slot and
session controls on the original Case before visiting the provider URL. It
preserves dirty fields, their Case version and lease, focus, and reading
position. Older refresh responses cannot overwrite newer controls. Save &
Exit refreshes the workspace in place when it is clean; with pending edits it
refreshes the session controls and reports the returned result without
rebasing or discarding the draft. The latest Draft becomes visible after the
staff member saves or cancels those edits. With no opener, the popup retains
a server-rendered route back to the Case.

Close uses the displayed session version and fresh confirmation that the
external session is closed. A version conflict refreshes the controls and asks
for confirmation again. Close never silently substitutes the current version
for the version the staff member confirmed.

### Repair Spec

Repair Spec carries the specification set and raw estimate import. See
[Assessment](#assessment).

### Decisions

Decisions shows outcome, category, salvage value, roadworthiness with the
unroadworthy reason and, only while the vehicle is recorded unroadworthy,
whether temporary repairs are possible with their method and cost (operator,
24 September 2026), excess, betterment, claimant VAT registered, reserve,
equity (derived), repair duration and delays, report delay, storage per day,
recovery, hire start and daily cost, diminution and salvage logistics.
Financial ratio lines are permitted. The field meanings are owned by
[FRD-24](frd-24-engineer-findings-damage-valuation-and-settlement.md#settlement).

### Report

Image role, order, rotation and crop live on the image tile in Files. The
Report section shows the readiness list — one row per blocker with the
requirement, its source, why it is outstanding, what clears it and a link to
the section that clears it
([FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#report-readiness))
— wording blocks, Generate / Preview report draft, and a separate Fee pane
for the agreed fee, description lines and fee note preview
([FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#report-generation-entry-point)).
The content switches are under **On the report** in Valuation. The report
renders the sign-off Engineer tuple and the marked damage diagram. The
**Statement of truth** cell shows the accepted statement the report prints,
read-only; no Case edits it
([FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#assessment-report-outcomes)).

Once the Case has an Audit, Report follows the view
([FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#audit-report-parity)).
In the Audit view the report card's status begins with the Audit reference,
and above the card one line stands for the Inspection's sent report: its
title, "{Case/PO} · Sent {date}" and an **Inspection view** link. In the
Inspection view the card shows the Inspection report with the status
"{Case/PO} · Sent {date}"; it can be opened and downloaded, and the
readiness list, generation and delivery are not shown.

### Files

Files is one panel with three tabs: Documents, Images and Correspondence.
All three are rendered, so a no-script visit shows the lists one after the
other under their own headings. The panel header carries Add evidence,
which opens Upload for this Case with the destination already declared
([FRD-18](frd-18-manual-upload.md#upload-for-a-declared-case)), Open
Box case folder (or the folder's own state chip before custody is confirmed)
and Open Operations. Once the Case has an Audit, a second chip follows for
the `a.` audit folder, in the Case folder chip's tones: **Box audit ·
confirmed**, **Box audit folder: preparing** while it is being created, or
**Box audit folder: unavailable**.

**Documents** lists every live file as a row: filename, role, size, origin,
recorded time and custody-state chip, with Preview, Save as and, while
editing, delete. When an Audit lists **Original report missing**, each
non-image row also offers **Mark as original report** while editing. That
action assigns the Audit report role, clears the requirement and fills the
[Original report](#original-report) cells from that document. While the
Engineer sections are editable, a confirmed row that exactly one estimate
format recognises — an Audatex that arrived by email, say — also offers
**Import as repair spec** (operator, 25 September 2026). It imports that
file through the same import as the Repair Spec section, with no second copy
([Assessment](#assessment)).

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

Images on a vehicle-images record, a Triage Case or an Unidentified item
carry the same crop, rotation and tags. Their rules are
in [FRD-19](frd-19-image-led-intake-and-pairing.md#operator-surfaces).

**Correspondence** lists every email linked to the Case, whatever its
classification: the email the Case was created from, received mail
associated with it later and uploaded `.eml` files, newest first
([FRD-20](frd-20-mailbox-workspace.md#case-correspondence-view)). Each row's
**Open message** shows that message in a dialog over the Case: sender,
received time, recipients, its text and attachment names. The dialog changes
nothing. Where the record offers **Reply**, **Reply all** and **Forward**, the
dialog does too; each opens the record's composer with this Case chosen. Its
**Open full message** leads to the Inbox message record, which keeps
classification and the Case link; without script the row opens that record.
Compose sits above the list where staff mail is available
([FRD-21](frd-21-outbound-correspondence-and-sent-evidence.md#outbound-correspondence)).
A file whose bytes are one of these emails is read here and is not repeated
on Documents.

### Notes

Notes merges Case notes, business events, chase outcomes and AI events,
newest first, each with date, time and actor. **Add Case note** sits at the
top and needs no edit session. **Record chase** is a dialog, offered while a
chase is scheduled and the lease is held
([FRD-13](frd-13-case-lifecycle-and-workflow.md#due-work-and-chasing)). There
is no Case tasks panel.

The workspace keeps the missing-material reason, next chase, last recorded
outcome and next permitted action together. A Triage Case's due target and
chaser are on the Triage Case page
([FRD-03](frd-03-triage.md#normal-workflow-and-completion-evidence)).

### Assessment

The Engineer workbench is the Damage, Valuation, Repair Spec, Decisions and
Report sections of the Case record. The sections can always be read and are
read-only in Completed. An image has one place (v28 P50): its report
role, its order and the tools that change them are on its tile under Files —
a distinct `Close-up` first, `Overview` second, optional supporting images in
explicit order, and non-destructive crops that leave the retained source and
its hash untouched. Beneath the grid a line counts what the report uses. The
tile also carries Rotate, **Full page** and Remove (v28 P41): Full page is a
flag on an image the report uses, so the image prints on a page of its own;
Remove sets the role to Not used and the file stays on the Case, with Undo
for eight seconds; the grip drags a tile above the one it lands on and the
order the tiles then stand in is the report's supporting order. While the
Case edits, clicking the image itself toggles whether the report uses it
(v28 P27). The Report section carries no image surface.

The Repair Spec section (v28 P31: the word "Estimate" stays for an imported
repairer's document) carries the repair specification set (`EXT-09`): named
specs with source, the one labour rate — a rate card or a typed figure in
one control (P33), lifted by the regional uplift (P17, + 15 %, suggested
when the repairer, claimant or storage postcode is in London or the Home
Counties) — VAT categories, lines with a Material amount each (P48; the
Materials total is the column's sum) and totals. There are no repair days
and no notes on a spec (P32); a spec is renamed by double-clicking its tab.
One spec is Current and drives the report. A new spec — typed in, imported
or returned from Glass's — is Current as soon as it is recorded; **Use repair
spec** switches to another live spec. Each version's rate prices
panel, paint and Specialist work-unit hours. The VAT rule is owned by
[FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#estimate-vat-on-the-rendered-report);
an unknown repairer VAT status never gates **Use repair spec** (P10). Lines
an import brought in read `imported · AX`, `GL`, `JSON` or `AI` on their
Source chip (P18); a cell Core finds off-pattern reads amber and named
Off-pattern, and the rollup carries the off-pattern amount as specialist
(P37). **Delete all lines** sits beside Add line and asks first; a removed
line or lines can be put back from the toast for eight seconds (P16). No
provider-versus-assessed savings figure is shown. Reading and editing are one
layout (operator, 23 September 2026): a spec that cannot be changed — every
spec while reading, and a discarded spec while editing — shows the
same header cells, the same grid columns and the same contract, discount and
VAT bars as the editor, each value greyed in its control's place and a line's
Type in the editor's words; a scaled spec's Target % of value bar stands in
its place with the Scaled state; the tools (add and delete lines, the Target
% of value controls, Reset to repairer status) are drawn only while the spec
edits. The spec has no save of its own: the ribbon Save records it, and a
spec left unchanged is not rewritten. Apply and Remove scaling save first and
then scale the saved spec. The More menu holds New repair spec
(editing, recorded by the Save and starting on the one enabled labour-rate
card), **Print Repair Spec** for a saved spec with lines, and Compare,
greyed out until the Case holds two specs (P9). Previewing the document
does not save or discard pending edits. The section also
carries **Send to AI**, which creates an `AI-10` `Estimate` job
([AI Job List](frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md#ai-job-list)),
disabled without an Engineer's Value. The Report section reads in two tabs (v28 P24): **Report**, everything the
report itself carries, and **Fee**, the fee note the agreed fee makes: the
agreed fee, the VAT the report charges on it and their total in one row, the
description lines below, and the generated fee note to download. Without
script both panes stand.

The Report tab carries **Report wording** (v28 P30): every narrative block
the report prints, in print order, each with its heading, its wording and
whether the report carries it. A block tracks the Case's fields until the
Engineer writes their own wording, and Recompose puts the composed sentence
back. The move controls and the paragraph the Engineer adds are enhancements
over controls the one Save form already carries; the heading, the wording
and the On report choice stand without script. The panel is absent while the
Case cannot yet be projected into a report, where the readiness rail already
states what is outstanding. What the blocks are and what each composes from
is owned by
[FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#report-wording-blocks).

The Report section's More menu offers the three documents as previews (v28
P42) — the report, the Repair Spec and the images — each opening the document
the Case would actually produce rather than a picture of one, and offers
Generate for a companion document the confirmed generation does not yet hold.
The delivery form offers the Case's known addresses on every recipient field
(v28 P21), the documents to attach (v28 P22), and states the name the report
will be attached under and the covering line it will carry (v28 P23) before
Prepare delivery is pressed.

Report-draft generation and preview sit
on the Report section
([FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#report-generation-entry-point)).

**Raw estimate import** (`EXT-12`) is available to staff with Case edit rights
in editable states from Repair Spec. The keyboard-accessible **Import** action
opens the native file picker; dropping a file over Repair Spec uses
the same upload path and shows a temporary drop overlay. Exactly one supported
PDF, XML or JSON file is accepted. From read mode, the server acquires the Case
edit lease against the submitted Case version before storing the file through
the normal Case document upload flow. A dropped file whose bytes are already
confirmed in Case Files, and a Documents row's **Import as repair spec**,
import that stored file instead of storing another copy.

After the source is confirmed in Case Files, its registered provider parser
runs immediately. A successful import records the new named spec as the
Current one, on the one enabled labour-rate card, and displays its lines in
the editor. A parser refusal creates no partial spec;
the confirmed original remains in Case Files so the same source can be retried.
Only registered parser types are accepted. An ambiguous file is refused, not
guessed. Provenance and replay rules are owned by
[FRD-25](frd-25-repair-estimates-imports-and-glasss-sessions.md#canonical-repair-specifications).
Manual line entry remains a separate capability and keeps no source file,
hash or parser provenance.

## States and transitions

The page offers a transition only where its Core use case permits it for the
current state and account ([FRD-13](frd-13-case-lifecycle-and-workflow.md)).
Section editability by state:

| State | Overview, Inspection, Vehicle, Files, Notes | Engineer sections |
| --- | --- | --- |
| Not ready, Review, With Engineer | Editable under the lease | Editable with `PerformCasework` |
| Held | Read-only | Read-only |
| Completed, Query | Read-only (Return to Engineer to edit) | Read-only |

Whatever the state, the Inspection view of a Case with an Audit is
read-only apart from Files' and Notes' actions that need no edit lease.

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
- A `view` value on a Case without an Audit is ignored. Every write changes
  the current values, the Audit's once it exists, and returns to the Audit
  view; the Inspection's values never change after Create audit.

## Acceptance evidence

Acceptance covers the ten sections and the `?section=` jump, the Report
readiness list and the Next action each linking a blocker to its section,
the read-only rule in Completed, the Actions menu per state, and the one
Save. It also covers the views: no Views card without an Audit; after Create
audit the card and the Audit view by default; the Inspection view read-only
with its label on each editable head, including for the lease holder; Report
in each view; the audit folder chip in each state; the Create audit dialog;
and a standalone Audit's single view with its Original report. Web tests
cover the Vehicle section's read-only lookup facts beside Transmission edited
in place, and the Damage Incident narrative and Report Statement of truth
reading their report owners. Authenticated Web tests cover server-owned
behaviour; they do not prove client-side interaction or visual correctness.
Browser acceptance exercises the keyboard Import action, picker and
section-scoped drop overlay. Deployment and live acceptance are separate
evidence tiers
([engineering](../engineering.md#required-evidence-tiers)).

## Links

- Capabilities: `UI-09`, `UI-15`, `UI-17` in
  [capabilities](../capabilities.md).
- Related FRDs: [FRD-01](frd-01-case-identity-and-lifecycle.md),
  [FRD-05](frd-05-documents-extraction-and-custody.md),
  [FRD-06](frd-06-vehicle-and-engineering-evidence.md),
  [FRD-24](frd-24-engineer-findings-damage-valuation-and-settlement.md),
  [FRD-25](frd-25-repair-estimates-imports-and-glasss-sessions.md),
  [FRD-07](frd-07-eva-and-external-engineering-handoff.md),
  [FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md),
  [FRD-27](frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md),
  [FRD-12](frd-12-operator-experience.md),
  [FRD-13](frd-13-case-lifecycle-and-workflow.md),
  [FRD-14](frd-14-record-edit-leases.md),
  [FRD-19](frd-19-image-led-intake-and-pairing.md),
  [FRD-21](frd-21-outbound-correspondence-and-sent-evidence.md).
- Design: [design](../design/README.md).
- Technical constraints:
  [ADR-0031](../adr/0031-automation-actor-contract-without-eva-export-tools.md),
  [ADR-0056](../adr/0056-one-case-per-work-data-and-triage-case-type.md).
