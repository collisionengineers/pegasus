# Checklist — INTK-049

- [ ] Step 1 — Revalidate [[TICK-041]] after it is merged into `dev`,
  record the exact document-OCR registration caller, refresh packet evidence,
  update FRD-02/FRD-06 and make every file declaration concrete.
- [ ] Step 2 — Implement and unit-test the single Core UK candidate/resolution
  policy for O/0 and I/1, including deterministic ordering, structural
  filtering, the eight-candidate bound and unchanged confirmed matching.
- [ ] Step 3 — Add and integration-test intake-owned durable requests and
  candidate attempts, including replay constraints, migration, generated model
  artifacts and Worker grants.
- [ ] Step 4 — Process every candidate through the existing
  `IVehicleLookupAdapter`, retaining all results, retrying honestly and
  classifying only the conclusive whole set.
- [ ] Step 5 — Gate single/group image routing and the real [[TICK-041]]
  document-OCR route on terminal ambiguity work; pass only one uniquely
  resolved registration into their existing downstream policies.
- [ ] Step 6 — Run the reuse, simplification, efficiency and altitude pass;
  refresh current architecture, run focused/canonical validation, record all
  results, and open one PR to `dev`.
- [ ] [pre-review] Prove O/0, I/1, mixed/multiple positions, each supported UK
  format, invalid/foreign shapes, unique/no/multiple matches,
  retry/unavailable/failed outcomes, provenance, replay and grouping.
- [ ] [pre-review] Prove Case search, staff-confirmed and embedded-text values,
  ordinary Case lookup and `VrmRegistrationMatching` retain exact behavior.
- [ ] [pre-review] Prove the named image, OCR and Worker production callers,
  runtime artifact dependencies, schema constraints and least-privilege grants.
- [ ] [pre-review] Retain focused and canonical command output with every exit
  code; do not erase or conceal an earlier failure.
- [ ] [pre-review] Write the post-implementation report and stop at the open PR;
  do not merge, deploy or write post-merge proof.
- [ ] [post-merge] Verify the exact merged SHA and generated migration artifacts
  through kanmer-verify.

## Progress notes

2026-09-04: Scope refreshed to the operator-approved evidence-led O/0 and I/1
map, supported GB/Northern Ireland structures and UK-provider-only boundary.
The local labelled-corpus check failed because no case-attributed labels were
available, so it does not support additional pairs.

2026-09-04: [[TICK-041]] remains untaken in Backlog and still blocks this
ticket. Per the approved one-ticket plan, INTK-049 remains in Preparing and
must not be taken or partially implemented until that real document-OCR caller
lands.
