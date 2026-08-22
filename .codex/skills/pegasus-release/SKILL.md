---
name: pegasus-release
description: Release Pegasus to production — promote dev to main, build immutable artifacts, provision, deploy Web and Worker, smoke, verify and refresh the current-state docs. Use whenever a release, promotion or deployment to the Azure estate is requested.
---

# Releasing Pegasus

The whole route, in order, with the traps that have actually bitten. `azd up` is
**not** the release procedure and never has been.

The authorities this compresses are [`docs/runbook.md`](../../../docs/runbook.md)
(§ Deployment and release, § Release artifacts and bootstrap, § Durable Worker
activation and rollback) and
[`docs/engineering.md`](../../../docs/engineering.md) (§ Branches and delivery).
Where this file and those disagree, they win — but everything below has been
executed against the live estate, which the general guidance has not.

## The estate

| Thing | Exact name |
| --- | --- |
| Resource group | `rg-pegasus-prod` |
| Subscription | `e6076573-23a5-46a8-acef-7e22d264e5db` |
| azd environment | `pegasus-prod` |
| Web (Container App) | `pegasus-prod-web-252ow37gij` |
| Worker (Function App, Flex Consumption) | `pegasus-prod-worker-252ow37gij` |
| Registry | `pegasusprodacr252ow37gij` |
| Key Vault | `pegasusprodkv252ow37g` |
| SQL | `pegasus-prod-sql-252ow37gij` / database `pegasus` |
| App Insights | `pegasus-prod-appi-252ow37gij` |

Read-only Azure checks need no approval. **Every write needs explicit operator
approval for the exact target**, and the `main` update additionally needs the
words `MERGE AUTH GRANTED` immediately before the push.

---

## 1. Preflight

```bash
gh pr checks <pr>                      # every check green, no exceptions
git fetch origin --prune
git rev-parse origin/main origin/dev
git merge-base --is-ancestor origin/main origin/dev && echo "fast-forward valid"
git log --oneline origin/main..origin/dev   # exactly what is about to ship
```

Also check what is deployed *now*, so the release has a before:

```bash
az containerapp revision list -g rg-pegasus-prod -n pegasus-prod-web-252ow37gij \
  --query "[?properties.active].{name:name,image:properties.template.containers[0].image}" -o tsv
az functionapp config appsettings list -g rg-pegasus-prod \
  -n pegasus-prod-worker-252ow37gij --query "[?contains(name,'Schedule')].{n:name,v:value}" -o tsv
```

> **Trap — the deployed revision is not `main`.** The active revision is built from
> whichever commit was released, and `main` may carry later docs-only commits.
> Confirm with `git merge-base --is-ancestor <sha> <deployed-sha>` before claiming
> anything is live.

## 2. Promote `dev` to `main` — exact-SHA atomic fast-forward

**Requires `MERGE AUTH GRANTED` from the operator, immediately before the push.**

```bash
SHA=$(git rev-parse origin/dev)
git push --atomic --force-with-lease=refs/heads/dev:$SHA \
  origin $SHA:refs/heads/main $SHA:refs/heads/dev
git fetch origin --prune
git rev-parse origin/main origin/dev     # both MUST equal $SHA
```

A GitHub merge/rebase/squash is **not** this and does not replace it. A failed
preflight, a rejected transaction or an unequal read-back **stops the release** —
never repair it with a rebase, reset or force push.

## 3. Build immutable artifacts

From a clean tree at the exact promoted SHA:

```bash
git checkout main && git merge --ff-only origin/main
git status --porcelain      # must be empty, including untracked
pwsh ./scripts/Build-ReleaseArtifacts.ps1 -Version '0.1.0-alpha.1' -SourceRevision "$SHA"
```

`$Version` must match `^\d+\.\d+\.\d+-alpha\.\d+$` — it is the product version, not
the release number. Output lands in `artifacts/releases/<version>/`: `web.zip`,
`worker.zip`, `web-image.tar.gz` (OCI layout), `efbundle.exe`, `release-manifest.json`.

Read the manifest. `migrationIdentity` tells you whether this release carries a new
migration; if it equals the last release's, there is nothing to apply and you should
say so rather than running a bundle for form's sake.

```bash
pwsh ./scripts/Test-AzureDeploymentPlan.ps1 -Mode Local
pwsh ./scripts/Test-AzureDeploymentPlan.ps1 -Mode Artifact \
  -ManifestPath artifacts/releases/<version>/release-manifest.json
```

## 4. Push the Web image to ACR

> **Trap — there is no Docker on the release workstation.** `az acr login` fails
> with `DOCKER_COMMAND_ERROR`. The image is an OCI archive built by the .NET SDK,
> and `oras` moves it.

```pwsh
$t = az acr login -n pegasusprodacr252ow37gij --expose-token -o json | ConvertFrom-Json
$t.accessToken | oras login $t.loginServer -u '00000000-0000-0000-0000-000000000000' --password-stdin
oras cp --from-oci-layout "artifacts/releases/<version>/web-image.tar.gz:$SHA" `
        "pegasusprodacr252ow37gij.azurecr.io/pegasus/web:$SHA"
```

Check the digest `oras` reports equals `webImage.digest` in the manifest. If it does
not, you are about to deploy something other than what you built.

## 5. Point the azd environment at *this* release

> **Trap — the azd environment is stale and is not authoritative.** It has been
> found carrying the *previous* release's image digest and revision suffix, and
> once carried retired Key Vault names. Provision will then either redeploy the old
> image or fail with
> `Field 'template.revisionsuffix' is invalid ... revision with suffix <old> already exists`.
> That collision is the *lucky* outcome; without it you silently redeploy the old build.

```pwsh
azd env get-values | Select-String 'SECRET_URI|PEGASUS_WEB_|WORKER_ACTIVATION|AZURE_'
```

Confirm before provisioning:

- every `*_SECRET_URI` names **`pegasusprodkv252ow37g`**;
- `PEGASUS_WORKER_ACTIVATION` is exactly `approved-live-worker` (any other value,
  or omission, disables all nine functions);
- `AZURE_RESOURCE_GROUP` is `rg-pegasus-prod`.

Then set this release's two provisioning inputs:

```pwsh
azd env set PEGASUS_WEB_IMAGE_DIGEST '<webImage.digest from the manifest>'
azd env set PEGASUS_WEB_REVISION_SUFFIX '<first 12 chars of the source SHA>'
```

`WEB_IMAGE_REFERENCE` and `WEB_CONTAINER_APP_REVISION` are **outputs**, not inputs —
setting them achieves nothing.

```pwsh
pwsh ./scripts/Test-AzureDeploymentPlan.ps1 -Mode PreProvision -Environment pegasus-prod `
  -ManifestPath artifacts/releases/<version>/release-manifest.json `
  -WorkerActivation 'approved-live-worker' -ExpectedLiveWorkerActivation 'approved-live-worker'
```

## 6. Provision

```pwsh
azd provision --no-prompt
```

Needed whenever `infra/` changed — timer schedules, app settings, roles. A code-only
release still benefits from it as a no-op consistency check.

> Note: a provision that fails on the Web app may still have **succeeded** for the
> Function App. Re-read the worker settings before assuming nothing changed.

## 7. Deploy

Web is deployed by the provision above (the container app takes the digest).

**Worker — use config-zip and nothing else:**

```bash
az functionapp deployment source config-zip \
  --resource-group rg-pegasus-prod --name pegasus-prod-worker-252ow37gij \
  --src ./artifacts/releases/<version>/worker.zip
```

> **Trap — never `azd deploy worker --from-package`.** It triggers a remote Oryx
> build that rejects the pre-published package and crash-loops the host until a good
> package lands.

`host.json` ships **inside** `worker.zip`, so any queue/host setting change needs
this step even when `infra/` did not change.

## 8. Migrations, only if there is one

`efbundle.exe` builds the **Web** host, so run it from `src/Pegasus.Web` with the
Production process environment: `ASPNETCORE_ENVIRONMENT=Production`,
`Runtime__Profile=Production`, `ConnectionStrings__Pegasus`,
`AzureIdentity__WebClientId`, both storage account names, the custody service URI,
`Box__BaseUri`/`Box__UploadUri`/`Box__RootFolderId`, and **shape-valid placeholder**
`Box__ConfigJson`/`Box__ClientSecret` — the config must parse as Box JWT JSON or host
construction fails. Set `AZURE_TOKEN_CREDENTIALS=AzureCliCredential` so
`Authentication=Active Directory Default` uses the release operator's sign-in. The
bundle itself takes only `--connection`.

## 9. Smoke

```pwsh
pwsh ./scripts/Invoke-ProductionSmoke.ps1 `
  -BaseUri 'https://pegasus-prod-web-252ow37gij.ashymushroom-676209e5.uksouth.azurecontainerapps.io' `
  -ExpectedSourceRevision "$SHA" -ExpectedVersion '0.1.0-alpha.1' `
  -ResourceGroupName 'rg-pegasus-prod' `
  -SubscriptionId 'e6076573-23a5-46a8-acef-7e22d264e5db' `
  -ExpectedWorkerActivation 'approved-live-worker'
```

Health, exact version/SHA, anonymous denial, https redirect, and all nine worker
activation settings. Then confirm the revision is actually serving:

```bash
az containerapp show -g rg-pegasus-prod -n pegasus-prod-web-252ow37gij \
  --query "{mode:properties.configuration.activeRevisionsMode,traffic:properties.configuration.ingress.traffic}" -o json
```

Two active revisions during transition is normal in Single mode; 100% traffic goes to
the latest.

## 10. Verify behaviour, not just deployment

Smoke proves the right bytes are running. It proves nothing about the change. For
anything touching intake, ask the operator to send a real instruction and read the
result from production SQL, Box and the app.

Querying production SQL needs an Entra token — `sqlcmd -G` fails with
`Failed to resolve the UPN for the current windows account`:

```pwsh
$tok = az account get-access-token --resource https://database.windows.net/ --query accessToken -o tsv
Add-Type -AssemblyName System.Data
$c = New-Object System.Data.SqlClient.SqlConnection
$c.ConnectionString = "Server=tcp:pegasus-prod-sql-252ow37gij.database.windows.net,1433;Database=pegasus;Encrypt=True;"
$c.AccessToken = $tok; $c.Open()
```

## 11. Refresh the current-state docs — the release is not finished without this

In the **same task**, before it merges:

- [`docs/current-architecture.md`](../../../docs/current-architecture.md) — the as-built shape;
- [`docs/operations.md`](../../../docs/operations.md) — the release row (number, date,
  source SHA, image digest, revision name, migrations) and what the release proved
  beyond smoke.

Copy `artifacts/releases/<version>/` somewhere outside the worktree before removing
any worktree — the release evidence is not recoverable afterwards.

---

## Traps, collected

| Symptom | Cause | Do this |
| --- | --- | --- |
| `revision with suffix <old> already exists` | azd env holds the previous release's suffix | set `PEGASUS_WEB_REVISION_SUFFIX` and `PEGASUS_WEB_IMAGE_DIGEST` for *this* release |
| Deploy "succeeds", old code runs | same drift, without a suffix collision | always verify the active revision's digest against the manifest |
| `DOCKER_COMMAND_ERROR` on `az acr login` | no Docker on the workstation | `--expose-token` + `oras login` + `oras cp` |
| Worker crash-loops after deploy | `azd deploy worker --from-package` ran Oryx | redeploy with `config-zip` |
| All nine worker functions disabled | `PEGASUS_WORKER_ACTIVATION` not exactly `approved-live-worker` | fix the azd env, re-provision |
| `efbundle` fails constructing the host | `Box__ConfigJson` not shape-valid JWT JSON | supply placeholder JSON of the right shape |
| `MSB3027` / `MSB3021` file locked during build | a running host holds the DLL | `dotnet build-server shutdown`, rebuild |
| MSBuild child node exited prematurely on a long test run | node contention | `dotnet test --no-build` in chunks |
| CI dies in `actions/checkout` at ~5 min | stale merge ref | close and reopen the PR; rerunning does not help |
| Nothing in App Insights | see PLAT-034 — Web was never instrumented and the Worker had no ingestion credential | do not diagnose blind; fix telemetry first |

## Never

- `azd up` as the release procedure.
- Force-pushing or rewriting `dev` or `main`.
- Deploying the Worker with `azd deploy worker`.
- Claiming a capability is live because its code is on `main` — check it is an
  ancestor of the **deployed** revision.
- Recording a release in `operations.md` that smoke did not actually prove.
