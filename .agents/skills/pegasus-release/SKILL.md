---
name: pegasus-release
description: Promote and release Pegasus through its authorised terminal route, or perform a promotion-only update without redeploying unchanged application code. Use for Pegasus production promotion, deployment, rollback, or release verification.
---

# Release Pegasus

Use the repository scripts and the exact source SHA. `azd up`, `azd deploy web`
and `azd deploy worker` are not release procedures.

Run the route from an authorised Windows x64 or Linux x64 PowerShell 7 terminal
using that platform's native tools and storage throughout the run. Before
preflight, dot-source `scripts/PegasusPlatform.ps1` and call
`Get-PegasusMigrationBundle` to verify the supported workstation and its bundle
identity. Require both `az account show` and `azd auth login --check-status` to
identify the intended operator. Authentication is not write approval. Deployed
Web and Worker remain Linux; Web is a code-deployed App Service Web App
(ADR-0049) and the Worker a Flex Consumption Function App. No container image,
registry or image tooling is part of the route.

The [index](../../../docs/index.md) identifies each policy owner. This skill
owns the release procedure; engineering owns verification policy and operations
owns dated observations. Resolve conflicts within those scopes before the
affected operation.

## Fixed production target

| Target | Name |
| --- | --- |
| Subscription | `e6076573-23a5-46a8-acef-7e22d264e5db` |
| Tenant | `858cf5b3-aa0a-47a6-9b40-4851fd0afa94` |
| Resource group | `rg-pegasus-prod` |
| azd environment | `pegasus-prod` |
| Web App | `pegasus-prod-web-252ow37gij` |
| Web plan | `pegasus-prod-web-plan-252ow37gij` (Linux, B1) |
| Web public origin | `https://pegasus-prod-web-252ow37gij.azurewebsites.net/` |
| Worker | `pegasus-prod-worker-252ow37gij` |
| Key Vault | `pegasusprodkv252ow37g` |
| SQL | `pegasus-prod-sql-252ow37gij` / `pegasus` |
| App Insights | `pegasus-prod-appi-252ow37gij` |

The Web plan and Web App sit in the platform region unless
`PEGASUS_WEB_LOCATION` places them elsewhere (section 8). Every other resource
stays in the platform region. Until the first App Service release completes,
the retired Container App `pegasus-prod-web-252ow37gij` in the Container Apps
environment and the registry `pegasusprodacr252ow37gij` still exist; the
[cutover section](#12-one-time-cutover-from-the-container-app) owns them.

Read-only GitHub and Azure checks need no approval. A `dev` to `main` update
needs fresh `MERGE AUTH GRANTED` immediately before the push. Every Azure or
database write needs explicit approval naming the exact targets and operation.
Artifact approval does not grant either permission.

Use PowerShell 7 throughout. Set these once and pass the environment explicitly:

```powershell
$releaseEnvironment = 'pegasus-prod'
$subscriptionId = 'e6076573-23a5-46a8-acef-7e22d264e5db'
$resourceGroup = 'rg-pegasus-prod'
$webApp = 'pegasus-prod-web-252ow37gij'
$webPlan = 'pegasus-prod-web-plan-252ow37gij'
$webOrigin = 'https://pegasus-prod-web-252ow37gij.azurewebsites.net/'
$workerApp = 'pegasus-prod-worker-252ow37gij'
$version = '0.1.0-alpha.1'
```

## 1. Choose the route

Inspect `origin/main..origin/dev` before doing anything else.

- **Promotion only:** no deployable application, infrastructure, migration,
  dependency, or runtime-configuration change. Promote the exact SHA, update no
  Azure resource, and stop. Documentation, tests and release-tooling changes
  alone are not a new deployed release.
- **Full release:** any deployable application, infrastructure, migration,
  dependency, or runtime-configuration change. Follow the one applicable route
  selected in section 6.
- **First App Service release:** a full release that also executes
  [section 12](#12-one-time-cutover-from-the-container-app) once.
- **Rollback or diagnosis:** read
  [references/troubleshooting.md](references/troubleshooting.md) only when the
  normal route fails or rollback is requested.

## 2. Preflight the candidate

Do not alter the caller's checkout. Fetch, record both remote heads, verify the
fast-forward, and inspect every commit and merged PR in the candidate range.
Every included task PR must have passed its required review and CI unless the
operator has explicitly waived that exact check for that exact PR.

```powershell
git fetch origin --prune
$mainSha = (git rev-parse origin/main).Trim()
$releaseSha = (git rev-parse origin/dev).Trim()
git merge-base --is-ancestor $mainSha $releaseSha
if ($LASTEXITCODE -ne 0) { throw 'origin/main is not an ancestor of origin/dev.' }
git log --oneline --decorate "$mainSha..$releaseSha"
git diff --stat "$mainSha..$releaseSha"
```

Read the deployed state before requesting approval. The Web App reports its
state and stack; the running bytes identify themselves at
`/diagnostics/version` (anonymous, read-only):

```powershell
az account show --query '{subscription:id,tenant:tenantId}' --output json
az webapp show --subscription $subscriptionId `
  --resource-group $resourceGroup --name $webApp `
  --query '{state:state,stack:siteConfig.linuxFxVersion,host:defaultHostName}' --output json
Invoke-RestMethod -Uri ([uri]::new([uri]$webOrigin, 'diagnostics/version')) | ConvertTo-Json
az functionapp config appsettings list --subscription $subscriptionId `
  --resource-group $resourceGroup --name $workerApp `
  --query "[?contains(name,'Schedule') || starts_with(name,'AzureWebJobs.')].{name:name,value:value}" --output json
```

Before the first App Service release the Web App does not exist and
`az webapp show` fails; read the Container App instead as section 12 describes.

For a promotion-only change, obtain fresh `MERGE AUTH GRANTED`, perform section
3, verify both remote refs, and stop without building or writing Azure state.

Before promoting a full-release candidate, building its artifacts, requesting
live-write approval, or changing any Azure state, classify its migration against
the deployed migration identity. Record `unchanged`, `additive`, or
`destructive`; for a destructive change, identify each non-additive operation,
the exact affected capability, and the forward-only recovery boundary. Resolve
uncertainty before proceeding. The destructive classification also requires a
concrete approved short-outage window; after actual release it must be outside
typical usage, but this procedure does not invent standing hours. Approval later
binds that recorded route, exact manifest, exact targets and, for destructive
containment, the exact source SHA the old serving Web workload reports at
`/diagnostics/version` immediately before containment. For a first App Service
destructive cutover, that workload is the Container App and approval also names
all of its active revisions.

## 3. Promote the reviewed exact SHA

Immediately after fresh `MERGE AUTH GRANTED`, use the already recorded SHA. Do
not recalculate it after approval.

```powershell
git push --atomic --force-with-lease="refs/heads/dev:$releaseSha" origin `
  "${releaseSha}:refs/heads/main" "${releaseSha}:refs/heads/dev"
if ($LASTEXITCODE -ne 0) { throw 'Atomic promotion failed.' }
git fetch origin --prune
$promotedMain = (git rev-parse origin/main).Trim()
$promotedDev = (git rev-parse origin/dev).Trim()
if ($promotedMain -ne $releaseSha -or $promotedDev -ne $releaseSha) {
  throw 'Remote read-back does not equal the approved release SHA.'
}
```

The lease is an equality guard, not permission to rewrite history. Never retry a
failure with rebase, reset, an unleased force push, or a different SHA.

## 4. Build in an isolated exact-SHA worktree

Create a disposable detached worktree outside the caller's checkout. The ignored
azd environment is required in that worktree; copy only `.azure/pegasus-prod`
from the existing repository checkout and verify it before use.

```powershell
$gitCommonDirectory = (git rev-parse --path-format=absolute --git-common-dir).Trim()
$primaryRepository = Split-Path -Parent $gitCommonDirectory
$releaseRoot = Join-Path (Split-Path -Parent $primaryRepository) "pegasus-worktrees/release-$($releaseSha.Substring(0,8))"
$environmentSource = Join-Path $primaryRepository '.azure/pegasus-prod'
if (-not (Test-Path -LiteralPath $environmentSource)) { throw 'The pegasus-prod azd environment is unavailable.' }
git worktree add --detach $releaseRoot $releaseSha
New-Item -ItemType Directory -Force -Path (Join-Path $releaseRoot '.azure') | Out-Null
Copy-Item -Recurse -LiteralPath $environmentSource `
  -Destination (Join-Path $releaseRoot '.azure/pegasus-prod')
Set-Location $releaseRoot
if ((git status --porcelain).Count -ne 0) { throw 'Release worktree is not clean.' }
pwsh ./scripts/Build-ReleaseArtifacts.ps1 -Version $version -SourceRevision $releaseSha
$manifestPath = "artifacts/releases/$version/release-manifest.json"
pwsh ./scripts/Test-AzureDeploymentPlan.ps1 -Mode Local
pwsh ./scripts/Test-AzureDeploymentPlan.ps1 -Mode Artifact -ManifestPath $manifestPath
$manifestSha256 = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash
$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json -Depth 10
$webPackagePath = "artifacts/releases/$version/web.zip"
$webPackageSha256 = @($manifest.artifacts | Where-Object name -eq 'web.zip')[0].sha256
```

The manifest must use schema 3. Its `migrationRuntimeIdentifier` and
`migrationBundleName` must match the workstation: `win-x64`/`efbundle.exe` on
Windows or `linux-x64`/`efbundle` on Linux. The three artifacts are `web.zip`,
`worker.zip` and that migration bundle. `web.zip` is a framework-dependent
Linux x64 publish for the platform `DOTNETCORE|10.0` stack with
`Pegasus.Web.dll` at its root; `worker.zip` targets Linux x64 and carries
`.azurefunctions/`. Build and migrate on the same workstation platform; do not
rename the bundle.

Record the manifest SHA-256, source SHA, `web.zip` SHA-256, migration identity
and exact Azure operations. Obtain explicit approval for that manifest and those
targets before the first Azure write.

## 5. Validate the approved artifacts and define the Web read-back

```powershell
pwsh ./scripts/Test-AzureDeploymentPlan.ps1 -Mode PreDeploy `
  -ManifestPath $manifestPath -ManifestSha256 $manifestSha256
```

The `[uri]$webOrigin` cast matters: with a string first argument PowerShell 7 binds the obsolete `Uri(string, bool)` overload, drops the relative path and probes `/`, which redirects to sign-in and makes the wait report a healthy site as not ready (Releases 52 and 54).
The Web App runs from the deployed package and identifies its bytes at
`/diagnostics/version`. The read-back below requires the site `Running`,
`/health/ready` answering 200 and the version endpoint reporting the exact
release SHA and version. A site that reports an earlier SHA is still serving the
previous package; a site whose health check fails is recycled by the platform
and is not evidence.

```powershell
function Wait-PegasusExpectedWebSite {
  param(
    [Parameter(Mandatory)][string] $ExpectedSourceRevision,
    [Parameter(Mandatory)][string] $ExpectedVersion
  )

  $handler = [Net.Http.HttpClientHandler]::new()
  $handler.AllowAutoRedirect = $false
  $client = [Net.Http.HttpClient]::new($handler)
  $client.Timeout = [TimeSpan]::FromSeconds(30)
  try {
    for ($attempt = 1; $attempt -le 24; $attempt++) {
      $siteJson = az webapp show --subscription $subscriptionId `
        --resource-group $resourceGroup --name $webApp `
        --query '{state:state,stack:siteConfig.linuxFxVersion}' --output json
      if ($LASTEXITCODE -ne 0) { throw 'Unable to read the Web App state.' }
      try { $site = ($siteJson -join "`n") | ConvertFrom-Json }
      catch { throw 'Web App state read-back was not valid JSON.' }
      if ([string]$site.stack -cne 'DOTNETCORE|10.0') { throw "Web App stack is '$($site.stack)', not DOTNETCORE|10.0." }
      if ([string]$site.state -ceq 'Running') {
        $ready = $null
        $reported = $null
        try {
          $ready = $client.GetAsync([uri]::new([uri]$webOrigin, 'health/ready')).GetAwaiter().GetResult()
          if ($ready.IsSuccessStatusCode) {
            $reported = $client.GetStringAsync([uri]::new([uri]$webOrigin, 'diagnostics/version')).GetAwaiter().GetResult() | ConvertFrom-Json
          }
        }
        catch { $reported = $null }
        if ($null -ne $reported -and
            $reported.sourceSha -ceq $ExpectedSourceRevision -and
            $reported.version -ceq $ExpectedVersion) { return }
      }
      elseif ([string]$site.state -cnotin @('Stopped', 'Starting', 'Running')) {
        throw "Web App reported an unexpected state '$($site.state)'."
      }
      if ($attempt -lt 24) { Start-Sleep -Seconds 5 }
    }
    throw 'The Web App did not become Running, ready and at the approved release within the bounded wait.'
  }
  finally {
    $client.Dispose()
  }
}
```

## 6. Classify the migration route

Execute the migration classification recorded in section 2. Do not run migration
or bootstrap when the identity is unchanged. If the candidate, deployed identity,
approved targets, or approved old serving-Web source SHA changed since
classification, stop and obtain a fresh classification and approval.

For an additive migration, read and follow
[references/database-migration.md](references/database-migration.md): migration
and runtime grants finish before provisioning Web or deploying Worker.

Choose exactly one route. An unchanged identity, or an additive identity after
that recipe succeeds, executes section 8 and then section 11. A destructive
identity with an existing Web App executes section 7, the migration recipe,
section 9, section 10, and then section 11. A first App Service destructive
cutover executes section 12.3's dedicated route instead. Do not execute the
other route's deployment steps.

For a destructive migration, obtain approval for a short Web and Worker outage,
the exact manifest and exact targets. After actual release, the approved window
must be outside typical usage; record the concrete window then, without
inventing standing hours here. Confirm the candidate has no schema-dependent
startup/background work outside disabled Functions and that its Flex update
strategy is `Recreate` or the documented default. A `RollingUpdate`, unproved
strategy, or inventory drift stops the operation.

For a destructive route with an existing Web App, before any write re-read the
exact target inventory, require the Web App `Running` on `DOTNETCORE|10.0`, and
bind the source SHA it reports to the exact `$approvedOldWebSourceSha` named in
the approval. The first App Service destructive cutover has no Web App to read:
it must not run this App Service precondition or section 7's Web App containment
block, and instead follows section 12.3.

The destructive route has one bounded staging exception: while the old schema is
still intact, install the approved **new** Worker package with every Worker
Function disabled. This avoids assuming a stopped-Flex deployment state and is
not business activation. Disabled settings suppress triggers only; they do not
prove that a Function host cannot initialize. Do not use Function master keys or
the portal to invoke any Function during maintenance.

```powershell
$webSiteJson = az webapp show --subscription $subscriptionId `
  --resource-group $resourceGroup --name $webApp `
  --query '{state:state,stack:siteConfig.linuxFxVersion,host:defaultHostName}' --output json
if ($LASTEXITCODE -ne 0) { throw 'Unable to read the Web App.' }
if ([string]::IsNullOrWhiteSpace($approvedOldWebSourceSha)) {
  throw 'Destructive migration approval must name the exact old Web source SHA.'
}
try { $webSite = ($webSiteJson -join "`n") | ConvertFrom-Json }
catch { throw 'Web App read-back was not valid JSON.' }
if ([string]$webSite.state -cne 'Running' -or [string]$webSite.stack -cne 'DOTNETCORE|10.0') {
  throw 'Destructive migration requires the Web App Running on DOTNETCORE|10.0 before containment.'
}
if ("https://$($webSite.host)/" -cne $webOrigin) { throw 'Web App hostname differs from the fixed target.' }
$oldWebVersion = Invoke-RestMethod -Uri ([uri]::new([uri]$webOrigin, 'diagnostics/version'))
$oldWebSourceSha = [string]$oldWebVersion.sourceSha
if ($oldWebSourceSha -cne $approvedOldWebSourceSha) {
  throw 'The Web App reports a source SHA that differs from the exact approved old SHA.'
}
```

## 7. Destructive route: stage, contain, then migrate

Dot-source `scripts/PegasusPlatform.ps1` and use its canonical Worker Disabled
setting census. Do not duplicate the setting names. Set every census member to
`true`, suppress the setting response, check native exit, and run the full
Worker-only disabled smoke (never `-ActivationOnly`).

```powershell
. ./scripts/PegasusPlatform.ps1
$workerDisabledSettings = @(Get-PegasusWorkerDisabledSettingNames)
$disableArguments = @($workerDisabledSettings | ForEach-Object { "$_=true" })
az functionapp config appsettings set --subscription $subscriptionId `
  --resource-group $resourceGroup --name $workerApp --settings $disableArguments --output none
if ($LASTEXITCODE -ne 0) { throw 'Unable to disable the exact Worker census.' }
pwsh ./scripts/Invoke-ProductionSmoke.ps1 -WorkerOnly `
  -ResourceGroupName $resourceGroup -SubscriptionId $subscriptionId `
  -ExpectedWorkerActivation disabled
if ($LASTEXITCODE -ne 0) { throw 'Worker disabled census smoke failed.' }

az functionapp deployment source config-zip --subscription $subscriptionId `
  --resource-group $resourceGroup --name $workerApp `
  --src "./artifacts/releases/$version/worker.zip"
if ($LASTEXITCODE -ne 0) { throw 'Approved Worker package staging failed.' }
pwsh ./scripts/Invoke-ProductionSmoke.ps1 -WorkerOnly `
  -ResourceGroupName $resourceGroup -SubscriptionId $subscriptionId `
  -ExpectedWorkerActivation disabled
if ($LASTEXITCODE -ne 0) { throw 'Staged Worker disabled census smoke failed.' }
```

Staging success proves only that the approved new package was staged. Never use
`azd deploy worker`; it invokes a remote Oryx build against an already-published
package. Stop the Function App and require a bounded `Stopped` read-back. Then
stop the Web App; bounded polling must prove the site `Stopped` and its public
origin no longer serving the application (`/health/live` must not answer 2xx; a
stopped site answers 403). Every Azure command or JSON parse failure stops the
route. An unhealthy host, trigger-disabled setting, or missing response is not
containment evidence. Stopping the site does not stop its deployment (Kudu)
endpoint, which section 9 relies on.

```powershell
az functionapp stop --subscription $subscriptionId --resource-group $resourceGroup --name $workerApp
if ($LASTEXITCODE -ne 0) { throw 'Worker stop failed.' }
$workerStopped = $false
for ($attempt = 1; $attempt -le 12; $attempt++) {
  $workerState = (az resource show --subscription $subscriptionId `
    --resource-group $resourceGroup --name $workerApp --resource-type 'Microsoft.Web/sites' `
    --api-version 2024-04-01 --query properties.state --output tsv).Trim()
  if ($LASTEXITCODE -ne 0) { throw 'Unable to read Worker state.' }
  if ($workerState -ceq 'Stopped') { $workerStopped = $true; break }
  Start-Sleep -Seconds 5
}
if (-not $workerStopped) { throw 'Worker did not read back as Stopped.' }

az webapp stop --subscription $subscriptionId --resource-group $resourceGroup --name $webApp --output none
if ($LASTEXITCODE -ne 0) { throw "Unable to stop $webApp." }
$webContained = $false
$handler = [Net.Http.HttpClientHandler]::new()
$handler.AllowAutoRedirect = $false
$probe = [Net.Http.HttpClient]::new($handler)
$probe.Timeout = [TimeSpan]::FromSeconds(30)
try {
  for ($attempt = 1; $attempt -le 12; $attempt++) {
    $webState = (az webapp show --subscription $subscriptionId `
      --resource-group $resourceGroup --name $webApp --query state --output tsv).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Unable to read Web App state.' }
    $serving = $true
    try {
      $live = $probe.GetAsync([uri]::new([uri]$webOrigin, 'health/live')).GetAwaiter().GetResult()
      $serving = $live.IsSuccessStatusCode
    }
    catch { $serving = $false }
    if ($webState -ceq 'Stopped' -and -not $serving) { $webContained = $true; break }
    if ($webState -cnotin @('Stopped', 'Running')) { throw "Web App reported an unexpected state '$webState' during containment." }
    Start-Sleep -Seconds 5
  }
}
finally {
  $probe.Dispose()
}
if (-not $webContained) { throw 'Old Web did not read back Stopped and unserved.' }
```

Immediately before SQL, take a fresh containment read-back rather than relying
on the earlier polling result:

```powershell
$preSqlWorkerState = (az resource show --subscription $subscriptionId `
  --resource-group $resourceGroup --name $workerApp --resource-type 'Microsoft.Web/sites' `
  --api-version 2024-04-01 --query properties.state --output tsv).Trim()
if ($LASTEXITCODE -ne 0 -or $preSqlWorkerState -cne 'Stopped') {
  throw 'Worker is not freshly confirmed Stopped before destructive SQL.'
}
$preSqlWebState = (az webapp show --subscription $subscriptionId `
  --resource-group $resourceGroup --name $webApp --query state --output tsv).Trim()
if ($LASTEXITCODE -ne 0 -or $preSqlWebState -cne 'Stopped') {
  throw 'Web App is not freshly confirmed Stopped before destructive SQL.'
}
$preSqlServing = $true
try {
  $preSqlLive = Invoke-WebRequest -Uri ([uri]::new([uri]$webOrigin, 'health/live')) -MaximumRedirection 0 -SkipHttpErrorCheck
  $preSqlServing = [int]$preSqlLive.StatusCode -ge 200 -and [int]$preSqlLive.StatusCode -lt 300
}
catch { $preSqlServing = $false }
if ($preSqlServing) { throw 'Web App is still serving the application before destructive SQL.' }
```

Then follow [references/database-migration.md](references/database-migration.md).
If SQL, grants, or migration-head verification fails, leave the Worker stopped,
the Web App stopped and every Disabled setting true. From this point recover
forward only: never start the old Worker package or the old Web package against
changed or unknown schema.

## 8. Normal route: provision, deploy Web, then deploy Worker

For an unchanged migration identity, or after the additive migration recipe
succeeds, retain the ordinary ordering: provision the approved infrastructure
with Web activation approved and the Worker approved live, deploy the approved
`web.zip` to the Web App, wait for the exact release to answer, deploy the
approved Worker ZIP and run the full smoke. Do not use the destructive
containment steps or its disabled-first staging exception for this route.

`PEGASUS_WEB_LOCATION` is set only when the approval names a Web region other
than the platform region (the quota route chosen in section 12); leave it unset
otherwise. `PreProvision` reads the App Service quota for the effective Web
region and refuses a region without quota for the plan SKU. `az webapp deploy`
authenticates with the operator's Entra token; basic publishing credentials are
disabled by the template. `--clean true` removes the previous package contents
and `--restart true` restarts the site onto the new package.

```powershell
azd env set PEGASUS_WEB_ACTIVATION approved -e $releaseEnvironment
if ($LASTEXITCODE -ne 0) { throw 'Unable to set normal-route Web activation.' }
azd env set PEGASUS_WORKER_ACTIVATION approved-live-worker -e $releaseEnvironment
if ($LASTEXITCODE -ne 0) { throw 'Unable to set normal-route Worker activation.' }
pwsh ./scripts/Test-AzureDeploymentPlan.ps1 -Mode PreProvision `
  -Environment $releaseEnvironment -ManifestPath $manifestPath `
  -WorkerActivation approved-live-worker -ExpectedLiveWorkerActivation approved-live-worker
if ($LASTEXITCODE -ne 0) { throw 'Normal-route pre-provision validation failed.' }
azd provision -e $releaseEnvironment --no-prompt
if ($LASTEXITCODE -ne 0) { throw 'Normal-route provisioning failed.' }
az webapp deploy --subscription $subscriptionId `
  --resource-group $resourceGroup --name $webApp `
  --src-path $webPackagePath --type zip --clean true --restart true --output none
if ($LASTEXITCODE -ne 0) { throw 'Normal-route Web package deployment failed.' }
Wait-PegasusExpectedWebSite -ExpectedSourceRevision $releaseSha -ExpectedVersion $version
az functionapp deployment source config-zip --subscription $subscriptionId `
  --resource-group $resourceGroup --name $workerApp `
  --src "./artifacts/releases/$version/worker.zip"
if ($LASTEXITCODE -ne 0) { throw 'Normal-route Worker deployment failed.' }
pwsh ./scripts/Invoke-ProductionSmoke.ps1 `
  -BaseUri $webOrigin `
  -ExpectedSourceRevision $releaseSha -ExpectedVersion $version `
  -ExpectedWebPackageSha256 $webPackageSha256 `
  -ResourceGroupName $resourceGroup -SubscriptionId $subscriptionId `
  -ExpectedWorkerActivation approved-live-worker
if ($LASTEXITCODE -ne 0) { throw 'Normal-route exact release smoke failed.' }
```

Provisioning may restart the site when it changes app settings; on this route
that restart runs the previous package against a compatible schema and is
acceptable. The Web package deployment, not provisioning, changes the served
bytes.

## 9. Destructive route: deploy the new Web package, then provision with Worker disabled

Read the azd environment and refuse stale or wrong targets. Every secret URI
must name `pegasusprodkv252ow37g`; `AZURE_RESOURCE_GROUP` must be
`rg-pegasus-prod`. After verified migration, grants and migration head, first
deploy the approved `web.zip` to the **stopped** Web App so that no start,
however caused, can run the old package against the new schema: the deployment
endpoint stays available while the site is stopped, and `--restart false` leaves
the site stopped. Then set Web activation to `approved`, retain the desired
Worker value as `disabled`, and provision. `PreProvision` must observe the
Worker disabled before provision. Check each native exit and run the full
Worker-only disabled smoke. A configuration update may start the Function
host, but its approved new bytes were staged before SQL; do not deploy the
Worker ZIP again. The Web App is started only in section 10.

```powershell
$stagedWebState = (az webapp show --subscription $subscriptionId `
  --resource-group $resourceGroup --name $webApp --query state --output tsv).Trim()
if ($LASTEXITCODE -ne 0 -or $stagedWebState -cne 'Stopped') {
  throw 'The Web App must read back Stopped before the new package is deployed.'
}
az webapp deploy --subscription $subscriptionId `
  --resource-group $resourceGroup --name $webApp `
  --src-path $webPackagePath --type zip --clean true --restart false `
  --track-status false --output none
if ($LASTEXITCODE -ne 0) { throw 'New Web package deployment failed.' }
$postDeployWebState = (az webapp show --subscription $subscriptionId `
  --resource-group $resourceGroup --name $webApp --query state --output tsv).Trim()
if ($LASTEXITCODE -ne 0 -or $postDeployWebState -cne 'Stopped') {
  throw 'The Web App did not remain Stopped after the package deployment.'
}
azd env set PEGASUS_WEB_ACTIVATION approved -e $releaseEnvironment
if ($LASTEXITCODE -ne 0) { throw 'Unable to set approved Web activation.' }
azd env set PEGASUS_WORKER_ACTIVATION disabled -e $releaseEnvironment
if ($LASTEXITCODE -ne 0) { throw 'Unable to retain disabled Worker activation.' }
pwsh ./scripts/Test-AzureDeploymentPlan.ps1 -Mode PreProvision `
  -Environment $releaseEnvironment -ManifestPath $manifestPath `
  -WorkerActivation disabled -ExpectedLiveWorkerActivation disabled
if ($LASTEXITCODE -ne 0) { throw 'Disabled Worker pre-provision validation failed.' }
azd provision -e $releaseEnvironment --no-prompt
if ($LASTEXITCODE -ne 0) { throw 'New Web provisioning failed.' }
pwsh ./scripts/Invoke-ProductionSmoke.ps1 -WorkerOnly `
  -ResourceGroupName $resourceGroup -SubscriptionId $subscriptionId `
  -ExpectedWorkerActivation disabled
if ($LASTEXITCODE -ne 0) { throw 'New Worker disabled census smoke failed.' }
```

## 10. Explicitly activate the compatible release

Keep the same approved manifest. Set only
`PEGASUS_WORKER_ACTIVATION=approved-live-worker`, preflight against the observed
disabled Worker, and provision once. Start the Web App and require the exact
release read-back of section 5. If the Worker remains stopped, start the exact
approved target and require a `Running` read-back. Completion requires the Web
App `Running` at the approved release, the Worker `Running`, every Disabled
value `false`, and a successful full smoke. If any of those fail, report an
unfinished outage; do not call the release successful or revive old bytes.

```powershell
azd env set PEGASUS_WORKER_ACTIVATION approved-live-worker -e $releaseEnvironment
if ($LASTEXITCODE -ne 0) { throw 'Unable to set approved Worker activation.' }
pwsh ./scripts/Test-AzureDeploymentPlan.ps1 -Mode PreProvision `
  -Environment $releaseEnvironment -ManifestPath $manifestPath `
  -WorkerActivation approved-live-worker -ExpectedLiveWorkerActivation disabled
if ($LASTEXITCODE -ne 0) { throw 'Worker activation pre-provision validation failed.' }
azd provision -e $releaseEnvironment --no-prompt
if ($LASTEXITCODE -ne 0) { throw 'Compatible release activation failed.' }
az webapp start --subscription $subscriptionId --resource-group $resourceGroup --name $webApp --output none
if ($LASTEXITCODE -ne 0) { throw 'Web App start failed.' }
Wait-PegasusExpectedWebSite -ExpectedSourceRevision $releaseSha -ExpectedVersion $version
$workerState = (az resource show --subscription $subscriptionId `
  --resource-group $resourceGroup --name $workerApp --resource-type 'Microsoft.Web/sites' `
  --api-version 2024-04-01 --query properties.state --output tsv).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Unable to read Worker state after activation.' }
if ($workerState -cne 'Running' -and $workerState -cne 'Stopped') {
  throw "Worker state after activation is neither Running nor Stopped: '$workerState'."
}
if ($workerState -ceq 'Stopped') {
  az functionapp start --subscription $subscriptionId --resource-group $resourceGroup --name $workerApp
  if ($LASTEXITCODE -ne 0) { throw 'Worker start failed.' }
}
$workerRunning = $false
for ($attempt = 1; $attempt -le 12; $attempt++) {
  $workerState = (az resource show --subscription $subscriptionId `
    --resource-group $resourceGroup --name $workerApp --resource-type 'Microsoft.Web/sites' `
    --api-version 2024-04-01 --query properties.state --output tsv).Trim()
  if ($LASTEXITCODE -ne 0) { throw 'Unable to read Worker state after start.' }
  if ($workerState -ceq 'Running') { $workerRunning = $true; break }
  Start-Sleep -Seconds 5
}
if (-not $workerRunning) { throw 'Worker did not read back as Running.' }
pwsh ./scripts/Invoke-ProductionSmoke.ps1 `
  -BaseUri $webOrigin `
  -ExpectedSourceRevision $releaseSha -ExpectedVersion $version `
  -ExpectedWebPackageSha256 $webPackageSha256 `
  -ResourceGroupName $resourceGroup -SubscriptionId $subscriptionId `
  -ExpectedWorkerActivation approved-live-worker
if ($LASTEXITCODE -ne 0) { throw 'Exact release smoke failed.' }
```

The scripts at the released SHA own the exact Worker function and schedule
census. Do not duplicate a function count in the skill. The full smoke reads
the Web App state, stack, run-from-package setting and last deployment record,
compares the deployed package bytes with the approved `web.zip` when the
deployment endpoint exposes them and says so when it cannot, and proves the
running bytes at `/diagnostics/version`. It also reads the production database
(read-only) and fails unless an intake mailbox is activated, an unexpired
`Active` Graph subscription exists, and an inbound poll completed within 15
minutes. Smoke proves the right bytes, configuration, and intake liveness, not
the changed user journey. Run only the focused live behavioural check required
by the released change and record its result without overclaiming.

## 11. Record and retain evidence

Update `docs/operations.md` with the dated observed artifact, configuration,
migration and activation evidence. Update `docs/current-architecture.md` only
when source structure or composition changed; do not repeat operational artifact
identities there. Link retained attempts and failures from the release record.

Copy `artifacts/releases/$version` outside the disposable worktree before
removing it. The release is unfinished until operations records the deployed observation
and any source-structure change is reflected in current-architecture.

## 12. One-time cutover from the Container App

The first App Service release replaces the Container App with the Web App
under the same public-origin dependencies. Execute this section once, inside
that release, with the normal route of section 8 (or the destructive route
when the candidate also carries a destructive migration). Every write below
needs explicit operator approval naming the exact target and operation; the
list is the approval request, not the grant.

### 12.1 Pre-flight: App Service quota

On 2026-09-13 a read-only quota check found the subscription's App Service VM
quota in the platform region `uksouth` at 0 for every SKU, `B1` included,
while `ukwest` and `westeurope` carried an aggregate quota of 30. Provisioning
a B1 plan in a region with quota 0 fails. Read the current figures before
provision and choose one route:

```powershell
foreach ($region in @('uksouth', 'ukwest')) {
  $quota = az rest --method get --url "https://management.azure.com/subscriptions/$subscriptionId/providers/Microsoft.Web/locations/$region/providers/Microsoft.Quota/quotas?api-version=2023-02-01" --output json | ConvertFrom-Json
  "$region : " + (($quota.value | Where-Object { $_.name -in @('B1', '*') } | ForEach-Object { "$($_.name)=$($_.properties.limit.value)" }) -join ' ')
}
```

- **Quota increase (platform region):** the operator requests a UK South
  App Service `B1` quota increase through the subscription's quota request
  route and waits for the read-back above to show `B1` at 1 or more. The Web
  plan and Web App then sit in `uksouth` with everything else; leave
  `PEGASUS_WEB_LOCATION` unset.
- **Separate Web region:** the operator names a region whose read-back shows
  quota (`ukwest` on 2026-09-13) and the Web plan alone moves there:
  `azd env set PEGASUS_WEB_LOCATION ukwest -e $releaseEnvironment`. SQL,
  storage, Key Vault, telemetry and the Worker stay in `uksouth`; Web traffic
  to them crosses regions. Record the chosen region in the release record.

`Test-AzureDeploymentPlan.ps1 -Mode PreProvision` repeats the read for the
effective Web region and refuses to continue at 0. A quota that reads 0 is
not repaired by retrying the provision.

### 12.2 Read the retiring Container App

The Web App does not exist yet, so section 2 reads the Container App instead.
Record the active revision, its image digest and the source SHA it reports:

```powershell
$oldOrigin = 'https://pegasus-prod-web-252ow37gij.ashymushroom-676209e5.uksouth.azurecontainerapps.io/'
az containerapp revision list --subscription $subscriptionId `
  --resource-group $resourceGroup --name $webApp `
  --query "[?properties.active].{name:name,image:properties.template.containers[0].image}" --output json
Invoke-RestMethod -Uri ([uri]::new([uri]$oldOrigin, 'diagnostics/version')) | ConvertTo-Json
```

The Container App and the Web App share the name `pegasus-prod-web-252ow37gij`
in different resource types; every command in this skill names the type, so
no command addresses the wrong one.

### 12.3 Provision, deploy and smoke beside the retiring Container App

For an unchanged or additive migration identity, run section 8 as written. The
template no longer declares the Container Apps environment, the Container App,
the registry or its role assignment, and `azd provision` deletes nothing it no
longer declares: they remain in the resource group untouched while the Web App
is created next to them. Provision also re-points the Worker's
`Graph__ChangeNotificationUrl` at the Web App origin. The smoke proves the Web
App serving the exact release at `$webOrigin` while the Container App still
serves the old origin. Set `$firstAppServiceDestructiveCutover = $false` before
the section 12.5 retirement commands.

For a destructive migration, use this first-App-Service route instead of
section 9, which requires an existing stopped App Service Web App. Before any
write, prove no `Microsoft.Web/sites` Web App named `$webApp` exists, record all
active Container App revisions, and bind the old origin's source SHA and the
complete revision list to approval as `$approvedOldContainerAppSourceSha` and
`$approvedOldContainerAppRevisions`. An absent expected revision, an additional
active revision, a Web App appearance, a source-SHA change, or other inventory
drift stops the route and requires fresh approval. Set
`$firstAppServiceDestructiveCutover = $true` for this route so section 12.5
performs that exact fresh comparison before its first containment write. Define
and run this read-only guard before the first Worker or Container App write, then
leave it in the terminal for section 12.5 to run again:

```powershell
function Assert-FirstCutoverLegacyInventory {
  if ([string]::IsNullOrWhiteSpace($approvedOldContainerAppSourceSha) -or
      @($approvedOldContainerAppRevisions).Count -eq 0) {
    throw 'First-cutover approval must name the old Container App source SHA and every active revision.'
  }
  $newWebApps = @(& az resource list --subscription $subscriptionId `
    --resource-group $resourceGroup --resource-type 'Microsoft.Web/sites' `
    --query "[?name == '$webApp'].id" --output tsv | Where-Object {
      -not [string]::IsNullOrWhiteSpace($_)
    })
  if ($LASTEXITCODE -ne 0 -or $newWebApps.Count -ne 0) {
    throw 'First-cutover inventory found an App Service Web App before containment.'
  }
  $oldContainerVersion = Invoke-RestMethod -Uri ([uri]::new([uri]$oldOrigin, 'diagnostics/version'))
  if ([string]$oldContainerVersion.sourceSha -cne $approvedOldContainerAppSourceSha) {
    throw 'The retiring Container App source SHA differs from the approved value.'
  }
  $observedOldRevisions = @(& az containerapp revision list --subscription $subscriptionId `
    --resource-group $resourceGroup --name $webApp `
    --query "[?properties.active].name" --output tsv | Where-Object {
      -not [string]::IsNullOrWhiteSpace($_)
    })
  if ($LASTEXITCODE -ne 0) {
    throw 'Unable to read active retiring Container App revisions.'
  }
  $revisionDifference = @(Compare-Object `
    -ReferenceObject @($approvedOldContainerAppRevisions | Sort-Object) `
    -DifferenceObject @($observedOldRevisions | Sort-Object))
  if ($revisionDifference.Count -ne 0) {
    throw 'The retiring Container App active revision set differs from approval.'
  }
}

Assert-FirstCutoverLegacyInventory
```

If Provider API credentials exist, tell each holder the new base address before
Container App containment. Confirm the update after the new Web App smoke in
section 12.4.

While the old schema remains intact, run section 7 through its Worker `Stopped`
read-back: reuse its canonical Disabled-setting census, stage the approved
Worker ZIP, and stop the Worker. Do not run section 7's App Service Web
containment or its App Service fresh read-back. Then use section 12.5's
containment commands for the old Container App. Before SQL, require fresh proof
that it has no active revisions, each captured revision has no replicas, and
`$oldOrigin/health/live` is not 2xx, together with section 7's Worker `Stopped`
read-back. The existing migration and grant recipe begins only after both
containment checks pass.

Once SQL begins, never reactivate the Container App. If containment, migration,
grants, bootstrap, or migration-head verification fails, leave the Container App
inactive and unserved and the Worker stopped with every Disabled setting true.

Only after the migration recipe succeeds, set
`PEGASUS_WEB_ACTIVATION=approved` and `PEGASUS_WORKER_ACTIVATION=disabled`, run
the existing `PreProvision` check against disabled Worker settings, and run
`azd provision` to create the new empty App Service. Provisioning before SQL is
prohibited on this route. Reuse section 7's Worker stop/read-back if provision
starts the Function host. Stop the new Web App, prove it is `Stopped`, deploy
the approved `web.zip` with `az webapp deploy`, using
`--clean true --restart false --track-status false`, and prove it remains stopped.
Startup tracking defaults
to true on Linux and waits for a deliberately stopped site to start; disabling
that client wait does not change deployment or activation. Then use section 10
unchanged to activate the compatible release. Any later failure leaves the new
Web App stopped, the old Container App inactive, and the Worker stopped.

### 12.4 Re-point every consumer of the public hostname

The public origin changes from the Container App hostname to
`pegasus-prod-web-252ow37gij.azurewebsites.net`. For an unchanged or additive
cutover, perform these steps after the smoke passes and before section 12.5. For
a destructive cutover, the Container App is already unserved before SQL: carry
out these steps after section 10's smoke within the approved migration window.
That approval covers the new public URL, any interval in which old public upload
links are unavailable, and needed operator notifications; do not leave the old
Container App serving while consumers are moved.

1. **Staff sign-in.** Staff authenticate with the application's own cookie
   scheme at `/Account/SignIn`; there is no Entra app registration redirect
   URI for staff and nothing to re-point. Staff use the new origin.
2. **External MCP connector consent.** Pegasus is the OAuth authorization
   server for the automation connector (`/authorize`, `/connect/token`,
   resource `/mcp`). The connector's own redirect URIs
   (`AUTOMATION_MCP_REDIRECT_URIS`) do not change. The connector's registration
   of the Pegasus server URL does: an Administrator re-adds the connector at
   `https://pegasus-prod-web-252ow37gij.azurewebsites.net/mcp` and completes
   consent again, because tokens and the resource metadata at
   `/.well-known/oauth-protected-resource/mcp` are bound to the origin the Web
   App now reports in `AutomationMcp__PublicOrigin`. Tokens issued for the old
   origin are not valid for the new resource.
3. **Graph mail webhook.** The Worker maintains one subscription per approved
   intake mailbox from `Graph__ChangeNotificationUrl`, which provision moved to
   `https://pegasus-prod-web-252ow37gij.azurewebsites.net/hooks/microsoft-graph/mail`.
   Maintenance renews an existing `Active` subscription in place and a renewal
   does not change its notification URL, so every existing subscription keeps
   notifying the old origin until it is re-created. Re-creation happens only
   when a mailbox generation advances. The operator step, per approved intake
   mailbox in `/Administration/Mailboxes`, is: set the mailbox **Disabled**,
   save, then set it **Approved** again and save, in one sitting. The next
   `InboxRecoveryFunction` run (every five minutes) creates a new subscription
   at the new URL. Confirm the current generation's `Active` subscription in
   Mailboxes and read back the Worker's exact `Graph__ChangeNotificationUrl`.
   The generic smoke liveness line alone does not prove either condition:

   ```powershell
   $notificationUrl = (& az functionapp config appsettings list `
     --subscription $subscriptionId --resource-group $resourceGroup --name $workerApp `
     --query "[?name == 'Graph__ChangeNotificationUrl'].value | [0]" --output tsv).Trim()
   if ($LASTEXITCODE -ne 0 -or $notificationUrl -cne "${webOrigin}hooks/microsoft-graph/mail") {
     throw 'Worker Graph notification URL is not the new Web App webhook.'
   }
   ```

   Consequence to state in the release record: re-enable establishes a new start
   boundary, so mail delivered to that mailbox between the disable and the
   re-enable is not backfilled; keep the window seconds long and outside typical
   usage. The superseded Graph subscriptions expire within six days on their own;
   the five-minute recovery poll carries intake throughout, so the webhook change
   affects immediacy, not delivery.
4. **Public upload links.** Links are issued as absolute URLs on the origin
   current at issue time. Every unexpired link issued before cutover carries the
   Container App hostname and stops working when that origin is disabled in
   12.5; a member of staff issues a fresh link from the Case for any outstanding
   request. Links have a seven-day lifetime (`DocumentRequests__LifetimeHours`),
   so the exposure ends within a week of cutover.
5. **Provider API base address.** The Provider API is served at the new origin.
   For the destructive route, holders were notified before containment; after
   the new Web App smoke, confirm each holder has updated the base address.
   For the unchanged or additive route, notify and confirm before the old origin
   is disabled. Credentials do not change.
6. **Glass's return.** `Glass__CallbackBaseUri` is derived from the Web App
   origin by the template. Confirm that deployed setting uses the new origin.
   The launch client supplies the resulting per-session callback in the
   provider launch URL's `caller` parameter; the current integration contract
   defines no provider-account return-address registration to change.

### 12.5 Retire the Container App

For an unchanged or additive cutover, only after 12.3 and 12.4 are complete and
recorded, and with explicit approval naming each target and operation, stop the
old origin. Deactivation keeps the retired app recoverable during the
observation period; deletion is a later approved release. For a destructive
cutover, section 12.3 already performed containment before SQL; confirm and
record the retained inactive state here without reactivating the Container App.

```powershell
if ($firstAppServiceDestructiveCutover) {
  Assert-FirstCutoverLegacyInventory
  $activeOldRevisions = @($approvedOldContainerAppRevisions)
}
else {
  $activeOldRevisions = @(& az containerapp revision list --subscription $subscriptionId `
    --resource-group $resourceGroup --name $webApp `
    --query "[?properties.active].name" --output tsv | Where-Object {
      -not [string]::IsNullOrWhiteSpace($_)
    })
  if ($LASTEXITCODE -ne 0) { throw 'Unable to read active retired Container App revisions.' }
}
foreach ($revision in $activeOldRevisions) {
  if ([string]::IsNullOrWhiteSpace($revision)) { continue }
  az containerapp revision deactivate --subscription $subscriptionId `
    --resource-group $resourceGroup --name $webApp --revision $revision --output none
  if ($LASTEXITCODE -ne 0) { throw "Unable to deactivate retired Container App revision $revision." }
  $retiredReplicas = @(& az containerapp replica list --subscription $subscriptionId `
    --resource-group $resourceGroup --name $webApp --revision $revision `
    --query "[].name" --output tsv | Where-Object {
      -not [string]::IsNullOrWhiteSpace($_)
    })
  if ($LASTEXITCODE -ne 0 -or $retiredReplicas.Count -ne 0) {
    throw "Retired Container App revision $revision still has replicas."
  }
}
az containerapp ingress disable --subscription $subscriptionId --resource-group $resourceGroup `
  --name $webApp --output none
if ($LASTEXITCODE -ne 0) { throw 'Unable to disable the retired Container App ingress.' }
$remainingOldRevisions = @(& az containerapp revision list --subscription $subscriptionId `
  --resource-group $resourceGroup --name $webApp `
  --query "[?properties.active].name" --output tsv | Where-Object {
    -not [string]::IsNullOrWhiteSpace($_)
  })
if ($LASTEXITCODE -ne 0 -or $remainingOldRevisions.Count -ne 0) {
  throw 'The retired Container App still has active revisions.'
}
```

Confirm the old origin no longer answers `/health/live` with 2xx and that the
Web App smoke still passes. In a later, separately approved release, after the
observation period the operator names, delete the retired resources in this
order: `az containerapp delete --name pegasus-prod-web-252ow37gij`,
`az containerapp env delete` for the Container Apps environment in
`rg-pegasus-prod`, and `az acr delete --name pegasusprodacr252ow37gij`. Record
each deletion in operations. A custom domain for the Web App is a separate
operator decision and is not part of this cutover.

## Recovery and diagnostics

Read [recovery and diagnostics](../../../docs/runbook.md) only for diagnosis or a separately authorized recovery operation.
