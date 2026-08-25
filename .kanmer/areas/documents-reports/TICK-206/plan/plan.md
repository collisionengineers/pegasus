# Plan — TICK-206: Map renderer templates to capabilities and decide proposed retirements

## Approach

Treat TICK-206 as a resolved product-decision and acceptance slice subsumed by [[SIMPLI-014]], not as an independent repository implementation. The approved initial surface is one closed rendererref1 assessment-report operation covering `total_loss | repairable | cash_in_lieu | contract_repair`, plus its accepted fee-note artifact/family. Shared deterministic rendering mechanics map to RPT-01; the assessment outcomes, fee note, and itemised repair specification map to RPT-02; accepted-data activation remains EXT-08 with CASE-31/ENG-02 upstream. No caller supplies or discovers a workspace template ID.

The 12-entry workspace catalogue is evidence, not the Pegasus capability map. `fee-note` mechanics may be reused only behind the accepted fee-note contract; `repairable-contract-repair-report` and `total-loss-report` are superseded as internal evidence by the closed four-outcome assessment operation. Every other legacy entry remains unavailable and non-discoverable. SIMPLI-014 actively owns the overlapping FRD-11, Core contract/allow-list, Infrastructure mapping/assets, host removal, tests, and docs changes, so TICK-206 creates no separate worktree or diff.

## Governing docs

- **Modifies and meets — `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md`, through SIMPLI-014 only.** The owning PR must record one canonical initial activation boundary: rendererref1 assessment plus fee note only; four Core-owned outcomes; every legacy/unknown template selector unavailable and fail-closed; later Audit, diminution, addendum, valuation, evidence-pack, letter, Part 35, and generic report families require separate accepted callers/contracts. TICK-206 makes no separate FRD edit.
- **Meets — `docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md`.** Only caller-backed mechanisms enter existing application projects behind a Core port. Unsupported workspace presets, authoring catalogues, and standalone hosts do not become a second product surface. No new ADR is required.
- **Conforms — `docs/capabilities.md`.** RPT-01/RPT-02/EXT-08/CASE-31/ENG-02 remain the registry/schedule join keys; RPT-03–RPT-05 and adjacent valuation capabilities remain separately gated. The mapping behaviour stays in FRD-11, not duplicated as a second normative table. Any capabilities edit is limited to correcting a stale cross-reference/note and must not claim deployment or delivery.

## Steps

1. Confirm that SIMPLI-014's final scope retains the TICK-206 decision: one typed assessment operation with four Core-owned outcomes and the accepted fee-note output; no caller-supplied template ID; unsupported/unknown families fail before Infrastructure dispatch; only resources/mechanics needed by the approved family migrate.
2. Ensure SIMPLI-014's FRD-11 change records the closed activation allow-list once and leaves `docs/capabilities.md` as schedule/registry rather than duplicating behaviour. The capability join is RPT-01 + RPT-02 with EXT-08 activation and CASE-31/ENG-02 upstream; RPT-03–RPT-05 and valuation/letter/evidence families remain unavailable until separately accepted.
3. After SIMPLI-014's independently reviewed PR is merged, inspect its exact Core/Infrastructure/resource/host/test/docs diff. Verify the application exposes only assessment and fee-note operations, maps all four outcomes, rejects arbitrary/legacy template selectors before adapter rendering, has no template-discovery/authoring surface, and does not copy unused presets/assets into production without a concrete internal caller.
4. Run focused positive and negative acceptance checks on merged `dev`: each approved outcome and fee-note path reaches the same Core-owned use case/Infrastructure adapter; every legacy ID—`market-valuation-evidence`, `advert-evidence-pack`, `fee-note` as a raw selector, `expert-report`, `blank-letterhead`, `repairable-contract-repair-report`, `total-loss-report`, `addendum-report`, `diminution-rebuttal`, `roadworthy-criminal-report`, `part-35-response`, and `response-letter`—is absent from caller discovery and cannot be dispatched by ID. Unknown IDs fail identically.
5. Record a no-code post-implementation report and outcome linking the SIMPLI-014 PR, merge commit, FRD mapping, positive/negative tests, representative renders, and proof. State that TICK-206 was subsumed and created no repository branch, worktree, commit, PR, deployment, or cloud action; then complete its remaining Kanmer gates.

## Verification

The post-implementation report and eventual proof will cite SIMPLI-014's exact merged PR/commit and record read-only checks on merged `dev`:

- inspect FRD-11 for the single initial assessment/fee-note activation rule and explicit later-family boundary;
- inspect Core contracts/callers for a typed closed operation and four outcomes, with no arbitrary string template selector or public catalogue/list operation;
- inspect Infrastructure dispatch/resources for only caller-backed assessment/fee-note mappings and fail-closed unknown/unsupported handling;
- focused positive tests for all four assessment outcomes and fee-note generation through the composed Core → Infrastructure path;
- parameterized negative tests for all 12 legacy IDs plus an unknown ID, proving no UI/API/MCP/discovery/validation/render path accepts them;
- source/resource/build checks proving unused authoring presets, generic models, host catalogue endpoints, and unsupported template assets were not retained without a caller;
- representative Chromium/PDF evidence for the approved family, without claiming automatic accepted-assessment triggering, deployment, approval, issue, or send;
- confirmation that TICK-206 itself has no repository commit, PR, worktree, deployment, or cloud action.

Final acceptance depends on SIMPLI-014's merged implementation and evidence. TICK-206 owns the resolved mapping and acceptance slice only; SIMPLI-014 owns all repository changes.

## Risks / open questions

- **Active overlap:** every implementation file in TICK-206's survey is claimed by SIMPLI-014. Mitigation: no independent worktree or diff.
- **Legacy ID leakage:** reusing the old catalogue mechanically could leave unsupported templates discoverable or callable. Mitigation: test the actual application caller/dispatch boundary with the complete explicit legacy-ID set.
- **False mapping by name:** `fee-note`, `total-loss-report`, and `repairable-contract-repair-report` resemble approved outputs but do not equal the accepted contracts. Mitigation: expose typed application operations/outcomes, not legacy IDs; reuse mechanics only behind those types.
- **Dormant unsupported code:** copying unused presets “for later” creates unowned policy/test surface. Mitigation: migrate only concrete caller-backed assets/mechanics and rely on Git history/reference evidence for future work.
- **Capability registry duplication:** adding a second normative map to `docs/capabilities.md` risks drift. Mitigation: keep behaviour in FRD-11 and use capabilities only as stable IDs/schedule.
- **Operator questions:** none remain; the operator explicitly approved only rendererref1 assessment and fee-note activation.

## Retrospective acceptance clarification — 2026-08-25

The original verification wording asked for parameterized rejection tests for every legacy string identifier. The merged application contract contains no caller-supplied template-selector field, so manufacturing such an endpoint only to test it would reintroduce the forbidden surface. Structural evidence is stronger and simpler: a full search for all 12 former catalogue identifiers under current `origin/dev` application source/tests finds no unsupported identifier; `fee-note` appears only as the accepted typed output artifact (plus unrelated mailbox-classification prose). Unknown strings are likewise unrepresentable at the Core caller boundary. This satisfies the plan's non-discovery/fail-closed intent without adding a compatibility seam.
