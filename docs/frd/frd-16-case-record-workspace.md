# FRD-16: Case record workspace

> Owner capabilities: UI-09, UI-15, UI-17 · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- A Case is one scrolling page at `/Cases/{id}` with ten sections. Every
  section can always be read.
- Editing is one page-wide session over one lease. Edit Case and, while
  editing, Done sit in the ribbon. Every change is saved as it is made: a
  cell as it is left, a composite editor when it is left or after a short
  pause. Each save records the Case fields, the Repair Spec and the
  valuation calculation together, and the page keeps editing.
- One Actions menu offers only what Core allows for the current state;
  Create audit alone is always listed on an Inspection + Audit Case, greyed
  out with its reason when refused. Assign Engineer is the only way out of
  Review.
- The Engineer sections (Damage, Valuation, Estimate, Settlement, Report)
  are editable by every enabled staff role in Not ready, Review and With
  Engineer, and read-only in Held,
  Completed and Query.
- Staff with Case edit rights can import an estimate on the Repair Spec
  section using its keyboard-accessible Import action or a section-scoped
  file drop. The imported spec is the one in use at once.
- Once an Inspection + Audit Case has its Audit, a Views card heads the
  aside. The Audit view is the default; the Inspection view edits the
  Inspection's own values and report.

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

- the Case/PO as the page heading, under "Case workspace";
- registration, claimant, principal and Engineer (the registration has its
  own cell, operator, 8 October 2026);
- chips for state, Case type (Audit, Inspection + Audit) and, while someone
  else holds the edit lease, "{name} is editing" with a lock. A held Case
  reads "Held · review on {date}" when the hold has a review date;
- **Edit Case**, or while editing the Editing badge, its status word
  (Saving…, Saved {time}, or why the last change was not saved) and **Done**;
- the one **Actions** menu.

When a colleague holds the lease the ribbon shows who, and offers **Take
over**. Any staff member may take over; no reason is needed and the takeover
is recorded in history. There is no Renew editing: editing lasts while the
page is open (operator, 6 October 2026). The rule is in
[FRD-14](frd-14-record-edit-leases.md#take-over).

**The section row** sits under the ribbon. It lists the section links, marks
the current section as you scroll, and carries **Refresh**. The page always
scrolls; there is no Tabs layout (operator, 9 October 2026). Each section's
folded state is remembered per browser and painted by the server.
`?section=` jumps to a section. Sections below the fold load lazily.

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

An aside beside the sections holds **Figures** (three figures), **Next
action** and, while anything is outstanding, **Report not ready** (operator,
8 October 2026). Next action holds the AI drafts ready on the Case with their
per-kind action and one step: the first outstanding Case requirement while
the Case is Not ready or Held, else the state's own step, else, while the
report is not ready, its first blocker in page order. A requirement or a
blocker as the step is drawn in full (what is missing, its source and reason)
with one full-width control to where it is cleared, and no sentence on how
to resolve it: the control is the how (v36 item AB, 9 October 2026); a step
whose control says what the step says, such as Assign Engineer or Create
audit, is that control alone, and any other step names itself above its
section's control.
**Report not ready** is a card of its own below Next action, folded until
the browser opens it and remembered per browser like the section folds. It
lists everything outstanding, the step's own item included: every
outstanding Case requirement, in whatever state, then the report's
readiness list, every blocker under the name of the section that
clears it, each with the requirement as its link, its source and why it is
outstanding; the link is what clears it (9 October 2026)
([FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#report-readiness)).
Original report missing links to Files, where Mark as original report is;
the other Case requirements link to Case details. The card is headed
**Outstanding requirements** when no blocker remains.
Where a tab inside that section clears it, the link opens that tab too
(operator, 1 October 2026): a missing Overview or report image
source opens Files on **Images**, and the agreed fee or its description
lines open Report on **Fee**.
Where the blocker names one field, the Next action control and the card's
link also outline that field's cell in amber, unfolding any fold over it, and
focus its control while the Case is in edit; the outline stays until the
next jump (operator, 9 October 2026). The Sign-off Engineer blocker outlines
the Sign-off Engineer cell, and the report's Retail and Trade outline the
Valuation guide cards.
The rows run in page order (operator, 2 October 2026): section by section
from the top, then by where the field that clears each sits within its
section, so working down the list moves down the page; a blocker no Case
section clears comes last, under Accounts where it opens Accounts. The
Cases list's Current work names the same first row.
The card shows beside every section; a long list scrolls
within the aside rather than pushing the sections down.
When a newer fact has made the current generation stale, the Next action
carries the dismissable warning "A newer fact changed after this generation.
Generate again before delivery." There is no page-wide stale bar and no
second stale notice in Report (operator, 28 September 2026).
While a linked message is currently classified a cancellation and the Case
is open, the Next action also carries **Cancellation received** with **Open
message** ([FRD-13](frd-13-case-lifecycle-and-workflow.md#cancellation-messages)).
In Review the Next action is **Assign Engineer**, with the Actions menu's own
control that opens its dialog; where the menu does not offer it, the step is
named without a control (issue 1025).
Once the report is ready, the Next action is **Generate report** until the
report is stored, or **Waiting for the report to be stored** while its file
is on its way to Box. Delivery is the Next action only once the report is
stored (operator, 27 September 2026). Once the report is sent, the Next
action is **Create audit** on an Inspection + Audit Case that has no Audit
yet, with the Actions menu's own control (greyed with its reason where Core
refuses it), and **Mark completed** on any other Case (operator, 2 October
2026).
Once the Case has an Audit, the **Views** card heads the aside
([Inspection and Audit views](#inspection-and-audit-views)).
While a Triage Case is linked to the Case, a **Linked cases** card follows
Views and precedes Figures: one row per linked Triage, its `t.` Case/PO as a
link, its state and its current finding; there is no card while nothing is
linked (operator, 7 October 2026;
[FRD-03](frd-03-triage.md#triage-findings-on-the-linked-case)).
Below 1441px the aside folds into a strip above the sections.

Actions post in place and the record's parts refresh without navigation.
An action, Refresh or a link away waits for a change not yet sent to land
first; a document action (tag, New tag, In report) posts in place and
redraws its own tile. After a tag, untag or In report the other sections
follow as after a save, Files and Notes staying as loaded, so a report
blocker it clears leaves the Report offering Generate report with no reload
(operator, 9 October 2026).

**Edit session.** The whole record enters one edit mode over one lease
([FRD-14](frd-14-record-edit-leases.md#case-edit-lease)). Every change is
saved as it is made (save as you go, operator, 29 September 2026): a simple
cell when it is left, and a composite editor — the Repair Spec grid, the
Damage plan and its list, the Valuation calculator and cards, the Report
wording blocks — when focus leaves it or after a short pause. Each save is
the one Save command (operator, 23 September 2026): it records the Case
fields, the Repair Spec, the guide cards and the valuation calculation as
they are, under one version, and the session keeps its lease. The Repair
Spec and the Valuation calculator have no save of their own. One save is in
flight at a time; a change made during it is saved when it lands, and an
action (Done included) or a link away waits for it and goes ahead only once
it has landed; a Refresh pressed during a save is declined. A save that is
refused or lost stops what waited on it. A value the browser cannot accept
is not sent: the status word says so. The page never redraws what the
operator is typing in: after a save the notices, the ribbon, the aside and
the dialogs are drawn afresh, so a blocker clears, the state chip moves and a
newly permitted action appears within the round trip. The server answers a
save with those parts alone, and the page is not reloaded (operator, 29
September 2026). A section that shows another's value (the Engineer's Value
and the salvage share in Settlement, a registration in the Report title)
follows the save: once nothing waits on it, the page catches up, below, and
Files and Notes stay as loaded (operator, 7 October 2026). The answer also carries the
version, the lease and the key the next save sends, and the Files section
when a crop or rotation was recorded. A save that ends editing redirects to
the page, as every other command does. A refusal refuses the whole save, keeps
every typed value in its box, says why in the ribbon's status word and as a
notice, and the next change tries again. A decision's partner field (the
salvage category and value of a total loss, the reason a vehicle is
unroadworthy, the agreed sum of a contract repair) is a readiness item, never
a save refusal, so each field saves as it is left (operator, 6 October 2026).

**Catching up.** System work moves the Case under the session without ending
it ([FRD-14](frd-14-record-edit-leases.md#case-edit-lease)). When a save's
answer, or the minute heartbeat's, shows the Case past the version the page
holds, or a save is refused or not confirmed, the page catches up once
nothing is in flight; after a save that landed it catches up once nothing
waits on the queue either. It reads the Case again and draws afresh the
ribbon, the notices, the aside, the dialogs (unless one is open) and each
section that holds no value not yet sent and no open dialog, keeping the
place, the focused control and what is typed in it. A section holding a value not yet sent stays as the
operator has it. When a colleague now holds the Case, every section stays as
it is, so what the operator typed remains to copy. The ribbon has **Done**, which ends editing and releases
the lease; there is no Save and no Cancel, because nothing is unsaved: a
wrong value is retyped, and the history names each change. Ctrl S saves now,
a composite's typing included; without script the ribbon's **Save now** is
the save.
Pressing a section's Edit enters edit mode in place: the section stays where
it was on the screen. Selecting a tab also updates the section that Refresh
submits; after a refresh its active lazy body loads. While editing, Files and
Notes still load when they come into view, because neither has a field in
the one Save. Every other section renders with the page.

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
the Principal's claim number everywhere it appears. Our ref is the separate,
immutable Case reference. The Registration, Make and Model inputs live in
the Vehicle section's edit state, not on Overview.

While editing, a refused save shows the current and proposed values as a
non-destructive conflict. A Case save needs
no reason; its history line names the changed fields. Holds, releases,
corrections and a return to engineering record a reason.

**Sections**, in order: **Case details**, **Claim**, **Original report** (an
Audit Case only), **Inspection details**, **Vehicle** (with **Damage** and
**Valuation** inside it, each its own foldable panel under the Vehicle link),
**Repair Spec**, **Decisions**, **Report**, **Files**, **Notes**.
Every section can always be read. The Engineer sections (Damage, Valuation,
Repair Spec, Decisions, Report) are editable by every enabled staff role in Not
ready, Review and With Engineer under the normal edit authority, and read-only
in Held and after completion. The Engineer's Value is a staff finding, typed
or filled by the calculation from a guide card on Valuation; the report's
Retail and Trade values are that card's figures.

### Inspection and Audit views

An Inspection + Audit Case whose Audit has been created
([FRD-13](frd-13-case-lifecycle-and-workflow.md#create-audit)) has two
views of the same record: the **Audit** view and the **Inspection** view.
Every other Case, including one whose Audit is not yet created, a standalone
Audit and a Triage Case, has one view and no Views card; a `view` value in
its address is ignored.

**The Views card** heads the aside, above Figures, and exists only once the
Audit exists. It holds two rows: "Inspection · {Case/PO}" with a "Sent"
chip when the Inspection report was sent (none when the Audit was created
first), and "Audit · a.{Case/PO}" with the Case's state chip. The current view
reads plain and the other is a link. `?view=audit` and `?view=inspection`
address the two views; the Audit view
is the default, and a write returns to it. The ribbon is unchanged: its
heading stays the Case/PO.

**The Audit view** is the Case as it is worked: every section reads and
edits the Audit's values, and the Actions menu, Next action and the report
readiness follow the Audit.

**The Inspection view** shows the Inspection's values and its report and
edits them (operator, 2 October 2026): every section head offers Edit as
in the Audit view, under the one Case edit lease, and every change saved
there writes the Inspection's own values, before and after its report is
sent. Such a save changes nothing of the Case's own: its state, due date,
completeness and matching are the Audit's. Files and Notes are shared by
both views. The Inspection view's Report section generates the Inspection
report, prepares and sends its delivery, and the Actions menu's Mark report
sent takes its evidence, all on the Inspection's own work and without
changing the Case's state (operator, 1 October 2026); a sent Inspection
report may be generated and sent again when needed (operator, 2 October
2026). The Next action is the viewed work's (operator, 2 October 2026): in
the Inspection view it states the Inspection report's own step, lists that
report's blockers while it is not ready, and its links stay in the
Inspection view; once the Inspection report is sent it states nothing,
unless a later change made that report stale.

### Actions menu

The one **Actions** menu offers only what the Core use cases permit for the
current state, in and out of an edit session (operator, 29 September 2026),
with one exception: Create audit is always listed on an Inspection + Audit
Case and, when refused, is greyed out with the refusal as its hover text
(operator, 1 October 2026).
An item taken inside a session keeps it: the page holds the next lease and
stays in edit mode until Done or a link away from the Case, unless the
action left the Case terminal or archived (operator, 9 October 2026).
An item taken outside a session runs under a lease claimed for that one
action and consumed by it, the way Generate report does
([FRD-14](frd-14-record-edit-leases.md#case-edit-lease)); a refused action
frees the lease it claimed, as does a request abandoned mid-action. While a
colleague holds the lease the menu offers nothing that needs one, so only
Export case remains; the ribbon names them and offers Take over, a Completed
or Query Case included. A
Completed or Query Case offers no Edit, since its sections read and Return
to Engineer needs no session. The rules behind each action are in
[FRD-13](frd-13-case-lifecycle-and-workflow.md#actions).

- **Assign Engineer**, in Review. Its dialog selects an
  eligible enabled staff account. The one handoff assigns them and
  enters With Engineer. There is no reviewed checkbox and no separate start
  action ([FRD-13](frd-13-case-lifecycle-and-workflow.md#assign-engineer)).
- **Export case**, on every standard Case in every state. It downloads the
  Case export, needs no lease and never changes the Case
  ([FRD-07](frd-07-case-export.md#export-case)).
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
- **Send chaser**, on any open Case where staff mail is composed in
  (operator, 5 October 2026). It is a link, not a dialog, and needs no lease:
  it opens the composer with this Case chosen, To addressed to the sender of
  the Case's instruction and the sender of each image-intake e-mail paired to
  the Case and the repairer's directory e-mail, Subject the registration and
  claimant, and Message the Case chaser template rendered from the Case
  ([FRD-21](frd-21-outbound-correspondence-and-sent-evidence.md#outbound-correspondence)).
  When its Sent evidence arrives on a Not ready Case with a scheduled chase,
  the chase is recorded and the next one scheduled
  ([FRD-13](frd-13-case-lifecycle-and-workflow.md#due-work-and-chasing)).
- **Create audit**, after Correct principal, listed on every Inspection +
  Audit Case and live while the Case is in work, whether or not its
  Inspection report is sent; when refused it is greyed out and its hover
  text states the refusal
  ([FRD-13](frd-13-case-lifecycle-and-workflow.md#create-audit)). Its
  compact dialog states the Case, the Audit reference and the Engineer, asks
  for no reason and has no outcome. It posts in place and lands on the Audit
  view with no success notice; a refusal shows its reason.
- **Close case**, after a separator and in red. Its compact dialog offers
  only the outcomes Core permits and needs an outcome and a reason
  ([FRD-13](frd-13-case-lifecycle-and-workflow.md#close-case)).

### Overview

Overview carries no list of outstanding requirements: they are the aside's
Next action and **Report not ready** card, and nowhere else (operator, 8
October 2026). The workflow position is the ribbon's state chip, so there
is no strip of stages. Each requirement is a named unmet item from the
instruction- or image-completeness set, with title, source, reason and the
section that clears it. There is never a percentage or a count
([FRD-13](frd-13-case-lifecycle-and-workflow.md#readiness-and-review)). An
Audit with no original report at all, neither a filed report nor one kept
from the instruction email, also has **Original report missing**, sourced
from Audit.

Then the Case and Principal cards, each folding and staying folded per
browser; the Sign-off Engineer is decided on the Case card. Case type, Our ref and Principal are
the ribbon's facts and are not repeated as cells (v36 item E, 9 October 2026); Received stays a
greyed cell, with no padlock; the Case card carries the derived Matter line across its foot and, once a
report has been sent, when and from which mailbox. A select's empty option carries the cell's
absent word (Not recorded, Unassigned), so the control reads as the box does. The Claim source is chosen from the active
Claim Source contacts. A Notes band shows the Principal's and the Claim
source's Notes on every Case, read-only and absent when the record has none
([FRD-04](frd-04-parties-accounts-and-access.md#contacts-administration)),
beside this Case's own Principal and Claim source notes. Then Accident
circumstances beside Notes from client.

### Claim

The claimant's cells four across — name, contact, address (spanning two
cells, so it neither wraps reading nor clips editing; 9 October 2026), Claimant VAT
status (the instruction's words, not the repairer's status the Repair Spec
records; operator, 28 September 2026) — and,
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
  custody then files that report as the Case's Audit report, and the other
  attachments as instruction documents;
- when files reach an open Audit that lists **Original report missing** —
  through Add evidence, through Upload and Add to an existing case, or by
  e-mail, matched or linked by staff — and exactly one of them is a report
  Pegasus recognises, from that file (#901, operator 28 September 2026).
  Pegasus records it as the original report in its own name. It recognises
  only the report layouts its third-party report profiles know; when none of
  the files is recognised, two or more are, or one could not be read,
  nothing is recorded and staff mark the report;
- when the report is a scan, from its OCR text once that completes, through
  the same recognition: the Case already exists, created from the message,
  and the filed document is read for it as soon as its text is;
- when staff **Mark as original report**, from that document.

A filled cell is tagged **Extracted** until staff change it. A fill lands
only on a cell neither staff nor the Automation Actor has recorded, and never
clears one, so their values are never overwritten; a value another fill
recorded takes the newer reading (operator, 7 October 2026). A cell stays
blank for staff when the report prints no value for it, prints two different
values, or prints a word the cell's list does not hold. Roadworthiness reads
a printed Yes/No or Roadworthy/Unroadworthy. Repairable status reads a
printed Repairable, Repair or Total loss; the report never fills Cash in lieu
or Contract repair.

Repairable status alone falls back to the Audit's intake verdict — the
report's literal repairable or total-loss wording, or the Principal API's
declared verdict — when the report prints no outcome or cannot be read. A
declared verdict fills the cell when the Case is created, before any report
is filed. When the report and the verdict disagree, a later report fills
nothing: the cell stays as it was, blank or holding the verdict.

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
that neither staff nor the Automation Actor has recorded, and records the
lookup's own facts. It never overwrites an extracted, staff-entered or
Automation-recorded value. There is no checks panel and
no suggestion table. The Experian seam is stated once, on the Vehicle history
area (v36 item K, 9 October 2026); the section head carries no pill. The
Vehicle history area holds the history-check narrative as read-only text,
editable in edit mode
([FRD-06](frd-06-vehicle-and-engineering-evidence.md#vehicle-data-and-mot-enrichment)).
A **DVLA & MOT lookup** outcome line says what the lookup itself did (looked
up and current, or a failure reason), separately from whether it filled a
field.

### Damage

Damage shows the **plan**: a top-down drawing of the recorded Vehicle type —
the car, the van or the motorbike
([FRD-24](frd-24-engineer-findings-damage-valuation-and-settlement.md#damage-record))
— with one yellow comic burst per recorded damage, and Underside, Interior
and Mechanical chips. While editable, dashed band guides show the eight
areas, pressing and dragging on the vehicle sizes a disc, dragging a burst
moves it, the readout names the area under the pointer, and Reset returns
the damage to the values held when the edit opened. A disc stays as it was
drawn, names the areas it touches and is never wider than half the vehicle;
its burst carries no number and may spill past the outline. Beside it sits
the recorded-areas list, numbered in recorded order, with the areas,
severity and note per damage, and the other
damage facts; the list rows and the chips stand in the same places in read
and edit mode, greyed when they cannot be edited. The **Incident narrative**
is read-only: it is the report's Nature of Incident wording, the Engineer's
own from Report wording or else the sentence composed from the recorded
damage
([FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#report-wording-blocks)).
The field set is owned by
[FRD-24](frd-24-engineer-findings-damage-valuation-and-settlement.md#damage-record).

### Valuation

Valuation draws its sources as guide cards in a grid (operator, 8 October
2026), then the value increases, the deductions, the **Engineer's Value** with
the basis its calculation starts from, and **On the report** as one line. The
Engineer's Value is the one box: a field of the Case form, greyed while
reading, that each save records. The report's **Retail value** and **Trade
value** are the chosen card's figures; nobody types them, and with no card
chosen they are blank and stay report blockers. The section head reads the
Engineer's Value the Case holds and follows each save without a reload.

Valuation lists each entry with its source, date, time, retail and trade
values, and guide month (`EXT-10`). A calculated Engineer's Value entry
carries a mileage when the Case has one. Sources are Glass's, Brego, Super
CAP, CAP and Cazana guide cards, Engineer's Value and AI market research
(automation only). Read and edit show the same cards, three to a row (two at
1180px and below, one at 760px and below): each guide source is one card with
its Retail, Trade and Guide month holding that source's latest recorded
figures (no mileage: the Case's own is used, operator, 24 September 2026), as
text while reading and as inputs in the same place while editing; any box may
be blank and is saved as entered. While editing, the boxes belong to the Case
form, and a connected source's card has **Get valuation**, a small button
centred at the card's foot (operator, 9 October 2026), which asks the
provider for the Case's accepted registration and mileage in that month and
fills the boxes in place, without redrawing the page. Glass's and Cazana are
the connected sources; a Glass's valuation's PDF report is filed on the Case's
Documents after the figures have answered and appears there on the next load
([FRD-24](frd-24-engineer-findings-damage-valuation-and-settlement.md#valuation-sources)).
A source with no connected provider shows "{Source} valuation is
unavailable. Contact an administrator or report a problem." on its card from
the start, as one quiet line under its figures rather than a notice box
(operator, 9 October 2026), and offers no Get valuation (28 September 2026,
kept 8 October 2026); a connected source that cannot answer shows the same
sentence as its notice when pressed. Reading keeps the Value increases
sub-panel, the deductions and the Calculation line in the same geometry as
editing, greyed, so entering edit grows nothing (operator, 9 October 2026);
the sub-panel still lists only what the calculation applied (8 October 2026).
A card has no Save of its own (23 September 2026): each save records
every card whose figures changed, a card left blank or unchanged records
nothing, and the same source and month replaces the earlier card; a typed
figure saves the same way. AI market research is a card of its own
(operator, 6 October 2026) holding its latest figures, a **Valuation month**
and its own **Get valuation**, which create a `MarketResearch` job; the card
reads "Researching · {month}" until it completes, and a re-run replaces its
figures. A recorded research card also states the month, mileage and date it
was asked with, and each earlier research stays a card of its own.
The result is filed without the Case edit lease, so it returns while the
Engineer is still editing and does not end the edit; the card says so
([FRD-27](frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md#ai-job-list)).

A click anywhere on a card with a retail, or Enter or Space on it, is the
Engineer's decision to use it (operator, 8 October 2026): its name, labels,
figures, boxes and padding all count; only the card's own buttons and links do
something else. The card is drawn with a red border and the word **Selected**,
and the next save records the calculation against it even when it is
unchanged, including for a card typed in the same edit, and writes the
report's Retail and Trade from it. A card with no retail answers the click
with "Enter the retail value on this card to use it."; a click into one of its
boxes only focuses the box. A save after a card was chosen, while the
Engineer's Value no longer holds that card's calculated figure, is refused
with "The Engineer's Value no longer matches the figure you chose to use."
(operator, 9 October 2026). A save also records a calculation that changed
since the last save — a different basis card, the basis card's retail or
trade, or any of its controls — and writes Retail and Trade from that card.
Any other save records no calculation and leaves Retail and Trade as they
are. There is no Use this value button and no Apply (operator, 23 September
2026).

The value increases are one list in two columns: every active preset with
its figure, then **Add 20 % VAT** with its amount, then two **Other…** rows; a
VAT-registered claimant's VAT row is disabled with that reason beside it.
Reading lists only what the recorded calculation applied, each with a tick,
and the calculator opens on that applied selection while the Engineer's Value
holds that calculation's figure. The deductions are the **Condition
deduction**, then a **Previous total loss** tick box with a −10 % / −20 %
switch, live only while ticked; ticking starts at −10 %, and its amount
stands beside it. The calculation shows what the save will use, from the
retail as typed and the claimant's VAT as the form holds it: its result fills
Engineer's Value, which is the one place the figure stands, and beside it the
section names the basis ("from {source} retail"), says "None yet" while
editing with no card chosen, or says why a figure cannot be worked out. A
figure typed over the result is the Engineer's own and records no
calculation. There is no second total and no applied block; while Engineer's
Value holds a recorded calculation's figure, its label carries that
calculation's source as one word (operator, 6 October 2026)
([FRD-24](frd-24-engineer-findings-damage-valuation-and-settlement.md#valuation-sources)).
The calculator applies presets and custom lines through Core. Valuation
sources are owned by
[FRD-24](frd-24-engineer-findings-damage-valuation-and-settlement.md#valuation-sources).

### Glass's window and Case edits

**Glass's** is the one button: it continues the staff member's live session,
reopens the estimate the repair spec on the screen belongs to, or starts a new
estimate
([FRD-25](frd-25-repair-estimates-imports-and-glasss-sessions.md#glasss-launch-and-return)).
It opens the provider window from the staff gesture once any
change not yet sent has landed. Only a confirmed save continues with the
freshly rendered authority: a refused or lost save makes no provider
request. A blocked popup gives an actionable refusal. Fetch again, shown
beside Glass's for a `Failed` session whose export was unreadable, follows
the same rule.

The same-origin launch handoff refreshes only the Glass's launch slot and
session controls on the original Case before visiting the provider URL. It
preserves the Case version and lease, focus, and reading position. Older
refresh responses cannot overwrite newer controls. Save & Exit refreshes the
workspace in place once any change not yet sent has landed, so the latest
Draft is visible at once. With no opener, the popup retains a
server-rendered route back to the Case.

Close uses the displayed session version and fresh confirmation that the
external session is closed. A version conflict refreshes the controls and asks
for confirmation again. Close never silently substitutes the current version
for the version the staff member confirmed.

### Repair Spec

Repair Spec carries the specification set and raw estimate import. See
[Assessment](#assessment).

### Decisions

Decisions shows outcome, category, salvage value, roadworthiness with the
unroadworthy reason, the agreed contract sum, labour hours (read-only, from
the Current repair spec), storage per day and the recovery charge (recorded
here since 9 October 2026). The temporary repairs, excess, betterment,
reserve, delays, hire, diminution and salvage logistics it once showed are
no longer recorded (operator, 9 October 2026;
[FRD-24](frd-24-engineer-findings-damage-valuation-and-settlement.md#settlement)).
A recorded choice (outcome, category, roadworthiness) reads as the
greyed value box and edits as a select whose empty option is Not recorded
(operator, 9 October 2026; the v28 radio group is gone). There is no figures
strip: the aside's Figures card is the home of the repair cost, the Engineer's
Value and their ratio (operator, 9 October 2026). The field meanings are owned by
[FRD-24](frd-24-engineer-findings-damage-valuation-and-settlement.md#settlement).

### Report

Whether the report uses an image, its order, rotation and crop live on the
image tile in Files. The
Report section shows wording blocks, Generate / Preview report draft, and a
separate Fee pane for the agreed fee, description lines and fee note preview
([FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#report-generation-entry-point)).
**Generate report** is offered in and out of edit mode when nothing blocks;
in edit mode it waits for a change not yet sent to land. While something blocks, the head shows
the one availability label **Not ready** in both modes (operator, 26
September 2026); the blocker list itself is the aside's Report not ready card
(operator, 8 October 2026).
The content switches are under **On the report** in Valuation. The report
renders the sign-off Engineer tuple and the marked damage diagram. The
diagram is the Case page's own plan: the report and the Damage section draw
the same vehicle and place each burst alike (operator, 27 September 2026). The
**Statement of truth** is a sub-panel folded until the browser opens it,
remembered per browser (v36 item P, 9 October 2026); its cell shows the accepted
statement the report prints, read-only; no Case edits it
([FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#assessment-report-outcomes)).

**The report card** names the report and says where it stands in plain words
(operator, 27 September 2026). Before any generation it reads "No generation
yet." After one it reads "Generated" with the date and time, then one chip:
Stored, Storing, Storage failed, Not confirmed or Not generated. Only Stored
is green. A report that was never drawn shows Not generated alone, with no
date. No raw state name is shown. What each word means is in
[FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#report-generation-entry-point).

Once the report is stored the card offers **Open report**. It opens the stored
report, which ends with its fee note, in the page's document viewer; without
script the link gives the file. After **Generate report** stores a report,
that report opens in the viewer by itself, once. A report still being filed
shows the warning notice "The report is still being filed to Box." in amber,
never as a confirmation.

Once the Case has an Audit, Report follows the view
([FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#audit-report-parity)).
In the Audit view the report card's status begins with the Audit reference
and the section shows the Audit report alone; the Views card is the way to
the Inspection report (operator, 2 October 2026). In the
Inspection view the card shows the Inspection report with its generation
status and the same Generate and Send report controls, acting on
the Inspection's own work under the session's lease or one claimed for the
generation, before and after that report is sent (operator, 1 and 2 October
2026). The Next action there is the
Inspection report's own step and lists that report's blockers while it is
not ready ([Inspection and Audit views](#inspection-and-audit-views)).

### Files

Files is one panel with three tabs: Documents, Images and Correspondence.
All three are rendered, so a no-script visit shows the lists one after the
other under their own headings. The panel header carries Add evidence,
which opens Upload for this Case with the destination already declared
([FRD-18](frd-18-manual-upload.md#upload-for-a-declared-case)), Open
Box case folder (once custody is confirmed; before that, the folder's own
state chip). Once the Case has an Audit, a second chip follows for
the `a.` audit folder, in the Case folder chip's tones: **Box audit ·
confirmed**, **Box audit folder: preparing** while it is being created, or
**Box audit folder: unavailable**.

**Documents** lists every live file as a row: filename, role, size, origin,
recorded time and custody-state chip, with Preview, Save as and, while
editing, delete. A market research findings file carries the Market research
role ([FRD-24](frd-24-engineer-findings-damage-valuation-and-settlement.md#market-research-requests)).
When an Audit lists **Original report missing**, each
non-image row also offers **Mark as original report** while editing: the
route for a report Pegasus did not recognise when it was filed. That action
assigns the Audit report role, clears the requirement and fills the
[Original report](#original-report) cells from that document. While the
Engineer sections are editable, a confirmed row Pegasus has read as an
estimate — an Audatex that arrived by email, say — also offers
**Import as repair spec** (operator, 25 September 2026). It imports that
file through the same import as the Repair Spec section, with no second copy
([Assessment](#assessment)). The Worker reads each filed version once, soon
after filing, with that import's own parse, and records the answer on the
version; a file's name never decides it, because every PDF names the PDF
format. An instruction letter is therefore never offered the import, and a
file not yet read is not offered it until it has been (operator, 1 October
2026).

**Images** is one grid of the Case's image documents. A vehicle-images
record that is associated with the Case and not yet merged into it lists its
photographs below the grid, under its Image reference. When the merge
completes they are Case images and the record's group goes, so each
photograph shows once
([FRD-19](frd-19-image-led-intake-and-pairing.md#pairing-and-merge)). Each
tile shows:

- a lazy-loaded thumbnail that expands to the full image, with the original
  filename as the accessible name. It is served only by an authorised staff
  endpoint that returns the stored image media type inline. Non-image
  material stays on the forced-download route;
- its applied tag chips
  ([FRD-05](frd-05-documents-extraction-and-custody.md#image-tags)), and for
  a Case image a Tag picker naming every vocabulary entry plus New tag with
  a colour;
- while the Case edit lease is held, **In report**, on or off, posted at
  once like a tag, so readiness reads it with no Case save (operator, 26
  September 2026). A tag, New tag or In report is a document action: it
  posts in place and redraws its own tile, and the other sections follow
  it as they follow a save;
- Preview and, while the Case edit lease is held, Crop.

While the Case edit lease is held the tile's tools are one panel joined to
the bottom of the image (operator, 1 October 2026). Its first row is **In
report**, drawn as a tick box, with the image's **Order** beside it while
the report uses the image. Its second row is one toolbar of same-size icon
buttons, each named on hover: Tag, Crop, Rotate and Print on its own page,
with the drag handle at the end. Without the lease the tile has no tools;
one line under the image says In report, its order and Full page, or Not
in report. No tool on the tile depends on the account type or on With
Engineer.

The Crop lease gate is the record's whole edit mode
([FRD-14](frd-14-record-edit-leases.md#case-edit-lease)). Crop is unavailable
once the Case reaches Completed or Query, and never on an archived Case. It
is not tied to With Engineer, so a Review-state Case shows Crop. Images open
in a full-screen viewer (title, tag, position, Rotate, Zoom, Download, Pop
out, In report while editing, which is the tile's own In report, and a
filmstrip). Crop happens on the viewer stage. Opening a document brings the
viewer into view. A crop is a stored rectangle: the tile and the report show
the cropped region and Download returns the original.

**Pop out** (operator, 9 October 2026) opens the Case's images in their own
window, `/Cases/{id}/Images`, so they can stay on a second screen while the
Case is worked on: the Files head offers it once one image can be read, and
the viewer's Pop out opens the window on the image in view and closes the
viewer here. The window is the read-mode Images tab under the same viewer
(Rotate, Zoom, Download, the filmstrip); it holds no edit lease and posts
nothing, so it offers no Crop, Tag or In report, and opening it does not end
an edit session on the Case page ([FRD-14](frd-14-record-edit-leases.md)).
One window per Case: Pop out pressed again brings it forward on the chosen
image. An image added to the Case appears in the window when it is reloaded.

Images on a vehicle-images record or an Unidentified item carry the same
crop, rotation and tags. Their rules are
in [FRD-19](frd-19-image-led-intake-and-pairing.md#operator-surfaces). A
Triage Case takes no crop and no tag
([FRD-15](frd-15-work-centre-queues-and-search.md#the-triage-case-page)).

**Correspondence** lists every email linked to the Case, whatever its
classification: the email the Case was created from, received mail
associated with it later, uploaded `.eml` files and the Sent items of mail
Pegasus sent for the Case once the Sent-evidence poll has observed them,
newest first ([FRD-20](frd-20-mailbox-workspace.md#case-correspondence-view)).
A Sent item's classification cell reads **Sent**. Each row's
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
newest first, each with date, time and actor. A general correspondence send
appears here once its Sent item is observed, as its sender (the member of
staff, or Automation for the Automation Actor), **Correspondence sent** and
the subject; the message body is never history
([FRD-21](frd-21-outbound-correspondence-and-sent-evidence.md#outbound-correspondence)).
**Add Case note** sits at the top and needs no edit session. **Record chase** is a dialog, offered while a
chase is scheduled and the lease is held
([FRD-13](frd-13-case-lifecycle-and-workflow.md#due-work-and-chasing)); a
chaser sent from the Actions menu records its chase here by itself once its
Sent evidence arrives. There is no Case tasks panel.

The workspace keeps the missing-material reason, next chase, last recorded
outcome and next permitted action together. A Triage Case's due target and
chaser are on the Triage Case page
([FRD-03](frd-03-triage.md#normal-workflow-and-completion-evidence)).

### Assessment

The Engineer workbench is the Damage, Valuation, Repair Spec, Decisions and
Report sections of the Case record. The sections can always be read and are
read-only in Completed. An image has one place (v28 P50): whether the report
uses it, its order and the tools that change them are on its tile under
Files, and non-destructive crops leave the retained source and its hash
untouched. A new image is in the report. The tile has no report
role: its tag decides how it prints, the first tagged `Overview` first and
the first other one tagged `Close-up` second, the rest as supporting images
in order (operator, 7 October 2026). The tile's order is the place its image
prints, shown as soon as the image is in the report. The two tagged places
are fixed: their order cannot be changed and they have no grip, and ordering
moves only the rest. The Overview prints on page 1 and the
Close-up leads the image pages
([FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#report-generation-entry-point)).
Beneath the grid a line counts what the report uses, out of the images that
can print
([FRD-06](frd-06-vehicle-and-engineering-evidence.md#ordinary-image-vrm-and-image-analysis));
an image still being stored is in neither number. The tile also carries
Rotate and **Full page** (v28 P41): Full page is a flag on an image the
report uses, so the image prints on a page of its own; the grip drags a tile
above the one it lands on, and a typed order moves the tile to that place;
the order the tiles then stand in is the report's order. The tile shows the
whole image. Clicking the image opens the full-screen viewer, while editing
or reading; In report is pressed on the tile or in the viewer (operator,
7 October 2026). The Report section carries no image surface.

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
an unknown repairer VAT status never gates **Use repair spec** (P10), and it
does block the report (operator, 27 September 2026). VAT categories nobody
chose by hand follow the Repairer VAT status as it changes (operator, 28 September 2026). Lines an import brought
in read `imported · AX`, `GL`, `JSON` or `AI` on their
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
edits. The spec has no save of its own: each save records it, and a spec
left unchanged is not rewritten. Moving the Target % of value slider
previews the scaled spec in amber cells, the rollup and the readout, and
records nothing (operator, 28 September 2026). Apply and Remove scaling wait
for a change not yet sent to land and then scale the saved spec. The More
menu holds New repair spec (editing, recorded by the next save and starting
on the one enabled labour-rate card), **Print Repair Spec** for a saved spec
with lines, and Compare, greyed out until the Case holds two specs (P9).
Previewing the document records nothing. The section also
carries **Send to AI**, which creates an `AI-10` `Estimate` job
([AI Job List](frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md#ai-job-list)),
disabled without an Engineer's Value. The Report section reads in two tabs (v28 P24): **Report**, everything the
report itself carries, and **Fee**, the fee note the agreed fee makes: the
agreed fee, the VAT the report charges on it and their total in one row, the
description lines below, and the generated fee note to download. The agreed
fee opens with the Principal's default fee, tagged Principal, until staff
change it
([FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#report-readiness)).
Without script both panes stand.

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
Generate for the Repair Spec or the images when the confirmed generation does
not yet hold it, in or out of edit mode as Generate report is. Generate report
makes the separate fee note with the report (operator, 7 October 2026), and
once the separate fee note is confirmed the report card offers Open fee note
beside Open report (issue 912).
The delivery form offers the Case's known addresses on every recipient field
(v28 P21), the documents to attach (v28 P22), with Report and Fee note ticked,
and states the name the report will be attached under (v28 P23) and the
covering message it will carry, in an editable box pre-filled from the Case
report delivery template, before Send report is pressed.

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

The Inspection view of a Case with an Audit follows the same table: the
state is the Case's, so the Audit's, and a save there writes the
Inspection's values only.

## Edge cases and fail-closed behaviour

- A lease a colleague now holds shows the holder; the next save is refused
  and the typed values stay on screen. A save over a value the system filled
  since the page loaded it is a non-destructive conflict showing current and
  proposed values.
- A refused or unknown save response keeps the proposed values for review;
  the next change tries again.
- A change not yet sent lands before Done, Refresh, navigation or an
  immediate action; a document action (tag, New tag, In report) posts in
  place and redraws its own tile, the other sections following as after a
  save. Closing the tab sends a change not yet
  sent as the page hides.
- An action bar for a state with no permitted action shows the state and no
  control.
- Crop is refused on an archived Case and in Completed or Query.
- An ambiguous raw estimate file is refused with its reason.
- A `view` value on a Case without an Audit is ignored. Every write names
  the view it was posted from and returns to it: from the Audit view it
  changes the Audit's values, from the Inspection view the Inspection's
  (operator, 2 October 2026). The one Case edit lease and Case version
  serialise edits to both works.

## Acceptance evidence

Acceptance covers the ten sections and the `?section=` jump, the Report
readiness list in the aside's Report not ready card linking each blocker to its section,
the read-only rule in Completed, the Actions menu per state, and save as
you go: a save keeps the session and returns the authority the next one
carries, and the ribbon offers Done and no Save or Cancel. Web tests cover
what a save's answer draws and the authority it carries; they do not prove
the script's commit on change, and the browser walk does. It also covers
the views: no Views card without an Audit; after Create
audit the card and the Audit view by default; the Inspection view read-only
with its label on each editable head, including for the lease holder; Report
in each view; the audit folder chip in each state; the Create audit dialog;
and a standalone Audit's single view with its Original report, recorded from
a report added later through Add evidence, Upload or a matched e-mail. Web tests
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
  [FRD-07](frd-07-case-export.md),
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
