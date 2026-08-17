# Functional requirements (FRD)

A functional requirements document specifies **how a capability must behave** —
inputs and outputs, states, rules, edge cases, fail-closed behaviour, and the
evidence that proves it works. An FRD implements outcomes owned by the
[PRD](../prd/README.md), cites [`design.md`](../design/README.md) for UI behaviour, and
never invents product scope or records a technical decision (those belong to the
PRD and the [ADRs](../adr/README.md)). Business truth is owned upstream by
[`operator-notes.md`](../operator-notes.md).

Each FRD is owned by one or more capability IDs; the join is the *Canonical
owner* column in [`capabilities.md`](../capabilities.md).

## Documents

| FRD | Domain | Capability families |
| --- | --- | --- |
| [FRD-01](frd-01-case-identity-and-lifecycle.md) | Case & reference identity, lifecycle, edit/recovery, chasing, action history | CASE |
| [FRD-02](frd-02-intake-and-source-identity.md) | Intake starts, upload links, occurrence identity, pre-case gates, matching/association | INT |
| [FRD-03](frd-03-triage.md) | Triage workflow and completion evidence | TRI |
| [FRD-04](frd-04-parties-accounts-and-access.md) | Parties/principals/orgs/accounts, staff role-access, action history | ACC |
| [FRD-05](frd-05-documents-extraction-and-custody.md) | Supported source boundary, staging, Box custody | DOC |
| [FRD-06](frd-06-vehicle-and-engineering-evidence.md) | Inspection address, image/VRM analysis, MOT enrichment, professional findings | INT (image), ENG |
| [FRD-07](frd-07-eva-and-external-engineering-handoff.md) | Focused EVA handoff, external boundary | EXT |
| [FRD-08](frd-08-email-mailbox-and-background-processing.md) | Mailbox taxonomy, eval boundary, outbound/Sent evidence, poll governance | MAIL |
| [FRD-09](frd-09-provider-and-intermediary-routes.md) | Route vs provider identity, routing/association, provider API contract | API |
| [FRD-10](frd-10-mcp-automation-and-actor-boundary.md) | Automation Actor boundary, direct-write model, tool inventory | MCP |
| [FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md) | Report correction/finality, targeted sending, reviewed AI proposals | RPT, AI |
| [FRD-12](frd-12-operator-experience.md) | Operator experience, dashboard freshness/reconciliation | UI |

## Template

Every FRD uses this structure. Preserve every rule that was normative in its
source; heading slugs are chosen to match the anchors that already link here.

```md
# FRD-NN: <domain>

> Owner capabilities: <IDs> · Source PRD: <link> · Design: docs/design/README.md#<...>

## Purpose
One paragraph: which PRD outcomes this behaviour serves.

## Behaviour
Inputs/outputs and the normative rules ("must", "never", "fails closed").

## States and transitions
Enumerated states and the allowed transitions / gates.

## Edge cases and fail-closed behaviour
Ambiguity, exhaustion, conflict, missing evidence.

## Acceptance evidence
What proves the behaviour: the exact evidence tier, tests, or operator sign-off.

## Links
Capability IDs, related FRDs, the technical ADR(s) that constrain implementation.
```
