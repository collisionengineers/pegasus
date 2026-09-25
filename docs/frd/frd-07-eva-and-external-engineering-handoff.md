# FRD-07: EVA and external engineering handoff

> Owner capabilities: CASE-21, CASE-30, EXT-03, EXT-04 · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- EVA is optional. Each Principal picks one report route: Pegasus, EVA ZIP
  export, manual EVA API, or automatic EVA API on entering Review.
- Sending to EVA never changes the Case state. Only Hand to Engineer moves a
  Case from Review to With Engineer.
- The ZIP is an export only. Pegasus never claims EVA received it.
- An API send records one of four outcomes: Succeeded, Rejected, Partial,
  Unknown. Nothing is retried automatically.
- Once a Case has been sent, later changes reach EVA only by an explicit
  re-send, which creates a second EVA claim.

## Purpose

This document owns the two optional handoff routes to EVA, the external
engineering system: the ZIP package and the direct API submission. It says
what is sent, when a send is allowed, what is recorded, and what a send does
not prove. Native engineering work is owned by
[FRD-13](frd-13-case-lifecycle-and-workflow.md) and never needs EVA.

## Behaviour

## EVA and external engineering handoff

### EVA handoff routes

Each Principal has exactly one report route, set by its report-generation
policy ([ADR-0048](../adr/0048-principal-report-generation-policies.md)):

| Route | What staff see | What Pegasus does |
| --- | --- | --- |
| Pegasus | No EVA action | Generates the report itself ([FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md)) |
| EVA ZIP | **Download ZIP** | Builds the package below; export only |
| Manual EVA API | **Send via API** | Submits over the API when staff choose |
| Automatic EVA API | A staff retry only after the automatic send failed | Submits once when the Case enters Review |

The default route is Pegasus. Manual and automatic API sends use the same
validated command and the same delivery ledger. A replacement Principal
inherits the route and recipient settings. A disabled Principal's settings
stay as history. Changing a Principal's route does not send a Case that is
already in Review.

**When EVA is available.** EVA is offered in Review and again in With
Engineer as a re-send. A Case reaches Review when the required items in
Workflow configuration are complete
([FRD-13](frd-13-case-lifecycle-and-workflow.md#readiness-and-review)). EVA
adds no second readiness policy of its own: no extra field checks, evidence
status, Case custody or Audit custody rule.

**Sending never moves the Case.** A ZIP download or an API send records its
handoff evidence and the Case version it used. The Case stays in its current
state, with the same version and edit lease. Hand to Engineer
([FRD-13](frd-13-case-lifecycle-and-workflow.md#hand-to-engineer)) is the only
way from Review to With Engineer.

### Focused EVA manual handoff

**What Download ZIP sends.** Pressing Download ZIP confirms the values
currently on the Case. A populated suggestion is exported and keeps its
`Suggested` provenance. VAT and mileage are optional. If mileage is present,
mileage and its unit must be saved together. If Inspection Date is blank,
the export date is the named system default. The export has no activation or
mapping-acceptance switch; the API route does, per Principal.

The package is deterministic UTF-8 JSON with these keys in this exact order,
plus every eligible retained Case-vehicle image:

1. `Work Provider`
2. `VRM`
3. `Vehicle Model`
4. `Claimant Name`
5. `Reference`
6. `Incident Date`
7. `Instruction Date`
8. `Inspection Date`
9. `Inspection Address`
10. `Accident Circumstances`
11. `VAT Status`
12. `Mileage`
13. `Mileage Unit`

`Reference` is the work provider's reference, not the Pegasus Case
reference. `Instruction Date` is the Case's Received date
([FRD-23](frd-23-case-draft-fields-provenance-and-global-checks.md#instruction-field-meanings))
as `dd/MM/yyyy`; every Case has one, so it is never blank. The archive holds
the JSON and an `Images/` folder only. There is no manifest and no
provenance sidecar. Pegasus does not choose or order images for EVA, with
one exception: an image tagged Third party image
([FRD-05](frd-05-documents-extraction-and-custody.md#image-tags)) is left
out. An image's custody status is used to find its verified bytes, not as a
readiness decision.

**What is recorded.** Every successful export writes a replay-safe Case
history record with the Case version, mapping identity, exported values and
provenance, archive hashes, and image identities and hashes. The first
successful export also writes the once-per-Case `First sent to Engineer`
history line that the dashboard counts. That line is history, not a state
change. Later exports are further history records. The HTTP download carries
the archive's SHA-256 as `Content-Digest`.

### Direct EVA API submission

Pegasus can submit a Case to EVA over EVA's API. It sends the same mapped
values and the same eligible images as the ZIP. The route was built against
EVA's test credentials on 2026-08-27.

**Claimant address.** The API also sends the accepted claimant address as
`ClmAdd`. EVA requires it and allows at most 40 characters. The current
Confirmed value is used before a Fact; a suggestion or unresolved value is
not accepted. A value that is missing, whitespace-only, too long, or contains
control or format characters blocks a new submission before any image is
fetched or EVA is called. Nothing is recorded and the Case is unchanged.
Valid text, including ordinary punctuation, is sent unchanged; Pegasus never
truncates it or swaps in another party's address. This is an API
prerequisite, not a Case-readiness or ZIP gate. The thirteen-field package is
unchanged.

**Values EVA has no field for.** The inspection date and the mileage go as
labelled lines in the instruction note. The work provider travels the same
way, because the claimant name occupies `InsName`. The Case's Received
date is not sent; EVA sets its own instruction date on arrival.

**Automatic sends.** An automatic API policy creates one durable intent in
the same transaction that moves the Case into Review. The Worker may run only
that intent, using the existing operation identity and delivery ledger.
Repeated delivery or Review events cannot create another intent. The intent
is never rebuilt later.

**Re-sends.** Once a Case has been submitted, later changes reach EVA only
through an explicit re-send. From With Engineer the Send to EVA dialog offers
Download ZIP, and Send via API when the Principal enables it. A re-send over
the API is a new, separately recorded submission with its own outcome and EVA
identifiers. EVA cannot update a claim, so a re-send creates a second claim.
That is the operator's deliberate act in the dialog, never a retry and never
an update.

**Outcomes.** Every submission records one outcome. The four stay distinct.

| Outcome | Meaning | Retried |
| --- | --- | --- |
| Succeeded | EVA accepted the instruction and returned its identifiers | No |
| Rejected | EVA refused it and said why | No; the same payload would be refused again |
| Partial | EVA accepted it but returned no identifier | No; the Case did reach EVA |
| Unknown | Delivery could not be determined | No automatic retry; staff check EVA and decide |

An `Unknown` result may already have reached EVA. It is never retried
without a staff decision. Staff see a clear failure message telling them to
check EVA and retry if no claim was created. This applies to failed manual
and automatic sends. There is no attestation form, confirmation record or
persistent retry block. Replaying an exact completed operation returns its
retained outcome after authorisation, without calling EVA again.

Pegasus keeps both EVA identifiers: the response identifier and the File
Reference that EVA embeds in its message text, which is what an operator
quotes.

**When an API send is allowed.** The Case must be in Review, or in With
Engineer for a re-send, and must have at least one eligible image to send,
exactly as the ZIP does. No other readiness rule is repeated. Every attempt,
delivered or not, is recorded in Case history.

- A failure found before the transport call leaves nothing recorded.
- A failure found only after EVA has accepted, such as a version conflict on
  the post-delivery check, still records the submission and its history,
  because the delivery happened.
- In every case the Case state, version and edit lease are unchanged.

**Evidence tiers.** A vendor schema and recorded traffic show the contract;
they are not acceptance of a real Pegasus submission. Dated deployment and
external-call evidence belongs in [operations](../operations.md).
Credentials, an EVA API route for a Principal, and a live acceptance run are
each authorised separately.

### External boundary

The ZIP package and the API are optional external routes. Native estimates,
imported provider estimates and accepted AI estimates stay Pegasus-owned
engineering behaviour. Estimate import is in
[FRD-25](frd-25-repair-estimates-imports-and-glasss-sessions.md#retained-pdf-estimate-import);
this adapter adds no calculation policy.

A vendor schema is evidence, not a real-call result and not permission to
act. Success, rejection, partial and unknown outcomes stay distinct. An
explicit staff re-send is new confirmed work; it never authorises a blind
retry of an uncertain send.

## States and transitions

EVA sends change no Case state. The submission record itself moves through
one of the four outcomes above and then stays there. An `Unknown` outcome is
terminal until staff choose an explicit re-send, which is a new record.

## Edge cases and fail-closed behaviour

- Missing or invalid `ClmAdd` blocks the send before EVA is called; nothing
  is recorded.
- Rejected or Unknown is not a handoff; the Case is unchanged.
- A replayed operation returns its stored outcome and does not call EVA.
- A changed Principal route never sends a Case already in Review.
- A Third party image is never included.

## Acceptance evidence

Core tests cover the package field order, image exclusion, the `ClmAdd`
rules, outcome recording, replay and the no-state-change rule. Integration
tests cover Download ZIP and Send via API over HTTP with a recorded EVA
transport. A live EVA call is a separate, separately authorised evidence tier
([engineering](../engineering.md#required-evidence-tiers)).

## Links

- Capabilities: `CASE-21`, `CASE-30`, `EXT-03`, `EXT-04` in
  [capabilities](../capabilities.md).
- Related FRDs: [FRD-05](frd-05-documents-extraction-and-custody.md),
  [FRD-06](frd-06-vehicle-and-engineering-evidence.md),
  [FRD-25](frd-25-repair-estimates-imports-and-glasss-sessions.md),
  [FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md),
  [FRD-13](frd-13-case-lifecycle-and-workflow.md).
- Technical constraints:
  [ADR-0048](../adr/0048-principal-report-generation-policies.md).
