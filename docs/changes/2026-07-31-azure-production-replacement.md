---
id: 2026-07-31-azure-production-replacement
type: infrastructure
status: in_progress
risk: critical
created: 2026-07-31
updated: 2026-07-31
issue: https://github.com/collisionengineers/pegasus/issues/311
pull_request: pending
baseline: 20d36666c694365e34e351b41262f6a85dafd6e7
target_release: 0.1.0-alpha.1
roadmap_horizon: Now
mode: development
supersedes: none
superseded_by: none
---

# Change: Deploy Pegasus production and retire CollisionSpike test estate

## Outcome

Implement and prove the direct local-to-production release route for Pegasus
`0.1.0-alpha.1`, deploy a fresh production estate in `rg-pegasus-prod`, activate
the approved live integrations behind existing Core ports, obtain explicit
operator acceptance, and retire only the exact approved predecessor resources.

The controlling execution sequence is the repository-root
[production replacement runbook](../../azure-production-replacement-plan.md).
Planned, implemented, caller-proved, locally verified, deployed, live-verified,
accepted, and retired remain separate evidence states.

## Authority and fixed boundaries

- [ADR-0014](../adr/0014-local-to-production-deployment.md) fixes the topology as
  isolated local development followed directly by production. No Azure
  development, test, integration, or staging environment may be introduced.
- `Pegasus.Core` retains business policy and the ports
  `IApprovedInboxSource`, `IApprovedSentSource`, `ICaseCustody`, and
  `IVehicleLookupAdapter`. Infrastructure implements production adapters; Web
  and Worker remain composition roots.
- Executable infrastructure remains under `infra/`; release automation remains
  under `scripts/`; Azure documentation remains under `docs/azure/`.
- Production uses fresh Azure SQL application data. Predecessor cases, users,
  queues, Durable state, and test application data are not migrated.
- Local `corpus/` material is immutable and is never uploaded.
- This record authorizes no merge. Cloud preview, provisioning, credential,
  identity, production-data, and destructive checkpoints retain the runbook's
  exact-target approval and stop conditions.

## Delivery checkpoints

1. Reconcile deployment authority and implement a fail-closed production-only
   Bicep and `azd` route.
2. Implement production Graph, Box, DVLA, and DVSA adapters behind the existing
   Core ports, with production startup validation and disabled triggers.
3. Add the release, validation, bootstrap, smoke, archive, and retirement
   scripts named by the runbook.
4. Prove the local build, test, Bicep, QDOS pressure, and immutable-artifact
   gates from the exact reviewed source revision.
5. Perform separately approved exact-target cloud preview, provisioning,
   integration activation, acceptance, retirement, and recovery checkpoints.

## Deferred-capability impact

- Scan-like OCR, Foundry, Maps, Vision, capture UI, direct EVA API, mailbox
  mutation, and all `Next` or later capabilities remain excluded.
- The preserved seams are the existing Core ports, stable case/document IDs,
  Azure SQL migration stream, Box root identity, immutable Graph message IDs,
  and release-manifest artifact hashes.
- Activation evidence is a real caller through the production composition root,
  followed by live integration evidence and explicit operator acceptance.
- The irreversible choices are the production-only topology, fresh production
  application database, immutable build-once artifacts, and non-reuse of
  predecessor application state.

## Evidence state

- Delivery identity: **implemented** on branch
  `feat/azure-production-replacement`, issue #311.
- Production route, adapters, startup validation, and release scripts:
  **implemented**, not deployed or live verified.
- Local verification: **verified** on 2026-07-31. Restore, zero-warning Release
  build, 539 non-corpus tests, Bicep compilation, local deployment-plan
  validation, and the revision-bound QDOS CI-pressure profile (3/3, no skips)
  passed. This is not live or operator acceptance.
- Immutable artifacts: **locally verified**. `web.zip`, `worker.zip`,
  `efbundle.exe`, and their SHA-256 manifest were built and revalidated from a
  clean exact revision; production packaging must still be repeated after any
  later source change or review amendment.
- Azure preview/provisioning: **not run**.
- Deployment, live verification, operator acceptance, predecessor retirement,
  and recovery exercise: **not performed**.
