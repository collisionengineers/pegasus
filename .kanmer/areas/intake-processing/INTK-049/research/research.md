# Research

## Question

How should Pegasus resolve an `O` / `0` ambiguity in a vehicle
registration produced by ordinary-image recognition or document OCR, using
the existing DVLA/DVSA lookup path without changing trusted registrations?

## Findings

### The ambiguity policy belongs in Core

`VehicleLookupRequest` in
`src/Pegasus.Core/Vehicle/LookupContracts.cs` validates one already-normalised
uppercase registration. It has no candidate-generation or ambiguity policy.
`VrmRegistrationMatching` in
`src/Pegasus.Core/ImageIntake/VrmRecognition.cs` already owns the accepted
image-read comparison rules, but deliberately rejects substitutions; it only
allows exact, one-character-missing, and one specific inserted-`1` match.
Changing that match helper to accept `O` / `0` substitutions would silently
weaken case association before provider evidence exists.

Implication: add one bounded Core policy in the vehicle boundary that derives
distinct `O` / `0` candidates and resolves provider outcomes. Do not make
`VrmRegistrationMatching` treat the characters as equivalent.

### The production adapter already supplies the evidence shape

`DvlaDvsaProductionAdapter` performs both provider calls for one
`VehicleLookupRequest`. It returns the existing typed outcomes
`Current`, `Stale`, `Partial`, `NotFound`, `Throttled`,
`Unavailable`, or `Failed`, with response identity and retrieval/source
times. `VehicleLookupResult.EnsureValidFor` binds every result to its exact
requested registration.

Implication: reuse `IVehicleLookupAdapter` once per candidate. A viable
candidate is one returning the existing evidence-bearing outcomes
`Current`, `Stale`, or `Partial`; do not invent a second provider result
taxonomy. Preserve every result, including misses and failures.

### Existing automatic lookup starts too late for image-initiated identities

`ReconcileAutomaticVehicleLookups` and
`EfVehicleWorkflowStore.EnqueueDueAsync` operate on active Case workflows
and enqueue one durable request per `(CaseId, Registration)`.
`VehicleLookupRequestEntity` and its external work item require a Case ID.
By contrast, image recognition routes a usable read through
`ImageIntakeAutomation` and `EfImageIntakeStore` to an existing Case, an
Image-initiated pre-Case identity, or Unidentified. An Image-initiated identity
is not a Case workflow and must not be fabricated as one merely to reuse the
case lookup table.

Implication: disambiguation for image reads must occur before the routing
decision acts on a registration, or through a separately durable intake-owned
work record. It must feed the resolved candidate back into the existing
group/single-image routing policy. The case reconciliation sweep remains the
caller for exact established Case registrations and must not expand all Case
values indiscriminately.

### Route provenance is available at intake, not reliably from Case value kind

Image recognition persists engine/model identity, suggested registration,
confidence, outcome, and receipt/asset identity in the image-intake records.
Instruction fields retain intake evidence labels and extraction policy
provenance, but `CaseDataSourceKind` only distinguishes broad intake evidence;
it does not itself say “document OCR”. Document Intelligence OCR for scan-like
PDF pages is currently absent and allocated as INT-16, while ordinary-image
VRM recognition is live and in-process.

Implication: the OCR caller should opt into ambiguity resolution at the point
where the OCR result still has explicit route provenance. Do not infer OCR
later from generic Case data or apply the policy to staff-confirmed and
ordinary embedded-text instruction values. The document side is wired when
its real OCR caller exists; no dormant caller or fake activation belongs here.

### Candidate expansion must be bounded and deterministic

A standard registration read is seven characters in the current image matching
policy. Exhaustively toggling every `O` / `0` position gives at most
`2^7 = 128` distinct candidates for that accepted shape, including the
original. The general `VehicleLookupRequest` currently permits twenty
characters, for which unbounded expansion would be inappropriate.

Implication: the Core generator should accept only the route's already-valid,
normalised VRM shape, order the original first and remaining candidates
deterministically, de-duplicate, and enforce an explicit maximum candidate
count. An over-limit input abstains rather than truncating silently. The exact
bound is a technical safety constant owned beside the generator, not copied
into each caller.

### Resolution must fail closed across all terminal outcomes

One evidence-bearing candidate can resolve the ambiguity only after all
generated candidates have terminal outcomes. Zero evidence-bearing candidates
means no resolution. More than one means ambiguity. A retryable or unavailable
candidate means the set is not conclusively exhausted and therefore cannot
justify selecting another apparent hit.

Implication: the orchestration persists/returns every attempt and distinguishes
resolved, no match, ambiguous, and incomplete/unavailable. It never updates a
registration merely because the first candidate succeeds.

## Verified premises

- Read-only source inspection confirmed the current single-registration
  request contract and typed provider results.
- Read-only source inspection confirmed automatic lookup is Case-bound and
  idempotent per Case/registration.
- Read-only source inspection confirmed image recognition retains route
  provenance and routes pre-Case Image Intake identities.
- The governing FRDs require DVSA for every Case, preservation of provider
  provenance, and fail-closed intake association.
- Document OCR remains deferred in the current architecture; this ticket must
  provide its integration contract without claiming that route is live.

## Assumptions

- “Opposing combinations” means every distinct combination obtained by
  independently swapping each `O` and `0`, including the original.
- The existing evidence-bearing lookup outcomes define a viable provider match.
- No historical backfill is included; this is prospective processing and
  idempotent replay of newly handled machine reads.
