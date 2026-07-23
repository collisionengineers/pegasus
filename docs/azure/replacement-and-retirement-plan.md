# Azure replacement and retirement plan

This plan replaces the old application deliberately. It authorizes no deletion or cloud mutation.

## Decision classes

| Class | Assets |
|---|---|
| Replace | old API, orchestrator, parser, EVA, Box and required enrichment/OCR compute with v2 Web/Worker/adapters |
| Migrate before replacement | PostgreSQL application data, evidence blobs, relevant queue/Durable work, integration configuration |
| Decide/possibly retain | capture Static Web App, Foundry account/project/deployments, shared ACR and ValuationBot images, Document Intelligence F0 |
| Retain | Visual Studio accounts and default workspace until a separate shared-ownership audit says otherwise |
| Likely defer/retire | location/Maps/Vision and cloud evaluation functions when their v2 scope is explicitly excluded and evidence is preserved |

## Non-negotiable blockers

Do not delete either CollisionSpike resource group until all are true:

- PostgreSQL schema/data is exported, imported, and reconciled by table row counts and business invariants.
- Evidence blobs are checksummed, copied or deliberately retained, and mapped to owning cases.
- Intake producers are quiesced; normal, poison, shadow, backfill, sent, and Outlook queues are drained or archived; Durable instances are terminal or deliberately abandoned with evidence.
- ACR `valuationbot-mcp` ownership and export/retention is decided.
- Foundry deployments/project ownership is decided.
- Capture UI ownership is decided.
- Shared/default Log Analytics use is audited.
- Third-party credential rotation/revocation is scheduled.
- Fresh live traffic proves old callers are zero and new callers are healthy.
- An explicit rollback window has expired and the user approves the exact deletion targets.

## Migration sequence

1. Freeze this inventory and export old IaC/deployment outputs needed to reproduce configuration names and role intent.
2. Create v2 in a separate resource group from reviewed Bicep. Do not deploy over old resources.
3. Establish managed identities, least-privilege roles, health endpoints, telemetry, and SQL Entra access.
4. Export PostgreSQL schema/data. Record counts, constraints, sequences, date ranges, and orphan relationships. Import into Azure SQL through an explicit mapping; do not assume PostgreSQL types map literally.
5. Enumerate evidence blobs with size and checksum. Copy to the chosen retained store and verify all hashes.
6. Replace one integration at a time behind one v2 adapter: EVA, Box, Graph/mail, embedded PDF extraction, scanned OCR, then any approved enrichment.
7. Shadow or replay a redacted/genuine local corpus cohort through v2. Do not upload the local corpus during this step.
8. Quiesce old mailbox/intake producers. Drain queues and Durable state, reconcile counts, and stop old triggers without deleting them.
9. Cut operator traffic to v2. Monitor health, errors, queue age, duplicate actions, database writes, Box/EVA outcomes, and operator acceptance through the rollback period.
10. Rotate or revoke external credentials after each integration cutover; do not perpetually copy old secrets.
11. Remove leaf compute first, then dedicated plans and package storage, then dedicated vaults/telemetry. Remove the managed OCR child through its parent lifecycle.
12. Delete old groups only after shared/data-bearing assets are separated and a fresh subscription-wide dependency/role query is clean.

## Resource disposition

| Old asset | v2 disposition | Retirement evidence |
|---|---|---|
| API Function + plan/storage/App Insights | Web/API on App Service | operator/API traffic on v2; old requests zero; rollback expired |
| Orchestrator Function + plan/storage/App Insights | Worker on Flex plus one shared Core | producers stopped; all queues/Durable instances reconciled |
| Parser Function + plan/storage/telemetry | embedded-text library inside one extraction adapter | genuine PDF benchmark meets accepted threshold |
| OCR ACA/Function/environment/storage/telemetry/ACR image | Document Intelligence adapter only for scanned/insufficient inputs | OCR cohort parity, cost measured, shared ACR separated |
| PostgreSQL | Azure SQL | schema and row-count reconciliation plus business invariants |
| Evidence storage | Box or explicitly chosen v2 retention path | complete checksum manifest and case linkage |
| EVA Function | one Infrastructure EVA adapter | accepted read/write parity and credential rotation |
| Box Function/webhook | one Infrastructure Box adapter | webhook and file/folder lifecycle parity |
| Enrichment Function | consolidate only if first-MVP decision says yes | decision, real caller, and provider evidence |
| Location Function + Maps/Vision | defer/retire unless operator value is established | explicit scope decision and no remaining caller |
| Evaluation Function | local ignored corpus harness | frozen cohort/holdout reporting and no cloud caller |
| Main Static Web App | Razor Pages Web | operator acceptance and bookmarked App Service URL |
| Capture Static Web App | separate retain/replace decision | ownership and roadmap decision |
| Foundry/project/deployments | retain, prune individually, or repurpose | deployment-by-deployment usage/cost decision |
| Default workspace | retain pending shared audit | no non-CollisionSpike data source or explicit separate plan |

## Destructive-operation runbook

For each proposed deletion:

1. Refresh inventory, activity, diagnostic traffic, role assignments, locks, backups, and dependencies on the same day.
2. Resolve the exact resource IDs into a reviewed text manifest; no broad wildcard or computed group deletion.
3. Capture export/checksum/reconciliation and rollback-location evidence.
4. Use Azure what-if or a non-destructive show/list command where available.
5. Ask the user to approve the exact resource IDs and recovery consequences.
6. Remove a small leaf batch, verify platform and business behavior, then continue.
7. Re-run Resource Graph and role-assignment queries for orphans.

`az group delete --name rg-collisionspike-dev` is explicitly prohibited as an opening or convenience step.
