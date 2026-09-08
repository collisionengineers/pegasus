# Product requirements (PRD)

A PRD owns product need, users, outcomes, scope, permanent exclusions, quality
targets and acceptance. [FRDs](../frd/README.md) own behavior and
[ADRs](../adr/README.md) own technical choices. Current operator instructions
can amend intent; record the resulting requirement here with its provenance.
[Capabilities](../capabilities.md) links stable identities to these owners;
Kanmer owns work allocation. The [index](../index.md) owns document conventions.

## Documents

| PRD | Scope |
| --- | --- |
| [Pegasus — product requirements](pegasus-product.md) | Purpose, users, outcomes, scope, permanent boundaries, quality/capacity targets, acceptance model |

## Template

```md
# <product / area>

## Purpose, users, and outcomes
Who it is for and the outcomes it must achieve.

## Scope and permanent boundaries
What is in, and what is explicitly out (permanent boundaries, not backlog).

## Quality and capacity targets
Non-functional targets: concurrency, latency, capacity, security posture.

## Acceptance model
The evidence states and what "accepted" means.

## Links
Capability IDs, and the FRDs that implement these outcomes.
```
