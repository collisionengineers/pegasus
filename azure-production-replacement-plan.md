# Pegasus Production Replacement and CollisionSpike Retirement

## Summary

Replace the unused CollisionSpike Azure estate with Pegasus `0.1.0-alpha.1` using the accepted **local → production** route:

1. Complete and review the missing production adapters and release tooling locally.
2. Build Web, Worker, and migration artifacts once and record SHA-256 provenance.
3. Provision only `rg-pegasus-prod` in UK South using Bicep and `azd`.
4. Migrate Azure SQL, create least-privilege runtime users, and bootstrap application Administrator `alex`.
5. Deploy and live-verify Web, Worker, Graph, Box, DVLA, DVSA, telemetry, alerts, and the real alpha workflow.
6. Record Alex’s final acceptance.
7. Immediately archive predecessor configuration and ACR images, then delete every predecessor resource except the adopted Box and enrichment Key Vaults.
8. Complete the full isolated recovery exercise before any second production release.

This plan authorizes no cloud action, merge, credential access, deployment, or deletion. Each external stage requires exact-target approval when executed.

## Locked End State

- Environments: isolated local development and production only; no Azure dev/test/staging resources.
- Production: subscription `e6076573-23a5-46a8-acef-7e22d264e5db`, tenant `858cf5b3-aa0a-47a6-9b40-4851fd0afa94`, resource group `rg-pegasus-prod`, region `uksouth`.
- Compute/data: Linux B1 Web, FC1 .NET 10 isolated Worker, S0 Azure SQL, two Standard LRS storage accounts, distinct Web/Worker identities, new Pegasus Key Vault, Log Analytics and Application Insights.
- Integrations required before acceptance:
  - Graph through the Worker managed identity, scoped by Exchange Application RBAC to `instructions@collisionengineers.co.uk`.
  - Box production custody rooted at folder `392761581105`.
  - Official DVLA Vehicle Enquiry Service v1.2.
  - Official DVSA MOT History API v1.
  - EVA remains the accepted manual JSON/image handoff.
- Secrets:
  - Retain and adopt `cespkboxkvv76a47` and `cespkenrichkvgi62sd`.
  - Grant secret-level access only to the identities and exact secrets that call them.
  - Delete `cespk-pg-kv-dev`, `cespkevakvufa3ci`, and `cespklockva7tzj2` after the secret census confirms no production caller.
- Predecessor data: preserve non-secret configuration and unique ACR OCI images only. Intentionally discard evidence blobs, PostgreSQL data, queues, Durable state, telemetry, and other test data.
- Acceptance and retirement: Alex is the technical, operator, and final business approver. There is no predecessor rollback window after acceptance.
- Recovery: the first release may launch without completed RPO/RTO proof, but the second production release is blocked until the full isolated recovery exercise passes.
- Monitoring: 31-day retention, adaptive sampling, 0.1 GB/day Application Insights cap, and a £75 monthly budget notifying `digital@collisionengineers.co.uk` at actual 50%, 80%, and 100%, plus forecast 100%. Alerts never stop resources.

## Exact Execution Sequence

### 1. Stabilize the repository and delivery identity

1. Do not touch the current dirty worktree until its ongoing UI and ADR-0014 edits are committed or otherwise resolved by their owner. Never stash, reset, clean, move, or absorb unrelated files.
2. Refresh from the resulting exact head:

```powershell
git status --short
git branch --show-current
git rev-parse HEAD
git diff --check
```

3. Create the scoped branch `feat/azure-production-replacement`, GitHub issue titled `Deploy Pegasus production and retire CollisionSpike test estate`, and change record `docs/changes/2026-07-31-azure-production-replacement.md`.
4. Reconcile current authority around ADR-0014. Update `.azure/deployment-plan.md`, `docs/azure/replacement-and-retirement-plan.md`, `docs/azure/predecessor-teardown-and-pegasus-deployment-plan.md`, architecture, operations, capabilities, and open decisions. Do not edit the generated Azure Monitor skill package.
5. Do not add a new top-level `azure/` directory. Executable IaC remains under `infra/`; Azure documentation remains under `docs/azure/`.

### 2. Implement the runnable production route

1. Change Bicep to accept production only and remain fail-closed unless `deploymentMode=approved-live-deployment` is explicitly supplied.
2. Replace the current single storage account with:
   - transport/deployment storage: Functions host state, package container, `intake-work`, `intake-work-poison`, `external-work`, and `external-work-poison`;
   - custody/protection storage: transient intake, Web authentication ring, and Box-link ring.
3. Apply exact RBAC:
   - Worker: required transport Blob/Table roles and queue roles; custody access only to transient intake and Box-link material.
   - Web: custody access to transient intake, authentication ring, and Box-link material; queue-send rights only where its caller requires them.
   - Worker receives no Web authentication-ring access.
   - Neither runtime identity receives DDL, deployment rights, storage keys, or broad SQL roles.
4. Configure production only:
   - `ASPNETCORE_ENVIRONMENT=Production`;
   - `Runtime__Profile=Production`;
   - local authentication disabled for Application Insights;
   - `remoteBuild: false`;
   - no `SCM_DO_BUILD_DURING_DEPLOYMENT=true`;
   - every Worker trigger disabled by default.
5. Add the production adapters behind existing Core ports:
   - `IApprovedInboxSource` and `IApprovedSentSource`: Microsoft Graph immutable IDs, delta/replay, Inbox ingestion and Sent evidence for `instructions@`; no send/move/delete/category/flag/read-state operations.
   - `ICaseCustody`: Box root `392761581105`, ancestry checking before every SDK call, folder/file version creation and controlled update only; no delete/move/copy/share.
   - `IVehicleLookupAdapter`: DVLA VES and DVSA MOT implementations with source/version/time provenance and typed current/stale/partial/not-found/invalid/denied/throttled/unavailable outcomes.
6. Pin reviewed dependencies in lock files: `Microsoft.Graph 6.2.0`, `Box.Sdk.Gen 1.12.0`, and compatible .NET 10 resilience/authentication packages. Preserve one Core policy owner.
7. Add production startup validation. Production must fail before listening if required endpoints, mailbox/folder identities, Box root, secret references, identity IDs, or transport/custody resources are absent or if a Development adapter is selected.
8. Add release scripts within `scripts/`, not a new project:
   - `Build-ReleaseArtifacts.ps1`;
   - `Test-AzureDeploymentPlan.ps1`;
   - `Invoke-AzureDatabaseBootstrap.ps1`;
   - `Invoke-ProductionAdministratorBootstrap.ps1`;
   - `Invoke-ProductionSmoke.ps1`;
   - `Invoke-PredecessorArchive.ps1`;
   - `Invoke-PredecessorRetirement.ps1`.
9. `Build-ReleaseArtifacts.ps1` must produce ignored artifacts:
   - `web.zip` for `linux-x64`;
   - `worker.zip` for `linux-x64`;
   - self-contained `efbundle.exe` for `win-x64`;
   - a manifest recording source commit, clean/scoped status, SDK/tool versions, migration identity, file sizes, and SHA-256 hashes.
10. The bootstrap command must prompt interactively for username and password, create only `alex` with the `Administrator` role, force first password change, fail if any application user already exists, and never accept a password through arguments, environment files, logs, or tracked configuration.

### 3. Local proof and immutable packaging

Run from the repository root:

```powershell
dotnet --info
az version
azd version
az bicep version
dotnet tool restore
dotnet restore ./Pegasus.slnx
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"
az bicep build --file ./infra/main.bicep
pwsh ./scripts/Test-AzureDeploymentPlan.ps1 -Mode Local
pwsh ./scripts/Invoke-QdosAlphaAcceptance.ps1 -Profile CiPressure -SourceRevision (git rev-parse HEAD)
pwsh ./scripts/Build-ReleaseArtifacts.ps1 -Version 0.1.0-alpha.1 -SourceRevision (git rev-parse HEAD)
pwsh ./scripts/Test-AzureDeploymentPlan.ps1 -Mode Artifact -ManifestPath ./artifacts/releases/0.1.0-alpha.1/release-manifest.json
```

Stop if any test skips required evidence, the worktree scope differs from the manifest, a package hash changes, Bicep exposes a secret, or a production setting permits a Development adapter.

Open the PR and obtain review. Production packaging must be repeated from the final reviewed head. Merging remains blocked until the prompt contains `MERGE AUTH GRANTED`.

### 4. Exact-target read and archive preflight

After separate approval for read-only inspection of the named subscription, predecessor groups, three candidate vaults, ACR, quota, pricing, roles, and deployment history:

```powershell
$PegasusSubscription = 'e6076573-23a5-46a8-acef-7e22d264e5db'
$PegasusTenant = '858cf5b3-aa0a-47a6-9b40-4851fd0afa94'
$PegasusLocation = 'uksouth'
$PegasusProdRg = 'rg-pegasus-prod'
$PegasusAzdEnv = 'pegasus-prod'
$PegasusOldRg = 'rg-collisionspike-dev'
$PegasusOldChildRg = 'cespkocr-env-dev_FunctionApps_247f14f1-8d57-491f-a325-a97e99634117'
$PegasusArchive = 'C:\Users\Alex\Documents\Pegasus-Predecessor-Archive\collisionspike-dev'

az login --tenant $PegasusTenant
az account set --subscription $PegasusSubscription
az account show --query '{subscription:id,tenant:tenantId,user:user.name,state:state}' --output json
azd auth login --tenant-id $PegasusTenant
azd auth login --check-status
az ad signed-in-user show --query '{id:id,upn:userPrincipalName}' --output json
```

Capture fresh inventory without secret values:

```powershell
az resource list --resource-group $PegasusOldRg --output json
az resource list --resource-group $PegasusOldChildRg --output json
az lock list --resource-group $PegasusOldRg --output json
az lock list --resource-group $PegasusOldChildRg --output json
az role assignment list --resource-group $PegasusOldRg --all --output json
az monitor activity-log list --resource-group $PegasusOldRg --offset 30d --output json
az keyvault secret list --vault-name cespkboxkvv76a47 --query '[].{name:name,enabled:attributes.enabled,updated:attributes.updated}' --output json
az keyvault secret list --vault-name cespkenrichkvgi62sd --query '[].{name:name,enabled:attributes.enabled,updated:attributes.updated}' --output json
az keyvault secret list --vault-name cespk-pg-kv-dev --query '[].{name:name,enabled:attributes.enabled,updated:attributes.updated}' --output json
```

Run the repository archive script to save resource configuration, tags, identities, roles, deployments, secret names, and costs under `$PegasusArchive`. It must not call `az keyvault secret show` or retrieve any secret value.

Install the required OCI client and archive every unique `ce-ocr` and `valuationbot-mcp` digest:

```powershell
winget install --exact --id ORASProject.ORAS --version 1.3.0
az acr repository list --name cespkocracraeee76 --output json
az acr manifest list-metadata --registry cespkocracraeee76 --name ce-ocr --output json
az acr manifest list-metadata --registry cespkocracraeee76 --name valuationbot-mcp --output json
az acr login --name cespkocracraeee76
pwsh ./scripts/Invoke-PredecessorArchive.ps1 -ArchiveRoot $PegasusArchive -IncludeOciImages -ExcludeData
```

Hash the archive and stop unless every unique digest is present. Do not download the four evidence blobs or export PostgreSQL, queues, Durable state, or telemetry.

### 5. Production preview and provisioning

Obtain separate approval for the exact preview. Configure `azd`:

```powershell
$PegasusUser = az ad signed-in-user show --query '{id:id,upn:userPrincipalName}' --output json | ConvertFrom-Json

azd env new $PegasusAzdEnv --subscription $PegasusSubscription --location $PegasusLocation
azd env set -e $PegasusAzdEnv AZURE_SUBSCRIPTION_ID $PegasusSubscription
azd env set -e $PegasusAzdEnv AZURE_LOCATION $PegasusLocation
azd env set -e $PegasusAzdEnv AZURE_ENV_NAME prod
azd env set -e $PegasusAzdEnv AZURE_PRINCIPAL_ID $PegasusUser.id
azd env set -e $PegasusAzdEnv AZURE_PRINCIPAL_NAME $PegasusUser.upn
azd env set -e $PegasusAzdEnv PEGASUS_DEPLOYMENT_MODE approved-live-deployment
azd env set -e $PegasusAzdEnv BOX_ROOT_FOLDER_ID 392761581105
```

Run and retain the preview:

```powershell
azd provision -e $PegasusAzdEnv --preview --no-prompt |
    Tee-Object ./artifacts/releases/0.1.0-alpha.1/azd-provision-preview.txt
```

Fail on any resource outside `rg-pegasus-prod`, any dev/staging resource, any delete/replace operation, incorrect region/SKU, one-storage topology, shared-key access, local authentication, broad role, remote build, enabled Worker trigger, OCR/Foundry/Maps/Vision/capture resource, or secret-bearing output.

After exact approval of that preview:

```powershell
azd provision -e $PegasusAzdEnv --no-prompt
azd env refresh $PegasusAzdEnv
azd env get-values -e $PegasusAzdEnv
az resource list --resource-group $PegasusProdRg --output table
```

Verify B1, FC1, S0, two storage accounts, two identities, new Key Vault, telemetry resources, action group, alert rules, and budget before continuing.

### 6. Bind external identities and retained vaults

1. Construct secret resource IDs from the approved secret-name census without retrieving values.
2. Grant:
   - Web and Worker secret-level `Key Vault Secrets User` access only to the Box secrets they call;
   - Worker secret-level access only to the DVLA/DVSA secrets.
3. Use commands of this exact form for every approved secret:

```powershell
az role assignment create `
  --assignee-object-id $WorkerPrincipalId `
  --assignee-principal-type ServicePrincipal `
  --role 'Key Vault Secrets User' `
  --scope "$EnrichmentVaultId/secrets/$ApprovedSecretName"
```

4. Adopt the two surviving vaults without moving them:

```powershell
az tag update --resource-id $BoxVaultId --operation Merge `
  --tags app=pegasus lifecycle=retained-production-dependency environment=prod

az tag update --resource-id $EnrichmentVaultId --operation Merge `
  --tags app=pegasus lifecycle=retained-production-dependency environment=prod
```

5. Remove obsolete predecessor runtime role assignments from those vaults after recording their exact assignment IDs.
6. Configure Graph through Exchange Application RBAC, without an additive organisation-wide Entra `Mail.Read` grant:

```powershell
Connect-ExchangeOnline
New-ServicePrincipal `
  -AppId $WorkerClientId `
  -ObjectId $WorkerPrincipalId `
  -DisplayName 'Pegasus Production Worker'

New-ManagementScope `
  -Name 'Pegasus Production Instructions Mailbox' `
  -RecipientRestrictionFilter "PrimarySmtpAddress -eq 'instructions@collisionengineers.co.uk'"

New-ManagementRoleAssignment `
  -Name 'Pegasus Production Instructions Mail Read' `
  -App $WorkerPrincipalId `
  -Role 'Application Mail.Read' `
  -CustomResourceScope 'Pegasus Production Instructions Mailbox'

Test-ServicePrincipalAuthorization `
  -Identity $WorkerPrincipalId `
  -Resource 'instructions@collisionengineers.co.uk'

Test-ServicePrincipalAuthorization `
  -Identity $WorkerPrincipalId `
  -Resource 'digital@collisionengineers.co.uk'

Disconnect-ExchangeOnline -Confirm:$false
```

The first test must be in scope and the negative control out of scope. Exchange documents that Entra and Exchange grants are additive, so an unscoped Entra grant is a stop condition. [Microsoft Exchange Application RBAC](https://learn.microsoft.com/en-us/exchange/permissions-exo/application-rbac)

### 7. Database, application bootstrap, and immutable deployment

Apply the exact hashed migration bundle using the deploying user as the temporary SQL Entra administrator:

```powershell
pwsh ./scripts/Test-AzureDeploymentPlan.ps1 `
  -Mode PreMigration `
  -Environment $PegasusAzdEnv `
  -ManifestPath ./artifacts/releases/0.1.0-alpha.1/release-manifest.json

& ./artifacts/releases/0.1.0-alpha.1/efbundle.exe `
  --connection $PegasusMigratorConnectionString

pwsh ./scripts/Invoke-AzureDatabaseBootstrap.ps1 `
  -Environment $PegasusAzdEnv `
  -ManifestPath ./artifacts/releases/0.1.0-alpha.1/release-manifest.json
```

The bootstrap script creates `pegasus_web_runtime` and `pegasus_worker_runtime` from the provisioned managed-identity client-ID SIDs, assigns only the migration-defined custom roles, verifies the exhaustive allow matrix and `DELETE` denials, and proves neither runtime identity has DDL.

Create the first application Administrator interactively:

```powershell
pwsh ./scripts/Invoke-ProductionAdministratorBootstrap.ps1 `
  -Environment $PegasusAzdEnv `
  -UserName alex `
  -PackagePath ./artifacts/releases/0.1.0-alpha.1/web.zip
```

Deploy the already hashed packages. Never use `azd up`, plain `azd deploy`, or release-time `azd package`:

```powershell
azd deploy web -e $PegasusAzdEnv `
  --from-package ./artifacts/releases/0.1.0-alpha.1/web.zip `
  --no-prompt

Invoke-WebRequest "https://$WebAppName.azurewebsites.net/health/live"
Invoke-WebRequest "https://$WebAppName.azurewebsites.net/health/ready"

azd deploy worker -e $PegasusAzdEnv `
  --from-package ./artifacts/releases/0.1.0-alpha.1/worker.zip `
  --no-prompt
```

Current `azd` supports separate `--preview` provisioning and `deploy --from-package`; these are the required build-once/deploy-same-artifact boundaries. [Azure Developer CLI command reference](https://learn.microsoft.com/en-us/azure/developer/azure-developer-cli/reference)

### 8. Live integration activation and acceptance

With all Worker triggers still disabled, obtain separate approvals and run controlled probes:

1. Sign in as `alex`, change the temporary password, and prove Administrator-only routes plus Engineer/User denials.
2. Graph: prove managed-identity reads from the `instructions@` Inbox and Sent Items, immutable IDs, delta continuation, and denial of an out-of-scope mailbox. No mailbox mutation.
3. Box: create and version one controlled non-corpus technical artifact beneath `392761581105`; prove cross-root denial. Retain the smoke artifact because delete is prohibited.
4. DVLA: use the official production VES endpoint and the retained API key. VES v1.2 uses `POST /vehicle-enquiry/v1/vehicles` and `x-api-key`. [DVLA VES v1.2 specification](https://developer-portal.driver-vehicle-licensing.api.gov.uk/apis/vehicle-enquiry-service/v1.2.0-vehicle-enquiry-service.html)
5. DVSA: use registration lookup with cached OAuth client-credentials tokens and `X-API-Key`; prove current, not-found, invalid, denied, throttled, and unavailable mappings without inducing provider throttling. [DVSA authentication](https://documentation.history.mot.api.gov.uk/mot-history-api/authentication/), [DVSA API specification](https://documentation.history.mot.api.gov.uk/mot-history-api/api-specification/)
6. Enable functions one boundary at a time:

```powershell
az functionapp config appsettings set --resource-group $PegasusProdRg --name $WorkerAppName `
  --settings 'AzureWebJobs.PendingWorkDispatchFunction.Disabled=false'

az functionapp config appsettings set --resource-group $PegasusProdRg --name $WorkerAppName `
  --settings 'AzureWebJobs.IntakeWorkFunction.Disabled=false' `
             'AzureWebJobs.IntakePoisonFunction.Disabled=false' `
             'AzureWebJobs.StagedArtifactReconciliationFunction.Disabled=false'

az functionapp config appsettings set --resource-group $PegasusProdRg --name $WorkerAppName `
  --settings 'AzureWebJobs.ExternalWorkFunction.Disabled=false' `
             'AzureWebJobs.ExternalPoisonFunction.Disabled=false'

az functionapp config appsettings set --resource-group $PegasusProdRg --name $WorkerAppName `
  --settings 'AzureWebJobs.InboxPollFunction.Disabled=false' `
             'AzureWebJobs.SentEvidencePollFunction.Disabled=false'

az functionapp config appsettings set --resource-group $PegasusProdRg --name $WorkerAppName `
  --settings 'AzureWebJobs.DueWorkSweepFunction.Disabled=false'

az functionapp restart --resource-group $PegasusProdRg --name $WorkerAppName
```

7. After each batch, verify queue age, poison handling, idempotency, database writes, dependency results, and telemetry correlation before enabling the next.
8. Run the 30-minute/eight-session capacity profile and the real QDOS alpha journey through Graph/manual intake, extraction, case allocation, Box custody, DVLA/DVSA, completeness/review, EVA export, and exact Sent evidence.
9. Safely fire and acknowledge every alert route. Read back the £75 budget and its 50/80/100 actual and 100 forecast notifications.
10. Alex records explicit acceptance. State separately:
    - deployed;
    - live-verified integrations and callers;
    - accepted;
    - recovery objectives not yet proved.

### 9. Immediate predecessor retirement

After acceptance, obtain fresh approval naming the exact reviewed deletion-manifest hash and every resource ID. Do not use `azd down` or delete `rg-collisionspike-dev`.

1. Stop predecessor compute and triggers:

```powershell
pwsh ./scripts/Invoke-PredecessorRetirement.ps1 `
  -Stage Stop `
  -ManifestPath "$PegasusArchive\retirement-manifest.json"
```

2. Re-run dependency, role, lock, activity, and traffic checks. Confirm zero callers.
3. Confirm the retained set contains only:
   - `cespkboxkvv76a47`;
   - `cespkenrichkvgi62sd`.
4. Delete one exact ID per command, in these batches:
   - Function/Web/Static Web Apps and the OCR parent wrapper;
   - Foundry deployments/project, remaining AI/Maps/Vision/Document Intelligence;
   - dedicated App Service/Flex plans and predecessor identities;
   - PostgreSQL and all ten predecessor storage accounts, explicitly accepting irreversible data loss;
   - ACR after OCI digest verification;
   - dedicated Application Insights, Log Analytics, and old action group;
   - `cespk-pg-kv-dev`, `cespkevakvufa3ci`, and `cespklockva7tzj2`, without purge;
   - remaining leaf resources and orphaned role assignments.

```powershell
pwsh ./scripts/Invoke-PredecessorRetirement.ps1 `
  -Stage DeleteBatch `
  -ManifestPath "$PegasusArchive\retirement-manifest.json" `
  -Batch <reviewed-batch-name>
```

The script must resolve and print each ID, verify it belongs to one of the two exact predecessor groups, reject both retained vault IDs, require the approved manifest hash, and call:

```powershell
az resource delete --ids $ExactReviewedResourceId
```

5. Delete the OCR managed child group only after its parent wrapper has been removed and the child inventory is empty:

```powershell
$RemainingChild = az resource list --resource-group $PegasusOldChildRg --query 'length(@)' --output tsv
if ($RemainingChild -ne '0') { throw "Managed OCR child group is not empty." }

az group delete --name $PegasusOldChildRg --subscription $PegasusSubscription --yes
```

6. Leave `rg-collisionspike-dev` present with only the two adopted vaults. Verify:

```powershell
az resource list --resource-group $PegasusOldRg --output table
az resource list --resource-group $PegasusOldChildRg --output table
az graph query -q @"
Resources
| where resourceGroup =~ 'rg-collisionspike-dev'
   or resourceGroup =~ 'cespkocr-env-dev_FunctionApps_247f14f1-8d57-491f-a325-a97e99634117'
| project id, name, type, resourceGroup
"@ --first 1000
```

7. Remove any predecessor-only Graph application credentials and Exchange assignments only after exact ownership proof. Do not revoke the Box or DVLA/DVSA credentials now consumed by Pegasus.
8. Record deleted IDs, deletion times, soft-deleted vault state, surviving vault IDs, archive hashes, and verification output. Do not claim the resource group itself was deleted.

### 10. Mandatory post-launch recovery gate

Before any second production deployment:

1. Restore Azure SQL to a new isolated recovery database in `rg-pegasus-prod`.
2. Deploy the same retained Web/Worker hashes to temporary recovery resources.
3. Recreate exact runtime identities/roles, run migrations, and verify health plus the real smoke journey without calling production external effects.
4. Record achieved recovery point and elapsed restoration time.
5. Require RPO ≤15 minutes and RTO ≤4 hours.
6. Delete only the exact temporary recovery resources after evidence review and separate deletion approval.
7. If this proof is absent or fails, the second release stops before migration or deployment.

## Interfaces and Tests

- No new business API or Core policy owner.
- Existing ports remain authoritative: `IApprovedInboxSource`, `IApprovedSentSource`, `ICaseCustody`, and `IVehicleLookupAdapter`.
- Add typed production options for Graph mailbox/folders, Box root and secret references, DVLA VES, DVSA MOT History, managed identities, transport/custody storage, telemetry, and release metadata.
- Test:
  - Graph paging, delta reset, immutable IDs, throttling, malformed MIME/attachments, exact mailbox/folder allowlist, and zero mutation endpoints.
  - Box ancestry, idempotent folder/file versioning, cross-root denial, retry-visible failure, and delete/move/copy/share prohibition.
  - DVLA/DVSA success, partial, not-found, invalid, stale, denied, token expiry, throttling, unavailable, malformed, and confirmed-value preservation.
  - Two-storage topology, secret-level vault access, Worker authentication-ring denial, Entra-only SQL, exhaustive runtime SQL grants/denials, disabled triggers, B1/FC1/S0, telemetry cap, budget/alerts, and absence of dev/OCR/Foundry/Maps/Vision/capture resources.
  - Bootstrap once-only behavior, forced password change, health/readiness, package-hash enforcement, and refusal of `azd up`, plain deploy, remote build, changed artifacts, or mismatched targets.

## Explicit Stop Conditions

Stop immediately on an unreviewed dirty-file overlap, stale target inventory, missing exact approval, unexpected preview change, unverified secret ownership, broad Graph/Key Vault/Storage/SQL access, missing live provider entitlement, package hash drift, a Development adapter in production, failed real caller, failed alert delivery, unexplained predecessor traffic, archive digest mismatch, or any deletion-manifest discrepancy.
