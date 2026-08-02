# Pegasus Production Replacement and CollisionSpike Retirement

## Summary

Replace the unused CollisionSpike Azure estate with Pegasus `0.1.0-alpha.1` using the accepted **local → production** route:

1. Complete and review the missing production adapters and release tooling locally.
2. Build Web, Worker, and migration artifacts once and record SHA-256 provenance.
3. Provision the production foundation, Container Apps environment, and Basic ACR in `rg-pegasus-prod` using Bicep and `azd`, with the public Web resource still absent.
4. Upload and verify the locally built OCI image, migrate Azure SQL, create least-privilege runtime users, and bootstrap application Administrator `alex`.
5. Provision the Web Container App by immutable image digest, then deploy and live-verify Worker, Graph, Box, DVLA, DVSA, telemetry, alerts, and the real alpha workflow.
6. Record Alex’s final acceptance.
7. Immediately archive predecessor configuration and ACR images, then delete every predecessor resource except the adopted Box and enrichment Key Vaults.
8. Complete the full isolated recovery exercise before any second production release.

This plan authorizes no cloud action, merge, credential access, deployment, or deletion. Each external stage requires exact-target approval when executed.

## Executed Outcome — 2026-08-02

The operator supplied the required execution and destructive-operation authority.
The production replacement and manifest-bound predecessor retirement have now
run. This is deployment and live-verification evidence for the scopes named
below; it is not evidence for an unperformed recovery exercise or an untested
provider outcome.

- Production was deployed only to `rg-pegasus-prod`; no Azure development,
  test, integration, or staging environment was created.
- The deployed Web source revision is
  `94997dd036a48cde23fce0f960b159a2b4a921c0`. Container App
  `pegasus-prod-web-252ow37gij` is active on immutable image digest
  `sha256:da11059f89e42d74d93ea7ed732d6b7ed8faca7ceb106ecb68875e5d5d8eda75`.
  Its `/health/live` and `/health/ready` endpoints both returned HTTP 200 after
  predecessor retirement.
- The production Razor Pages sign-in, forced first-password change, and
  authenticated operator routes were exercised as `alex`. The Worker Function
  App `pegasus-prod-worker-252ow37gij` is `Running`; all nine function-disable
  settings are `false`.
- Microsoft Graph Inbox and Sent Items processing was live-verified through the
  production Worker. The approved mailbox policy contains both inbound-intake
  and sent-evidence scopes. Production telemetry showed 83 successful Worker
  executions in the final 20-minute readback and zero exceptions since the
  final Worker activation at `2026-08-01T23:55:24Z`.
- Metadata-only App Service reference status returned `Resolved` for all six
  approved retained-vault settings: `Box__ConfigJson`, `Box__ClientSecret`,
  `Dvla__ApiKey`, `Dvsa__ApiKey`, `Dvsa__ClientId`, and
  `Dvsa__ClientSecret`. No secret value was retrieved or recorded. This proves
  configuration resolution, not every Box, DVLA, or DVSA business outcome.
- Revision-bound QDOS CI pressure passed 3/3 with run ID
  `6d387276e878444bb7963afd6f2c0aa3` and evidence SHA-256
  `66b0f315e9fb9cbd56ae6c1bed0a9c910b78b714f589b858ef4c0c906e9c0147`.
- The final predecessor archive is
  `C:\Users\Alex\Documents\Pegasus-Predecessor-Archive\collisionspike-dev-retirement-20260802T000640Z`.
  Its `archive-manifest.json` SHA-256 is
  `D0A2D03A09D54142F3337B0A186131133DB9D8B19180048AB71544D88522808A`;
  all 16 unique `ce-ocr` and `valuationbot-mcp` OCI digests were verified before
  the predecessor registry was removed.
- The executable `retirement-manifest.json` SHA-256 is
  `3CC3F1224239E9F30B687302EE813AB081950DC4B020AF84368BDD8AC2D40CBF`.
  Its eight ordered resource batches completed. All 30 delete-classified role
  assignments are absent and all seven retain-classified assignments remain.
- `rg-collisionspike-dev` intentionally remains as the container for exactly
  `cespkboxkvv76a47` and `cespkenrichkvgi62sd`. The managed OCR child resource
  group is absent. The obsolete vaults `cespk-pg-kv-dev`,
  `cespkevakvufa3ci`, and `cespklockva7tzj2` are soft-deleted without purge and
  are scheduled for platform purge on 2026-08-09.
- The first-release recovery exception remains in force: no second production
  release may proceed until section 10's isolated RPO/RTO exercise passes.

## Locked End State

- Environments: isolated local development and production only; no Azure dev/test/staging resources.
- Production: subscription `e6076573-23a5-46a8-acef-7e22d264e5db`, tenant `858cf5b3-aa0a-47a6-9b40-4851fd0afa94`, resource group `rg-pegasus-prod`, region `uksouth`.
- Compute/data: Linux/AMD64 Razor Pages Web on Azure Container Apps Consumption, FC1 .NET 10 isolated Worker, Basic ACR, S0 Azure SQL, two Standard LRS storage accounts, distinct Web/Worker identities, new Pegasus Key Vault, Log Analytics and Application Insights.
- Web scale: UK South, external HTTPS ingress on port 8080, single active revision, 0.5 vCPU, 1 GiB, minimum zero and maximum one replica. Cold start is accepted and tested.
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
   - `New-PredecessorRetirementManifest.ps1`;
   - `Invoke-PredecessorRetirement.ps1`.
9. `Build-ReleaseArtifacts.ps1` must produce ignored artifacts:
   - `web.zip` for `linux-x64`, retained only for interactive Administrator bootstrap;
   - `web-image.tar.gz`, a Linux/AMD64 OCI image whose repository, source-revision tag, and manifest digest are recorded;
   - `worker.zip` for `linux-x64`;
   - self-contained `efbundle.exe` for `win-x64`;
   - a schema-2 manifest recording source commit, clean/scoped status, SDK/tool versions, migration identity, all file sizes and SHA-256 hashes, and the OCI descriptor identity.
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
az role assignment list --resource-group $PegasusOldRg --include-inherited --output json
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
$PegasusRegistry = 'cespkocracraeee76'
$PegasusRegistryServer = "$PegasusRegistry.azurecr.io"
$PegasusRegistryToken = az acr login --name $PegasusRegistry --expose-token --query accessToken --output tsv
try {
    $PegasusRegistryToken | oras login $PegasusRegistryServer `
      --username 00000000-0000-0000-0000-000000000000 `
      --password-stdin
    pwsh ./scripts/Invoke-PredecessorArchive.ps1 `
      -ArchiveRoot $PegasusArchive `
      -IncludeOciImages `
      -ExcludeData
} finally {
    oras logout $PegasusRegistryServer
    $PegasusRegistryToken = $null
}
```

Hash the archive and stop unless every unique digest is present. Do not download the four evidence blobs or export PostgreSQL, queues, Durable state, or telemetry.

#### Executed preflight evidence — 2026-08-01

- Azure CLI and `azd` authenticated to the exact tenant and enabled subscription
  as `digital@collisionengineers.co.uk`; the signed-in principal object ID is
  `06b65d89-b4dd-4e64-927e-0f154b4f9427`.
- Fresh read-only inventory found 52 resources in `rg-collisionspike-dev` and
  one Container App in the managed OCR child group. The groups have no locks.
  Role assignments, deployments, 30-day activity, non-secret vault metadata,
  secret names, ACR metadata, and 30-day predecessor usage were archived.
- ORAS 1.3.0 was installed. Docker was unavailable, so ACR authentication used
  an ephemeral `--expose-token` value piped to `oras login --password-stdin`;
  the session was logged out in `finally` and the token was not written to the
  archive or repository.
- `C:\Users\Alex\Documents\Pegasus-Predecessor-Archive\collisionspike-dev`
  contains 16 verified OCI layouts: four `ce-ocr` digests and twelve
  `valuationbot-mcp` digests. `archive-manifest.json` contains 264 entries,
  verifies 9,731,767,698 bytes, and has SHA-256
  `F88BC99D2CA742433E6A36860D3EDA65629A95AA70FC9FA2143DBB8768F9B47C`.
- No secret value, evidence blob, PostgreSQL row, queue message, Durable state,
  or telemetry payload was retrieved. No Azure resource was written, changed,
  stopped, or deleted.
- UK South advertises Linux B1 and FC1 with .NET 10 isolated. The public Azure
  Retail Prices API returned GBP 0.0136/hour for Linux B1 and GBP 0.4584/day
  for SQL S0; FC1 and storage remain workload-sensitive. At 730 hours and 30
  days, the two fixed compute/database meters are approximately GBP 23.68 per
  month before storage, transactions, telemetry, networking, Key Vault, and
  external-provider charges.
- The principal has subscription-scope Owner authority. `Microsoft.Sql` and
  `Microsoft.Quota` are currently `NotRegistered`; every other provider used by
  the template is registered. Consequently the subscription-specific S0 and
  generic quota reads could not complete. Provider registration is a cloud
  write and was not included in this phase's approval.

On 2026-08-01, separate exact approval was granted and `Microsoft.Sql` was
registered successfully. The subscription-specific read then returned SQL
Standard S0 as available in UK South at 10 DTUs. The later autonomous
deployment direction authorised the exact plan execution; `Microsoft.Quota`
was registered and used for the now-superseded App Service capacity check
retained as historical evidence below.

### 5. Two-stage production preview and provisioning

The earlier B1 and P0v4 App Service quota attempts are retained as dated
historical evidence only. ADR-0015 removes App Service and the aggregate
`Microsoft.Web` quota from the active route. No quota update or support request
is required for the Web replacement.

Read provider state first. If any provider is not registered, stop and obtain
exact approval for that namespace and subscription before running its
registration command. Then confirm UK South support:

```powershell
az provider show --namespace Microsoft.Sql --query registrationState --output tsv
az provider show --namespace Microsoft.App --query registrationState --output tsv
az provider show --namespace Microsoft.ContainerRegistry --query registrationState --output tsv
```

If a read is not `Registered`, stop and obtain approval naming that exact
namespace before running only its applicable command:

```powershell
az provider register --namespace Microsoft.Sql --subscription $PegasusSubscription --wait
az provider register --namespace Microsoft.App --subscription $PegasusSubscription --wait
az provider register --namespace Microsoft.ContainerRegistry --subscription $PegasusSubscription --wait

az provider show --namespace Microsoft.Sql --query registrationState --output tsv
az provider show --namespace Microsoft.App --query registrationState --output tsv
az provider show --namespace Microsoft.ContainerRegistry --query registrationState --output tsv

az sql db list-editions --location $PegasusLocation --available `
  --edition Standard --service-objective S0 --show-details max-size --output json
az provider show --namespace Microsoft.App `
  --query "resourceTypes[?resourceType=='managedEnvironments'].locations" --output json
az extension add --name containerapp --upgrade
```

Stop unless all three providers read `Registered`, S0 is returned, and UK South
is listed for Container Apps managed environments.

Because the current Bicep schema cannot represent a fractional Log Analytics
cap, later provisioning approval must also name the two immediate
post-provision writes that set the exact new Log Analytics workspace and
Application Insights component to 0.1 GB/day. That approval must name both
target resource names, both `az monitor ... update` operations, and accept the
brief interval between resource creation and verified cap application. No
package, credential, database, or trigger operation may occur during that
interval.
Configure `azd`:

```powershell
$PegasusUser = az ad signed-in-user show --query '{id:id,upn:userPrincipalName}' --output json | ConvertFrom-Json

azd env new $PegasusAzdEnv --subscription $PegasusSubscription --location $PegasusLocation
azd env set -e $PegasusAzdEnv AZURE_SUBSCRIPTION_ID $PegasusSubscription
azd env set -e $PegasusAzdEnv AZURE_TENANT_ID $PegasusTenant
azd env set -e $PegasusAzdEnv AZURE_LOCATION $PegasusLocation
azd env set -e $PegasusAzdEnv PEGASUS_ENVIRONMENT_NAME prod
azd env set -e $PegasusAzdEnv AZURE_PRINCIPAL_ID $PegasusUser.id
azd env set -e $PegasusAzdEnv AZURE_PRINCIPAL_NAME $PegasusUser.upn
azd env set -e $PegasusAzdEnv PEGASUS_DEPLOYMENT_MODE approved-live-deployment
azd env set -e $PegasusAzdEnv PEGASUS_WEB_ACTIVATION disabled
azd env set -e $PegasusAzdEnv BOX_ROOT_FOLDER_ID 392761581105
```

Set only exact versioned secret references established by an approved
metadata-only read. Box server authentication requires both retained secrets;
a static access token is prohibited because it expires and is not present in
the retained vault:

```powershell
azd env set -e $PegasusAzdEnv BOX_CONFIG_JSON_SECRET_URI $ApprovedBoxConfigJsonSecretUri
azd env set -e $PegasusAzdEnv BOX_CLIENT_SECRET_SECRET_URI $ApprovedBoxClientSecretSecretUri
azd env set -e $PegasusAzdEnv DVLA_API_KEY_SECRET_URI $ApprovedDvlaApiKeySecretUri
azd env set -e $PegasusAzdEnv DVSA_API_KEY_SECRET_URI $ApprovedDvsaApiKeySecretUri
azd env set -e $PegasusAzdEnv DVSA_CLIENT_ID_SECRET_URI $ApprovedDvsaClientIdSecretUri
azd env set -e $PegasusAzdEnv DVSA_CLIENT_SECRET_SECRET_URI $ApprovedDvsaClientSecretSecretUri
azd env set -e $PegasusAzdEnv DVSA_SCOPE 'https://tapi.dvsa.gov.uk/.default'
```

Run and retain the base preview:

```powershell
azd provision -e $PegasusAzdEnv --preview --no-prompt |
    Tee-Object ./artifacts/releases/0.1.0-alpha.1/azd-base-preview.txt
```

The base preview must create the production foundation, Basic ACR, Container
Apps environment, identities and `AcrPull`, SQL, storage, Worker, Key Vault,
telemetry, alerts unrelated to the Web resource, and budget. It must not create
the Web Container App or its HTTP 5xx alert. Fail on any resource outside
`rg-pegasus-prod`, any dev/staging resource, delete/replace operation, App
Service Web resource, tag-only image, one-storage topology, shared-key access,
local authentication, broad role, remote build, enabled Worker trigger,
OCR/Foundry/Maps/Vision/capture resource, or secret-bearing output.

Stop after the preview. Record its exact resource IDs, operations, regional
SKUs, fixed and consumption cost exposure, and absence of delete/replace
changes. Provision only after the user explicitly approves that exact preview.

The approved 2026-08-01 preview inputs are complete. The retained enrichment
Function configuration supplied the entitlement-specific DVSA tenant ID
without printing it; its API host and scope matched the official DVSA
production documentation, and the derived token URL is set in the local
`pegasus-prod` environment. The predecessor orchestrator's versioned Key Vault
application credential resolved the exact Graph mailbox, Inbox, and Sent Items
metadata without reading any message or attachment. An exploratory temporary
`FullAccess` assignment to the deploying user was removed and verified absent.

The first ARM preview exposed an `azd` reserved-variable collision:
`AZURE_ENV_NAME` was always expanded to `pegasus-prod` even though Bicep permits
only `prod`. The parameter now uses `PEGASUS_ENVIRONMENT_NAME=prod`; local plan
validation and Bicep compilation pass. Later B1 and P0v4 previews stopped on
App Service quota without creating a workload resource. ADR-0015 supersedes
that hosting route; the failed quota requests and tracking IDs remain evidence,
not commands to resume.

Provision the base only after its preview is clean and explicitly approved:

```powershell
azd provision -e $PegasusAzdEnv --no-prompt
azd env refresh -e $PegasusAzdEnv
azd env get-values -e $PegasusAzdEnv
```

Apply the fractional telemetry cap after provisioning because the current Bicep
schema exposes the Log Analytics cap as an integer. Set and verify both limits;
the effective workspace-based Application Insights limit is the lower of the
two:

```powershell
$ApplicationInsightsName = azd env get-value -e $PegasusAzdEnv APPLICATION_INSIGHTS_NAME
$LogAnalyticsWorkspaceName = azd env get-value -e $PegasusAzdEnv LOG_ANALYTICS_WORKSPACE_NAME

az monitor log-analytics workspace update `
  --resource-group $PegasusProdRg `
  --workspace-name $LogAnalyticsWorkspaceName `
  --set workspaceCapping.dailyQuotaGb=0.1 `
  --output none

az monitor app-insights component billing update `
  --resource-group $PegasusProdRg `
  --app $ApplicationInsightsName `
  --cap 0.1 `
  --stop false `
  --output none

$WorkspaceCap = az monitor log-analytics workspace show `
  --resource-group $PegasusProdRg `
  --workspace-name $LogAnalyticsWorkspaceName `
  --query workspaceCapping.dailyQuotaGb `
  --output tsv
$ApplicationInsightsCap = az monitor app-insights component billing show `
  --resource-group $PegasusProdRg `
  --app $ApplicationInsightsName `
  --query dataVolumeCap.cap `
  --output tsv

if ([decimal]$WorkspaceCap -ne 0.1 -or [decimal]$ApplicationInsightsCap -ne 0.1) {
    throw "Telemetry cap verification failed. Workspace=$WorkspaceCap; ApplicationInsights=$ApplicationInsightsCap"
}

az resource list --resource-group $PegasusProdRg --output table
```

Verify Basic ACR with admin disabled, the Container Apps environment, Web
identity `AcrPull`, FC1, S0, two storage accounts, two identities, new Key
Vault, 31-day retention, adaptive sampling, both 0.1 GB/day telemetry caps,
action group, base alert rules, and budget before continuing. Prove the Web
Container App is still absent. A failed cap write or readback is a stop gate:
do not bind credentials, upload the image, migrate, deploy packages, or activate
triggers.

### 6. Bind external identities and retained vaults

1. Construct secret resource IDs from the approved secret-name census without retrieving values.
2. Grant:
   - Worker secret-level `Key Vault Secrets User` access only to the Box secrets it calls;
   - Worker secret-level access only to the DVLA/DVSA secrets;
   - Web receives no third-party secret access because it has no Graph, Box,
     DVLA, or DVSA caller.
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

Bind every artifact to the operator-approved schema-2 manifest before the first
registry or database write:

```powershell
$ReleaseManifestPath = './artifacts/releases/0.1.0-alpha.1/release-manifest.json'
$ApprovedReleaseManifestSha256 = '<operator-approved SHA-256 from the final reviewed artifact>'

pwsh ./scripts/Test-AzureDeploymentPlan.ps1 `
  -Mode PreUpload `
  -ManifestPath $ReleaseManifestPath `
  -ManifestSha256 $ApprovedReleaseManifestSha256
```

Upload the exact locally built OCI archive to the new production ACR. The tag
is navigation only; the locally inspected and registry-reported digests must
match before any Web activation:

```powershell
$ReleaseManifest = Get-Content $ReleaseManifestPath -Raw | ConvertFrom-Json
$AcrName = azd env get-value -e $PegasusAzdEnv CONTAINER_REGISTRY_NAME
$AcrServer = azd env get-value -e $PegasusAzdEnv CONTAINER_REGISTRY_LOGIN_SERVER
$AcrId = az acr show --resource-group $PegasusProdRg --name $AcrName --query id --output tsv
$WebImageArchive = './artifacts/releases/0.1.0-alpha.1/web-image.tar.gz'
$WebTag = $ReleaseManifest.webImage.tag
$LocalDigest = $ReleaseManifest.webImage.digest

$ArmAudienceStatus = az acr config authentication-as-arm show `
  --registry $AcrName --query status --output tsv
```

Stop and obtain approval naming `$AcrId`, repository `pegasus/web`, tag
`$WebTag`, digest `$LocalDigest`, the ORAS upload, and—only when the readback is
not `enabled`—the exact ACR authentication-as-ARM configuration write. Then run:

```powershell
if ($ArmAudienceStatus -ne 'enabled') {
  az acr config authentication-as-arm update --registry $AcrName --status enabled --output none
}

$AcrToken = az acr login --name $AcrName --expose-token --query accessToken --output tsv
try {
  $AcrToken | oras login $AcrServer `
    --username 00000000-0000-0000-0000-000000000000 `
    --password-stdin
  oras cp --from-oci-layout "${WebImageArchive}:$WebTag" `
    "${AcrServer}/pegasus/web:$WebTag"
} finally {
  oras logout $AcrServer
  $AcrToken = $null
}

$RegistryDigest = az acr manifest show-metadata --registry $AcrName `
  --name "pegasus/web:$WebTag" --query digest --output tsv
if ($RegistryDigest -ne $LocalDigest) {
  throw 'Registry digest differs from the reviewed OCI artifact.'
}
```

Resolve the exact SQL targets and migration identity, then stop and obtain
approval naming the server ID, database ID, manifest hash, migration identity,
`efbundle.exe` schema write, and the runtime principal/role bootstrap:

```powershell
$SqlServerFqdn = azd env get-value -e $PegasusAzdEnv AZURE_SQL_SERVER_FQDN
$SqlServerName = $SqlServerFqdn.Split('.')[0]
$SqlServerId = az sql server show --resource-group $PegasusProdRg --name $SqlServerName --query id --output tsv
$SqlDatabaseId = az sql db show --resource-group $PegasusProdRg --server $SqlServerName `
  --name (azd env get-value -e $PegasusAzdEnv AZURE_SQL_DATABASE_NAME) --query id --output tsv
$ReleaseManifest.migrationIdentity
```

After that exact approval, apply the hashed migration bundle using the deploying
user as the temporary SQL Entra administrator while the public Web resource is
still absent:

```powershell
pwsh ./scripts/Test-AzureDeploymentPlan.ps1 `
  -Mode PreMigration `
  -Environment $PegasusAzdEnv `
  -ManifestPath $ReleaseManifestPath `
  -ManifestSha256 $ApprovedReleaseManifestSha256

& ./artifacts/releases/0.1.0-alpha.1/efbundle.exe `
  --connection $PegasusMigratorConnectionString

pwsh ./scripts/Invoke-AzureDatabaseBootstrap.ps1 `
  -Environment $PegasusAzdEnv `
  -ManifestPath $ReleaseManifestPath `
  -ManifestSha256 $ApprovedReleaseManifestSha256
```

The bootstrap script first proves that its clean checkout is the exact source
revision named by the operator-approved manifest. It then creates
`pegasus_web_runtime` and `pegasus_worker_runtime` from the provisioned
managed-identity client-ID SIDs, assigns only the migration-defined custom
roles, rejects direct, inherited, nested-role, and ownership authority, compares
effective per-table DML with the exhaustive allow matrix and `DELETE` denials,
and proves neither runtime identity has DDL.

Create the first application Administrator interactively:

```powershell
pwsh ./scripts/Invoke-ProductionAdministratorBootstrap.ps1 `
  -Environment $PegasusAzdEnv `
  -UserName alex `
  -ManifestPath $ReleaseManifestPath `
  -ManifestSha256 $ApprovedReleaseManifestSha256
```

Activate the Web only after migration and Administrator bootstrap. The second
preview may add only the digest-pinned Container App and its HTTP 5xx alert; it
must contain no delete, replacement, telemetry-cap reset, tag-only image, or
other infrastructure change:

```powershell
azd env set -e $PegasusAzdEnv PEGASUS_WEB_IMAGE_DIGEST $RegistryDigest
azd env set -e $PegasusAzdEnv PEGASUS_WEB_REVISION_SUFFIX ($ReleaseManifest.sourceRevision.Substring(0,12))
azd env set -e $PegasusAzdEnv PEGASUS_WEB_ACTIVATION approved

azd provision -e $PegasusAzdEnv --preview --no-prompt |
  Tee-Object ./artifacts/releases/0.1.0-alpha.1/azd-web-preview.txt
```

Stop after the Web preview. It may add only the exact digest-pinned Container
App and its HTTP alert. Record the resolved IDs, digest, region, scale limits,
and cost exposure, and obtain explicit approval of that exact preview before
provisioning:

```powershell
azd provision -e $PegasusAzdEnv --no-prompt
azd env refresh -e $PegasusAzdEnv

$WebContainerAppName = azd env get-value -e $PegasusAzdEnv WEB_CONTAINER_APP_NAME
$WebFqdn = azd env get-value -e $PegasusAzdEnv WEB_CONTAINER_APP_FQDN

az containerapp show --resource-group $PegasusProdRg --name $WebContainerAppName --output json
az containerapp revision list --resource-group $PegasusProdRg --name $WebContainerAppName --output json

$AcrAdminEnabled = az acr show --resource-group $PegasusProdRg --name $AcrName `
  --query adminUserEnabled --output tsv
$AnonymousPullEnabled = az resource show --resource-group $PegasusProdRg `
  --resource-type Microsoft.ContainerRegistry/registries --name $AcrName `
  --api-version 2023-07-01 --query properties.anonymousPullEnabled --output tsv
$DeployedImage = az containerapp show --resource-group $PegasusProdRg `
  --name $WebContainerAppName --query 'properties.template.containers[0].image' --output tsv
$MinimumReplicas = az containerapp show --resource-group $PegasusProdRg `
  --name $WebContainerAppName --query properties.template.scale.minReplicas --output tsv
$MaximumReplicas = az containerapp show --resource-group $PegasusProdRg `
  --name $WebContainerAppName --query properties.template.scale.maxReplicas --output tsv
if ($AcrAdminEnabled -ne 'false' -or $AnonymousPullEnabled -eq 'true' -or
    $DeployedImage -ne "${AcrServer}/pegasus/web@$RegistryDigest" -or
    $MinimumReplicas -ne '0' -or $MaximumReplicas -ne '1') {
  throw 'Container App or ACR readback differs from the approved release.'
}

Invoke-WebRequest "https://$WebFqdn/health/live"
Invoke-WebRequest "https://$WebFqdn/health/ready"

azd deploy worker -e $PegasusAzdEnv `
  --from-package ./artifacts/releases/0.1.0-alpha.1/worker.zip `
  --no-prompt
```

Never run `azd deploy web`, `azd up`, plain `azd deploy`, `az acr build`,
release-time `azd package`, or any other command that rebuilds the Web image.
The Web deployment identity is its exact ACR digest; Worker remains the exact
hashed ZIP. [Azure Developer CLI command reference](https://learn.microsoft.com/en-us/azure/developer/azure-developer-cli/reference)

### 8. Live integration activation and acceptance

With all Worker triggers still disabled, obtain separate approvals and run controlled probes:

1. Allow the Web Container App to reach zero replicas, verify that state through `az containerapp replica list`, then measure the first cold request and a second warm request. Both health endpoints must become successful within the 120-second startup allowance.
2. Sign in as `alex`, change the temporary password, and prove Administrator-only routes plus Engineer/User denials. Preserve the authenticated session across a revision restart and a later scale-to-zero cycle to prove the Blob-backed Data Protection ring.
3. Graph: prove managed-identity reads from the `instructions@` Inbox and Sent Items, immutable IDs, delta continuation, and denial of an out-of-scope mailbox. No mailbox mutation.
4. Box: create and version one controlled non-corpus technical artifact beneath `392761581105`; prove cross-root denial. Retain the smoke artifact because delete is prohibited.
5. DVLA: use the official production VES endpoint and the retained API key. VES v1.2 uses `POST /vehicle-enquiry/v1/vehicles` and `x-api-key`. [DVLA VES v1.2 specification](https://developer-portal.driver-vehicle-licensing.api.gov.uk/apis/vehicle-enquiry-service/v1.2.0-vehicle-enquiry-service.html)
6. DVSA: use registration lookup with cached OAuth client-credentials tokens and `X-API-Key`; prove current, not-found, invalid, denied, throttled, and unavailable mappings without inducing provider throttling. [DVSA authentication](https://documentation.history.mot.api.gov.uk/mot-history-api/authentication/), [DVSA API specification](https://documentation.history.mot.api.gov.uk/mot-history-api/api-specification/)
7. Enable functions one boundary at a time:

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

8. After each batch, verify queue age, poison handling, idempotency, database writes, dependency results, and telemetry correlation before enabling the next.
9. Run the 30-minute/eight-session capacity profile and the real QDOS alpha journey through Graph/manual intake, extraction, case allocation, Box custody, DVLA/DVSA, completeness/review, EVA export, and exact Sent evidence.
10. Safely fire and acknowledge every alert route. Read back the £75 budget and its 50/80/100 actual and 100 forecast notifications.
11. Alex records explicit acceptance. State separately:
    - deployed;
    - live-verified integrations and callers;
    - accepted;
    - recovery objectives not yet proved.

### 9. Immediate predecessor retirement

After acceptance, do not use `azd down` or delete `rg-collisionspike-dev`.

1. Create a new stop-inventory archive root; never reuse the preflight archive.
   Generate its exact manifest deterministically, inspect its stop IDs, and
   obtain approval of its SHA-256 and every stop target:

   Before the archive command, stop and obtain exact read approval naming
   subscription `e6076573-23a5-46a8-acef-7e22d264e5db`, resource groups
   `rg-collisionspike-dev` and
   `cespkocr-env-dev_FunctionApps_247f14f1-8d57-491f-a325-a97e99634117`,
   metadata-only reads of vaults `cespkboxkvv76a47`, `cespkenrichkvgi62sd`,
   `cespk-pg-kv-dev`, `cespkevakvufa3ci`, and `cespklockva7tzj2`, ACR
   `cespkocracraeee76` and its repositories, the subscription-wide
   role-assignment census, deployment, lock and activity metadata, and 30-day
   cost metadata. This grants no token retrieval, image download, stop, or
   deletion.

```powershell
$PegasusStopArchive = "$PegasusArchive-stop-$(Get-Date -AsUTC -Format 'yyyyMMddTHHmmssZ')"
pwsh ./scripts/Invoke-PredecessorArchive.ps1 `
  -ArchiveRoot $PegasusStopArchive `
  -ExcludeData
$StopRetirementManifestPath = pwsh ./scripts/New-PredecessorRetirementManifest.ps1 `
  -ArchiveRoot $PegasusStopArchive
$StopRetirementManifestSha256 = (Get-FileHash $StopRetirementManifestPath -Algorithm SHA256).Hash
pwsh ./scripts/Invoke-PredecessorRetirement.ps1 `
  -Stage Inspect `
  -ManifestPath $StopRetirementManifestPath |
  Tee-Object "$PegasusStopArchive-inspection.json"
```

Stop here. Review the displayed archive hash, retained IDs, stop IDs, and all
future deletion batches, then obtain approval naming
`$StopRetirementManifestSha256` and every exact stop resource ID. Only after
that approval run:

The Function Apps platform-owned child Container App remains inventory-bound
but is never a direct stop target because its managed resource group applies a
deny assignment. Stop and later delete its exact same-name Function App
`parentResourceId`; Azure owns the child lifecycle.

```powershell
pwsh ./scripts/Invoke-PredecessorRetirement.ps1 `
  -Stage Stop `
  -ManifestPath $StopRetirementManifestPath
```

2. After the stop, create a second new timestamped final archive root. Producers
   remained live until step 1, so neither the preflight nor stop archive is
   deletion evidence. Authenticate ORAS, rerun the complete non-secret archive
   and OCI census. Before `az acr login`, stop and obtain exact approval naming
   the same reads plus ACR token retrieval and download of every recorded
   `ce-ocr` and `valuationbot-mcp` digest into the named local archive root:

```powershell
$PegasusRetirementArchive = "$PegasusArchive-retirement-$(Get-Date -AsUTC -Format 'yyyyMMddTHHmmssZ')"
$PegasusRegistryToken = az acr login --name $PegasusRegistry --expose-token --query accessToken --output tsv
try {
  $PegasusRegistryToken | oras login $PegasusRegistryServer `
    --username 00000000-0000-0000-0000-000000000000 --password-stdin
  pwsh ./scripts/Invoke-PredecessorArchive.ps1 `
    -ArchiveRoot $PegasusRetirementArchive `
    -IncludeOciImages `
    -ExcludeData
} finally {
  oras logout $PegasusRegistryServer
  $PegasusRegistryToken = $null
}
$ArchivedRoles = Get-Content "$PegasusRetirementArchive\subscription-role-assignments.json" -Raw |
  ConvertFrom-Json |
  Where-Object { $_.scope -match '/resourceGroups/(rg-collisionspike-dev|cespkocr-env-dev_FunctionApps_247f14f1-8d57-491f-a325-a97e99634117)(/|$)' } |
  Select-Object id, scope, principalId, principalName, roleDefinitionName
$ArchivedRoles | Format-Table
```

If this large OCI copy is interrupted or times out before
`archive-manifest.json` exists, resume only that exact incomplete root with
`-ResumeIncompleteOciArchive`. The archive script verifies every completed
local layout by digest, replaces an invalid partial layout, and downloads only
missing digests. Resume mode refuses a completed archive.

Stop and classify every displayed assignment as `retain` or `delete`, with a
non-empty rationale. Approve every exact ID and both sets. Persist that decision
outside the bound archive; no archived candidate may be omitted or added. A
role scoped to a resource, managed child, or child group being retired cannot
be marked `retain`: migrate it to a surviving scope or classify it `delete`
before generating the executable manifest:

```powershell
$RoleDispositionPath = "$PegasusRetirementArchive-role-dispositions.json"
$ApprovedRoleDispositions = @(
  # One approved object for every displayed candidate:
  # [ordered]@{ id='<exact ID>'; disposition='retain'; rationale='<reason>' }
  # [ordered]@{ id='<exact ID>'; disposition='delete'; rationale='<reason>' }
)
$ApprovedRoleDispositions | ConvertTo-Json -Depth 4 |
  Set-Content -LiteralPath $RoleDispositionPath -Encoding utf8NoBOM

$FinalRetirementManifestPath = pwsh ./scripts/New-PredecessorRetirementManifest.ps1 `
  -ArchiveRoot $PegasusRetirementArchive `
  -RoleDispositionPath $RoleDispositionPath `
  -RequireRoleDisposition
$FinalRetirementManifestSha256 = (Get-FileHash $FinalRetirementManifestPath -Algorithm SHA256).Hash
pwsh ./scripts/Invoke-PredecessorRetirement.ps1 `
  -Stage Inspect `
  -ManifestPath $FinalRetirementManifestPath |
  Tee-Object "$PegasusRetirementArchive-inspection.json"
```

3. Re-run dependency, role, lock, activity, and traffic checks. Confirm zero
   callers. Review the generator's complete retained set, platform-owned child
   IDs, live type-level `managedBy` owner, separately bound exact same-name
   Function App `parentResourceId`, locally scoped role-assignment candidates,
   and dependency-ordered batches. Classify every role-assignment candidate as retained or
   predecessor-only. Then obtain approval naming
   `$FinalRetirementManifestSha256`, its bound `archiveManifestSha256`, every
   exact stop/delete resource ID, the bound managed child-group ID, live
   type-level `managedBy` owner, exact `parentResourceId`, and every exact
   predecessor-only role assignment ID.
4. Confirm the retained set contains only:
   - `cespkboxkvv76a47`;
   - `cespkenrichkvgi62sd`.
5. Delete one exact ID per command, in these batches:
   - Function/Web/Static Web Apps and the OCR parent wrapper;
   - Foundry deployments/project, remaining AI/Maps/Vision/Document Intelligence;
   - dedicated App Service/Flex plans and predecessor identities;
   - PostgreSQL and all ten predecessor storage accounts, explicitly accepting irreversible data loss;
   - ACR after OCI digest verification;
   - dedicated Application Insights, Log Analytics, and old action group;
   - `cespk-pg-kv-dev`, `cespkevakvufa3ci`, and `cespklockva7tzj2`, without purge;
   - remaining leaf resources.

```powershell
pwsh ./scripts/Invoke-PredecessorRetirement.ps1 `
  -Stage DeleteBatch `
  -ManifestPath $FinalRetirementManifestPath `
  -Batch <reviewed-batch-name>
```

The executor rejects any live resource absent from the approved manifest and
refuses skipped batches. When the bound parent managed environment is deleted,
the executor stops at the child-group gate. Wait for Azure-managed cleanup to
leave that group empty, then stop and obtain separate approval naming the
manifest hash, exact child resource-group ID, archived/current `managedBy`
parent, and group deletion. Execute only through the bound runner:

```powershell
pwsh ./scripts/Invoke-PredecessorRetirement.ps1 `
  -Stage DeleteManagedChildGroup `
  -ManifestPath $FinalRetirementManifestPath
```

Resume the same reviewed batch if another ID remains, then continue in manifest
order.

The script enforces manifest batch order, verifies all surviving stop targets
remain stopped, resolves and prints each ID, verifies it belongs to one of the
two exact predecessor groups, rejects both retained vaults and every descendant,
requires the approved manifest hash, deletes the parent managed environment
without directly deleting its platform-owned child resources, and calls:

```powershell
az resource delete --ids $ExactReviewedResourceId
```

After the resource batches, remove only the exact role assignments classified
and approved as predecessor-only. Never derive these IDs from a live query at
deletion time, and do not remove any assignment needed by either retained
vault:

```powershell
pwsh ./scripts/Invoke-PredecessorRetirement.ps1 `
  -Stage DeleteRoleAssignment `
  -ManifestPath $FinalRetirementManifestPath `
  -RoleAssignmentId $ExactApprovedRoleAssignmentId
```

6. Confirm the manifest-bound managed child group is absent. The runner above is
   the only authorised group-deletion route; never issue an unbound direct
   `az group delete`.

7. Leave `rg-collisionspike-dev` present with only the two adopted vaults. Verify:

```powershell
az resource list --resource-group $PegasusOldRg --output table
az group exists --name $PegasusOldChildRg --subscription $PegasusSubscription
az graph query -q @"
Resources
| where resourceGroup =~ 'rg-collisionspike-dev'
   or resourceGroup =~ 'cespkocr-env-dev_FunctionApps_247f14f1-8d57-491f-a325-a97e99634117'
| project id, name, type, resourceGroup
"@ --first 1000
```

8. Remove any predecessor-only Graph application credentials and Exchange assignments only after exact ownership proof. Do not revoke the Box or DVLA/DVSA credentials now consumed by Pegasus.
9. Record deleted IDs, deletion times, soft-deleted vault state, surviving vault IDs, refreshed archive hashes, and verification output. Do not claim the resource group itself was deleted.

### 10. Mandatory post-launch recovery gate

Before any second production deployment:

1. Restore Azure SQL to a new isolated recovery database in `rg-pegasus-prod`.
2. Deploy the same retained Web image digest and Worker ZIP hash to temporary recovery resources; do not rebuild or retag the Web image.
3. Recreate exact runtime identities/roles, run migrations, and verify health plus the real smoke journey without calling production external effects.
4. Record achieved recovery point and elapsed restoration time.
5. Require RPO ≤15 minutes and RTO ≤4 hours.
6. Delete only the exact temporary recovery resources after evidence review and separate deletion approval.
7. If this proof is absent or fails, the second release stops before migration or deployment.

## Interfaces and Tests

- No new business API or Core policy owner.
- Existing ports remain authoritative: `IApprovedInboxSource`, `IApprovedSentSource`, `ICaseCustody`, and `IVehicleLookupAdapter`.
- Add typed production options for Graph mailbox/folders, Box root and secret references, DVLA VES, DVSA MOT History, managed identities, transport/custody storage, telemetry, and schema-2 release/OCI metadata.
- Test:
  - Graph paging, delta reset, immutable IDs, throttling, malformed MIME/attachments, exact mailbox/folder allowlist, and zero mutation endpoints.
  - Box ancestry, idempotent folder/file versioning, cross-root denial, retry-visible failure, and delete/move/copy/share prohibition.
  - DVLA/DVSA success, partial, not-found, invalid, stale, denied, token expiry, throttling, unavailable, malformed, and confirmed-value preservation.
  - Two-storage topology, secret-level vault access, Worker authentication-ring denial, Entra-only SQL, exhaustive runtime SQL grants/denials, disabled triggers, Container Apps scale 0–1 at 0.5 vCPU/1 GiB, Basic ACR with managed-identity pull, FC1/S0, telemetry cap, budget/alerts, and absence of dev/App-Service-Web/OCR/Foundry/Maps/Vision/capture resources.
  - Bootstrap once-only behavior, forced password change, cold and warm health/readiness, OCI and package-hash enforcement, authentication continuity across a scale-to-zero cycle, and refusal of `azd up`, `azd deploy web`, plain deploy, ACR/remote build, tag-only or changed artifacts, or mismatched targets.

## Explicit Stop Conditions

Stop immediately on an unreviewed dirty-file overlap, stale target inventory, missing exact approval, unexpected preview change, unverified secret ownership, broad Graph/Key Vault/Storage/SQL access, missing live provider entitlement, package hash drift, a Development adapter in production, failed real caller, failed alert delivery, unexplained predecessor traffic, archive digest mismatch, or any deletion-manifest discrepancy.
