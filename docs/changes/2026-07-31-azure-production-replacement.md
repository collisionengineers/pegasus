---
id: 2026-07-31-azure-production-replacement
type: infrastructure
status: in_progress
risk: critical
created: 2026-07-31
updated: 2026-08-01
issue: https://github.com/collisionengineers/pegasus/issues/311
pull_request: https://github.com/collisionengineers/pegasus/pull/312
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
- [ADR-0015](../adr/0015-host-web-on-container-apps-consumption.md) replaces
  the blocked App Service Web route with Container Apps Consumption, a separate
  Basic ACR, scale-to-zero, and locally built digest-pinned OCI deployment.
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
- Container Apps replacement: **implemented, locally infrastructure-verified
  on 2026-08-01, not deployed**. ADR-0015, the replacement/runbook authority,
  Basic ACR and conditional Container Apps Bicep, schema-2 OCI packaging,
  digest verification, managed-identity pull, and scale-to-zero validation are
  present. Bicep compilation, the local Azure deployment-plan validator, local
  OCI publication/ORAS descriptor readback, restore, zero-warning Release
  build, Core tests (179/179), and Architecture tests (71/71) passed.
- The non-corpus Integration project: **locally verified** on 2026-08-01. Its
  canonical single-process run completed in 24 minutes 45 seconds: 290 passed,
  zero failed, and the optional inactive
  `RunnerManifestInvokesCoreGateThroughActualWebHost` QDOS acceptance-manifest
  profile skipped as designed because no run-owned acceptance manifest was
  activated. The earlier ten-minute cutoff was shorter than the suite's normal
  duration and is superseded by this completed result.
- Local verification: **verified** on 2026-07-31. Restore, zero-warning Release
  build, 539 non-corpus tests, Bicep compilation, local deployment-plan
  validation, and the revision-bound QDOS CI-pressure profile (3/3, no skips)
  passed. This is not live or operator acceptance.
- Previous immutable ZIP artifacts: **locally verified**. `web.zip`, `worker.zip`,
  `efbundle.exe`, and their SHA-256 manifest were built and revalidated from a
  clean exact revision; production packaging must still be repeated after any
  later source change or review amendment. The ADR-0015 schema-2 manifest and
  `web-image.tar.gz` route are implemented but not yet packaged from a clean
  reviewed revision.
- Predecessor retirement tooling: **implemented and locally fail-closed
  verified, not executed**. Fresh evidence reads now check command/JSON success,
  census five vaults and subscription-wide resource-scoped roles, bind archive
  contents, exact resources, managed child-group ownership and role
  dispositions into a schema-2 retirement manifest, reject live inventory
  drift and skipped batches, and require separate stop, child-group, resource,
  and role deletion gates. A disposable manifest passed inspection and rejected
  a tampered archive; two independent reviews completed cleanly.
- Exact-target predecessor read/archive preflight: **performed and locally
  verified** on 2026-08-01 under explicit approval. Fresh inventories and
  non-secret metadata were captured outside the repository. All 16 unique ACR
  digests resolve from local OCI layouts; the 264-entry, 9,731,767,698-byte
  archive manifest has SHA-256
  `F88BC99D2CA742433E6A36860D3EDA65629A95AA70FC9FA2143DBB8768F9B47C`.
  No excluded data or secret values were retrieved and no Azure resource was
  mutated.
- Production readiness reads: **performed** for the service-specific gates. UK
  South advertises FC1/.NET 10 and SQL Standard S0. Under exact approval,
  `Microsoft.Sql` and `Microsoft.Quota` were registered on 2026-08-01. The B1
  increase request `b9df19cc-54b2-4876-9c4c-1eb9ba99076a` failed with
  `QuotaNotAvailableForResource`, and the later P0v4 preview exposed aggregate
  quota zero. ADR-0015 now selects Container Apps Consumption and a separate
  Basic ACR; current provider availability and a clean two-stage preview remain
  unverified. No Azure workload resource has yet been created.
- Retained credential metadata: **read and locally configured** under exact
  approval. One enabled versioned URI was found for each of `box-config-json`,
  `box-client-secret`, `dvla-api-key`, `dvsa-api-key`, `dvsa-client-id`, and
  `dvsa-client-secret`; no secret value was read. The Box route now uses the
  retained JWT configuration and client secret to obtain short-lived SDK
  authorization headers instead of requiring a static access token.
- Azure preview: **reached ARM preflight and stopped before any change**. The first
  attempt identified eleven missing integration inputs. After binding the
  approved non-secret/versioned metadata and correcting Box authentication, the
  second attempt stopped on four inputs. The retained enrichment configuration
  then supplied and validated the entitlement-specific DVSA token route, and
  Graph resolved the mailbox object ID. Two metadata-only Inbox reads returned
  `ErrorItemNotFound`, including after an approved temporary `FullAccess`
  assignment that was subsequently removed and verified absent. The latest
  predecessor application identity then resolved both folder IDs without a
  message or attachment request. ARM preview exposed and prompted correction of
  the reserved `AZURE_ENV_NAME` parameter collision. Local validation passes;
  ARM preflight then stopped on UK South B1 quota `0` with one VM required. The
  exact quota request failed and the P0v4 replacement preview then exposed a
  separate `Total Regional VMs` aggregate limit of 0. ADR-0015 supersedes that
  route with Container Apps Consumption and a Basic production ACR. The quota
  operations are retained only as historical evidence and must not be resumed.
  No workload resource was created, changed, or deleted.
- Azure provisioning: **not run**.
- Deployment, live verification, operator acceptance, predecessor retirement,
  and recovery exercise: **not performed**.
