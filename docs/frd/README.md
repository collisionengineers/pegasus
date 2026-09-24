# Functional requirements (FRD)

A functional requirements document says **how a capability must behave**:
inputs and outputs, states, rules, edge cases, what fails closed, and the
evidence that proves it works. An FRD implements outcomes owned by the
[PRD](../prd/README.md), cites the [design README](../design/README.md) for
visual and interaction rules, and never invents product scope or records a
technical decision (those belong to the PRD and the
[ADRs](../adr/README.md)). Current operator instructions and the owning PRD
establish product intent.

Each FRD is owned by one or more capability IDs; the join is the *Canonical
owner* column in [`capabilities.md`](../capabilities.md).

## Documents

| FRD | Owns | Capability families |
| --- | --- | --- |
| [FRD-01](frd-01-case-identity-and-lifecycle.md) | Case identity: Principal, reference and prefixes, Audit reference, Case types, the Inspection and Audit values of one Case, parties snapshot | CASE |
| [FRD-02](frd-02-intake-and-source-identity.md) | Intake starts, Unidentified, receipt identity, received file history | INT |
| [FRD-03](frd-03-triage.md) | Triage as a Case type: creation, workflow, completion evidence, due target, chaser and Case link | TRI |
| [FRD-04](frd-04-parties-accounts-and-access.md) | Parties, Principals, organisations, staff accounts, role access, action history | ACC |
| [FRD-05](frd-05-documents-extraction-and-custody.md) | Supported source boundary, staging, Box custody | DOC |
| [FRD-06](frd-06-vehicle-and-engineering-evidence.md) | Inspection address, image and VRM analysis, vehicle data and MOT enrichment, MOT mileage estimation | INT (image), EXT |
| [FRD-07](frd-07-eva-and-external-engineering-handoff.md) | EVA handoff routes and the external boundary | EXT |
| [FRD-08](frd-08-email-mailbox-and-background-processing.md) | Inbound mail identity, taxonomy, classification, Case association of mail | MAIL |
| [FRD-09](frd-09-provider-and-intermediary-routes.md) | Route versus provider identity, provider API contract | API |
| [FRD-10](frd-10-mcp-automation-and-actor-boundary.md) | Automation Actor boundary, direct-write model, tool inventory | MCP |
| [FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md) | Report outcomes, Audit parity, renderer, generation, correction and finality, estimate VAT | RPT |
| [FRD-12](frd-12-operator-experience.md) | Shell, navigation, display labels and the page contract | UI |
| [FRD-13](frd-13-case-lifecycle-and-workflow.md) | Case states, readiness, Hand to Engineer, actions, Create audit, Close case, Archive, chasing, Completed and Query | CASE |
| [FRD-14](frd-14-record-edit-leases.md) | Edit leases and record edit scopes, Take over, refusals | CASE, TRI, ACC |
| [FRD-15](frd-15-work-centre-queues-and-search.md) | Work Centre, Cases queues, pre-Case records, the Triage Case page, Search, Operations, freshness | UI |
| [FRD-16](frd-16-case-record-workspace.md) | The Case record page, its Inspection and Audit views, and the Engineer workbench | UI, ENG |
| [FRD-17](frd-17-administration-workspace.md) | Administration areas: accounts, contacts, workflow configuration, logs, reports | UI, MI |
| [FRD-18](frd-18-manual-upload.md) | Staff upload page, upload limits and confirmation | INT |
| [FRD-19](frd-19-image-led-intake-and-pairing.md) | Vehicle images that arrive without an instruction: grouping, pairing, merge | INT |
| [FRD-20](frd-20-mailbox-workspace.md) | The Inbox: browsing, preview, classify, link, move, dismiss | UI, MAIL |
| [FRD-21](frd-21-outbound-correspondence-and-sent-evidence.md) | Sending mail from Pegasus and proving a report was sent | MAIL |
| [FRD-22](frd-22-pre-case-gates-matching-and-association.md) | Pre-Case gates, matching conflicts, reversible association, staff linking to any Case | INT |
| [FRD-23](frd-23-case-draft-fields-provenance-and-global-checks.md) | Case draft fields, field provenance, global vehicle and value checks | INT |
| [FRD-24](frd-24-engineer-findings-damage-valuation-and-settlement.md) | Engineer findings, damage record, valuation sources, settlement, Market Research | CASE, ENG, EXT |
| [FRD-25](frd-25-repair-estimates-imports-and-glasss-sessions.md) | Repair specifications, Glass's sessions, estimate sources, PDF estimate import | ENG, EXT |
| [FRD-26](frd-26-mailbox-allowlist-activation-wake-up-and-recovery.md) | Mailbox allowlist, activation, wipe, wake-up and recovery | MAIL, INT |
| [FRD-27](frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md) | Send to AI, reviewed proposals, the AI Job List, connector settings | AI, MCP |

## Template

Every FRD uses this structure. Keep every rule that was normative in its
source. Write plainly: short sentences, say who does what, keep every number
and condition. Heading slugs stay stable because other documents link to
them; when a heading moves, repair every inbound link.

```md
# FRD-NN: <title>

> Owner capabilities: <IDs> · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version
Three to five bullets a new reader can act on.

## Purpose
One paragraph: which PRD outcomes this behaviour serves, and which FRDs own
the neighbouring rules.

## Behaviour
Inputs, outputs and the normative rules ("must", "never", "fails closed").

## States and transitions
The states and the allowed transitions or gates.

## Edge cases and fail-closed behaviour
Ambiguity, exhaustion, conflict, missing evidence.

## Acceptance evidence
What proves the behaviour: the exact evidence tier, tests, or operator sign-off.

## Links
Capability IDs, related FRDs, the technical ADRs that constrain implementation.
```

Adding an FRD: take the next unused number, add its row above, and point
its capability rows in `capabilities.md` at its anchors.
