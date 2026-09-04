---
name: pegasus-release
description: Promote and release Pegasus through its authorised terminal route, or perform a promotion-only update without redeploying unchanged application code. Use for Pegasus production promotion, deployment, rollback, or release verification.
---

# Release Pegasus

Use the repository scripts and the exact source SHA. `azd up` and `azd deploy
worker` are not release procedures.

Run the route from the authorised Linux x64 terminal on Linux-native storage.
Before preflight, require `uname -m` to report `x86_64`, `oras version` to
report 1.3.4, and both `az account show` and `azd auth login --check-status` to
identify the intended operator. Authentication is not write approval.

[`docs/runbook.md`](../../../docs/runbook.md) and
[`docs/engineering.md`](../../../docs/engineering.md) are authoritative. Stop
if they disagree with this skill.

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
  dependency, or runtime-configuration change. Follow every remaining section.
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

The manifest must use schema 3, `migrationRuntimeIdentifier` `linux-x64` and
`migrationBundleName` `efbundle`. The four artifacts are `web.zip`,
`worker.zip`, `web-image.tar.gz` and `efbundle`.

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

## 6. Apply a new migration before application packages

Compare `migrationIdentity` with the deployed release recorded in
`docs/operations.md`. If unchanged, do not run the migration or database
bootstrap. If new, read and follow
[references/database-migration.md](references/database-migration.md). Migration
and runtime grants must finish before provisioning Web or deploying Worker.

## 7. Provision Web and infrastructure

Read the azd environment and refuse stale or wrong targets. Every secret URI
must name `pegasusprodkv252ow37g`; `AZURE_RESOURCE_GROUP` must be
`rg-pegasus-prod`; `PEGASUS_WORKER_ACTIVATION` must be
`approved-live-worker`.

```powershell
azd env get-values -e $releaseEnvironment --no-prompt
$revisionSuffix = $releaseSha.Substring(0,12)
azd env set PEGASUS_WEB_IMAGE_DIGEST $manifest.webImage.digest -e $releaseEnvironment
azd env set PEGASUS_WEB_REVISION_SUFFIX $revisionSuffix -e $releaseEnvironment
pwsh ./scripts/Test-AzureDeploymentPlan.ps1 -Mode PreProvision `
  -Environment $releaseEnvironment -ManifestPath $manifestPath `
  -WorkerActivation approved-live-worker `
  -ExpectedLiveWorkerActivation approved-live-worker
azd provision -e $releaseEnvironment --no-prompt
```

Provision deploys the digest-pinned Web image and any infrastructure or app
setting changes. Read back the active revision and digest; do not trust the
command's success message alone.

## 8. Deploy Worker

```powershell
az functionapp deployment source config-zip --subscription $subscriptionId `
  --resource-group $resourceGroup --name $workerApp `
  --src "./artifacts/releases/$version/worker.zip"
```

Never use `azd deploy worker`; it invokes a remote Oryx build against the
already-published package.

## 9. Smoke the exact release

```powershell
pwsh ./scripts/Invoke-ProductionSmoke.ps1 `
  -BaseUri 'https://pegasus-prod-web-252ow37gij.ashymushroom-676209e5.uksouth.azurecontainerapps.io' `
  -ExpectedSourceRevision $releaseSha -ExpectedVersion $version `
  -ResourceGroupName $resourceGroup -SubscriptionId $subscriptionId `
  -ExpectedWorkerActivation approved-live-worker
az containerapp show --subscription $subscriptionId `
  --resource-group $resourceGroup --name $webApp `
  --query '{mode:properties.configuration.activeRevisionsMode,traffic:properties.configuration.ingress.traffic}' --output json
```

The scripts at the released SHA own the exact Worker function and schedule
census. Do not duplicate a function count in the skill. The full smoke also
reads the production database (read-only) and fails unless an intake mailbox
is activated, an unexpired `Active` Graph subscription exists, and an inbound
poll completed within 15 minutes. Smoke proves the right bytes, configuration,
and intake liveness, not the changed user journey. Run only the focused live
behavioural check required by the released change and record its result without
overclaiming.

## 10. Record and retain evidence

Update `docs/current-architecture.md` and `docs/operations.md` with the observed
SHA, manifest hash, digest, revision, migration and evidence. Deliver those
changes through the normal reviewed PR to `dev`, then use a fresh authorised
promotion-only pass to put the docs on `main`; do not redeploy unchanged
application code.

Copy `artifacts/releases/$version` outside the disposable worktree before
removing it. The release is unfinished until both current-state documents match
what was actually deployed.
