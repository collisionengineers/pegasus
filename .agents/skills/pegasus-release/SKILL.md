---
name: pegasus-release
description: Promote and release Pegasus through its authorised terminal route, or perform a promotion-only update without redeploying unchanged application code. Use for Pegasus production promotion, deployment, rollback, or release verification.
---

# Release Pegasus

Use the repository scripts and the exact source SHA. `azd up` and `azd deploy
worker` are not release procedures.

Run the route from an authorised Windows x64 or Linux x64 PowerShell 7 terminal
using that platform's native tools and storage throughout the run. Before
preflight, dot-source `scripts/PegasusPlatform.ps1` and call
`Get-PegasusMigrationBundle` to verify the supported workstation and its bundle
identity. Require `oras version` to report 1.3.4, and both `az account show` and
`azd auth login --check-status` to identify the intended operator.
Authentication is not write approval. Deployed Web/Worker remain Linux; no
Windows containers or Docker daemon are needed to build the OCI archive.

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
| Web | `pegasus-prod-web-252ow37gij` |
| Worker | `pegasus-prod-worker-252ow37gij` |
| ACR | `pegasusprodacr252ow37gij` |
| Key Vault | `pegasusprodkv252ow37g` |
| SQL | `pegasus-prod-sql-252ow37gij` / `pegasus` |
| App Insights | `pegasus-prod-appi-252ow37gij` |

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
$workerApp = 'pegasus-prod-worker-252ow37gij'
$registry = 'pegasusprodacr252ow37gij'
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

Read the deployed state before requesting approval:

```powershell
az account show --query '{subscription:id,tenant:tenantId}' --output json
az containerapp revision list --subscription $subscriptionId `
  --resource-group $resourceGroup --name $webApp `
  --query "[?properties.active].{name:name,image:properties.template.containers[0].image}" --output json
az functionapp config appsettings list --subscription $subscriptionId `
  --resource-group $resourceGroup --name $workerApp `
  --query "[?contains(name,'Schedule') || starts_with(name,'AzureWebJobs.')].{name:name,value:value}" --output json
```

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
containment, the exact currently active Web revision.

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
```

The manifest must use schema 3. Its `migrationRuntimeIdentifier` and
`migrationBundleName` must match the workstation: `win-x64`/`efbundle.exe` on
Windows or `linux-x64`/`efbundle` on Linux. The four artifacts are `web.zip`,
`worker.zip`, `web-image.tar.gz` and that migration bundle. The deployed
packages still target Linux x64 and the OCI image must inspect as linux/amd64.
Build and migrate on the same workstation platform; do not rename the bundle.

Record the manifest SHA-256, source SHA, image digest, migration identity and
exact Azure operations. Obtain explicit approval for that manifest and those
targets before the first Azure write.

## 5. Validate and upload the approved image

```powershell
pwsh ./scripts/Test-AzureDeploymentPlan.ps1 -Mode PreUpload `
  -ManifestPath $manifestPath -ManifestSha256 $manifestSha256
$token = az acr login --subscription $subscriptionId --name $registry --expose-token --output json | ConvertFrom-Json
$token.accessToken | oras login $token.loginServer `
  --username '00000000-0000-0000-0000-000000000000' --password-stdin
oras cp --from-oci-layout "artifacts/releases/$version/web-image.tar.gz:$releaseSha" `
  "$($token.loginServer)/pegasus/web:$releaseSha"
$remoteImage = oras manifest fetch "$($token.loginServer)/pegasus/web:$releaseSha" `
  --descriptor | ConvertFrom-Json
if ($remoteImage.digest -ne $manifest.webImage.digest) {
  throw 'Uploaded Web digest differs from the approved manifest.'
}
```

The uploaded digest must equal `webImage.digest` in the approved manifest.

`RunningAtMaxScale` is a normal running state: Azure has started the maximum
configured replica count. Accept it only alongside the same exact revision,
digest, `Healthy` and `Provisioned` checks as `Running`; see
[Azure revision running states](https://learn.microsoft.com/en-us/azure/container-apps/revisions#running-status).

```powershell
function Wait-PegasusExpectedWebRevision {
  param(
    [Parameter(Mandatory)][string] $ExpectedRevisionName,
    [Parameter(Mandatory)][string] $ExpectedImage
  )

  for ($attempt = 1; $attempt -le 12; $attempt++) {
    $revisionJson = az containerapp revision list --subscription $subscriptionId `
      --resource-group $resourceGroup --name $webApp --all `
      --query '[].{name:name,active:properties.active,health:properties.healthState,running:properties.runningState,provisioning:properties.provisioningState,image:properties.template.containers[0].image}' --output json
    if ($LASTEXITCODE -ne 0) { throw 'Unable to read Web revision health.' }
    $revisionText = $revisionJson -join "`n"
    if ($revisionText -notmatch '^\s*\[') { throw 'Web revision health read-back was not an array.' }
    try { $revisions = @($revisionText | ConvertFrom-Json) }
    catch { throw 'Web revision health read-back was not valid JSON.' }
    if (@($revisions | Where-Object {
      [string]::IsNullOrWhiteSpace([string]$_.name) -or -not ($_.active -is [bool])
    }).Count -ne 0) { throw 'Web revision health read-back has an invalid name or active state.' }

    $active = @($revisions | Where-Object { $_.active })
    if (@($active | Where-Object { $_.name -cne $ExpectedRevisionName }).Count -ne 0) {
      throw 'An unexpected Web revision is active.'
    }
    $expected = @($revisions | Where-Object { $_.name -ceq $ExpectedRevisionName })
    if ($expected.Count -gt 1) { throw 'Expected Web revision appears more than once.' }
    if ($expected.Count -eq 1 -and $expected[0].active) {
      $revision = $expected[0]
      foreach ($property in @('health', 'running', 'provisioning', 'image')) {
        if ([string]::IsNullOrWhiteSpace([string]$revision.$property)) {
          throw "Expected Web revision is missing $property state."
        }
      }
      if ($revision.image -cne $ExpectedImage) { throw 'Expected Web revision image differs from the approved digest.' }
      if ($revision.health -ceq 'Healthy' -and
          $revision.running -cin @('Running', 'RunningAtMaxScale') -and
          $revision.provisioning -ceq 'Provisioned') { return }
      if ($revision.health -ceq 'Unhealthy' -or
          $revision.running -in @('Stopped', 'Degraded', 'Failed', 'Unknown') -or
          $revision.provisioning -in @('Failed', 'Deprovisioning', 'Deprovisioned')) {
        throw 'Expected Web revision reached a terminal unhealthy state.'
      }
      if ($revision.health -cne 'None' -and $revision.health -cne 'Healthy') {
        throw 'Expected Web revision reported an unknown health state.'
      }
      if ($revision.running -cnotin @('Processing', 'Running', 'RunningAtMaxScale')) {
        throw 'Expected Web revision reported an unknown running state.'
      }
      if ($revision.provisioning -cne 'Provisioning' -and $revision.provisioning -cne 'Provisioned') {
        throw 'Expected Web revision reported an unknown provisioning state.'
      }
    }
    if ($attempt -lt 12) { Start-Sleep -Seconds 5 }
  }
  throw 'Expected Web revision did not become the sole active healthy revision.'
}
```

## 6. Classify the migration route

Execute the migration classification recorded in section 2. Do not run migration
or bootstrap when the identity is unchanged. If the candidate, deployed identity,
approved targets, or approved old Web revision changed since classification, stop
and obtain a fresh classification and approval.

For an additive migration, read and follow
[references/database-migration.md](references/database-migration.md): migration
and runtime grants finish before provisioning Web or deploying Worker.

Choose exactly one route. An unchanged identity, or an additive identity after
that recipe succeeds, executes section 8 and then section 11. A destructive
identity executes section 7, the migration recipe, section 9, section 10, and
then section 11. Do not execute the other route's deployment steps.

For a destructive migration, obtain approval for a short Web and Worker outage,
the exact manifest and exact targets. After actual release, the approved window
must be outside typical usage; record the concrete window then, without
inventing standing hours here. Before any write, re-read the exact target
inventory, require Web `Single` revision mode and exactly one active revision,
and bind it to the exact `$approvedOldWebRevision` named in the approval. Confirm the candidate has no
schema-dependent startup/background work outside disabled Functions and that its
Flex update strategy is `Recreate` or the documented default. A `RollingUpdate`,
unproved strategy, or inventory drift stops the operation.

The destructive route has one bounded staging exception: while the old schema is
still intact, install the approved **new** Worker package with every Worker
Function disabled. This avoids assuming a stopped-Flex deployment state and is
not business activation. Disabled settings suppress triggers only; they do not
prove that a Function host cannot initialize. Do not use Function master keys or
the portal to invoke any Function during maintenance.

```powershell
$webModeJson = az containerapp show --subscription $subscriptionId `
  --resource-group $resourceGroup --name $webApp `
  --query '{mode:properties.configuration.activeRevisionsMode}' --output json
if ($LASTEXITCODE -ne 0) { throw 'Unable to read Web revision mode.' }
$webRevisionsJson = az containerapp revision list --subscription $subscriptionId `
  --resource-group $resourceGroup --name $webApp --all `
  --query '[].{name:name,active:properties.active}' --output json
if ($LASTEXITCODE -ne 0) { throw 'Unable to read Web revision inventory.' }
if ([string]::IsNullOrWhiteSpace($approvedOldWebRevision)) {
  throw 'Destructive migration approval must name the exact old Web revision.'
}
if (($webRevisionsJson -join "`n") -notmatch '^\s*\[') {
  throw 'Web revision inventory was not an array.'
}
try {
  $webMode = ($webModeJson -join "`n") | ConvertFrom-Json
  $webRevisions = @(($webRevisionsJson -join "`n") | ConvertFrom-Json)
}
catch { throw 'Web inventory was not valid JSON.' }
if (@($webRevisions | Where-Object {
  [string]::IsNullOrWhiteSpace([string]$_.name) -or -not ($_.active -is [bool])
}).Count -ne 0) {
  throw 'Web revision inventory has an invalid name or active state.'
}
$activeWebRevisions = @($webRevisions | Where-Object { $_.active })
if ($webMode.mode -cne 'Single' -or $activeWebRevisions.Count -ne 1) {
  throw 'Destructive migration requires Single mode and exactly one active Web revision.'
}
$oldWebRevision = [string]$activeWebRevisions[0].name
if ($oldWebRevision -cne $approvedOldWebRevision) {
  throw 'Active Web revision differs from the exact approved old revision.'
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
deactivate only `$oldWebRevision`; bounded polling must prove that revision
inactive, its replica-list response is valid JSON `[]`, and no other revision is
active. Every Azure command or JSON parse failure stops the route. An unhealthy
host, trigger-disabled setting, or missing response is not containment evidence.

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

az containerapp revision deactivate --subscription $subscriptionId `
  --resource-group $resourceGroup --name $webApp --revision $oldWebRevision --output none
if ($LASTEXITCODE -ne 0) { throw "Unable to deactivate $oldWebRevision." }
$webContained = $false
for ($attempt = 1; $attempt -le 12; $attempt++) {
  $revisionJson = az containerapp revision list --subscription $subscriptionId `
    --resource-group $resourceGroup --name $webApp --all `
    --query '[].{name:name,active:properties.active}' --output json
  if ($LASTEXITCODE -ne 0) { throw 'Unable to read Web revision inventory.' }
  $webModeJson = az containerapp show --subscription $subscriptionId `
    --resource-group $resourceGroup --name $webApp `
    --query '{mode:properties.configuration.activeRevisionsMode}' --output json
  if ($LASTEXITCODE -ne 0) { throw 'Unable to read Web revision mode.' }
  $replicasJson = az containerapp replica list --subscription $subscriptionId `
    --resource-group $resourceGroup --name $webApp --revision $oldWebRevision --output json
  if ($LASTEXITCODE -ne 0) { throw 'Unable to read old Web revision replicas.' }
  $replicasText = $replicasJson -join "`n"
  $revisionsText = $revisionJson -join "`n"
  if ($revisionsText -notmatch '^\s*\[') { throw 'Web revision read-back was not an array.' }
  try {
    $revisions = @($revisionsText | ConvertFrom-Json)
    $pollWebMode = ($webModeJson -join "`n") | ConvertFrom-Json
    $replicas = @($replicasText | ConvertFrom-Json)
  }
  catch { throw 'Web containment read-back was not valid JSON.' }
  if ($replicasText -notmatch '^\s*\[') { throw 'Old Web replica read-back was not an array.' }
  if (@($revisions | Where-Object {
    [string]::IsNullOrWhiteSpace([string]$_.name) -or -not ($_.active -is [bool])
  }).Count -ne 0) { throw 'Web revision read-back has an invalid name or active state.' }
  if ($pollWebMode.mode -cne 'Single') { throw 'Web revision mode changed during containment.' }
  $oldRevisionRows = @($revisions | Where-Object { $_.name -ceq $oldWebRevision })
  if ($oldRevisionRows.Count -ne 1 -or
      -not ($oldRevisionRows[0].active -is [bool]) -or
      $oldRevisionRows[0].active) {
    throw 'Exact approved old Web revision did not read back inactive.'
  }
  $active = @($revisions | Where-Object { $_.active })
  if ($active.Count -eq 0 -and $replicas.Count -eq 0) { $webContained = $true; break }
  Start-Sleep -Seconds 5
}
if (-not $webContained) { throw 'Old Web did not read back inactive with zero replicas.' }
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
$preSqlModeJson = az containerapp show --subscription $subscriptionId `
  --resource-group $resourceGroup --name $webApp `
  --query '{mode:properties.configuration.activeRevisionsMode}' --output json
if ($LASTEXITCODE -ne 0) { throw 'Unable to re-read Web revision mode before SQL.' }
$preSqlRevisionsJson = az containerapp revision list --subscription $subscriptionId `
  --resource-group $resourceGroup --name $webApp --all `
  --query '[].{name:name,active:properties.active}' --output json
if ($LASTEXITCODE -ne 0) { throw 'Unable to re-read Web revisions before SQL.' }
$preSqlReplicasJson = az containerapp replica list --subscription $subscriptionId `
  --resource-group $resourceGroup --name $webApp --revision $oldWebRevision --output json
if ($LASTEXITCODE -ne 0) { throw 'Unable to re-read old Web replicas before SQL.' }
$preSqlRevisionsText = $preSqlRevisionsJson -join "`n"
$preSqlReplicasText = $preSqlReplicasJson -join "`n"
if ($preSqlRevisionsText -notmatch '^\s*\[' -or $preSqlReplicasText -notmatch '^\s*\[') {
  throw 'Pre-SQL Web read-back was not an array.'
}
try {
  $preSqlMode = ($preSqlModeJson -join "`n") | ConvertFrom-Json
  $preSqlRevisions = @($preSqlRevisionsText | ConvertFrom-Json)
  $preSqlReplicas = @($preSqlReplicasText | ConvertFrom-Json)
}
catch { throw 'Pre-SQL Web read-back was not valid JSON.' }
if ($preSqlMode.mode -cne 'Single' -or
    @($preSqlRevisions | Where-Object {
      [string]::IsNullOrWhiteSpace([string]$_.name) -or -not ($_.active -is [bool])
    }).Count -ne 0 -or
    @($preSqlRevisions | Where-Object { $_.active }).Count -ne 0 -or
    @($preSqlRevisions | Where-Object { $_.name -ceq $oldWebRevision }).Count -ne 1 -or
    @($preSqlRevisions | Where-Object { $_.name -ceq $oldWebRevision -and $_.active }).Count -ne 0 -or
    $preSqlReplicas.Count -ne 0) {
  throw 'Web is not freshly confirmed inactive with zero replicas before destructive SQL.'
}
```

Then follow [references/database-migration.md](references/database-migration.md).
If SQL, grants, or migration-head verification fails, leave the Worker stopped
and every Disabled setting true. From this point recover forward only: never
start the old Worker package or old Web revision against changed or unknown schema.

## 8. Normal route: provision, then deploy Worker

For an unchanged migration identity, or after the additive migration recipe
succeeds, retain the ordinary ordering: provision the approved Web and
infrastructure with the Worker approved live, then deploy the approved Worker
ZIP and run the full smoke. Do not use the destructive containment steps or its
disabled-first staging exception for this route.

```powershell
$revisionSuffix = $releaseSha.Substring(0,12)
azd env set PEGASUS_WEB_IMAGE_DIGEST $manifest.webImage.digest -e $releaseEnvironment
if ($LASTEXITCODE -ne 0) { throw 'Unable to set normal-route Web digest.' }
azd env set PEGASUS_WEB_REVISION_SUFFIX $revisionSuffix -e $releaseEnvironment
if ($LASTEXITCODE -ne 0) { throw 'Unable to set normal-route Web revision suffix.' }
azd env set PEGASUS_WEB_ACTIVATION approved -e $releaseEnvironment
if ($LASTEXITCODE -ne 0) { throw 'Unable to set normal-route Web activation.' }
azd env set PEGASUS_WORKER_ACTIVATION approved-live-worker -e $releaseEnvironment
if ($LASTEXITCODE -ne 0) { throw 'Unable to set normal-route Worker activation.' }
pwsh ./scripts/Test-AzureDeploymentPlan.ps1 -Mode PreProvision `
  -Environment $releaseEnvironment -ManifestPath $manifestPath `
  -WorkerActivation approved-live-worker -ExpectedLiveWorkerActivation approved-live-worker
if ($LASTEXITCODE -ne 0) { throw 'Normal-route pre-provision validation failed.' }
azd provision -e $releaseEnvironment --no-prompt
if ($LASTEXITCODE -ne 0) { throw 'Normal-route Web provisioning failed.' }
$expectedWebRevisionName = "$webApp--$revisionSuffix"
$expectedWebImage = "$registry.azurecr.io/pegasus/web@$($manifest.webImage.digest)"
Wait-PegasusExpectedWebRevision `
  -ExpectedRevisionName $expectedWebRevisionName -ExpectedImage $expectedWebImage
az functionapp deployment source config-zip --subscription $subscriptionId `
  --resource-group $resourceGroup --name $workerApp `
  --src "./artifacts/releases/$version/worker.zip"
if ($LASTEXITCODE -ne 0) { throw 'Normal-route Worker deployment failed.' }
pwsh ./scripts/Invoke-ProductionSmoke.ps1 `
  -BaseUri 'https://pegasus-prod-web-252ow37gij.ashymushroom-676209e5.uksouth.azurecontainerapps.io' `
  -ExpectedSourceRevision $releaseSha -ExpectedVersion $version `
  -ResourceGroupName $resourceGroup -SubscriptionId $subscriptionId `
  -ExpectedWorkerActivation approved-live-worker
if ($LASTEXITCODE -ne 0) { throw 'Normal-route exact release smoke failed.' }
```

## 9. Destructive route: provision the new Web while Worker remains disabled

Read the azd environment and refuse stale or wrong targets. Every secret URI
must name `pegasusprodkv252ow37g`; `AZURE_RESOURCE_GROUP` must be
`rg-pegasus-prod`. After verified migration, grants and migration head, use the
same approved Web digest and suffix, set Web activation to `approved`, and retain
the desired Worker value as `disabled`. `PreProvision` must observe the Worker
disabled before provision. Check each native exit, read back the new active Web
revision/digest, and run the full Worker-only disabled smoke. A configuration
update may start the Function host, but its approved new bytes were staged before
SQL; do not deploy the Worker ZIP again.

```powershell
$revisionSuffix = $releaseSha.Substring(0,12)
azd env set PEGASUS_WEB_IMAGE_DIGEST $manifest.webImage.digest -e $releaseEnvironment
if ($LASTEXITCODE -ne 0) { throw 'Unable to set the approved Web digest.' }
azd env set PEGASUS_WEB_REVISION_SUFFIX $revisionSuffix -e $releaseEnvironment
if ($LASTEXITCODE -ne 0) { throw 'Unable to set the approved Web revision suffix.' }
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
$expectedWebRevisionName = "$webApp--$revisionSuffix"
$expectedWebImage = "$registry.azurecr.io/pegasus/web@$($manifest.webImage.digest)"
Wait-PegasusExpectedWebRevision `
  -ExpectedRevisionName $expectedWebRevisionName -ExpectedImage $expectedWebImage
pwsh ./scripts/Invoke-ProductionSmoke.ps1 -WorkerOnly `
  -ResourceGroupName $resourceGroup -SubscriptionId $subscriptionId `
  -ExpectedWorkerActivation disabled
if ($LASTEXITCODE -ne 0) { throw 'New Worker disabled census smoke failed.' }
```

## 10. Explicitly activate the compatible release

Keep the same approved digest and suffix. Set only
`PEGASUS_WORKER_ACTIVATION=approved-live-worker`, preflight against the observed
disabled Worker, and provision once. If it remains stopped, start the exact
approved target and require a `Running` read-back. Completion requires the new
Web active and healthy at the approved digest, the Worker `Running`, every
Disabled value `false`, and a successful full smoke. If any of those fail, report
an unfinished outage; do not call the release successful or revive old bytes.

```powershell
azd env set PEGASUS_WORKER_ACTIVATION approved-live-worker -e $releaseEnvironment
if ($LASTEXITCODE -ne 0) { throw 'Unable to set approved Worker activation.' }
pwsh ./scripts/Test-AzureDeploymentPlan.ps1 -Mode PreProvision `
  -Environment $releaseEnvironment -ManifestPath $manifestPath `
  -WorkerActivation approved-live-worker -ExpectedLiveWorkerActivation disabled
if ($LASTEXITCODE -ne 0) { throw 'Worker activation pre-provision validation failed.' }
azd provision -e $releaseEnvironment --no-prompt
if ($LASTEXITCODE -ne 0) { throw 'Compatible release activation failed.' }
Wait-PegasusExpectedWebRevision `
  -ExpectedRevisionName $expectedWebRevisionName -ExpectedImage $expectedWebImage
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
  -BaseUri 'https://pegasus-prod-web-252ow37gij.ashymushroom-676209e5.uksouth.azurecontainerapps.io' `
  -ExpectedSourceRevision $releaseSha -ExpectedVersion $version `
  -ResourceGroupName $resourceGroup -SubscriptionId $subscriptionId `
  -ExpectedWorkerActivation approved-live-worker
if ($LASTEXITCODE -ne 0) { throw 'Exact release smoke failed.' }
```

The scripts at the released SHA own the exact Worker function and schedule
census. Do not duplicate a function count in the skill. The full smoke also
reads the production database (read-only) and fails unless an intake mailbox
is activated, an unexpired `Active` Graph subscription exists, and an inbound
poll completed within 15 minutes. Smoke proves the right bytes, configuration,
and intake liveness, not the changed user journey. Run only the focused live
behavioural check required by the released change and record its result without
overclaiming.

## 11. Record and retain evidence

Update `docs/operations.md` with the dated observed artifact, configuration,
migration and activation evidence. Update `docs/current-architecture.md` only
when source structure or composition changed; do not repeat operational artifact
identities there. Link retained attempts and failures from the release record.

Copy `artifacts/releases/$version` outside the disposable worktree before
removing it. The release is unfinished until operations records the deployed observation
and any source-structure change is reflected in current-architecture.

## Recovery and diagnostics

Read [recovery and diagnostics](../../../docs/runbook.md) only for diagnosis or a separately authorized recovery operation.
