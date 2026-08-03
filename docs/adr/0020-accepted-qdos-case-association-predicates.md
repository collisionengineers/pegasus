# ADR-0020: Accepted QDOS automatic case-association predicates

(Filed as ADR-0020 to resolve a numbering collision: this body was originally
headed ADR-0019, taken concurrently with the in-process ONNX VRM recognition
decision that kept the number.)

- Date: 2026-08-03
- Status: accepted
- Owners: Collision Engineers operator and Pegasus development team
- Relation: instantiates ADR-0008 clause 7 (each route policy owns its own
  evidence precedence and case-association rules) for the QDOS direct route;
  supersedes ADR-0008 clause 11's single-domain QDOS sender identity with the
  operator-accepted three-domain set; leaves the general multi-rule precedence
  and confidence questions in
  [open decisions](../open-decisions.md#mailbox-rule-activation-automatic-matching-and-confidence-display)
  open for every other route and surface

## Context

The mailbox-rule-activation open decision requires each automatic matcher to
stay inactive until its exact predicates and conservative outcomes are
operator-accepted. On 2026-08-03 the operator accepted the predicates below in
a recorded design session, grounded in the 329-email corpus evidence
(`docs/temp-plans/qdos-email-tells.md`): the durable QDOS claim identity is the
`NNNNN/N` reference tail (present in 326 of 329), handler prefixes vary on the
same claim, `qdoslaw.co.uk` uses a distinct reference grammar under the same
principal, instruction letters carry both the client and third-party vehicle,
and the predecessor's loose extraction produced false registrations from free
text. The settled requirements clause "Matching conflicts and reversible
association" already provided the frame: contradictory signals never silently
associate, and an incident-date mismatch may eliminate a candidate while a
matching date alone proves nothing.

## Decision

For mail on the accepted QDOS direct route only:

1. **Route identity.** QDOS direct sender identity is exact whole-domain
   equality against `qdosassist.co.uk`, `qdoslaw.co.uk`, or
   `qdosassists.co.uk` (`qdos_mail_route` v3) — no suffix or subdomain
   widening. An accepted domain alone still classifies nothing and associates
   nothing.
2. **Match keys** (`qdos_case_match` v1), extracted label-anchored with a
   required separator, never scraped from free text: the claim reference
   normalized to its durable token (the `NNNNN/N` tail for `qdosassist`
   references, full or bare; the letters grammar for `qdoslaw` references),
   the client-vehicle registration compacted to `[A-Z0-9]` (TP-prefixed
   labels are never harvested), and the claimant name as title-stripped
   surname plus first initial. Multiple distinct values for one key withdraw
   that key. The incident date (labelled fields plus the generated subject
   `on DD/MM/YYYY`) is never a positive key.
3. **Eliminator procedure.** Candidates are every QDOS case matching ANY key,
   in every lifecycle state (the operator confirmed staff do not archive; a
   post-report case is post-report stage). A candidate contradicted by the
   message's incident date or by another identity key present on both sides
   is eliminated. Exactly one survivor is an automatic association; zero is
   no match (instructions proceed to the normal creation gates); several fail
   closed as the recorded Ambiguous outcome, forcing `Needs sorting` with the
   competing candidates visible. A `Created in error` survivor redirects to
   its linked replacement case and is never associated itself. `NoKeys`
   remains distinguishable from `NoMatch`. No numeric confidence score,
   threshold, or display exists anywhere.
4. **Recording and reversal.** Every evaluation persists a decision record
   (keys, per-candidate hits and eliminations with reasons, outcome, policy
   key and version) one-to-one with the intake receipt. An automatic
   association is written idempotently by the system-worker identity with the
   match policy stamped, no-ops when any active association exists, and is
   reversible through the ordinary staff unlink with full history.

This pulls the QDOS-direct subset of MAIL-09 forward to `Now /
0.1.0-alpha.1`. General multi-provider association, the classified-email
workspace, and every other route's matchers remain allocated `Next / 0.3.0`
and stay gated by the open decision.

## Consequences

- The predicates are Core-owned, code-versioned policy
  (`QdosCaseMatchPolicy`, the shared eliminator in
  `EvaluateIntakeCaseMatch`); a behaviour change is a version bump, never a
  silent redefinition, and any normalization change requires an explicit
  rebuild of the derived match index.
- The match index is a read model of accepted case data maintained in the same
  transaction by every case-data writer — case acceptance, staff case-data
  save, vehicle-suggestion confirmation, and Created in error replacement
  creation — all through one shared projector, so write and read sides share
  the one provider grammar. Any new case-data writer must call the projector;
  the index starts empty at migration because no environment holds an accepted
  case before this release, so no backfill exists and a future normalization
  bump still requires the explicit rebuild above.
- The predecessor's false-registration shapes (`AND2`, `OCTOBER`, postcode
  outward codes, `X5 NOW`) are pinned as negative tests, and claim tokens
  proved collision-free across distinct labelled corpus cases.
- No generic rule engine, rule table, or admin editor is introduced; a second
  provider's matcher needs its own operator-accepted predicates and policy.
