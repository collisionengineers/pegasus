# Plan — TICK-215: Decide where report rendering executes in production

## Diff estimate

No repository diff. DOCS-002 already delivered the required durable decision as ADR-0028. This ticket closes the historical execution-location decision without duplicating SIMPLI-014 source integration or PLAT-007 container/IaC/deployment work.

## Approach

Treat ADR-0028 as the completed decision: rendering executes in process inside the existing Pegasus Web Container App, with Chromium/native/font dependencies in the existing Web image and the Flex Consumption Worker unchanged. The alternatives—Worker execution and a separate renderer app/job/service—remain rejected for the reasons already recorded in ADR-0028. Execution of this ticket is Kanmer-only reconciliation and evidence; it introduces no source, infrastructure, runtime, deployment, or governing-document change.

## Governing docs

- **Meets FRD-11:** preserves FRD-11 as the sole owner of report readiness, accepted inputs, deterministic output, immutable artifact/version identity and hash, correction, human approval, and fail-closed behaviour. This decision ticket does not implement or restate those rules.
- **Meets ADR-0025:** keeps CollisionRenderer integrated behind a Core-owned port with a real application caller and does not create a standalone repository, package, API, MCP host, service, job, or deployment unit. SIMPLI-014 owns that source integration.
- **Meets ADR-0028:** accepts the existing Web Container App as the production execution boundary, leaves Worker unchanged, and leaves image/runtime/IaC/capacity/recovery proof to PLAT-007. No new ADR is required because ADR-0028 is accepted and linked.

## Steps

1. Reconcile the ticket Outcome and acceptance statements to the accepted ADR-0028 decision, explicitly naming SIMPLI-014 as source-integration owner and PLAT-007 as runtime/deployment-proof owner.
2. Confirm the ticket retains refs to FRD-11, ADR-0025, and ADR-0028 and that its parked future-detachment question remains deferred behind measured evidence plus a new accepted ADR.
3. Record a post-implementation report stating that the decision was delivered by DOCS-002/PR #413, that this ticket made no repository or cloud change, and that no worktree/PR is needed for the Kanmer-only reconciliation.
4. Verify on merged `dev` that ADR-0028 is accepted and indexed, its decision names Web and rejects Worker/separate execution, and the three governing refs resolve; write proof at the evidence tier actually established.

## Verification

On merged `dev`, capture:

- `git rev-parse HEAD` and `git log -1 --format=%H -- docs/adr/0028-run-integrated-renderer-in-web-container-app.md`;
- `pwsh -NoProfile -File scripts/Test-DocumentationLinks.ps1`;
- direct inspection of ADR-0028 frontmatter, Decision, Consequences, and ADR-index row;
- Kanmer evidence that refs contain FRD-11, ADR-0025, and ADR-0028, open questions are resolved/parked, and no repository files, Azure resources, `main`, Worker, or separate deployment unit were changed by this ticket.

This proves the production execution-location decision only. It does not claim renderer integration, container readiness, deployed capacity, or operator acceptance.

## Risks / open questions

- Risk: this historical decision ticket expands into implementation already owned elsewhere. Mitigation: zero repository diff; SIMPLI-014 and PLAT-007 remain the named owners.
- Risk: ADR acceptance is mistaken for deployed capability. Mitigation: proof explicitly stops at the architecture-decision evidence tier.
- No operator-only question remains. Future detached execution stays parked until measured evidence shows Web cannot carry the workload; reconsideration then requires a new accepted ADR and exact-target cloud approval.
