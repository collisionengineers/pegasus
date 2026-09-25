# FRD-24: Engineer findings, damage, valuation and settlement

> Owner capabilities: CASE-22, CASE-28, ENG-02 to ENG-04, EXT-07, EXT-10, EXT-13 · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- Roadworthiness and Assessment are separate Engineer findings. A correction
  is a new reasoned version, never an edit of the old one.
- A damage entry records the areas it covers, a severity and a note. Impact
  location and severity are derived by `Pegasus.Core`, never typed in.
- Glass's, Brego, Super CAP, CAP and Cazana are guide valuation sources.
  Engineer's Value is adopted, with its basis card's retail and trade values,
  by a staff member's Save when the calculation changed, in a fixed order.
- Settlement saves with the Case's single workspace Save. Equity is derived,
  never typed in.
- AI and Market Research only propose. An authorised person decides.

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
derived from the other, and Triage findings never fill or change either one.
Every enabled human staff role may record or correct these findings under the
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

**No money effects.** Triage findings and their corrections have no effect
on a linked instruction Case or on any report, Audit reference, fee or
invoice. Invoicing is deferred
separately: a finding correction must not create, alter, credit or void an
invoice. Any later financial consequence needs the separately accepted,
versioned finance contract.

**AI proposes, people decide.** Automated or AI-assisted extraction may
propose candidate facts, confidence, damage observations, repair operations,
costs, flags, valuation comparables, roadworthiness, total-loss or salvage
evidence only where an allocated capability and accepted evaluation allow
it. `Pegasus.Core` and an authorised person own accepted facts, economics,
findings, outcome, legal use and approval. A skill, prompt, model, workspace,
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
no wider than half the vehicle. Every disc is clipped to the vehicle's body,
on the workspace and on the report alike.

The record also carries tyres and seat belts per corner, the spare tyre, the
centre belt, which airbags deployed in the Engineer's words (for example
`None` or `Driver and passenger front`, recorded under Tyres & seat belts and
printed in the report's Vehicle Details), unrelated damage with its
deduction, and paint or material transfer. `impact_location` and
`impact_severity` are derived from the areas by `Pegasus.Core`, never typed
in: one distinct area reads as itself, more read Multiple. The report prints
the marked diagram
([FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#assessment-report-outcomes)).

### Valuation sources

Valuation records keep guide month and source. Glass's, Brego, Super CAP,
CAP and Cazana are guide sources, each an entry card of the same shape. The
figures are typed by hand; **Get valuation** at the foot of the card asks
that source's connected provider for the month and fills the card's boxes,
and while the source has no working provider the card answers `{Source}
valuation is unavailable. Contact an administrator or report a problem.`
(23 September 2026). The card has no Save of its own: the Case's single
workspace Save records every changed card with whatever was entered, and any
of its month, retail and trade may be left blank (operator, 23 September
2026); a card with every box blank, or unchanged, records nothing. A guide
card carries no mileage (operator, 24 September 2026): the Case's own
accepted mileage is the one a valuation uses, both for the lookup and for the
Engineer's Value.
The basis is chosen by clicking a card (or Enter or Space on it), and only a
card with a retail value can be the basis, since the calculation starts from
retail; there is no Basis control beside the figures ([FRD-16](frd-16-case-record-workspace.md#case-workspace)). AI
market research is automation-only. No guide provider is
connected today; connecting one needs its own accepted decision
([ADR-0031](../adr/0031-automation-actor-contract-without-eva-export-tools.md)).

Every entry keeps its date and time, and the retail and trade values and
guide month it was given; a guide card may hold any of them blank. An
Engineer's Value or AI market research entry always carries its figures and a
mileage: an adopted Engineer's Value takes the Case's accepted mileage in
miles, so a Save that would adopt one while the Case has no mileage is
refused, as the lookup is. Glass's valuation and Glass's repair estimating are two systems
and both are used: the valuation source and the estimate import source keep
separate label entries and are never merged. An AI market research entry is
the proposal recorded by the `MarketResearch` job
([FRD-27](frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md#ai-job-list));
it never becomes the Engineer's Value by itself.

**Engineer's Value** is adopted only by an enabled human staff member's Case
Save, and only when the valuation calculation changed since the page opened
(a different basis card, the basis card's retail or trade, or any calculator
control; operator, 23 September 2026), in this order:
commercial VAT 20%, prior total loss 10% or 20%, fixed additions, then
condition deduction, rounding to whole pounds away from zero. No field of the
Save writes the adopted value directly; an unchanged calculation adopts
nothing. The adoption also records the basis card's retail and trade as the
Case's retail and trade values, which the report prints beside the
Engineer's Value; a later adoption replaces all three together. A basis card
without a trade figure records no trade value, and report readiness names
Trade value until trade is entered on that card and the Case saved, which
adopts again
([FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#report-readiness)).
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
reason; otherwise they are absent (operator, 24 September 2026), and the
report's Vehicle Details prints their values only for an unroadworthy vehicle;
for any other vehicle those rows read —. Beside the typed
reserve, a computed **Repair reserve** reads the Current repair
specification's VAT-inclusive cost rounded up to the next £50 on a
Repairable outcome, and Not applicable otherwise; it is never written.

**The unroadworthy reason bank (v28 P15).** An Engineer inserts a wording
into the reason, joined to what is already there with "and", and may save
the typed reason to the Principal's own bank. Seven standard wordings are
offered to every firm; a firm's saved wordings follow them. The wordings
print on the assessment report, so they are report wording. Nothing deletes
a wording.

Settlement saves with the Case's single workspace Save. Storage per day and
recovery use the existing typed Inspection members; a lump storage charge is
a separate fact. The repair total is read from the Current accepted repair
specification; repair days are no longer recorded (v28 P32). Equity
uses the report's existing calculation over accepted inputs and is absent
when those inputs are incomplete, never a made-up zero.

### Market Research requests

Choosing **Market Research** on the Valuation screen creates a ledger job.
External Claude Cowork, using the Pegasus connector and its own research
tools, does the research and produces files. The connector attaches those
files to the Case and the Automation Actor marks the job Completed. The tools
and the research run outside this repository. Research evidence and any
source-labelled valuation proposal never become the Engineer's Value on their
own. Job states and attribution are owned by
[FRD-27](frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md#ai-job-list).

### Valuation readiness

Any valuation check required before Review or Hand to Engineer must be
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
- No assessment field writes the Engineer's Value or its basis card's retail
  and trade; only a staff Save whose valuation calculation changed adopts
  them.
- A valuation source with no connected provider shows the card's notice and
  still lets the figures be typed by hand; the Case Save records them.
- Research evidence and an AI valuation proposal never become the Engineer's
  Value on their own.
- No circular readiness gate is acceptable.

## Acceptance evidence

Core tests cover the Engineer's Value order. Integration tests cover an
adoption recording its basis card's retail and trade and a basis card without
trade leaving Trade value outstanding. Web tests cover Airbags deployed and
the temporary repair rows in read and edit and through the Case Save. Live
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
  [ADR-0031](../adr/0031-automation-actor-contract-without-eva-export-tools.md).
