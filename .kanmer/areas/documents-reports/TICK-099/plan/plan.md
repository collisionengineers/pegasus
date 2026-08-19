# Plan — TICK-099: RPT-04 diminution rendering

## Approach

Close this ticket at the unsupported/deferred decision tier through a Kanmer-only reconciliation. RPT-04 is allocated to Later / 1.1.0, but allocation is not activation: the operator approved only the `rendererref1` assessment/fee-note families, and neither the generic workspace `diminution-rebuttal` preset nor current governing text supplies an accepted diminution contract. This is preferable to migrating dormant code, adding a switch, or inventing semantics because none has a real approved caller or evidence. No repository diff, renderer worktree activation, cloud change, or capability-delivery claim is planned.

## Governing docs

- **FRD-11 — meets without modification.** Preserve its accepted-facts, deterministic identity, human approval, immutable artifact/hash, correction, and fail-closed requirements. Since FRD-11 does not define diminution percentage semantics, calculation, wording, layout, approval evidence, or correction linkage, execution records RPT-04 as unavailable rather than extending the FRD by inference.
- **ADR-0025 — meets without modification.** Preserve the future architecture: a real RPT-04 caller would use a Core-owned report contract with Infrastructure rendering inside Pegasus. Do not activate the workspace, expose its catalogue ID, add a package/API/MCP host/deployment unit, or introduce an abstraction for a caller that does not exist.
- **Capability registry — observed, not modified.** `docs/capabilities.md` keeps RPT-04 at Later / 1.1.0 and explicitly says allocation only, with wording and approval evidence outstanding.

## Existing code and evidence reused

- Reuse the established unsupported-template disposition from [[TICK-206]]: only `rendererref1` assessment/fee-note families activate; other catalogue entries remain unavailable.
- Reuse FRD-11 and ADR-0025 as the existing behavioural and architecture boundaries.
- Reuse [[TICK-092]], [[TICK-093]], and [[TICK-094]] only as future accepted-data dependencies; do not duplicate their case, repair-specification, or Engineer-owned policy.
- Do not reuse `workspaces/report-renderer`'s generic `diminution-rebuttal` wording as product authority. It is source evidence without an approved Pegasus caller or contract.

## Steps

1. Reconcile TICK-099's Outcome to state that RPT-04 is unsupported, unavailable, and explicitly deferred; completion is at the deferral tier only.
2. Record activation prerequisites and ownership: a future linked activation ticket must establish accepted original-case identity/version, Engineer-entered percentage semantics and precision, calculation and rounding, wording/layout, human approval, correction/version linkage, caller, failure behaviour, and representative evidence.
3. Record prohibited substitutes: no callable dormant template, generic workspace preset, assessment-template adaptation, free-form caller content, placeholder, feature-gated descriptor, or inferred professional/legal wording.
4. Write a zero-repository-diff post-implementation report that maps the decision to FRD-11/ADR-0025 and keeps RPT-04 separate from the current SIMPLI-014 assessment/fee-note integration.
5. Verify on merged `dev` that the capability registry remains Later / 1.1.0, the approved active evidence remains assessment/fee-note only, TICK-206 keeps diminution inactive, the linked prerequisites remain unactivated, and the ticket branch has no repository diff. Hand off for independent review and later proof only at the deferral tier.

## Verification

The post-implementation report will capture:

- `rg -n -C 2 "RPT-04|diminution" docs/capabilities.md docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md`;
- inspection of [[TICK-206]] research and `reference/rendererref1/` to show the approved active family excludes diminution;
- focused inspection of the workspace `diminution-rebuttal` preset solely to confirm it is non-authoritative, not to derive requirements;
- current status of [[TICK-092]], [[TICK-093]], and [[TICK-094]];
- `git status --short --branch`, `git diff --stat origin/dev...HEAD`, and `git diff --name-only origin/dev...HEAD`.

After independent review, proof must say only that the unsupported capability is closed and fail-closed. It must not claim diminution rendering, a template, RPT-04 acceptance, deployment, or representative parity.

## Risks / open questions

- **Risk — deferred is mistaken for delivered.** Mitigation: Outcome, PIR, review, and proof explicitly name the decision tier and unavailable state.
- **Risk — generic workspace content becomes accidental policy.** Mitigation: prohibit exposing or adapting `diminution-rebuttal`; require an approved representative artifact and behavioural contract before activation.
- **Risk — premature shared abstractions constrain later work.** Mitigation: add no API, descriptor, flag, optional parameter, or template family until a real second caller and accepted contract exist.
- **Open questions:** none now. Percentage semantics and template/content approval remain parked until a concrete activation request and evidence exist.
