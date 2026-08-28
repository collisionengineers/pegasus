# Plan

## Approach

Build one Core-owned ambiguity operation around the existing
`IVehicleLookupAdapter`: it deterministically expands a qualifying
machine-read registration, records the result for every candidate, and resolves
only after the whole bounded set is terminal. Wire image intake to that operation
before its existing routing decision. Wire document OCR at its explicit result
boundary supplied by [[TICK-041]], rather than trying to infer OCR provenance
from generic Case fields.

This keeps one list and one resolution rule, reuses the existing provider
adapter and typed outcomes, and leaves exact staff/embedded-text lookup behavior
unchanged. [[TICK-041]] blocks implementation because the requested document-OCR
caller does not yet exist; planning and the image-side design are still complete.

## Governing docs

- `docs/frd/frd-02-intake-and-source-identity.md`: preserve grouped-image
  terminality and fail-closed association; only a uniquely provider-resolved VRM
  may enter the existing associate-or-register routing decision. The future
  scan-like OCR caller opts in before its result becomes an intake fact.
- `docs/frd/frd-06-vehicle-and-engineering-evidence.md`: reuse the existing
  DVLA/DVSA request/result contract and preserve provider, response identity,
  retrieval/effective/source time, and typed outcome for every candidate.
  Selecting a candidate never overwrites the raw machine reading.
- No ADR is required: this extends the accepted Core/Infrastructure boundary and
  existing lookup adapter rather than choosing a new store, runtime, provider,
  or deployment unit.

## Steps

1. Add a focused Core candidate/resolution policy beside the vehicle lookup
   contracts. Normalise a qualifying standard VRM, return the original first
   plus every distinct `O` / `0` permutation in stable order, enforce one
   explicit maximum, and abstain on invalid/over-limit input. Resolve only when
   all candidates are terminal: exactly one `Current`/`Stale`/`Partial`
   result succeeds; zero, multiple, or any unresolved retryable/unavailable
   attempt does not.

2. Add a durable machine-read ambiguity work shape using the repository's
   external-work conventions, owned by the intake receipt/asset rather than a
   fabricated Case. Persist raw read, route, policy/version, ordered candidate,
   attempt/result provenance, state, and a replay key. Add schema, indexes,
   model snapshot, and Worker permissions in the same diff. Reuse
   `IVehicleLookupAdapter` for each candidate and publish/retry every
   concurrency result rather than discarding it.

3. Insert the image caller after a confident terminal VRM suggestion and before
   `ImageIntakeGroupRoutingPolicy` acts on distinct registrations. A read
   without `O`/`0` follows the current path. A qualifying read waits for the
   durable resolution; unique success supplies the resolved registration to
   the unchanged group/single-image routing, while no-match, ambiguous, or
   incomplete results withhold association and Image Intake allocation. Retain
   both the recognition evidence and lookup attempts.

4. After [[TICK-041]] provides the real document-OCR result boundary, call the
   same ambiguity operation only for its explicit vehicle-registration
   candidates. Feed a unique resolution into the normal extracted-field path;
   route zero/multiple/incomplete outcomes to its named review/failure contract.
   Embedded-text extraction and staff confirmation do not opt in. Do not add a
   dormant registration or test-only production caller.

5. Add Core tests for deterministic generation, multiple ambiguous positions,
   bounds, result classification, incomplete attempts, and unchanged exact
   matching. Add integration tests proving image-group waiting, unique
   resolution, zero/multiple matches, unavailable/retry replay, durable
   provenance, idempotency, and no effect on non-machine Case lookups. Add the
   parallel OCR-route tests at the [[TICK-041]] caller.

6. Run the branch simplification pass over the implementation diff, recording
   reuse/simplification/efficiency/altitude findings and dispositions in this
   plan. Run focused tests, then the canonical locked restore, Release build,
   and non-Corpus test gate. Verify the real Worker/Web caller and shipped
   runtime permissions/artifact, not registration alone.

7. Update `docs/current-architecture.md` to describe only the route(s) actually
   wired in the resulting release. Do not describe document OCR as active until
   [[TICK-041]] and this integration are both caller-backed and deployed.

## Proof

- Core test output demonstrates exhaustive bounded generation and fail-closed
  resolution.
- Integration test output demonstrates durable, idempotent route-specific
  behavior and preservation of every provider result.
- A schema/grant inspection proves the Worker can process the new durable work.
- Caller tracing names the image pipeline and the real document-OCR boundary.
- Canonical solution commands complete with exit code 0.
- Post-merge verification uses controlled provider responses for unique,
  none, ambiguous, and unavailable cases; no live external write is required.

## Risks and mitigations

| Risk | Mitigation |
| --- | --- |
| Candidate calls multiply provider load | Standard-shape validation, explicit maximum, durable per-candidate idempotency, and existing retry/throttle outcomes |
| First hit is selected while another candidate is unavailable | Resolution waits for every candidate to reach a conclusive terminal outcome |
| Image association occurs before disambiguation | Make ambiguity terminality an input to the existing group routing decision |
| Generic Case facts are mistaken for OCR | Opt in only at explicit image/OCR boundaries while provenance is still known |
| Pre-Case work is forced into Case tables | Persist against intake receipt/asset identity and reuse only the adapter/result contract |
| Document route is claimed before it exists | [[TICK-041]] blocks implementation and supplies the required real caller |
