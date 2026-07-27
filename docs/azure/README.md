# Azure documentation

This directory separates target architecture from dated live evidence. Nothing
here authorises a cloud query, provision, deployment, credential change, or
resource deletion.

| Question | Owner |
| --- | --- |
| What currently exists | [current-inventory.md](current-inventory.md), dated and read-only until an authorised refresh |
| Target resource/release design | [ADR-0009](../architecture/decisions/ADR-0009-direct-terminal-azure-deployment.md) and [`.azure/deployment-plan.md`](../../.azure/deployment-plan.md) |
| Predecessor retirement | [replacement-and-retirement-plan.md](replacement-and-retirement-plan.md) |
| Platform delivery work | [operations](../operations.md), [deployment plan](../../.azure/deployment-plan.md), and a current change record when activated |

The intended route is a direct authorised terminal using committed Bicep and
`azd`, with an explicit immutable migration before deploying the Web and Worker
packages. It is not runnable or production-ready: packaging, migration-bundle,
identity, Entra-resolution, build-provenance, and remote-build-removal gaps are
listed in the deployment plan. `azd up` is not the production release route, and
GitHub Actions/OIDC deployment is `Never`.
