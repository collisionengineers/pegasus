---
id: ADR-0013
status: superseded
date: 2026-07-30
supersedes: []
superseded_by: [ADR-0029]
related_capabilities: []
related_frd: [frd-01, frd-02, frd-06, frd-07, frd-08, frd-12]
tags: [qdos, contract]
---
# ADR-0013: QDOS alpha implementation contract

## Context

The reviewed QDOS alpha plan exposed contradictions between retained proposals,
current product requirements, and the intended implementation boundary. This ADR
now retains only the durable technical decisions from that settlement; the
functional feature rules it originally carried have moved to their owning FRDs
(see [Functional behaviour](#functional-behaviour)). It does not accept the
delivery plan as a whole, prove implementation, authorize an Azure or other
external operation, or weaken any caller, evaluation, security, or
operator-acceptance gate.

## Decision

Clause numbers are stable and externally cited, so they are preserved; the
removed functional clauses (1–9 and 13) are mapped to their owning FRDs in
[Functional behaviour](#functional-behaviour).

10. **MCP actor boundary.** MCP is a management/development-controlled ingress
    for one named, vendor-neutral Automation Actor, owned by
    [ADR-0011](0011-restrict-mcp-to-automation-actor.md) and
    [ADR-0021](0021-automation-actor-direct-write-assessment-contract.md);
    ordinary staff receive no MCP access and no staff identity is impersonated.
11. **The domain action is `Send to AI`.** Provider-specific UI wording does not
    redefine the action. Claude is the current provider candidate for that later
    user-triggered action; Microsoft Foundry is the intended candidate platform
    for later AI query-response proposals. Exact client, model, transport,
    credential, evaluation, recovery, cost, and caller choices remain activation
    gates.
12. **Login protection is transient throttling.** The alpha uses generic
    authentication failure plus the accepted per-source and global request
    limits; it does not introduce persistent ASP.NET Identity account lockout.
14. **Azure targets remain unresolved until exact approval.** Subscription,
    resource group, region, Entra groups, SQL administration and migration
    identities, Box identity/root, alert recipients, budget scope, deployment
    commands, and destructive dispositions require fresh exact-target approval.
    No placeholder in the plan is executable authority.

## Consequences

- **Precedence.** [ADR-0011](0011-restrict-mcp-to-automation-actor.md) remains
  the Automation Actor boundary,
  [ADR-0021](0021-automation-actor-direct-write-assessment-contract.md) the
  Automation Actor direct-write and `Send to AI` transport contract, and
  [ADR-0012](0012-conservative-mot-mileage-estimation.md) the mileage-estimation
  policy; their other clauses are unchanged. Within its scope this ADR
  supersedes any retained wording that gives ordinary staff MCP access.
- **No external authority created.** This decision creates no Azure target,
  credential, deployment, migration, or destructive operation, and no
  ordinary-staff MCP route. The Automation Actor never impersonates staff.
- **Activation still gated.** Each deferred external or AI capability still
  requires its exact contract, identity and authorization boundary,
  representative evaluation where applicable, failure/recovery proof, real
  caller, exact-target approval, and operator acceptance.
- **Login protection.** Authentication throttling stays transient; no persistent
  Identity account-lockout state is added.

## Functional behaviour

The feature rules this contract originally carried now live in the FRDs; each
removed clause maps to its owner:

- [FRD-01 — Case identity and lifecycle](../frd/frd-01-case-identity-and-lifecycle.md):
  the mandatory instruction-completeness, image-completeness, and staff-review
  readiness gates with EVA-owned Engineer assignment (clause 3); manual
  cancellation handling with no automatic Case mutation (clause 4); and the
  three-digit-minimum-to-`9999` Case/PO sequence with fail-closed exhaustion
  (clause 7).
- [FRD-02 — Intake and source identity](../frd/frd-02-intake-and-source-identity.md):
  image-led material remaining a pre-Case Image intake (clause 1); the global
  vehicle identity/specification, vehicle-history/risk, and market-valuation
  progression gates (clause 2); and staff-initiated Box custody recovery with no
  automatic business retry (clause 5).
- [FRD-06 — Vehicle and engineering evidence](../frd/frd-06-vehicle-and-engineering-evidence.md):
  the deferred `AI-05` advisory image-readiness assessment that neither changes
  Case state nor creates an AI Proposal (clause 9).
- [FRD-07 — EVA and external engineering handoff](../frd/frd-07-eva-and-external-engineering-handoff.md):
  the focused EVA bundle exporting every eligible custody-confirmed Case-vehicle
  image with no alpha image-selection control (clause 8).
- [FRD-08 — Email, mailbox, and background processing](../frd/frd-08-email-mailbox-and-background-processing.md):
  the local email evaluation workbench remaining a separate evidence harness,
  not a QDOS-alpha product surface, caller, or acceptance checkpoint (clause 13).
- [FRD-12 — Operator experience](../frd/frd-12-operator-experience.md): the
  `New cases today` dashboard term and its Europe/London day-boundary counting
  rule (clause 6).
