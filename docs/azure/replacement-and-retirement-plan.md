# Azure replacement and retirement plan

This plan replaces the old pre-release application deliberately. Pegasus `0.1.0-alpha.1` starts with fresh application data: predecessor cases, users, action-history records, and queue state are not migrated or preserved as `0.1.0-alpha.1` release requirements. The exact implementation and execution sequence is the repository-root [production replacement runbook](../../azure-production-replacement-plan.md). This document authorizes no deletion or cloud mutation.

## Executed disposition — 2026-08-02

The production replacement and predecessor retirement were executed under the
operator's separate exact-target and destructive-operation authority.

- Pegasus production is deployed in `rg-pegasus-prod`. The active Web revision
  is sourced from `94997dd036a48cde23fce0f960b159a2b4a921c0` and uses image
  digest
  `sha256:da11059f89e42d74d93ea7ed732d6b7ed8faca7ceb106ecb68875e5d5d8eda75`.
  Both health endpoints returned HTTP 200 after retirement; the production
  Worker is running, all nine functions are enabled, and the final telemetry
  readback found 83 recent successful Worker executions with zero failures and
  zero exceptions since activation.
- The final archive root is
  `C:\Users\Alex\Documents\Pegasus-Predecessor-Archive\collisionspike-dev-retirement-20260802T000640Z`.
  The archive-manifest SHA-256 is
  `D0A2D03A09D54142F3337B0A186131133DB9D8B19180048AB71544D88522808A`;
  every one of the 16 unique predecessor OCI digests was verified before ACR
  deletion.
- Retirement executed from the exact manifest with SHA-256
  `3CC3F1224239E9F30B687302EE813AB081950DC4B020AF84368BDD8AC2D40CBF`.
  Its eight resource batches completed in order. All 30 predecessor-only role
  assignment IDs are absent; all seven retained IDs are present.
- `rg-collisionspike-dev` was not group-deleted. It now contains exactly the
  adopted vaults `cespkboxkvv76a47` and `cespkenrichkvgi62sd`. The managed OCR
  child group is absent. `cespk-pg-kv-dev`, `cespkevakvufa3ci`, and
  `cespklockva7tzj2` are soft-deleted without purge, with scheduled purge date
  2026-08-09.
- All six approved Box/DVLA/DVSA Key Vault references report `Resolved` through
  metadata-only App Service status. No secret value was read. Graph Inbox and
  Sent Items processing is live-verified; configuration resolution remains
  distinct from unperformed end-to-end Box, DVLA, or DVSA outcome probes.
- The predecessor test application data, queues, Durable state, evidence blobs,
  and telemetry were intentionally not migrated. Recovery remains the next
  release gate, not a condition retroactively inferred from deployment.

The remaining sections preserve the pre-execution decision and procedure record.
Where they describe a candidate or blocker, the executed disposition above is
the current live-state evidence.

## Pre-execution decision classes

| Class | Assets |
|---|---|
| Replace | old API, orchestrator, parser, EVA, Box, and only separately accepted enrichment compute with `0.1.0-alpha.1` Web/Worker/adapters; alpha has no OCR replacement |
| Retire with the predecessor | PostgreSQL pre-release application data and queue/Durable work; no `0.1.0-alpha.1` import or preservation requirement |
| Decide/possibly retain | data-bearing evidence storage, capture Static Web App, Foundry account/project/deployments, predecessor shared ACR and ValuationBot images, Document Intelligence F0 |
| Retain | Visual Studio accounts and default workspace until a separate shared-ownership review says otherwise |
| Likely defer/retire | location/Maps/Vision and cloud evaluation functions when their `0.1.0-alpha.1` scope is explicitly excluded and evidence is preserved |

## Pre-execution non-negotiable blockers

Do not delete either predecessor resource group until all are true:

- Intake producers and triggers are stopped so the old application cannot keep processing after retirement. Its pre-release queues and Durable state may then be discarded; they do not need migration or business reconciliation.
- The evidence container's predecessor-only ownership and disposition are confirmed. Its contents were not inspected during inventory and are not assumed to be disposable.
- Predecessor ACR `valuationbot-mcp` ownership and export/retention is decided. The separate Pegasus production Basic ACR is retained with the replacement estate.
- Foundry deployments/project ownership is decided.
- Capture UI ownership is decided.
- Shared/default Log Analytics use is reviewed.
- Third-party credential rotation/revocation is scheduled.
- Fresh live traffic proves old callers are zero and new callers are healthy.
- Alex's final acceptance has been recorded, all other blockers on this list are satisfied, and the user approves the exact deletion targets. There is no additional post-acceptance rollback window.

## Replacement sequence

1. Freeze this inventory and export old IaC/deployment outputs needed to reproduce configuration names and role intent.
2. Create `0.1.0-alpha.1` in a separate resource group from reviewed Bicep. The replacement Web runs in an Azure Container Apps Consumption environment and pulls its immutable image digest from a new Pegasus production Basic ACR. Do not deploy over old resources or adopt the predecessor ACR.
3. Establish managed identities, least-privilege roles, health endpoints, telemetry, and SQL Entra access.
4. Create a fresh Azure SQL database for `0.1.0-alpha.1`. Do not import the predecessor PostgreSQL cases, users, action-history records, or application state.
5. Keep operational history in its existing authoritative Box, EVA, Outlook, spreadsheet, or network-drive locations. Do not import predecessor evidence blobs into `0.1.0-alpha.1` merely because they exist in Azure; determine their ownership and disposition before deleting their storage.
6. Replace one in-scope integration at a time behind one `0.1.0-alpha.1` adapter: EVA, Box, Graph/mail, embedded PDF extraction, then any approved enrichment. Scan-like OCR remains deferred to the exact target owned by the [capability inventory](../capabilities.md) and is not an alpha replacement gate.
7. Shadow or replay a redacted/genuine local corpus cohort through `0.1.0-alpha.1`. Do not upload the local corpus during this step.
8. Stop old mailbox/intake producers and triggers. The predecessor's pre-release queues, Durable state, and test application data may be discarded after the exact retirement targets are approved.
9. Cut operator traffic to the Container App ingress FQDN. Monitor cold start, health, errors, queue age, duplicate actions, database writes, Box/EVA outcomes, and operator acceptance. Alex's final acceptance is the cutover point; there is no post-acceptance predecessor rollback window.
10. Rotate or revoke external credentials after each integration cutover; do not perpetually copy old secrets.
11. Remove leaf compute first, then dedicated plans and package storage, then dedicated vaults/telemetry. Remove the managed OCR child through its parent lifecycle.
12. Delete old groups only after shared/data-bearing assets are separated and a fresh subscription-wide dependency/role query is clean.

## Resource disposition

| Old asset | `0.1.0-alpha.1` disposition | Retirement evidence |
|---|---|---|
| API Function + plan/storage/App Insights | Web/API on Azure Container Apps Consumption, backed by the separate Pegasus production Basic ACR | operator/API traffic on the digest-pinned `0.1.0-alpha.1` revision; cold and warm paths accepted; old requests zero; final acceptance recorded |
| Orchestrator Function + plan/storage/App Insights | Worker on Flex plus one shared Core | producers stopped; exact old resources approved for retirement |
| Parser Function + plan/storage/telemetry | embedded-text library inside one extraction adapter | genuine PDF benchmark meets accepted threshold |
| OCR ACA/Function/environment/storage/telemetry/predecessor ACR image | no `0.1.0-alpha.1` replacement; retain only while an old caller still requires it, otherwise retire after caller and ownership proof | old callers zero, exact retirement approved, predecessor shared ACR archived and separated from the Pegasus production ACR; any future scan-like OCR follows a separately accepted `Next` plan |
| PostgreSQL | fresh Azure SQL with no predecessor import | exact old-server retirement approval after callers are stopped |
| Evidence storage | decide after a contents/ownership check; no automatic `0.1.0-alpha.1` import | confirmed predecessor-only ownership, explicit disposition, and exact storage retirement approval |
| EVA Function | manual JSON/image export in the `0.1.0-alpha.1`; direct API adapter deferred | accepted manual export, old callers zero, and credential disposition |
| Box Function/webhook | one bounded Infrastructure Box custody adapter; no replacement webhook is required for alpha | controlled create/version succeeds beneath the approved root, cross-root operations fail closed, and old callers are zero |
| Enrichment Function | consolidate only if `0.1.0-alpha.1` decision says yes | decision, real caller, and provider evidence |
| Location Function + Maps/Vision | defer/retire unless operator value is established | explicit scope decision and no remaining caller |
| Evaluation Function | local ignored corpus harness | frozen cohort/holdout reporting and no cloud caller |
| Main Static Web App | Razor Pages Web on Azure Container Apps Consumption | operator acceptance and bookmarked Container App ingress FQDN |
| Capture Static Web App | separate retain/replace decision | ownership and roadmap decision |
| Foundry/project/deployments | retain, prune individually, or repurpose | deployment-by-deployment usage/cost decision |
| Default workspace | retain pending shared review | no non-Pegasus data source or explicit separate plan |

## Destructive-operation runbook

For each proposed deletion:

1. Refresh inventory, activity, diagnostic traffic, role assignments, locks, backups, and dependencies on the same day.
2. Resolve the exact resource IDs into a reviewed text manifest; no broad wildcard or computed group deletion.
3. Confirm the target is predecessor-only and record that its pre-release application data has no migration or preservation requirement. Do not apply that decision to shared assets.
4. Use Azure what-if or a non-destructive show/list command where available.
5. Ask the user to approve the exact resource IDs and recovery consequences.
6. Remove a small leaf batch, verify platform and business behavior, then continue.
7. Re-run Resource Graph and role-assignment queries for orphans.

`az group delete --name rg-collisionspike-dev` is explicitly prohibited as an opening or convenience step.
