# Plan — TICK-098: evidence-gated Audit rendering

## Approach

Keep RPT-03 unavailable and fail closed until both independent prerequisites exist: [[TICK-093]] has delivered the shared versioned repair-specification aggregate, and a concrete representative Audit artifact has been supplied and explicitly approved through the evidence-triggered activation path recorded by [[TICK-207]]. [[TICK-205]] has already settled the domain rule—an Audit consumes exactly one accepted conservative specification version and one accepted maximised specification version and Core derives their monetary uplift—but that decision is necessary, not sufficient, evidence for rendering.

This plan deliberately does not reuse rendererref1 assessment presentation, add a dormant Audit descriptor, invent wording, or create a parallel Audit-only specification model. It is not ready for kanmer-execute now. When the external evidence trigger occurs, the new linked activation ticket required by TICK-207 researches and approves the artifact first; only then may TICK-098's behavior be implemented through the existing Core port and integrated Infrastructure adapter.

## Governing docs

- docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md owns future Audit input/output, readiness, deterministic calculation, artifact identity/provenance, review, and correction behavior. It is not modified until an actual Audit artifact is explicitly approved.
- docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md requires any future Audit rendering to remain behind the Core-owned contract in the monolith's Infrastructure adapter. No separate service, package, API, MCP host, workspace activation, or deployment unit is permitted.
- docs/frd/frd-06-vehicle-and-engineering-evidence.md, through [[TICK-093]], owns the shared accepted repair-specification aggregate, route/source provenance, role/version identity, and superseding corrections.
- No new governing document is justified now. Missing representative evidence is not an architectural decision.

## Steps

1. **Hold the closed boundary.** Verify [[TICK-207]] completes only its zero-diff deferral proof: no approved Audit template/model/registration exists; RPT-03 is absent and unavailable; assessment templates, generic expert templates, caller-authored blocks, placeholders, and dormant flags are forbidden substitutes.
2. **Deliver the shared data prerequisite through [[TICK-093]].** Reuse one versioned repair-specification aggregate and line vocabulary. It must support exact role, identity, version, ordered lines, accepted total, source route/artifact/version, actor/time, calculation basis, supersession, and correction lineage. For Audit, exactly one current accepted conservative version and one current accepted maximised version must coexist without overwriting or aliasing; no Audit-only duplicate aggregate is allowed.
3. **Wait for the evidence trigger and approve it before implementation.** The trigger is receipt of a concrete representative Collision Engineers Audit report/template. Create the linked activation ticket required by TICK-207, record the artifact as immutable evidence, and obtain explicit operator approval for its wording, layout, comparison labels, field mapping, conditional rules, signatures, fee treatment, representative cases, and artifact packaging. If any of those remain ambiguous, RPT-03 stays unavailable.
4. **Implement the accepted RPT-03 behavior only after steps 1–3.** Extend FRD-11 with the approved behavior, then reuse the existing Core report/readiness port and integrated Infrastructure renderer. Core selects the exact accepted pair, verifies same Audit case/reference and compatible calculation/rate/VAT bases, computes monetary uplift once as maximised accepted total minus conservative accepted total, and binds both identities/versions and the calculation-rule version into immutable provenance. Missing, duplicate, ambiguous, unconfirmed, cross-case, incompatible, stale, or uncustodied input fails closed. Percentage uplift remains unavailable until its denominator and rounding rule are separately accepted.
5. **Prove the activated capability.** Use only approved representative fixtures to test both specifications and their ordering/content, positive/zero uplift, corrections to either role, incompatible-basis rejection, deterministic retry, immutable artifact reference/version/hash/provenance/custody, and visual/PDF parity through real Chromium. Confirm generation does not imply approval, sending, external receipt, or case closure, and that earlier specification/report versions are never overwritten.

## Verification

- Read [[TICK-205]] proof to confirm the accepted role-labelled pair and Core-owned monetary uplift boundary.
- Read merged [[TICK-093]] proof and run its focused Core/persistence tests for coexistence, role uniqueness, accepted versions, provenance, compatibility, and correction lineage.
- Before activation, search the active catalogue/resources and composed surfaces to prove Audit remains absent, not merely hidden.
- After evidence approval and implementation, run focused Core report tests, persistence/integration tests, approved Audit renderer parity tests, and the canonical locked restore/build/test profile from docs/runbook.md.
- Retain generated evaluation artifacts under artifacts/; never alter or infer missing content in supplied reference evidence.

## Risks and open questions

- The mechanical Preparing gate becoming passable does not make RPT-03 implementation-ready. The evidence prerequisite remains deliberately external and unresolved until a concrete representative artifact is supplied.
- TICK-207's completion proves deferral, not template acceptance. A new linked activation ticket is created only when the actual artifact exists.
- Percentage uplift is explicitly outside current accepted behavior. Only monetary uplift between compatible accepted totals is authorized.
- No current operator question is actionable. The next operator decision is triggered by receipt of the representative artifact and concerns that concrete artifact rather than hypothetical wording.
