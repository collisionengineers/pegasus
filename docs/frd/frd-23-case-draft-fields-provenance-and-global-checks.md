# FRD-23: Case draft fields, provenance and global checks

> Owner capabilities: INT-19, INT-20 · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- Every Case datum keeps where it came from. Showing provenance is never
  the same as confirming the value.
- A value is a Fact, a Suggestion, or Confirmed. A Confirmed value wins for
  current use, and the earlier value stays in history.
- Every Case needs three global checks, or a recorded exception, before it
  can enter Review: vehicle identity and specification, vehicle history and
  risk, and market valuation.
- Mileage evidence has a fixed order. DVSA runs for every Case, fills the
  mileage only when nothing better exists, and a difference is shown as a
  discrepancy.
- The instruction field table says what each captured field means.

## Purpose

This document owns what a Case's drafted fields mean, how each value records
its provenance and kind, and the global vehicle and value checks a Case must
pass before Review. It serves the PRD outcome for no invented identity.
Receiving material is owned by
[FRD-02](frd-02-intake-and-source-identity.md). The gates before a Case
exists, and matching, are owned by
[FRD-22](frd-22-pre-case-gates-matching-and-association.md). Principal
identity is owned by [FRD-01](frd-01-case-identity-and-lifecycle.md), the
MOT mileage estimate and inspection address by
[FRD-06](frd-06-vehicle-and-engineering-evidence.md), and Case readiness by
[FRD-13](frd-13-case-lifecycle-and-workflow.md).

## Behaviour

### Field provenance and value kinds

Each Case datum keeps its current provenance: staff entry, extraction, AI
prefill or proposal, provider API, or another external vehicle or estimate
source, with its identity, version, and time. The UI shows provenance without
treating it as confirmation: one word in a source tag beside the value
(Extracted, AI, E-mail, Lookup, Principal, Automatic, Provider API), and no tag
on a value staff typed or corrected. A derived value names its inputs and
calculation rather than claiming a raw source.

Each datum also carries a value kind (Fact, Suggestion, or Confirmed) and a
source kind (intake evidence, mail route, case acceptance, staff correction,
vehicle lookup, provider setting, or Provider API). `work_provider_code`
names the Principal for match indexing and EVA export. It is Confirmed with
source kind case acceptance when staff acceptance names the Principal (see
[Ways intake starts](frd-02-intake-and-source-identity.md#ways-intake-starts)),
and Confirmed with source kind staff correction on a Wrong-Principal
replacement Case
([FRD-01](frd-01-case-identity-and-lifecycle.md#principal-reference-organisation-and-case-party-identity)).
A Confirmed value supersedes an earlier Fact or Suggestion for current use
without erasing it from history.

### Global vehicle and value checks

Every Case must pass three global checks unless a documented exception
applies: vehicle identity and specification, vehicle history and risk, and
market valuation. All three results, or their recorded exceptions, are
required before the Case can enter Review and appear in the Engineers queue.
An authorised staff reviewer may record an exception as a named, reasoned
Case action in permanent history. Provider and route policy choose the
provider, the required result, the acceptable provenance, and the
unavailable or failure behaviour for each check. This requirement names no
provider.

Vehicle details come from the instruction where present, otherwise from the
applicable DVLA or MOT source. Mileage evidence ranks as:

1. an accepted staff-entered value;
2. text extracted directly from the instruction, including a third-party
   engineer report supplied with a Principal's instruction;
3. Document Intelligence extraction from a scanned instruction, or future
   odometer-vision evidence;
4. a DVSA-derived estimate.

DVSA runs for every Case. Its estimate fills the Case mileage only when no
higher-tier value exists. A difference between the DVSA mileage and any
accepted staff-entered, instruction-extracted, Document Intelligence, or
odometer value is shown on the Case as a discrepancy. The odometer-vision
capability does not imply an activated AI caller before its own accepted
evaluation and integration contract. The estimate method is owned by
[FRD-06](frd-06-vehicle-and-engineering-evidence.md#conservative-mot-mileage-estimation).

### Instruction field meanings

A Work Instruction describes a claimant involved in a road traffic accident.
Capture:

| Field | Rule |
| --- | --- |
| Work Provider | Also called the Principal. |
| Claimant Name | From the instruction. |
| Claim Number | The Principal's external reference. |
| Vehicle Registration | The VRM. |
| Source Vehicle Description | Keep the instruction's combined claimant-vehicle description with its source locator, on the Case record and the Received screen. Do not split it into make and model by guesswork, and do not treat a third-party vehicle as the claimant's. The Case Vehicle section shows the looked-up or confirmed make and model instead. |
| Vehicle Make | From the instruction, or an authorised lookup when absent. |
| Vehicle Model | From the instruction, or an authorised lookup when absent. |
| Vehicle Mileage | From the instruction when supplied; MOT-based estimation when available. |
| Accident Circumstances | From the instruction. |
| Date of Incident | From the instruction. |
| Instruction Date | The document value; today's date if absent. |
| Inspection Address | FRD-06 inspection-location rules. |

## States and transitions

- A datum's value kind is Fact, Suggestion, or Confirmed. A Confirmed value
  supersedes an earlier Fact or Suggestion for current use without erasing
  it from history.
- A Case cannot enter Review or appear in the Engineers queue until all
  three global checks have a result or a recorded exception. The Case
  states themselves are owned by
  [FRD-13](frd-13-case-lifecycle-and-workflow.md#states-and-labels).

## Edge cases and fail-closed behaviour

- A global check has no result: the Case stays out of Review until an
  authorised staff reviewer records a named, reasoned exception.
- A higher-tier mileage value exists: the DVSA estimate never overwrites
  it. The difference is shown on the Case as a discrepancy.
- Mileage evidence is used in tier order: staff-entered, extracted from the
  instruction, Document Intelligence or odometer vision, then the DVSA
  estimate.
- A derived value names its inputs and calculation. It never claims a raw
  source.
- The instruction's combined vehicle description is never split into make
  and model by guesswork, and a third-party vehicle is never treated as the
  claimant's.
- Instruction Date is absent: today's date is used.

## Acceptance evidence

Acceptance proves the value kinds and source kinds, the mileage tier order,
the DVSA discrepancy display, and that a Case cannot enter Review without
the three global checks or their recorded exceptions. Deployment and live
evidence are separate tiers
([engineering](../engineering.md#required-evidence-tiers)).

## Links

- Capabilities: `INT-19`, `INT-20` in [capabilities](../capabilities.md).
- Related FRDs: [FRD-01](frd-01-case-identity-and-lifecycle.md),
  [FRD-02](frd-02-intake-and-source-identity.md),
  [FRD-06](frd-06-vehicle-and-engineering-evidence.md),
  [FRD-13](frd-13-case-lifecycle-and-workflow.md),
  [FRD-22](frd-22-pre-case-gates-matching-and-association.md).
