# Product requirements (PRD)

A product requirements document states **what the product must do and why** —
the business need, users, outcomes, scope, permanent boundaries, quality and
capacity targets, and the acceptance model. A PRD states no mechanics; *how* a
capability behaves is owned by the [FRDs](../frd/README.md), and technical
choices by the [ADRs](../adr/README.md). Business truth is owned upstream by
[`operator-notes.md`](../operator-notes.md); the schedule and capability-ID
registry by [`capabilities.md`](../capabilities.md).

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
