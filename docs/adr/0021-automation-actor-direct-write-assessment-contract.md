---
id: ADR-0021
status: accepted
date: 2026-08-03
supersedes: []
superseded_by: []
related_capabilities: []
related_frd: [frd-10, frd-11]
tags: [mcp, automation, ai]
---

# ADR-0021: Automation Actor direct-write assessment contract and the Send to AI transport slice

## Status

Accepted. The DevelopmentOffline-only composition gate for
`Features:AutomationMcp` (decision 1's final consequence) is amended by
[ADR-0026](0026-enable-automation-mcp-by-explicit-deployment-configuration.md);
every other clause of this record stands.

## Context

ADR-0011 and ADR-0013 clause 10 fixed MCP as a management/development-controlled
ingress for one named vendor-neutral Automation Actor invoking only its
approved inventory of ordinary operational Core use cases. The merged MCP-01–04
ingress implemented that contract with nine tools, per-area scopes, permanent
attributable history, and a two-layer kill switch, all behind a composition
gate limited to DevelopmentOffline evidence runs. Its settled
identity/authentication/tool-inventory contract lived only in
current-architecture.md/operations.md prose after its temp plan was deleted; the backlog
queued promoting it to an ADR.

Two operator decisions on 2026-08-03 reshaped the remaining work: the Automation
Actor writes assessment detail directly — reviewed through the existing manual
case-assignment workflow rather than a staged-suggestion store — and automations
are designed and safeguarded outside Pegasus, which owes only a comprehensive
toolset and logging parity. This ADR records the durable technical contract for
that ingress; the functional behaviour it implies now lives in FRD-10 and
FRD-11.

## Decision

1. **The Automation Actor contract is confirmed as implemented.** One seeded
   OpenIddict client-credentials registration authenticates the single
   vendor-neutral Automation client; `ActorKind.Automation` holds exactly
   `PerformCasework`; the streamable-HTTP `/mcp` surface registers only when
   `Features:AutomationMcp` enables it in the DevelopmentOffline profile;
   every tool wraps an existing Core use case behind per-area scopes with
   permanent attributable history, `mcp:` idempotency keys, and the
   Administrator kill switch (~seconds to take effect).

2. **The tool inventory widens to fourteen tools and a fourth scope.**
   `pegasus_assessment_get` and `pegasus_assessment_update` under the new
   `automation.assessment` scope; the previously excluded candidates
   `pegasus_case_update_details`, `pegasus_eva_bundle_generate`, and
   `pegasus_eva_handoff_status` are reinstated under `automation.cases` —
   EVA generation pushes work *into* the human review point and records the
   CASE-21 proxy event exactly as a staff-triggered generation does.
   Structurally absent, on purpose: any finding-confirmation tool, any
   report-approval tool, and any tool that dispatches anything to a
   customer, principal, or external party. Estimate derivation (totals,
   worklists) stays absent until its formulas hold accepted EXT-09 authority.

3. **The Send to AI transport slice (AI-09) is implemented as gated work**
   on the MCP-01–04 implemented-but-gated precedent. `Features:SendToAi`
   composes it in the DevelopmentOffline profile only. The channel carries
   operator chat, never business data: the hand-off is a pointer (case
   reference, request identifier, short instruction), and content returns as
   the attributed Automation Actor writes described in FRD-11. The Core-owned
   work request tracks `Created → HandedOff → Completed` with `Failed`,
   `Cancelled`, and `Expired`; `HandedOff` maps to the connector's forwarded
   claim, never to "the provider read it"; completion is flipped by an
   operator-triggered reconcile reading the connector's reply record; and
   closing a request never applies or undoes case content. Cancellation takes
   a reason. Duplicate sends replay idempotently; the request identifier
   correlates the staff send, the channel delivery record, the provider reply,
   and — when the automation passes it — every ingress write of the session.
   An Administrator switch refuses new hand-offs immediately, beside the
   existing Automation client kill switch for the return path. The
   `.mcpb`/channel packaging remains an external client and may be revisited by
   the report-renderer integration work without reopening this contract.

## Consequences

- Pegasus-side safeguards are the gates, scopes, leases, versions, closed
  field vocabulary, structural absence of confirmation/dispatch tools, and
  logging parity; automation behaviour itself is designed and safeguarded
  outside this repository, and a workspace, skill, prompt, or model never
  becomes an application policy owner.
- The unconfirmed mark and the readiness rail make automation-recorded
  values visibly pending review on the assessment surface; the UI-15
  activation task owns the staff save paths and the full review
  presentation.
- Everything remains composition-gated off outside DevelopmentOffline
  evidence runs. No tier-5 external-client evidence, deployment, activation,
  or operator acceptance is claimed by this ADR; the queued tier-5 run
  covers the full fourteen-tool inventory in one recorded session, and
  production activation would additionally need a non-preview transport
  decision under the AI-09 contract.

## Functional behaviour

See [FRD-10](../frd/frd-10-mcp-automation-and-actor-boundary.md) and
[FRD-11](../frd/frd-11-reports-correspondence-and-reviewed-proposals.md).
