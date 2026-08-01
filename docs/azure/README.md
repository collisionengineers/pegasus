# Azure documentation

This route separates target design, predecessor transition, dated live evidence,
and executable operations. Nothing here authorizes a cloud read, provision,
deployment, credential/account change, data mutation, or resource deletion.
Every external operation requires separate explicit approval for its exact
target, scope, cost, and data boundary.

| Question | Owner and evidence state |
| --- | --- |
| What existed when last inspected? | [Current inventory](current-inventory.md), immutable dated 2026-07-23 live-read evidence; stale until an authorized refresh |
| What is the intended target topology and release route? | [Deployment plan](../../.azure/deployment-plan.md), [ADR-0007](../adr/0007-direct-terminal-azure-deployment.md), and [ADR-0015](../adr/0015-host-web-on-container-apps-consumption.md); target design only |
| How is the predecessor replaced and retired? | [Replacement and retirement plan](replacement-and-retirement-plan.md); intent and destructive gates, not execution authority |
| What is the prepared implementation and predecessor-teardown sequence? | [Predecessor teardown and Pegasus deployment plan](predecessor-teardown-and-pegasus-deployment-plan.md); prepared historical plan only, with every external action separately gated |
| How are source, validation, deployment, diagnosis, rollback, and recovery performed? | [Operations](../operations.md) plus the active change record; commands are run only in the separately approved evidence state |

The intended route is a direct authorized terminal using committed Bicep and
`azd`, with build-once/deploy-same-artifact provenance, local OCI construction,
digest-pinned Web activation, and an immutable database migration before Web
and Worker rollout. `azd up` is not the production
shortcut. The subscription, tenant, production resource group, deterministic
resource-name scheme, and predecessor targets are fixed by the active runbook;
runtime-generated suffixes and FQDNs are consumed only from verified `azd`
outputs. Predecessor names and resources are not reused by implication.

Pegasus is validated locally and deployed directly to production. It has no
Azure development, test, integration, or staging environment; see
[ADR-0014](../adr/0014-local-to-production-deployment.md). Historical plans
that describe one are not current topology or authority.
