# FRD-24: Engineer findings, damage, valuation and settlement

> Owner capabilities: CASE-22, CASE-28, ENG-02 to ENG-04, EXT-07, EXT-10, EXT-13 · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- Roadworthiness and Assessment are separate Engineer findings. A correction
  is a new reasoned version, never an edit of the old one.
- A damage entry records the areas it covers, a severity and a note. Impact
  location and severity are derived by `Pegasus.Core`, never typed in.
- Glass's, Brego, Super CAP, CAP and Cazana are guide valuation sources.
  Glass's is connected: Get valuation fetches its figures and files its PDF
  report on the Case.
  The Engineer's Value is a box on Valuation, typed or filled by the
  calculation. A click on a guide card is the Engineer's decision to use that
  card: the Save records the calculation against it and writes the report's
  Retail value and Trade value from it. Nobody types Retail or Trade.
  The preview shows what the Save will use.
- Settlement saves with the Case's single workspace Save. Equity is derived,
  never typed in.
- Staff and the Automation Actor record findings. AI market research only
  proposes: it never becomes the Engineer's Value by itself.

## Purpose

This document owns the Engineer's judgement on a Case: the professional
findings and their correction, the damage record, valuation sources and the
Engineer's Value, settlement, Market Research requests and valuation
readiness. It serves the PRD outcome for a definitive Engineer report. The
vehicle evidence underneath it (inspection address, registration, DVLA and
MOT data) is owned by
[FRD-06](frd-06-vehicle-and-engineering-evidence.md). Repair estimates,
imports and Glass's sessions are owned by
[FRD-25](frd-25-repair-estimates-imports-and-glasss-sessions.md). Report
outcomes are owned by
[FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#assessment-report-outcomes),
and AI job states by
[FRD-27](frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md#ai-job-list).

## Behaviour

### Professional engineering findings and correction

The Collision Engineers Engineer report is definitive for the Case.
Roadworthiness (`Roadworthy` or `Unroadworthy`) and Assessment
(`Repairable` or `Total loss`) are separate professional findings. Neither is
derived from the other. A linked Triage's finding fills either one only where
the Case holds nothing, once
([FRD-03](frd-03-triage.md#triage-findings-on-the-linked-case)); a value staff
entered is never replaced.
Every enabled human staff role, and the Automation Actor
([FRD-10](frd-10-mcp-automation-and-actor-boundary.md#assessment-writes);
operator, 7 October 2026), may record or correct these findings under the
existing state, lease and version rules. On an Inspection + Audit Case with
an Audit, the Audit's findings, damage, valuation and settlement are
recorded on the Audit's values; the Inspection's stay as its report was
sent ([FRD-01](frd-01-case-identity-and-lifecycle.md#principal-reference-organisation-and-case-party-identity)).

**Corrections.** A correction never edits an accepted or issued finding in
place. It creates a reasoned superseding report, finding or addendum with
actor, time, source, structured before-and-after values, and the earlier
artifact or version kept. Case edits follow the state, role, lease and
version rules in [FRD-13](frd-13-case-lifecycle-and-workflow.md#actions) and
[FRD-14](frd-14-record-edit-leases.md#case-edit-lease). Completed is
reversible and queries follow
[FRD-13](frd-13-case-lifecycle-and-workflow.md#completed-and-query).

**Retained figures.** Betterment figures and estimate `guide` codes recorded
on a source or an estimate version are evidence only. No finding, figure,
outcome, deduction or settlement meaning is derived from them. They are shown
as recorded.

**No money effects.** Beyond that one fill, Triage findings and their
corrections have no effect on a linked instruction Case, and none on any
report, Audit reference, fee or invoice. Invoicing is deferred
separately: a finding correction must not create, alter, credit or void an
invoice. Any later financial consequence needs the separately accepted,
versioned finance contract.

**AI proposes, people decide.** Automated or AI-assisted extraction may
propose candidate facts, confidence, damage observations, repair operations,
costs, flags, valuation comparables, roadworthiness, total-loss or salvage
evidence only where an allocated capability and accepted evaluation allow
it. `Pegasus.Core` and an authorised staff member or, within
[FRD-10](frd-10-mcp-automation-and-actor-boundary.md#mcp-automation-and-actor-boundary),
the Automation Actor own accepted facts, economics, findings, outcome, legal
use and approval. A skill, prompt, model, workspace,
external schema or imported reference never becomes OEM instruction, repair
policy, valuation authority, legal advice, Engineer approval or product
policy just by existing.

### Damage record

Report readiness no longer has separate Engineer name, qualification or
signature items. The Sign-off Engineer account
([FRD-04](frd-04-parties-accounts-and-access.md#staff-accounts)) is the only
source of the signatory tuple.

Each damage entry records the areas it covers, a severity and a note;
collision work has no separate damage type (v28 P5, ruled 20 September 2026).
The areas are the eight of the plan — Front, LH Front, RH Front, LH Side, RH
Side, Rear, LH Rear, RH Rear — and Underside, Interior and Mechanical. A disc
drawn on the plan is one entry naming one or more plan areas; each of the
other three is an entry of its own, recorded once. Two discs may cover the
same area and stay two entries. A drawn disc is kept as drawn — its centre on
the plan and its radius, no smaller than the plan's smallest disc and no
wider than half the vehicle — and the entry's areas are exactly the plan
areas that disc touches, which `Pegasus.Core` reads off the disc (operator
decision, 23 September 2026, superseding the 20 September ruling that the
record keeps the areas only). An entry recorded by area alone, from the
keyboard, keeps just its areas and is drawn with a disc centred between them,
no wider than half the vehicle.

The plan is the recorded Vehicle type's drawing: a van is drawn as the van, a
motorcycle or scooter as the motorbike, and anything else, or no type, as the
car (operator decision, 28 September 2026). All three share one body box, so
the eight areas and a saved disc stand in the same place on each. Every
entry's disc is drawn as the same yellow comic burst, whatever its severity,
with no number and not clipped, so it may spill past the vehicle's outline;
the burst is only its picture, and the disc alone decides the areas. The
workspace and the report draw alike.

The record also carries tyres and seat belts per corner, the spare tyre, the
centre belt, which airbags deployed in the Engineer's words (for example
`None` or `Driver and passenger front`, recorded under Tyres & seat belts),
unrelated damage with its deduction, and paint or material transfer.
`impact_location` and `impact_severity` are derived from the areas by
`Pegasus.Core`, never typed in: one distinct area reads as itself, more read
Multiple.

The report prints the marked diagram, which is the Case page's own plan, and
no damage or tyre table (operator, 27 September 2026). Tyres, seat belts,
airbags, the unrelated-damage deduction and paint or material transfer are
recorded on the Case and not printed. Unrelated damage prints as its own
paragraph when its switch is on
([FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#report-wording-blocks)).

### Valuation sources

Valuation records keep guide month and source. Glass's, Brego, Super CAP,
CAP and Cazana are guide sources, each an entry card of the same shape. The
figures are typed by hand; **Get valuation** in the card's head asks that
source's connected provider for the month and fills the card's boxes.
A source with no connected provider says so on its card before anything is
pressed, `{Source} valuation is unavailable. Contact an administrator or
report a problem.`, and offers no Get valuation (23 and 28 September 2026).
When Glass's answers that it does not value a vehicle of that age, its card
shows an info notice instead, `Glass's cannot value this vehicle because of
its age: Glass's values cars and motorcycles up to 20 years old and light
commercial vehicles up to 15.` (operator, 2 October 2026).
The card has no Save of its own: the Case's single
workspace Save records every changed card with whatever was entered, and any
of its month, retail and trade may be left blank (operator, 23 September
2026); a card with every box blank, or unchanged, records nothing. A guide
card carries no mileage (operator, 24 September 2026): the Case's own
accepted mileage is the one a valuation uses, both for the lookup and for the
Engineer's Value.
The basis is chosen by clicking anywhere on a card (or Enter or Space on
it), and only a card with a retail value can be the basis, since the
calculation starts from retail; there is no Basis control beside the figures
([FRD-16](frd-16-case-record-workspace.md#case-workspace)). AI market research
is automation-only.

Glass's is the one connected guide source
([ADR-0060](../adr/0060-glass-valuation-account-and-valuation-report.md)).
Its Get valuation signs in with the Glass's valuation account, values the
Case's accepted registration and mileage for the card's month, and fills the
card with Retail Transacted as its retail and Glass's Trade as its trade.
Each valuation also saves the vehicle to the account's Glass's stock list, as
the portal does, and once the figures have answered, files that stocked
vehicle's "Vehicle Valuation Report – Glass's Values Only" PDF on the Case's
Documents as `Glass's valuation {registration} {yyyy-MM}.pdf` (operator, 1
October 2026), only when the report's own text names the Case registration
(operator, 5 October 2026). Whatever stops a Glass's valuation, the card shows its notice.
Brego, Super CAP, CAP and Cazana have no connected provider; connecting one
needs its own accepted decision.

Every entry keeps its date and time, and the retail and trade values and
guide month it was given; a guide card may hold any of them blank. An
Engineer's Value or AI market research entry always carries its figures. An
Engineer's Value calculated from a guide card records the Case's accepted
mileage in miles when the Case has one; none is needed (operator, 26
September 2026). Glass's valuation and Glass's repair estimating are two systems
and both are used: the valuation source and the estimate import source keep
separate label entries and are never merged. An AI market research entry is
the proposal recorded by the `MarketResearch` job
([FRD-27](frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md#ai-job-list));
it never becomes the Engineer's Value by itself.

**Retail value, Trade value and Engineer's Value** are the three values the
report prints in its Vehicle Data table. Retail value and Trade value are the
chosen guide card's figures (operator, 8 October 2026): nobody types them.
The Save writes both from the card a calculation is chosen against, as that
save leaves the card, whenever it carries that choice; a figure the card
leaves blank is blank on the report. With no card chosen they are blank. The
Engineer's Value is the one box on Valuation, below the cards and the
calculation: typed, or filled by the calculation, and recorded by the Case's
single workspace Save like any field. No mileage is needed. Each of the three
is a report blocker until it is entered
([FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#report-readiness)).
A field save (the assessment save or `pegasus_assessment_update`) refuses
Retail value and Trade value; `pegasus_valuation_save` with a calculation
writes them the same way the Case Save does.

The calculation starts from the basis card's retail and applies, in this
order: commercial VAT 20%, prior total loss 10% or 20%, fixed additions, then
condition deduction, rounding to whole pounds away from zero.

**Choosing a card is the decision to use it** (operator, 28 September 2026;
a click on the card since 8 October 2026). A click anywhere on a guide card or
the AI market research card while editing, all but its own buttons and links,
chooses it as the basis, marks it **Selected**, and tells the Save that the
Engineer decided to use it. The Save then records the
calculation against that card, so the Case keeps where the figures came from,
even when the calculation is the one the page opened on. A card typed in the
same edit can be chosen the same way: the Save records the card and the
calculation together. A card with no retail has nothing to use, so the click
says so on the card and the Save, if asked, is refused with that reason. The Save also records the calculation when it changed since
the page opened (a different basis card, the basis card's retail or trade, or
any calculator control; operator, 23 September 2026). A save that does
neither records no calculation, so an unrelated save never adopts a value.
A figure typed over the Engineer's Value box after the calculation filled it
is the Engineer's own value: the Save records it as theirs and does not
record the calculation against the card.
If the box no longer holds the figure the Engineer chose to use (the preview
had not landed, or a card changed underneath), the Save is refused with that
reason and writes nothing, so a decision is never dropped silently. A
preview that fails or is refused puts the box back to its recorded value and
withdraws the decision; typing in the box withdraws it too. A card with no
retail answers the click with "Enter the retail value on this card to use
it.", and a cleared retail box is "no retail", not the recorded card's
figure.

**The preview shows what the Save will use.** The figure that fills the
Engineer's Value box, and the commercial VAT and previous total loss amounts
shown beside their controls, come from the retail on the chosen card as
typed, even unsaved, and the claimant's VAT position as the form holds it,
through the same Core calculation the Save runs. The Engineer's Value box is
the one place the result stands: there is no separate proposed total
(operator, 6 October 2026). While a preview is pending the amounts are
dimmed, a preview that fails says so, and a calculation that cannot be worked
out shows its own reason (deductions beyond the value, no retail to start
from), never "None yet". "None yet" means only that no card is chosen, and is
shown only while editing.

**What the page shows of a recorded calculation** (operator, 6 October
2026). While the Engineer's Value holds a recorded calculation's figure, its
label carries that calculation's source as one word, the calculator opens on
that calculation, and that source's card is the one marked Selected while
reading.
Once a different figure is saved in the box, none of that is shown: the
figure is the Engineer's own, and the earlier calculation stays in the Case's
history. The section head's figure and the source word follow each save
without a reload. There is no applied block and the page never reads "None
yet" beside a figure. Without script the Engineer's Value is typed, the
previous total loss percentages are chosen directly, and a recorded
calculation's result is not displayed.

**The Engineer's own value.** The Engineer's Value box takes a figure the
Engineer types with no card and no calculation. It is recorded as the staff
member's own value with their name and time, not as a guide source, and the
report reads it like any other Engineer's Value. A typed value needs no
calculation and no mileage; Retail value and Trade value still come only from
a chosen card and stay report blockers until one is chosen.
This calculation is current required behaviour. Extra rationale or
revaluation-history scope needs its own accepted contract.

### Settlement

The settlement fields are outcome, category, salvage value, roadworthiness
and the unroadworthy reason, for an unroadworthy vehicle whether temporary
repairs are possible with their method and cost, excess, betterment,
claimant VAT registered, reserve, equity (derived), repair delays, report
delay, storage per day, recovery, hire start and daily cost, diminution, and
salvage logistics. Equity is derived, never typed in.
Financial ratio lines are allowed, not required; the "no percentage" rule in
[FRD-13](frd-13-case-lifecycle-and-workflow.md#readiness-and-review) applies
only to completeness. Outcome meanings are owned by
[FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#assessment-report-outcomes).

The report prints the outcome, the roadworthiness and the unroadworthy
reason. On a total loss it prints the category and the salvage value. On a
contract repair it prints the agreed contract sum. The other settlement
fields are recorded on the Case and not printed (operator, 27 September
2026).

**The Decisions surface (v28, ruled 20 September 2026).** Outcome, Salvage
category and Roadworthiness are chosen from a radio group of their codes,
with the unset state offered as "Not recorded"; the select stays the posted
control, so a browser without script picks the same value from it. Salvage
value carries a slider reading the share of the Engineer's Value, with 5,
10, 15, 20 and 25 % snaps: the amount and the share are one fact and the
last touch wins. While the outcome is not a total loss the salvage rows are
absent and a "Salvage · Not applicable" line stands in their place. While
Roadworthiness is Unroadworthy, Temporary repairs possible (Yes or No),
Temporary repair method and Temporary repair cost follow the unroadworthy
reason; otherwise they are absent (operator, 24 September 2026). They are
recorded on the Case and not printed on the report (operator, 27 September
2026). Beside the typed reserve, a computed **Repair reserve** reads the
Current repair specification's VAT-inclusive cost rounded up to the next £50
on a Repairable outcome, and Not applicable otherwise; it is never written.

**The salvage matrix (operator, 29 September 2026).** When the Case's
Principal has a salvage matrix
([FRD-04](frd-04-parties-accounts-and-access.md#contacts-administration)),
the matrix fills the Salvage value while the Case edits. Once the outcome is
Total loss, a category is chosen and the Engineer's Value is known, the
value is the Engineer's Value times the percentage of the category's band
that holds it, to the penny, with a half penny rounding away from zero. A
change of outcome, category or Engineer's Value fills it again. When no
figure applies any more (N/A, no Engineer's Value, a value in a gap between
bands) a figure the matrix filled is cleared. A figure the Engineer sets by
typing, the slider or a snap is their own and the matrix leaves it;
emptying the value hands it back. A saved value equal to the matrix's figure
counts as the matrix's. Opening the edit fills nothing. The Save records the
value as it records a typed one, with no separate provenance. A Principal
without a matrix changes nothing.

**The unroadworthy reason bank (v28 P15).** An Engineer inserts a wording
into the reason, joined to what is already there with "and", and may save
the typed reason to the Principal's own bank. Seven standard wordings are
offered to every firm; a firm's saved wordings follow them. The wordings
print on the assessment report, so they are report wording. Nothing deletes
a wording.

Settlement saves with the Case's single workspace Save. Storage per day and
recovery use the existing typed Inspection members; a lump storage charge is
a separate fact. The repair total is read from the Current repair
specification; repair days are no longer recorded (v28 P32). Equity
uses the report's existing calculation over accepted inputs and is absent
when those inputs are incomplete, never a made-up zero.

### Market Research requests

Choosing **Market Research** on the Valuation screen creates a ledger job.
External Claude Cowork, using the Pegasus connector and its own research
tools, does the research and produces files. The connector files the findings
document, with the Market research document type, and the AI market research
card, and the job becomes Draft ready. Market research is a document type, not
an image tag (operator, 7 October 2026).
The tools and the research run outside this repository. Research evidence and
any source-labelled valuation proposal never become the Engineer's Value on
their own.

**The result returns while the Engineer is still editing** (operator, 28
September 2026). Filing the result needs no Case edit lease and no Case
version: a source card and its findings file are not a Case field edit, so
they do not wait for the Engineer's session and do not end it. The Case, its
version and the figures the Engineer is typing are left as they were, and the
Case history records that the research was attached. The edit-authority rule
is not bypassed: the result writes no Case field, and only the Engineer's
Save can adopt any figure from it. While the job runs the Researching card
says the result is filed without ending the edit; save or refresh to see it.
Job states and attribution are owned by
[FRD-27](frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md#ai-job-list).

### Valuation readiness

Any valuation check required before Review or Assign Engineer must be
resolvable at that stage by an authorised human staff member. The Engineer sections are
editable before handoff in Not ready and Review, so availability is not a
reason to defer such a check. Engineer's Value, settlement and report
calculations are engineering work, not invented pre-assignment blockers. A
named external-check failure shows its actual permitted resolution. No
circular readiness gate is acceptable.

## States and transitions

| Thing | States |
| --- | --- |
| Engineer finding | recorded; a correction is a superseding version, never an edit |

## Edge cases and fail-closed behaviour

- A correction never edits an accepted or issued finding in place.
- Equity is absent when its accepted inputs are incomplete, never a made-up
  zero.
- The Engineer's Value and its retail and trade values are professional
  findings: staff or the Automation Actor record them.
- A valuation source with no connected provider shows the card's notice from
  the start, offers no Get valuation, and still lets the figures be typed by
  hand; the Case Save records them. A connected source that cannot answer
  shows the same notice and fills nothing.
- A Glass's valuation whose report cannot be fetched or filed keeps the
  figures it answered; the Case simply has no report for it.
- A calculation that cannot be worked out never shows as an empty state: it
  shows why, and the Engineer's draft is kept.
- Research evidence and an AI valuation proposal never become the Engineer's
  Value on their own.
- No circular readiness gate is acceptable.

## Acceptance evidence

Core tests cover the Engineer's Value order, the preview using the typed
retail and the form's VAT position, provider availability, and Retail and
Trade being refused by every field save and canonicalized from a card.
Integration tests cover a typed Engineer's Value saving without a mileage and
clearing its blocker while Retail and Trade stay blockers, a calculation
recorded against its basis card with Retail and Trade written from that card,
a card typed in the same save being used, an overtyped Engineer's Value
staying the Engineer's own while Retail and Trade still follow the card, the
preview and the Save recording one figure, and a research result filed while
a staff member holds the edit lease. Web tests cover the guide cards in both
modes, the chosen card's Selected word, no Retail or Trade box, and the
calculation standing once below every card. Core tests cover the
salvage matrix's rules, band lookup, rounding and when a value follows the
matrix. Web tests cover Airbags deployed and
the temporary repair rows in read and edit and through the Case Save, and
the salvage matrix handed to the Case only while it edits. Integration tests
cover Glass's Get valuation against the scripted provider — its figures, month,
mileage and stock save, every failure answering the notice and a vehicle
too old to value answering its own sentence — and its report
filed on the Case without touching the open edit session. Live
Glass's evidence is a separate tier
([engineering](../engineering.md#required-evidence-tiers)).

## Links

- Capabilities: `CASE-22`, `CASE-28`, `ENG-02`–`ENG-04`, `EXT-07`, `EXT-10`,
  `EXT-13` in [capabilities](../capabilities.md).
- Related FRDs: [FRD-04](frd-04-parties-accounts-and-access.md),
  [FRD-06](frd-06-vehicle-and-engineering-evidence.md),
  [FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md),
  [FRD-13](frd-13-case-lifecycle-and-workflow.md),
  [FRD-14](frd-14-record-edit-leases.md),
  [FRD-16](frd-16-case-record-workspace.md),
  [FRD-25](frd-25-repair-estimates-imports-and-glasss-sessions.md),
  [FRD-27](frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md).
- Technical constraints:
  [ADR-0031](../adr/0031-automation-actor-contract-without-eva-export-tools.md),
  [ADR-0064](../adr/0064-automation-actor-staff-casework-parity.md).
