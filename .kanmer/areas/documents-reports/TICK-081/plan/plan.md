# Plan — TICK-081: accept EXT-08 end to end

## Approach

Treat EXT-08 as the combined capability-acceptance envelope for work owned elsewhere, not as another renderer/caller/deployment implementation branch. [[SIMPLI-014]] supplies the single integrated Core-port/Infrastructure adapter; [[TICK-092]], [[TICK-093]], and [[TICK-094]] supply accepted structured source data, canonical repair specification, and Engineer-owned decisions; [[TICK-096]] and [[TICK-097]] prove deterministic renderer behavior and the approved four-outcome assessment/fee-note family; [[TICK-206]] and [[TICK-216]] prove the closed template/wording/signature boundary; [[DOCS-001]] supplies the real complete-accepted-assessment caller and immutable retained result; [[PLAT-007]] supplies explicitly authorized deployed Azure proof. ADR-0028, delivered by [[DOCS-002]], fixes the existing Web Container App as the execution boundary.

TICK-081 is ready to close only when those merged proofs collectively demonstrate the local/composed and production tiers. It should create no duplicate Core contract, renderer adapter, caller, persistence model, IaC, Azure resource, or report behavior. If a gap exists, it returns to its owning dependency. No cloud write is part of planning or TICK-081 itself.

## Governing docs

- docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md owns readiness, accepted inputs, deterministic output, immutable identity/provenance/custody, fail-closed behavior, review, correction, and acceptance evidence. [[DOCS-001]] must leave it aligned with the implemented caller.
- docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md requires the integrated Core-port/Infrastructure-adapter boundary and prohibits a separate renderer system.
- ADR-0028, already accepted on dev, selects the existing Web Container App for in-process Chromium execution. [[PLAT-007]] implements and proves that choice without adding another runtime.
- docs/capabilities.md must reflect the operator's 2026-08-19 activation of EXT-08 and the exact activated dependency slice. That schedule reconciliation belongs in the implementation PR that makes the real capability true, preferably [[DOCS-001]], rather than a second TICK-081 implementation diff.
- No new ADR is required.

## Steps

1. **Accept the integrated renderer slice.** After [[SIMPLI-014]] merges, confirm one Core-owned render port, one Infrastructure adapter, production Chromium-compatible resources, Web composition, dependency-direction tests, and no remaining standalone workspace/API/MCP/package/service path. Confirm only the approved rendererref1 assessment and fee-note family can be selected.
2. **Accept capability data and behavior.** Confirm [[TICK-092]], [[TICK-093]], and [[TICK-094]] provide one accepted immutable source snapshot with exact versions/provenance, the canonical repair specification, selected Engineer authority, and Core-owned outcome/economics. Confirm [[TICK-096]], [[TICK-097]], [[TICK-206]], and [[TICK-216]] prove deterministic compute-once/fail-closed rendering for Total loss, Repairable, Cash in lieu, and Contract repair, exact approved wording/signatures, fee-note/repair-list content, and rejection of unsupported families.
3. **Accept the real caller and durable result.** After the preceding contracts are merged, [[DOCS-001]] must prove that completing and accepting all required assessment details invokes the renderer through the composed application exactly once per accepted input version; incomplete, ambiguous, stale, mismatched, unconfirmed, or uncustodied inputs fail closed. Retain immutable reference, version, artifact/payload/template/calculation hashes or versions, source provenance, custody, status, idempotent retry/recovery, and correction lineage. Generation remains distinct from approval, sending, receipt, invoicing completion, and case closure. Reconcile FRD-11 and docs/capabilities.md in that owning PR.
4. **Accept the existing-topology Azure deployment through [[PLAT-007]].** First complete PLAT-007 research/plan and all local/IaC/container validation. Immediately before any Azure write, obtain explicit operator approval naming the exact subscription/resource group/resources and operation. Then deploy the exact reviewed SHA through the existing Web Container App, with pinned Chromium/native/font dependencies, existing identity/storage/telemetry conventions, health, timeout/retry/restart/duplicate-delivery recovery, artifact custody, and no separate renderer deployment. Refresh docs/current-architecture.md and docs/operations.md in PLAT-007 after deployment.
5. **Close EXT-08 from combined proof.** On current merged dev, collate dependency merge SHAs/PRs, canonical restore/build/full and focused test evidence, representative four-outcome Chromium/PDF parity, real caller/idempotency/failure evidence, and PLAT-007 deployed telemetry/artifact/reference proof. Verify capabilities/FRD/current-state docs match reality. If every condition is proved, record TICK-081 as subsumed combined acceptance with no repository diff; otherwise return the precise gap to its owning ticket and do not claim EXT-08 or production delivery.

## Verification

- Inspect the merged implementation and proof documents for every structured blocker, including the exact SHA deployed by [[PLAT-007]].
- Run the canonical locked restore, Release build, focused Core/report/integration/architecture tests, and full test profile required by docs/runbook.md.
- Exercise complete and incomplete accepted-assessment paths through the composed application; prove deterministic/idempotent results and actionable staff-visible failure state.
- Inspect the four approved representative PDFs and retained report/fee-note identities, hashes, versions, provenance, custody, retry, and correction behavior.
- Verify deployed Web telemetry and retained artifact/reference on the explicitly approved Azure target, plus timeout/restart/duplicate/unavailable-renderer behavior.
- Verify no unsupported template, standalone renderer deployment, alternate policy owner, or false approval/send/receipt claim exists.

## Risks and open questions

- The board's document gate may become passable before the dependency and deployment evidence exists. That is not activation readiness; the checklist and structured blockers are authoritative for execution timing.
- Production proof cannot be obtained without an exact-target Azure write. PLAT-007 must stop for explicit approval immediately before that operation; read-only and local checks may proceed without it.
- The capability registry can drift if its schedule is updated before the real caller exists. Reconcile it in the caller-owning implementation PR and verify it again after deployment.
- No current product question remains. The only required operator interaction is the later exact-target cloud-write approval for PLAT-007.

## Operator correction — shared Audit/Inspection physical report — 2026-08-19

This supersedes any earlier plan statement that Audit rendering requires a separate representative template, layout, wording artifact, dormant family, or future activation ticket. The operator confirmed that Audit and Inspection processes differ internally, but the physical report output has no differences. Reuse the approved inspection/assessment report template and presentation through the existing Core render contract. Preserve Audit-specific workflow/data rules in their owning Core capabilities; do not create a second renderer template or presentation policy.
