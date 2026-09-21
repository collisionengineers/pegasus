# FRD-24: Engineer findings, damage, valuation and settlement

> Owner capabilities: CASE-22, CASE-28, ENG-02 to ENG-04, EXT-07, EXT-10, EXT-13 · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- Roadworthiness and Assessment are separate Engineer findings. A correction
  is a new reasoned version, never an edit of the old one.
- A damage entry records the areas it covers, a severity and a note. Impact
  location and severity are derived by `Pegasus.Core`, never typed in.
- Glass's, Brego, Super CAP, CAP and Cazana are guide valuation sources. Engineer's Value
  is adopted only by an explicit Apply, in a fixed order.
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

**No money effects.** Triage findings and their corrections have no Case,
report, Audit-reference, fee or invoice effect. Invoicing is deferred
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
same area and stay two entries. The record keeps the areas only: the disc is
drawn from them, centred between its areas and wide enough to reach each,
on the workspace and on the report alike.

The record also carries tyres and seat belts per corner, the spare tyre, the
centre belt, unrelated damage with its deduction, and paint or material
transfer. `impact_location` and `impact_severity` are derived from the areas
by `Pegasus.Core`, never typed in: one distinct area reads as itself, more
read Multiple. The report prints the marked diagram
([FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#assessment-report-outcomes)).

### Valuation sources

Valuation records keep guide month and source. Glass's, Brego, Super CAP,
CAP and Cazana are guide sources, each an entry card of the same shape. The
figures are typed by hand; **Get valuation** at the foot of the card asks
that source's connected provider for the month and, while the source has no
provider, answers `Error. Contact an administrator.` The basis is chosen by
clicking a card (or Enter or Space on it); there is no Basis control beside
the figures ([FRD-16](frd-16-case-record-workspace.md#case-workspace)). AI
market research is automation-only. No guide provider is
connected today; connecting one needs its own accepted decision
([ADR-0031](../adr/0031-automation-actor-contract-without-eva-export-tools.md)).

Every entry keeps its date, time, mileage, retail and trade values, plus the
guide month. Glass's valuation and Glass's repair estimating are two systems
and both are used: the valuation source and the estimate import source keep
separate label entries and are never merged. An AI market research entry is
the proposal recorded by the `MarketResearch` job
([FRD-27](frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md#ai-job-list));
it never becomes the Engineer's Value by itself.

**Engineer's Value** is adopted only by an explicit Apply, in this order:
commercial VAT 20%, prior total loss 10% or 20%, fixed additions, then
condition deduction, rounding to whole pounds away from zero. A generic
assessment save never writes the adopted value. This calculation is current
required behaviour. Extra rationale or revaluation-history scope needs its
own accepted contract.

### Settlement

The settlement fields are outcome, category, salvage value, excess,
betterment, claimant VAT registered, reserve, equity (derived), repair
delays, report delay, storage per day, recovery, hire start and daily
cost, diminution, and salvage logistics. Equity is derived, never typed in.
Financial ratio lines are allowed, not required; the "no percentage" rule in
[FRD-13](frd-13-case-lifecycle-and-workflow.md#readiness-and-review) applies
only to completeness. Outcome meanings are owned by
[FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#assessment-report-outcomes).

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
resolvable at that stage by an authorised person. The Engineer sections are
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
- A generic assessment save never writes the Engineer's Value; only an
  explicit Apply does.
- A valuation source with no connected provider shows a notice and still
  lets the figures be typed by hand.
- Research evidence and an AI valuation proposal never become the Engineer's
  Value on their own.
- No circular readiness gate is acceptable.

## Acceptance evidence

Core tests cover the Engineer's Value order. Live Glass's evidence is a
separate tier ([engineering](../engineering.md#required-evidence-tiers)).

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
