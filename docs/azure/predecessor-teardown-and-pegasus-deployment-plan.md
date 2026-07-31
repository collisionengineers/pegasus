# CollisionSpike test teardown and Pegasus Azure development deployment

Status: **Prepared plan only. No archive, credential, Azure, vendor, deployment,
or deletion operation is authorised by this document.**

Prepared: 2026-07-31, Europe/London.

## Summary

- Treat `rg-collisionspike-dev` and its managed OCR child group as an unused
  test estate. No migration, traffic cutover, operational rollback window, or
  predecessor availability gate applies.
- First implement and locally verify the repository-documented alpha
  integrations and direct-terminal release route. Then execute two separately
  approved cloud operations:
  1. archive the selected predecessor assets and delete all 53 resources plus
     both resource groups;
  2. provision and deploy Pegasus into `rg-pegasus-dev`.
- Use subscription `e6076573-23a5-46a8-acef-7e22d264e5db`, tenant
  `858cf5b3-aa0a-47a6-9b40-4851fd0afa94`, UK South, F1 Web, FC1 Worker, Basic
  Azure SQL, and the repository's Bicep/`azd` design.
- Preserve unrelated work, including the currently untracked
  `src/Pegasus.Web/Data/` and `temp_acceptance_store.cs` paths.

## 1. Predecessor teardown

### Archive and credential preparation

Create a fail-closed PowerShell teardown command and a static manifest
containing the exact resource IDs below. Its dry-run mode must resolve every
ID, reject additions or type mismatches, and perform no deletions.

Before deletion, under separately approved data-read and download authority:

- Download all four blobs from `cespkevidstdev01` to
  `C:\Users\Alex\Documents\Pegasus-Predecessor-Archive\collisionspike-dev`;
  retain names, sizes, ETags, SHA-256 hashes, and download verification.
- Inventory every ACR repository, tag, and digest and save each unique `ce-ocr`
  and `valuationbot-mcp` image as a verified OCI archive in the same archive.
- Record non-secret Foundry account, project, and deployment configuration;
  usage and cost metadata; Static Web App configuration; resource
  configuration; identities; roles; deployment history; and Key Vault secret
  names. Never retrieve secret values.
- Preserve the existing Graph, Box, DVLA, and DVSA external application
  identities, but revoke their old credentials. Issue new rotated values
  directly into the Pegasus Key Vault after provisioning; copy nothing from
  the old vaults.
- Record that PostgreSQL test data, Durable and queue state, package storage,
  telemetry, and all other unarchived content are intentionally discarded.

### Exact deletion manifest

Delete leaf resources individually in dependency order. Do not begin with
`az group delete` and do not use `azd down`.

| Batch | Exact resources removed |
| --- | --- |
| Function/Web compute | `cespk-api-dev`, `cespk-orch-dev`, `cespike-parser-dev-x7xt3d5ovhi7y`, `cespkenrich-fn-gi62sd`, `cespkeva-fn-ufa3ci`, `cespkeval-fn-6c6fxd`, `cespkbox-fn-v76a47`, `cespkloc-fn-a7tzj2`, and the `Microsoft.Web/sites` wrapper `cespkocr-fn-dev-glju3v` |
| Plans | `ASP-rgcollisionspikedev-007e`, `ASP-rgcollisionspikedev-bc54`, `cespike-parser-plan-dev`, `cespkenrich-plan-gi62sd`, `cespkeva-plan-ufa3ci`, `cespkeval-plan-6c6fxd`, `cespkbox-plan-v76a47`, `cespkloc-plan-a7tzj2` |
| OCR lifecycle | Managed child Container App `cespkocr-fn-dev-glju3v`, managed environment `cespkocr-env-dev`, and generated child group `cespkocr-env-dev_FunctionApps_247f14f1-8d57-491f-a325-a97e99634117`; remove the parent wrapper first and verify Azure's managed cleanup before acting on the child |
| Static sites | `cespk-spa-dev`, `cespk-capture-spa-dev` |
| Data and storage | PostgreSQL `cespk-pg-dev`; storage accounts `cespikestx7xt3d`, `cespkapistdev01`, `cespkboxstv76a47`, `cespkenrichstgi62sd`, `cespkevalst6c6fxd`, `cespkevastufa3ci`, `cespkevidstdev01`, `cespklocsta7tzj2`, `cespkocrstglju3v`, `cespkorchstdev01` |
| Vaults | `cespk-pg-kv-dev`, `cespkboxkvv76a47`, `cespkenrichkvgi62sd`, `cespkevakvufa3ci`, `cespklockva7tzj2` |
| AI, maps, and registry | `cespkdocintel-dev`, `cespkvision-dev`, `cespkmaps-dev`, Foundry account `digital-3339-resource`, project `digital-3339`, all 11 listed model deployments, ACR `cespkocracraeee76`, and identity `cespkocr-acrpull-id` |
| Observability | Application Insights `cespike-parser-ai-dev`, `cespkocr-ai-dev`, `cespk-api-dev`, `cespk-orch-dev`, `digital-3339-resource-appinsights`; workspaces `cespike-parser-law-dev`, `cespkocr-law-dev`, `digital-3339-resource-logs`; action group `Application Insights Smart Detection` |

Before the destructive run, obtain one fresh subscription-wide read to prove
there are no external role assignments, private endpoints, diagnostic
destinations, locks, or other dependants. Then obtain explicit deletion
approval naming the resolved IDs.

After the leaf inventory is empty, delete `rg-collisionspike-dev` and any
remaining empty managed child group. Do not purge soft-deleted Key Vaults;
`cespklockva7tzj2` is purge-protected. Verify both groups and all manifested IDs
are absent.

Explicitly retain:

- `DefaultResourceGroup-SUK` and its shared Log Analytics workspace;
- both `VisualStudioOnline-*` resource groups and accounts; and
- the local evidence and OCI archives.

## 2. Pegasus implementation and interfaces

### Alpha production adapters

Implement adapters behind the existing Core ports. Do not create another
policy owner or deployment unit.

- **Graph:** implement `IApprovedInboxSource` and `IApprovedSentSource` for
  `instructions@collisionengineers.co.uk`; use immutable Graph item identities
  and persisted delta/replay state; allow inbound-instruction and exact
  Sent-item-evidence reads only. Grant no send, move, delete, category, flag, or
  read-state mutation permission. Fence the application with Exchange
  Application RBAC and reject every non-approved mailbox or folder before
  constructing a Graph request.
- **Box:** implement `ICaseCustody` using the existing custody, outbox, and
  idempotency contracts. For development live tests, hard-fence writes to Box
  folder `392761581105`. Permit create or update of controlled non-corpus
  artifacts only; deny delete, move, copy, share, and targets outside the
  subtree. Automated tests use fakes only and never write to the live folder.
- **Vehicle enrichment:** implement direct, versioned DVLA VES and DVSA MOT
  adapters behind `IVehicleLookupAdapter`. Preserve provider, retrieval time,
  raw source identity, make, model, year, engine, fuel, MOT chronology, and
  mileage evidence separately. Never overwrite confirmed Case data; staff
  confirmation remains mandatory. Treat missing, stale, throttled, invalid,
  and unavailable results explicitly.
- Keep EVA as the documented deterministic manual JSON, images, and SHA-256
  manifest handoff. Add no EVA network call.

### Release and maintenance interfaces

Add two repository PowerShell entry points:

- a release command with explicit `Package`, `Preflight`, `Preview`,
  `Provision`, `Migrate`, `BootstrapAdministrator`, `DeployWeb`,
  `DeployWorker`, `Verify`, and `Rollback` stages; and
- a predecessor command with explicit `DryRun`, `Archive`, `Stop`,
  `DeleteBatch`, and `Verify` stages.

Add a production-only, terminal-invoked Web maintenance mode for the first
Administrator:

- run it as the signed-in SQL administrator
  `digital@collisionengineers.co.uk`;
- read the new username and password interactively, never from arguments or
  logs;
- fail unless migrations are current and no application user exists; and
- fail closed if invoked after initialization.

Add pinned `dotnet-ef` tooling and build:

- immutable Web and Worker zip packages;
- an idempotent EF migration bundle; and
- a SHA-256 provenance manifest containing source commit, scoped worktree
  state, SDK and tool versions, package hashes, and migration identity.

## 3. Infrastructure and deployment

### Bicep corrections

Replace the fail-closed `offline-replay` activation only after the
implementation and tests exist.

Provision `rg-pegasus-dev` with:

- Linux F1 App Service plan and .NET 10 Web App;
- FC1 Flex Consumption plan and .NET 10 isolated Worker;
- separate Web and Worker user-assigned identities;
- Azure SQL logical server and Basic `pegasus` database with Entra-only
  authentication;
- separate transport/deployment and custody/protection StorageV2 LRS accounts;
- Standard purge-protected Key Vault; and
- Log Analytics, Application Insights, a Pegasus development action group
  sending to `digital@collisionengineers.co.uk`, required metric and log
  alerts, and a GBP 100 monthly resource-group budget with 50%, 80%, and 100%
  alerts.

Correct the current single-storage implementation:

- transport/deployment storage owns Function host and package state plus
  ID-only work and poison queues;
- custody/protection storage owns transient intake and Web cryptographic rings;
- Worker receives only required host, queue, table, and intake-container roles;
- Web receives required custody and cryptographic-ring roles;
- Worker is explicitly denied access to the Web authentication ring; and
- shared-key access remains disabled.

Use `ASPNETCORE_ENVIRONMENT=Production` and `Runtime__Profile=Production` even
though the resource group is development. All Worker triggers begin disabled.

### Direct-terminal release sequence

For each cloud stage, obtain exact approval for target, writes, cost,
credentials, and data.

1. Resolve the signed-in user's Entra object ID and perform current quota,
   F1, FC1, .NET 10, SQL Basic, storage, role-assignment, provider credential,
   and pricing checks.
2. Create local `azd` environment `pegasus-dev`; set the exact subscription,
   `uksouth`, SQL administrator identity, and approved deployment mode.
3. Run `azd provision --preview`; fail on unexpected deletes, replacements,
   names, regions, tiers, public exposure, or role grants.
4. Run `azd provision` only after the reviewed preview receives exact approval.
5. Build and hash the migration bundle once, then apply it as the temporary SQL
   administrator.
6. Create `pegasus_web_runtime` and `pegasus_worker_runtime` contained users
   from the managed-identity client-ID SIDs, attach the committed custom roles,
   and verify the exhaustive grants and delete denials.
7. Run the interactive first-Administrator maintenance command.
8. Rotate or reissue the existing Graph, Box, DVLA, and DVSA credentials
   directly into the new Key Vault. Configure only Key Vault references and
   non-secret identifiers in app settings.
9. Deploy the hashed packages separately:

   ```powershell
   azd deploy web --from-package <web-package>
   azd deploy worker --from-package <worker-package>
   ```

10. Verify Web liveness and readiness, sign-in, telemetry correlation,
    database access, and disabled trigger state.
11. Under separate live-service approvals, exercise Graph read-only behavior,
    Box folder `392761581105`, and controlled DVLA and DVSA tests.
12. Enable only the documented alpha Worker functions after their dependencies
    pass. Record each setting change and verify queue, poison, timer, and
    idempotency behavior.
13. Never use `azd up`; provisioning, migration, Web deployment, and Worker
    deployment remain distinct.

Rollback redeploys the previous hashed Web and Worker packages. It never
down-migrates the database or deletes data-bearing Pegasus resources.

## Test and acceptance plan

- Run the canonical repository gates:

  ```powershell
  dotnet restore ./Pegasus.slnx
  dotnet build ./Pegasus.slnx --configuration Release --no-restore
  dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"
  ```

- Run Bicep build and lint plus release-script dry runs.
- Add Graph tests for immutable IDs, delta restart, duplicate and replay,
  scope rejection, throttling, malformed MIME or attachments, and proof that
  no mutation endpoint is called.
- Add Box tests for root fencing, idempotent folder or file creation,
  versioning, retry-visible failure, cross-root rejection, and prohibition of
  delete, move, copy, and share.
- Add DVLA and DVSA tests for valid, partial, conflicting, stale, throttled,
  unavailable, malformed, and retry outcomes without silent Case mutation.
- Add infrastructure tests proving two storage boundaries, exact RBAC, Worker
  denial from authentication material, disabled-by-default triggers,
  F1, FC1, and Basic tiers, budget and alerts, secret-free outputs, and no OCR,
  Foundry, Maps, Vision, or capture resources.
- Prove the migration bundle against disposable LocalDB first and then Azure
  SQL. Verify migration history, runtime aliases, grant census, initial
  Administrator bootstrap, and Web and Worker least privilege.
- Live development acceptance uses controlled non-corpus inputs only:
  approved read-only `instructions@collisionengineers.co.uk` Graph operations;
  manually initiated Box deployment tests under folder `392761581105`;
  approved DVLA and DVSA test identifiers; Web health and readiness;
  Administrator sign-in; Worker queue and timer execution; telemetry; alerts;
  and rollback package redeployment.
- Keep predecessor teardown verification and Pegasus deployment evidence
  separate. A deleted test estate does not prove Pegasus deployment;
  deployment does not prove live integration behavior or operator acceptance.

## Assumptions and deferred impact

- The repository documentation remains authoritative: development uses
  F1, FC1, and Basic tiers; direct-terminal Bicep and `azd`; separate
  migration; and build-once, deploy-same-artifact provenance.
- The predecessor contains no operational workload. PostgreSQL, queues,
  Durable state, telemetry, and unarchived test data may be irreversibly
  discarded.
- The 2026-07-31 live refresh was limited to the two authorized predecessor
  groups: 52 resources in `rg-collisionspike-dev` and one managed child
  Container App, with no locks. A subscription-wide dependency read remains
  mandatory immediately before deletion.
- No predecessor application data is imported into Pegasus.
- OCR, Foundry and AI, Maps and Vision, guided capture, direct EVA API,
  broader mailbox management, and production deployment remain deferred at
  their documented capability horizons. Existing Core identities and ports
  remain preserved; no dormant resource, credential, flag, queue, endpoint, or
  replacement implementation is added.
- Every external read or write, credential rotation, archive download, Azure
  provision or deployment, Box, Graph, DVLA, or DVSA call, and deletion receives
  separate exact-target approval at execution time.
