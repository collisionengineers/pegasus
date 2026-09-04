# Research

## Question

How should Pegasus resolve supported letter/digit ambiguity in a UK vehicle
registration produced by ordinary-image recognition or document OCR, using the
existing DVLA/DVSA route without weakening trusted-registration behavior?

## Conclusion

Add one Core-owned, UK-only candidate and resolution policy around the existing
`IVehicleLookupAdapter`. The evidence-led confusion map contains only
`O` ↔ `0` and `I` ↔ `1`. Candidate generation filters the permutations
through the supported GB and Northern Ireland structures, has a proven maximum
of eight candidates, and resolves only after every candidate is conclusive.

Do not change `VrmRegistrationMatching`. Its exact, one-character-missing and
inserted-fifth-position-`1` rules compare image reads with an already confirmed
Case registration and are distinct from provider-backed correction.

## Findings

### The policy belongs in Core

`VehicleLookupRequest` in
`src/Pegasus.Core/Vehicle/LookupContracts.cs` validates one normalized
uppercase registration. `DvlaDvsaProductionAdapter` already performs the
approved DVLA and DVSA calls for that one value and returns
`Current`, `Stale`, `Partial`, `NotFound`, `Throttled`,
`Unavailable` or `Failed`, retaining response identity and source times.

The new operation therefore reuses `IVehicleLookupAdapter` once per candidate
and the existing outcome taxonomy. Candidate generation, supported character
pairs, structural formats and final classification have one owner in Core.

### Supported structures bound the search

Normalize by removing spaces, uppercasing ASCII and rejecting non-alphanumeric
input. Candidate structures are:

| Family | Structure |
| --- | --- |
| GB current | `LLDDLLL` |
| GB prefix | `L D{1,3} LLL` |
| GB suffix | `LLL D{1,3} L` |
| Dateless / Northern Ireland | `L{1,3} D{1,4}` or `D{1,4} L{1,3}`, total length at most seven |

Generate substitutions only at `O`, `0`, `I` and `1`, retain only
values matching one of those structures, de-duplicate, and order the valid raw
read first followed by substitution count and ordinal comparison. Enumerating
the structure masks proves that no input can yield more than eight distinct
valid candidates; the length-five families are the maximum.

The policy provides UK-provider candidate handling only. It does not add
Republic of Ireland or European formats, normalization or providers. A foreign
registration that is textually indistinguishable from a supported UK structure
cannot be classified by characters alone; provider evidence still fails
closed.

### Resolution waits for a conclusive whole set

A candidate is viable only when the existing result is `Current`, `Stale`
or `Partial`. A unique viable candidate resolves only if every other candidate
is `NotFound`. More than one viable candidate is `Ambiguous`; all
`NotFound` is `NoMatch`. Any throttled, unavailable, failed or otherwise
unresolved attempt prevents a conclusion and becomes `Incomplete` or
`Failed` according to the existing retry exhaustion contract.

The original machine read, ordered candidates and every request/result remain
evidence. A successful first call never short-circuits the remaining set.

### Pre-Case work needs intake ownership

`ReconcileAutomaticVehicleLookups`,
`EfVehicleWorkflowStore.EnqueueDueAsync` and
`VehicleLookupRequestEntity` are Case-bound. Image recognition can identify a
vehicle before a Case exists, so reusing those rows would require a fabricated
Case and violate the intake invariant.

The durable ambiguity request must instead belong to the intake source evidence
and link to the existing external-work item. It records the route, raw read,
policy version, ordered candidates, per-candidate attempts/results and final
state. Its replay identity prevents duplicate candidate calls for the same
source evidence and read.

### Route provenance is available only at the caller boundary

`ImageIntakeAutomation` still has the recognition engine/model, asset,
receipt and grouped-image context before routing. The future scan-like document
OCR boundary supplied by [[TICK-041]] will likewise know that a registration is
machine-read. Both callers opt in there.

Staff-confirmed values, embedded-text instruction extraction, ordinary Case
lookups and case search keep exact-registration behavior. Generic Case source
kinds are not sufficient evidence that a value came from OCR.

### The document caller is not available

A live board check on 2026-09-04 found [[TICK-041]] still in Backlog, untaken,
and still blocking this ticket. Source inspection also found no active
scan-like document-OCR caller. The approved scope keeps both callers in this one
ticket, so INTK-049 must remain in Preparing until that dependency lands; a
dormant hook or image-only partial implementation would not satisfy the ticket.

### Additional confusion pairs lack evidence

The local corpus exists, but the focused
`VrmRecognitionCorpusEvaluationTests` check exited 1 because it contains no
case-attributed labelled images. That failure is retained as evidence: it
cannot justify adding another pair. The current map is limited to the two
operator-approved pairs. A future pair needs real labelled corpus or production
evidence and a separate scope decision.

## Verified premises

- Read-only source inspection confirmed the single-registration Core request,
  adapter and typed provider results.
- Read-only source inspection confirmed the current automatic lookup is
  Case-bound and image routing can occur pre-Case.
- Read-only source inspection confirmed the existing confirmed-registration
  matching rules, including the inserted-`1` plate-furniture case.
- FRD-02 owns grouped-image terminality and fail-closed association.
- FRD-06 owns exact provider outcomes and evidence provenance.
- Official DVLA/GOV.UK guidance confirms the current, prefix, suffix, dateless
  and Northern Ireland registration families used by the structural filter.
- The live Kanmer dependency check confirmed [[TICK-041]] has not supplied the
  document-OCR caller.

## Assumptions

- Provider outcomes remain the source of truth for whether a structurally valid
  candidate identifies a UK vehicle.
- No historical backfill is included. The behavior applies prospectively and
  to idempotent replay of newly handled machine reads.
- No new package, provider, runtime, deployment unit, feature flag or
  compatibility path is required.
