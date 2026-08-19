# Current estate — read-only Azure diagnostics for release 12

Measured 2026-08-19 ~12:00–12:25 UTC from the main checkout, signed in as
`digital@collisionengineers.co.uk`, subscription `e6076573-23a5-46a8-acef-7e22d264e5db`,
tenant `858cf5b3-aa0a-47a6-9b40-4851fd0afa94` [verified: `az account show`].
Every command below was read-only (`az … show/list/query`, `az rest GET`,
`azd env get-values`, `curl` GET, `Invoke-Sqlcmd` SELECT, `git` read-only).
No cloud state, repository file, or board file was changed. Secret values are
not reproduced; only setting names and non-secret values appear.

## 1. Last deployment

**Production serves release 10, source `d8de29cb94f396816595b1f9782980476166dbfa`,
deployed 2026-08-18 between 14:22 and 14:26 UTC.** The repo's release table is
correct; nothing has been deployed since.

| Fact | Value | Evidence |
|---|---|---|
| Web container app latest revision | `pegasus-prod-web-252ow37gij--d8de29cb94f3` (also `latestReadyRevisionName`) | [verified: `az containerapp show -n pegasus-prod-web-252ow37gij -g rg-pegasus-prod`] |
| Revision created | `2026-08-18T14:23:39+00:00`, Active=True, traffic 100, Healthy, RunningAtMaxScale, 1 replica | [verified: `az containerapp revision list`] |
| Image (digest-pinned) | `pegasusprodacr252ow37gij.azurecr.io/pegasus/web@sha256:4bd50f661be47f243aa041add5f0be0233c23778939e1fddabbb5c2eab2240a5` | [verified: `az containerapp show` + `az containerapp revision list`] |
| ACR manifest for that digest | tag `d8de29cb94f396816595b1f9782980476166dbfa`, createdTime = lastUpdateTime = `2026-08-18T14:22:37.6267776Z` | [verified: `az acr manifest list-metadata -r pegasusprodacr252ow37gij -n pegasus/web --orderby time_desc --top 10`] |
| ARM deployment (azd provision) | `pegasus-prod` Succeeded `2026-08-18T14:23:49.387145+00:00`; parameters `webImageDigest=sha256:4bd50f66…`, `webRevisionSuffix=d8de29cb94f3`, `webActivation=approved`, `workerActivation=approved-live-worker` | [verified: `az deployment group list -g rg-pegasus-prod`, `az deployment group show -g rg-pegasus-prod -n pegasus-prod`] |
| Other deployment in history | `Failure-Anomalies-Alert-Rule-Deployment-54cd91c3` Failed `2026-08-01T20:55:16Z` (App Insights smart-detection rule; only other entry) | [verified: same `az deployment group list`] |
| Function app `lastModifiedTimeUtc` | `2026-08-18T14:23:30.58` (site config write from the provision) | [verified: `az rest GET …/sites/pegasus-prod-worker-252ow37gij?api-version=2024-04-01`] |
| Worker package deployment (active) | id `e88b8e84-8643-4818-bc7d-b8b52aabf9aa`, deployer `az_cli`, remoteBuild=false, start `2026-08-18T14:25:06Z`, end `2026-08-18T14:26:11Z`, status 4 (success), `active: true` | [verified: `az rest GET …/sites/pegasus-prod-worker-252ow37gij/deployments`] |
| Earlier worker deployments | `c66c7e0a…` az_cli 2026-08-18 11:56–11:57 success (release 9); `c85ac98c…` LegionOneDeploy 11:55:26 **failed** remote Oryx build ("Couldn't detect a version for the platform 'dotnet'") — the known `azd deploy --from-package` failure; `2735f240…` core_tools 2026-08-14 06:07–06:08 success (un-numbered 14 Aug deployment) | [verified: same] |
| Worker package content/version | Not observable read-only: the package lives in `https://pegtrans252ow37gij.blob.core.windows.net/app-package` (functionAppConfig.deployment) and `az storage blob list --auth-mode login` was refused (operator lacks a data-plane role); the Functions admin endpoint needs a host key. The package being the release-10 build of `d8de29cb` is [assumed/not checked: only the deployment timestamp 14:25 UTC, 2 min after the image push, and `docs/operations.md` release 10 row support it]. |
| Web `/diagnostics/version` | HTTP 200 `{"version":"0.1.0-alpha.1","sourceSha":"d8de29cb94f396816595b1f9782980476166dbfa"}` | [verified: `curl https://pegasus-prod-web-252ow37gij.ashymushroom-676209e5.uksouth.azurecontainerapps.io/diagnostics/version`] |
| Worker runtime | `dotnet-isolated` 10.0, Flex Consumption (`FC1`), instanceMemoryMB 2048, maximumInstanceCount 20, deployment storage auth UserAssignedIdentity `pegasus-prod-worker-id-252ow37gij`, `linuxFxVersion` empty (Flex), httpsOnly true | [verified: `az rest GET …/sites/…`; `az appservice plan show -n pegasus-prod-worker-plan-252ow37gij`] |
| Worker functions (9) | DueWorkSweepFunction (timer), ExternalPoisonFunction (queue), ExternalWorkFunction (queue), InboxPollFunction (timer), IntakePoisonFunction (queue), IntakeWorkFunction (queue), PendingWorkDispatchFunction (timer), SentEvidencePollFunction (timer), StagedArtifactReconciliationFunction (timer); all `isDisabled: false`, language dotnet-isolated | [verified: `az functionapp function list -n pegasus-prod-worker-252ow37gij -g rg-pegasus-prod`] |

## 2. Current estate inventory

### Resource group `rg-pegasus-prod` (18 resources, all `Succeeded`, uksouth unless noted)
[verified: `az resource list -g rg-pegasus-prod -o table`]

- Identities: `pegasus-prod-web-id-252ow37gij` (clientId `e801d141-e876-471a-8829-222e9759b933`, principalId `f3b032cc-7591-4ea8-bd68-d165578c576f`), `pegasus-prod-worker-id-252ow37gij` (clientId `d7d9a0ad-a309-467d-9102-56a002fb0edc`, principalId `4f4d9606-3634-4c21-a1ee-3238351cfc69`) [verified: `az identity show`]
- Compute: `pegasus-prod-aca-env-252ow37gij` (managed environment), `pegasus-prod-web-252ow37gij` (container app), `pegasus-prod-worker-plan-252ow37gij` (serverFarm FC1 FlexConsumption), `pegasus-prod-worker-252ow37gij` (Microsoft.Web/sites, kind `functionapp,linux`)
- Data: `pegasus-prod-sql-252ow37gij` (+ databases `master`, `pegasus`), storage `pegtrans252ow37gij` (transport), `pegcustody252ow37gij` (custody)
- Registry/secrets: `pegasusprodacr252ow37gij` (Basic, admin user disabled), `pegasusprodkv252ow37g` (RBAC, soft-delete + purge protection, standard)
- Observability: `pegasus-prod-logs-252ow37gij` (Log Analytics), `pegasus-prod-appi-252ow37gij` (App Insights, appId `b2c7c738-3b1d-4018-8dc1-99e704f19e72`, workspace-based), `pegasus-prod-operations` (action group, global), `pegasus-prod-application-exceptions` (scheduled query rule), `pegasus-prod-web-http5xx` (metric alert, global)

### Web — Container App `pegasus-prod-web-252ow37gij`
[verified: `az containerapp show`, `az containerapp revision list`, `az containerapp ingress traffic show`]

- Revisions: exactly one — `--d8de29cb94f3` (see §1); `activeRevisionsMode: Single`; traffic `latestRevision: true, weight 100`.
- Ingress: external, FQDN `pegasus-prod-web-252ow37gij.ashymushroom-676209e5.uksouth.azurecontainerapps.io`, targetPort 8080, transport Auto, allowInsecure false.
- Scale: **minReplicas 1, maxReplicas 1**; 0.5 vCPU / 1Gi.
- Probes: Liveness `GET /health/live:8080` every 10 s (fail 3); Readiness `GET /health/ready:8080` every 5 s (fail 6); Startup `GET /health/live:8080` every 5 s (fail 24).
- Identity: UserAssigned `pegasus-prod-web-id-252ow37gij`; registry pull via that identity (no password secret).
- Container App secrets (names only): `box-config-json`, `box-client-secret`, `automation-mcp-client-secret`.
- Env names (23): `APPLICATIONINSIGHTS_CONNECTION_STRING`, `APPLICATIONINSIGHTS_AUTHENTICATION_STRING` (=`Authorization=AAD;ClientId=e801d141…`), `APPLICATIONINSIGHTS_ENABLEADAPTIVESAMPLING=true`, `ASPNETCORE_ENVIRONMENT=Production`, `ASPNETCORE_HTTP_PORTS=8080`, `Runtime__Profile=Production`, `ConnectionStrings__Pegasus` (redacted), `KEY_VAULT_URI=https://pegasusprodkv252ow37g.vault.azure.net/`, `TransportStorage__AccountName=pegtrans252ow37gij`, `CustodyStorage__AccountName=pegcustody252ow37gij`, `CustodyStorage__ServiceUri=https://pegcustody252ow37gij.blob.core.windows.net/`, `AZURE_CLIENT_ID`/`AzureIdentity__WebClientId=e801d141…`, `Box__BaseUri=https://api.box.com/2.0/`, `Box__UploadUri=https://upload.box.com/api/2.0/`, `Box__RootFolderId=405543781910`, `Box__ConfigJson` (secretRef), `Box__ClientSecret` (secretRef), `Features__AutomationMcp=true`, `AutomationMcp__ClientId=pegasus-automation`, `AutomationMcp__ClientSecret` (secretRef), `AutomationMcp__PublicOrigin=https://pegasus-prod-web-252ow37gij.ashymushroom-676209e5.uksouth.azurecontainerapps.io/`, `AutomationMcp__RedirectUris=https://claude.ai/api/mcp/auth_callback`.

### Worker — Function App `pegasus-prod-worker-252ow37gij`
[verified: `az rest GET …/sites/…`, `az functionapp config appsettings list`, `az rest GET …/config/configreferences/appsettings`]

- State `Running`, availability `Normal`, enabled, hostname `pegasus-prod-worker-252ow37gij.azurewebsites.net` (root GET → 200; `/admin/host/status` → 401 as expected anonymously) [verified: `curl`].
- Runtime/plan/functions: see §1.
- **Nine-function activation census — all nine `AzureWebJobs.<Function>.Disabled = false`** (DueWorkSweep, ExternalPoison, ExternalWork, InboxPoll, IntakePoison, IntakeWork, PendingWorkDispatch, SentEvidencePoll, StagedArtifactReconciliation) — i.e. the `approved-live-worker` contract `scripts/Invoke-ProductionSmoke.ps1` asserts. `PEGASUS_WORKER_ACTIVATION="approved-live-worker"` in the azd env agrees [verified: `azd env get-values -e pegasus-prod`].
- Schedules: `ApprovedInboxPollSchedule=45 * * * * *`, `SentEvidencePollSchedule=15 * * * * *`, `IntakeStagedArtifactReconciliationSchedule=30 * * * * *`, `PendingWorkDispatchSchedule=0 * * * * *`, `DueWorkSweepSchedule=0 */5 * * * *`.
- Other non-secret settings: `Runtime__Profile=Production`, `AzureIdentity__WorkerClientId=d7d9a0ad…`, `AzureWebJobsStorage__accountName=pegtrans252ow37gij`, `AzureWebJobsStorage__credential=managedidentity`, `AzureWebJobsStorage__clientId` (identity), `IntakeQueue__ServiceUri`/`ExternalWorkQueue__ServiceUri=https://pegtrans252ow37gij.queue.core.windows.net/`, `IntakeStorage__ServiceUri=https://pegcustody252ow37gij.blob.core.windows.net/`, `CustodyStorage__AccountName=pegcustody252ow37gij`, `TransportStorage__AccountName=pegtrans252ow37gij`, `KEY_VAULT_URI=https://pegasusprodkv252ow37g.vault.azure.net/`, `Graph__BaseUri=https://graph.microsoft.com/v1.0/`, `Graph__MailboxAddress=instructions@collisionengineers.co.uk`, `Graph__MailboxId=6118dbe0-4c94-48aa-8361-b803d6c9d52d`, `Graph__InboxFolderId`/`Graph__SentFolderId` (set), `Box__BaseUri`, `Box__UploadUri`, `Box__RootFolderId=405543781910`, `Dvla__BaseUri=https://driver-vehicle-licensing.api.gov.uk/vehicle-enquiry/v1/`, `Dvsa__BaseUri=https://history.mot.api.gov.uk/v1/trade/vehicles/registration/`, `Dvsa__Scope=https://tapi.dvsa.gov.uk/.default`, `Dvsa__TokenUri=https://login.microsoftonline.com/a455b827-244f-4c97-b5b4-ce5d13b4d00c/oauth2/v2.0/token`, `APPLICATIONINSIGHTS_AUTHENTICATION_STRING=Authorization=AAD;ClientId=d7d9a0ad…`, `APPLICATIONINSIGHTS_ENABLEADAPTIVESAMPLING=true`.
- Secret-bearing settings (names only): `APPLICATIONINSIGHTS_CONNECTION_STRING`, `ConnectionStrings__Pegasus`, `Box__ConfigJson`, `Box__ClientSecret`, `Dvla__ApiKey`, `Dvsa__ApiKey`, `Dvsa__ClientId`, `Dvsa__ClientSecret`.
- **Key Vault references: all 6 (12 rows incl. `APPSETTING_` mirrors) `Resolved` against `pegasusprodkv252ow37g` via UserAssigned identity** — `Box__ConfigJson`, `Box__ClientSecret`, `Dvla__ApiKey`, `Dvsa__ClientId`, `Dvsa__ClientSecret`, `Dvsa__ApiKey`.

### SQL — `pegasus-prod-sql-252ow37gij` / `pegasus`
[verified: `az sql db show`, `az sql server show`, `Invoke-Sqlcmd … -AccessToken (az account get-access-token --resource https://database.windows.net/)`]

- Server: FQDN `pegasus-prod-sql-252ow37gij.database.windows.net`, version 12.0, Entra-only auth **true**, admin `digital@collisionengineers.co.uk`, public network Enabled, min TLS 1.2.
- Database: edition Standard, SKU `Standard` capacity 10 (S0), maxSize 250 GB, Online, not zone-redundant, collation `SQL_Latin1_General_CP1_CI_AS`.
- **Migration head (top 5, `ORDER BY MigrationId DESC`, all ProductVersion 10.0.10):**
  1. `20260814094632_DropBoxFileRequests`
  2. `20260814092852_AddWorkerCaseCreationGrants`
  3. `20260813025241_StandaloneAuditReportDecision`
  4. `20260812010335_ManualInspectionAuditCustody`
  5. `20260811122654_CaseCustodyEvaRecovery`
  (then `20260811063940_QdosAllocationRecovery`, `20260806090000_ApprovedInboxPollStateIdentityAdoption`, `20260805223036_RetainedMailboxMessages`); 45 rows in `__EFMigrationsHistory`.
- Data-plane readbacks (context for §3): `Cases`=1, `Principals`=1; `ApprovedMailboxes` has one row `instructions@collisionengineers.co.uk` State `Approved`, `AllowInboundIntake=1`, **`AllowSentEvidence=0`**, `SentFolderIdentity` NULL; `ApprovedInboxPollStates.LastCompletedAtUtc = 2026-08-19 12:21:45 +00:00`, no failure code, no lease (inbox poll is live and advancing); `ApprovedSentPollStates` has one row for the same mailbox with a Sent folder identity and a Graph cursor.

### ACR — `pegasusprodacr252ow37gij` (Basic)
[verified: `az acr repository list`, `az acr repository show-tags`, `az acr manifest list-metadata`]

Repositories: `pegasus/web` (26 tags, each a full source SHA) and `pegasus/web-pegasus-prod` (one tag `azd-deploy-1786687004`, 2026-08-14T05:57:59Z — the un-numbered azd deploy). Last 10 `pegasus/web` manifests (created = lastUpdate):

| Tag (source SHA) | Digest | Created (UTC) |
|---|---|---|
| `d8de29cb94f3…` | `sha256:4bd50f661be47f243aa041add5f0be0233c23778939e1fddabbb5c2eab2240a5` | 2026-08-18T14:22:37 (**deployed**) |
| `f1e116c6eb93…` | `sha256:63e863242479…` | 2026-08-18T11:45:02 (release 9) |
| `a593bc890cf1…` | `sha256:e5d1d01d3603…` | 2026-08-18T10:45:04 (pushed, never a release row) |
| `75f39c70a343…` | `sha256:34b2d8d593e1…` | 2026-08-12T14:11:26 |
| `dd61ac56840d…` | `sha256:04d39c20f1fb…` | 2026-08-12T06:55:11 |
| `0f686b126fc6…` | `sha256:a42b5f197916…` | 2026-08-11T23:30:03 |
| `ded44fd7be0a…` | `sha256:c993eb0ee643…` | 2026-08-07T11:26:55 (release 8) |
| `32feefacc388…` | `sha256:c8a0ebac4011…` | 2026-08-05T13:27:47 (release 7) |
| `474a0924a6ba…` | `sha256:b2ceaf37e705…` | 2026-08-05T11:49:52 (release 6) |
| `c6571f771aab…` | `sha256:29d4fcffd555…` | 2026-08-04T08:04:14 (release 5) |

No tag for `feda958f` (the held release 11) exists — release 11 pushed no image [verified: `show-tags | grep feda958f` → 0].

### Key Vault — `pegasusprodkv252ow37g`
[verified: `az keyvault secret list --vault-name pegasusprodkv252ow37g`, `az keyvault show`]
Secret names (7, all enabled): `automation-mcp-client-secret` (updated 2026-08-18), `box-client-secret`, `box-config-json`, `dvla-api-key`, `dvsa-api-key`, `dvsa-client-id`, `dvsa-client-secret`. RBAC authorization on, soft-delete and purge protection on.

### Role assignments (names/scopes only)
[verified: `az role assignment list --assignee <principalId> --all`]

- **Web identity**: AcrPull (ACR); Monitoring Metrics Publisher (App Insights); Storage Blob Data Owner (`pegcustody…/containers/transient-intake`); Storage Blob Data Contributor (`pegcustody…/containers/authentication-ring`, `…/box-links`); Key Vault Secrets User on secrets `box-config-json`, `box-client-secret`, `automation-mcp-client-secret`; **and** one assignment not in current `infra/modules/platform.bicep`: role `Azure Service Bus Data Sender` (roleDefinitionId `69a216fc-b8fb-44d8-bc22-1f3c2cd27a39`) scoped to storage queue `pegtrans…/queueServices/default/queues/intake-work`, created 2026-08-01T20:45:09Z by the operator principal. SIMPLI-009's report says the Web's intake-queue sender assignment was removed from Bicep (source only); incremental ARM deployments do not delete it, so it persists [verified: `grep` of `infra/modules/platform.bicep` — no such role variable; `.worktrees/kanmer/.kanmer/areas/intake-processing/SIMPLI-009/post-implementation-report`].
- **Worker identity**: Storage Blob Data Owner, Storage Queue Data Contributor, Storage Table Data Contributor (all on `pegtrans252ow37gij`); Storage Blob Data Owner (`pegcustody…/containers/transient-intake`); Storage Blob Data Contributor (`pegcustody…/containers/box-links`); Monitoring Metrics Publisher (App Insights); Key Vault Secrets User on the vault itself **and** on each of the six secrets `box-config-json`, `box-client-secret`, `dvla-api-key`, `dvsa-client-id`, `dvsa-client-secret`, `dvsa-api-key`.

### Observability
[verified: `az monitor app-insights component show`, `az monitor log-analytics workspace show`]
- App Insights `pegasus-prod-appi-252ow37gij` exists, workspace-based on `pegasus-prod-logs-252ow37gij`, ingestion public.
- Log Analytics: SKU PerGB2018, retention 31 d, **dailyQuotaGb 0.1, `dataIngestionStatus: OverQuota`, `quotaNextResetTime: 2026-08-20T03:00:00Z`**. Usage for the last three days is ~98–105 MB/day (`ContainerAppConsoleLogs` ~40–44 MB, `AppMetrics` ~15–17, `AppDependencies` ~15–16, `AppTraces` ~11–15, `AppPerformanceCounters` ~11, `AppExceptions` 5–14, `AppRequests` ~2.5), so the cap trips daily at ~11:45–11:55 UTC and nothing is ingested from then until 03:00 UTC [verified: `az monitor log-analytics query … Usage | summarize by DataType, bin(1d)`; `union AppRequests, AppTraces, AppExceptions, ContainerAppConsoleLogs | summarize min/max TimeGenerated by Type, day`]. The single largest consumer is the Web container writing EF Core `Database.Command` info lines (`SELECT 1`, `SELECT OBJECT_ID(N'[__EFMigrationsHistory]')`, `SELECT [MigrationId], [ProductVersion] FROM [__EFMigrationsHistory]` — ~98 k console lines/day, consistent with the 5-second readiness probe) [verified: `ContainerAppConsoleLogs | summarize count() by substring(Log,0,100)`].

### azd environment `pegasus-prod` (local, default)
[verified: `azd env list`, `azd env get-values -e pegasus-prod` — works from the repo root; azd 1.29.0, update available 1.31.1]
Agrees with the estate on: `PEGASUS_WEB_IMAGE_DIGEST=sha256:4bd50f66…`, `PEGASUS_WEB_REVISION_SUFFIX=d8de29cb94f3`, `WEB_CONTAINER_APP_REVISION=…--d8de29cb94f3`, `PEGASUS_WORKER_ACTIVATION=approved-live-worker`, `PEGASUS_WEB_ACTIVATION=approved`, `DEPLOYMENT_MODE=approved-live-deployment`, all seven secret URIs on `pegasusprodkv252ow37g` (host checked, versions not reproduced), `AZURE_KEY_VAULT_NAME=pegasusprodkv252ow37g`, Graph mailbox/folder ids identical to the Worker settings. One stale value: `BOX_ROOT_FOLDER_ID=392761581105` (the integration-test folder) — it is **not** a Bicep parameter; `Box__RootFolderId=405543781910` is a literal in `infra/modules/platform.bicep:422,540` and is what both roles run with, so the variable is inert [verified: `az deployment group show … keys(properties.parameters)` has no box-root parameter; `grep` of `infra/`].

## 3. Health now

All measured 2026-08-19 ~12:10 UTC with `curl` against the public FQDN [verified: `curl -s -o body -w "%{http_code} %{redirect_url}"`]:

| Probe | Result |
|---|---|
| `GET /health/live` | 200 `Healthy` (0.09 s) |
| `GET /health/ready` | 200 `Healthy` |
| `GET /diagnostics/version` | 200 `{"version":"0.1.0-alpha.1","sourceSha":"d8de29cb94f396816595b1f9782980476166dbfa"}` |
| `GET /Cases` (anonymous) | 302 → `https://pegasus-prod-web-252ow37gij.ashymushroom-676209e5.uksouth.azurecontainerapps.io/Account/SignIn?ReturnUrl=%2FCases` (https — the release-3 forwarded-headers fix still holds) |
| `GET /Upload` (anonymous) | 302 → `https://…/Account/SignIn?ReturnUrl=%2FUpload` |
| `GET /` (anonymous) | 302 → `https://…/Account/SignIn?ReturnUrl=%2F` |
| Worker `GET /` | 200; `GET /admin/host/status` 401 (key required — not attempted) |

Recent errors (App Insights / Log Analytics, read-only; only the 03:00–~11:50 UTC ingestion windows exist, see §2) [verified: `az monitor app-insights query --app b2c7c738-…`, `az monitor log-analytics query -w <customerId>`]:

- **Web**: zero exceptions and zero request rows in the last 48 h of visible telemetry (the Web role emits no `requests`/`exceptions` into the visible window; its console logs are the EF info lines above).
- **Worker, last 24 h (visible window 03:01–11:49 UTC today)**: 1 584 `AppExceptions` rows (528 role-tagged + 1 056 untagged duplicates), **all one problem: `System.UnauthorizedAccessException: "The claimed mailbox is not approved for Sent-evidence polling."`** thrown at `Pegasus.Core.Workflow.PollSentEvidence.ExecuteAsync` (`PollSentEvidence.cs:218`) from `SentEvidencePollFunction`. `requests` in the visible 48 h: `SentEvidencePollFunction` 30/30 **failed**; `StagedArtifactReconciliationFunction` 30/0 failed; `InboxPollFunction` 29/0; `PendingWorkDispatchFunction` 28/0; `DueWorkSweepFunction` 5/0 (the App Insights `requests` view only retained ~11:19–11:49 UTC). Cause as recorded in data: `SentEvidencePollFunction.Disabled=false` (the nine-function `approved-live-worker` contract), but `ApprovedMailboxes.AllowSentEvidence=0` for the only mailbox, so every 60-second tick claims the sent-poll lease, fails the policy check and throws — once a minute since (at least) the start of the visible window, ~1 440 failed invocations/day, which also feeds the `AppExceptions` share of the daily quota. `docs/runbook.md` (lines ~517, ~561, ~955) states "`SentEvidencePollFunction` stays disabled unless separately approved", whereas `scripts/Invoke-ProductionSmoke.ps1` (`approved-live-worker`) asserts all nine `Disabled=false`; the estate matches the smoke script, not the runbook sentence. Whether this is the intended state is not a fact this document can settle [assumed/not checked: operator intent].
- Scheduled-query alert `pegasus-prod-application-exceptions` and metric alert `pegasus-prod-web-http5xx` exist; their firing history was not queried [not checked].

## 4. Gap vs repo

Git state at 12:20 UTC (the checkout in `C:\Users\Alex\Documents\GitHub\pegasus` is on branch `dev`; local `main` = `d8de29cb`) [verified: `git fetch origin`, `git rev-parse origin/main origin/dev`, `git rev-list --count`, `git merge-base --is-ancestor`]:

- `origin/main` = `d8de29cb94f396816595b1f9782980476166dbfa` (2026-08-18 13:52 UTC, PR #405) = **deployed sourceSha** = azd env revision suffix = ACR tag of the deployed digest. **No gap between production and `main`.**
- `origin/dev` = `560f741c89cd109a0f28e53a4e8172fdc2d3c279` (2026-08-19 12:16 UTC, PR #420 — it moved from `4ba63888`/PR #421 to `560f741c` *during* this measurement, so the numbers below are a snapshot). `main` is an ancestor of `dev` (fast-forwardable). **`dev` is 42 commits ahead of `main` (12 first-parent merges: PRs 407, 408, 409, 411, 412, 413, 414, 415, 418, 419, 420, 421).**
- Held release 11 (`feda958f`, PR #409, 2026-08-19 08:08 UTC) left no trace in Azure: no ACR tag, no revision, no deployment.
- **Three migrations exist on `dev` that are not in production** (`src/Pegasus.Infrastructure/Migrations`): `20260819093019_RetainedMailboxInternetMessageIdentity`, `20260819104953_MailClassificationCorrectionHistory`, `20260819112640_VersionedRepairSpecifications`. `origin/main`'s newest migration is `20260814094632_DropBoxFileRequests` = the production head, so production and `main` agree [verified: `git ls-tree -r origin/dev|origin/main` vs `__EFMigrationsHistory`].
- `infra/` and `azure.yaml`: **no diff** between `origin/main` and `origin/dev` [verified: `git diff --name-only origin/main origin/dev -- infra/ azure.yaml` → empty]. `scripts/` diff is limited to `Test-MarkdownPlacement.ps1`/`Test-TestMarkdownPlacement.ps1` (docs gate), not release scripts.
- `src/` diff main..dev: 38 files, +22 089/−56; `src/Pegasus.Worker` itself only `packages.lock.json` (+31), but `Pegasus.Core`/`Pegasus.Infrastructure` change, so a new Worker package is implied.
- `docs/operations.md` release table vs Azure: release-10 row (date, `d8de29cb…`, `sha256:4bd50f66…`, revision `--d8de29cb94f3`, no migration) and the release-9 row (migrations `20260814092852_…`, `20260814094632_…`) **agree** with Azure and with `__EFMigrationsHistory`. `f1e116c6`'s digest `63e86324…` and the older rows' digests all match ACR.

Drift / discrepancies found (facts, not findings about intent):

1. `docs/operations.md:278` says the Web runs "min 0 max 1 replica — cold start accepted"; Bicep (`infra/modules/platform.bicep:461-462`) and the live app both say **minReplicas 1, maxReplicas 1** (revision `RunningAtMaxScale`, 1 replica).
2. Web identity holds a stale `Azure Service Bus Data Sender` assignment on storage queue `intake-work` (created 2026-08-01) that current Bicep no longer declares — least-privilege residue, not a functional dependency.
3. azd env `BOX_ROOT_FOLDER_ID=392761581105` is stale and unused (Bicep literal `405543781910` governs).
4. Log Analytics daily cap (0.1 GB) is exhausted every day around 11:50 UTC; telemetry-based verification after that time of day is blind until 03:00 UTC.
5. `SentEvidencePollFunction` enabled + `AllowSentEvidence=0` → one failed invocation with an `UnauthorizedAccessException` per minute (see §3); `docs/runbook.md` and `Invoke-ProductionSmoke.ps1` state different expectations for that function.
6. ACR tag `a593bc89…` (2026-08-18 10:45) was pushed but corresponds to no release row — consistent with the "a pushed image is not a release" rule; noted for completeness.

## 5. Implications for release 12 (facts only)

- **Migrations pending:** three (`20260819093019_RetainedMailboxInternetMessageIdentity`, `20260819104953_MailClassificationCorrectionHistory`, `20260819112640_VersionedRepairSpecifications`) — plus any merged after `560f741c`. The route's explicit `efbundle` apply-before-packages step and the `__EFMigrationsHistory` readback are required for this release; the migration head to start from is `20260814094632_DropBoxFileRequests` (45 rows).
- **Infra:** no Bicep/azure.yaml change since the deployed commit; `Test-AzureDeploymentPlan.ps1` Artifact/PreUpload/PreMigration/PreProvision validation still applies. The local azd environment matches the estate for digest/revision/activation/vault URIs this time (unlike releases 8–9); its one stale variable is inert.
- **Worker package:** `Pegasus.Core`/`Infrastructure` change, so a new Worker package must be built and delivered via `az functionapp deployment source config-zip` (the only route that has succeeded on this estate; the `azd deploy --from-package` Oryx failure is on record in the deployment history), followed by the nine-setting census readback and a post-deploy `ApprovedInboxPollStates.LastCompletedAtUtc` advance (currently 2026-08-19 12:21:45 UTC).
- **Web:** new image + digest-pinned single revision + `/diagnostics/version` sourceSha match + `/Cases` https 302 — the smoke contract is unchanged and currently passing for release 10.
- **Observability for the verification window:** the Log Analytics cap trips ~11:50 UTC daily; any release smoke/watch that relies on App Insights must run between 03:00 UTC and the cap, or use the non-telemetry readbacks (`/diagnostics/version`, function host status via admin key, SQL poll-state) the runbook already names. The standing once-a-minute `SentEvidencePollFunction` exception will remain in the exception counts unless the data/config state changes; any "zero exceptions" post-release assertion must account for it.
- **Docs to refresh at release:** the `operations.md` replica statement (min 1, not 0), the release table (new row), and the Worker activation paragraph if the sent-evidence state changes.
