# Plan — TICK-100: evidence-gated addendum rendering

## Approach

Keep RPT-05 unavailable and fail closed. The workspace addendum-report entry is unsupported catalogue evidence, not an approved Pegasus capability. [[SIMPLI-014]] and [[DOCS-001]] establish the single integrated renderer and immutable report-version/correction foundation; [[TICK-092]], [[TICK-093]], and [[TICK-094]] establish accepted source data; [[TICK-096]] establishes deterministic rendering; [[TICK-208]] preserves per-version final Sent evidence through correction. None of those prerequisites defines addendum wording, amendment identity, approval/recovery behavior, or a real caller.

This plan is therefore not ready for kanmer-execute. The next product trigger is receipt of a concrete representative Collision Engineers addendum artifact plus an identified real case workflow/caller. At that point create a linked activation ticket, research the concrete evidence, obtain explicit operator approval, and only then implement RPT-05 through the existing Core port and Infrastructure adapter. This is smaller and safer than migrating a dormant template or inventing a generic amendment abstraction.

## Governing docs

- docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md already requires immutable report identities, reasoned successor versions, retained earlier artifacts/facts/actors/times/sources, human review, and no silent overwrite. It does not yet authorize addendum-specific wording or behavior and is not modified before concrete approval.
- docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md requires future addendum rendering to use the monolith's Core-owned contract and Infrastructure adapter. No separate service, package, API, MCP host, workspace activation, store, or deployment unit is permitted.
- No new ADR or governing document is needed now. The future evidence-triggered activation ticket updates FRD-11 only after the operator approves the actual amendment contract.

## Steps

1. **Preserve the unsupported boundary.** Use [[TICK-206]] and the merged [[SIMPLI-014]] evidence to confirm addendum-report is neither migrated, registered, discoverable, nor callable. Reject arbitrary template identifiers and generic authoring fallbacks; no placeholder, dormant flag, assessment clone, or workspace preset counts as RPT-05.
2. **Accept the shared foundations without activating addenda.** Confirm [[DOCS-001]] retains immutable base report reference/version/hash/template/payload/provenance/custody and reasoned successor lineage; [[TICK-092]], [[TICK-093]], and [[TICK-094]] provide exact accepted source versions without retyping; [[TICK-096]] supplies deterministic Core-owned rendering; and [[TICK-208]] preserves each issued version's own final Sent evidence. These types/stores are reused later, with no speculative addendum wrapper or second report aggregate.
3. **Wait for and approve the activation evidence.** The trigger is both a concrete representative addendum artifact and a named real workflow/caller. Create a linked activation ticket and obtain explicit approval for: amendment identity and reason; predecessor/base-report relationship; editable versus inherited fields; exact wording/layout/labels/signatures; required accepted facts and source versions; approval/recovery/correction rules; artifact packaging; representative cases; and caller timing/authorization. Ambiguity leaves RPT-05 unavailable.
4. **Implement only the approved delta.** After step 3, update FRD-11, add the smallest typed Core amendment contract/readiness policy, and map it through the existing integrated adapter. The addendum binds an exact immutable predecessor and exact accepted source versions, stores only the accepted amendment delta rather than retyped case data, creates a new immutable artifact/version/hash/provenance/custody record, and never inherits approval or Sent status. Missing, ambiguous, stale, mismatched, unauthorized, or uncustodied inputs fail closed.
5. **Prove the activated behavior.** With approved fixtures, test inherited case/source data plus versioned amendment, predecessor immutability, reason/actor/time/source provenance, deterministic retry, independent approval/Sent state, correction of the amendment by another successor, invalid predecessor/cross-case/stale/ambiguous rejection, and real-Chromium visual/PDF parity. Confirm all earlier reports and Sent evidence remain unchanged and generation does not imply approval, sending, receipt, invoicing completion, or case closure.

## Verification

- Inspect merged proof for [[SIMPLI-014]], [[DOCS-001]], [[TICK-092]], [[TICK-093]], [[TICK-094]], [[TICK-096]], and [[TICK-208]] and name the exact existing contracts/stores reused.
- Before activation, search composed Core/Web/Infrastructure surfaces and resources to prove addendum rendering is absent rather than merely hidden.
- After approval and implementation, run focused Core readiness/version-lineage tests, persistence/integration tests, approved addendum renderer parity tests, and the canonical locked restore/build/test profile from docs/runbook.md.
- Retain generated evaluation artifacts under artifacts/ and never modify or fabricate supplied reference evidence.
- Verify no second business model, renderer, catalogue, persistence stream, caller, or runtime boundary was introduced.

## Risks and open questions

- A legacy template name can be mistaken for product approval. Mitigation: activation requires concrete operator-approved evidence and a real caller, not catalogue presence.
- Building generic addendum infrastructure early would violate the no-abstraction rule. Mitigation: reuse DOCS-001/TICK-208 version lineage and wait for the actual amendment delta before shaping a contract.
- Amendment approval/recovery may intersect unresolved workflow behavior. Mitigation: the activation ticket records the exact caller and boundaries before implementation and leaves unrelated lifecycle states to their owning capability.
- No current operator question is actionable. The next questions are evidence-triggered and must be asked against the actual supplied artifact and caller.
