# Azure documentation

This route separates target design, predecessor transition, dated live evidence,
and executable operations. Nothing here authorizes a cloud read, provision,
deployment, credential/account change, data mutation, or resource deletion.
Every external operation requires separate explicit approval for its exact
target, scope, cost, and data boundary.

| Question | Owner and evidence state |
| --- | --- |
| What existed when last inspected? | [Current inventory](current-inventory.md), immutable dated 2026-07-23 live-read evidence; stale until an authorized refresh |
| What is the intended target topology and release route? | [Deployment plan](../../.azure/deployment-plan.md) and [ADR-0007](../adr/0007-direct-terminal-azure-deployment.md); target design only |
| How is the predecessor replaced and retired? | [Replacement and retirement plan](replacement-and-retirement-plan.md); intent and destructive gates, not execution authority |
| How are source, validation, deployment, diagnosis, rollback, and recovery performed? | [Operations](../operations.md) plus the active change record; commands are run only in the separately approved evidence state |

The intended route is a direct authorized terminal using committed Bicep and
`azd`, with build-once/deploy-same-artifact provenance and an immutable database
migration before Web and Worker rollout. `azd up` is not the production
shortcut. Target names remain unresolved in [open decisions](../open-decisions.md);
predecessor names and resources are not reused by implication.
