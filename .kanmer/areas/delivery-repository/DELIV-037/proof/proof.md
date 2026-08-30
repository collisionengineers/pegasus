# Proof — DELIV-037, release 37

Written on merged `main`. Code release `0b3ec847aae42ee1c1bee4fb99459f9192534dca`;
documentation promotion `fb3f07acc8cca8d9d8b57db8a431b607772436dc`.

## 1. Gate on the promotion SHA

```
dotnet restore ./Pegasus.slnx --locked-mode                                    → 0
dotnet build   ./Pegasus.slnx --configuration Release --no-restore             → 0
dotnet test    ./Pegasus.slnx --configuration Release --no-build \
               --filter "Category!=Corpus"                                     → 0
    Core.Tests        1178 passed / 0 failed
    ArchitectureTests  100 passed / 0 failed
    IntegrationTests  1222 passed / 0 failed / 2 skipped (pre-existing, corpus)
Test-MigrationGrants.ps1                → 0   87 migration files checked
Test-UiCatalogue.ps1                    → 0   54 routed sources, 58 prototypes
Test-AzureDeploymentPlan.ps1 -Mode Local → 0
```

Snapshot verify not re-run locally: `0b3ec847^{tree}` equals `b4cc5cc8^{tree}`,
the branch head CI's `test-ui` job verified at 27m04s.

## 2. Promotion

```
origin/main 783b4b88…  origin/dev 0b3ec847…   merge-base --is-ancestor → 0
git push --atomic --force-with-lease=refs/heads/dev:0b3ec847… origin \
  0b3ec847…:refs/heads/main 0b3ec847…:refs/heads/dev            → 0
read-back: origin/main = origin/dev = 0b3ec847…                 → equal
```

## 3. Artifacts

```
Build-ReleaseArtifacts.ps1 -Version 0.1.0-alpha.1 -SourceRevision 0b3ec847…  → 0
manifest sourceRevision      0b3ec847aae42ee1c1bee4fb99459f9192534dca
manifest migrationIdentity   20260829212237_GrantProviderSubmissionAcceptRecovery
manifest webImage.digest     sha256:47f57ea5031953ef93ccb09b2eb829b30d468647c96c0dc804310cc6f368595b
manifest SHA-256             5DC59E80A5A5CE324D391CF8BBDCBC7C4E33DAEDEB0BD5A2C592961A6CD1E7A7
Test-AzureDeploymentPlan.ps1 -Mode Local / Artifact / PreUpload              → 0 / 0 / 0
```

## 4. Image upload

```
oras cp --from-oci-layout web-image.tar.gz:0b3ec847… …/pegasus/web:0b3ec847…  → 0
oras manifest fetch … --descriptor  → sha256:47f57ea5…368595b
equals manifest.webImage.digest     → DIGEST MATCH CONFIRMED
```

## 5. Migration

Baseline before, read directly from production SQL:

```
head  20260827143200_GrantEvaSubmissions
count 76
```

```
Test-AzureDeploymentPlan.ps1 -Mode PreMigration …                             → 0
efbundle.exe --connection "Server=tcp:pegasus-prod-sql-252ow37gij…;Database=pegasus;
  Authentication=Active Directory Default;…"                                  → 0
  (from src/Pegasus.Web with ASPNETCORE_ENVIRONMENT=Production,
   Runtime__Profile=Production, ConnectionStrings__Pegasus,
   AZURE_TOKEN_CREDENTIALS=AzureCliCredential,
   AzureIdentity__WebClientId=e801d141-e876-471a-8829-222e9759b933,
   TransportStorage__/CustodyStorage__/IntakeQueue__ names and URIs,
   Box__BaseUri/UploadUri/RootFolderId, shape-valid placeholder
   Box__ConfigJson/Box__ClientSecret, Graph__BaseUri/TenantId/
   ChangeNotificationClientState placeholder, and the six Eva__ keys —
   all six are on the Production fail-fast list, so the host will not
   construct without them)
  Output: eleven "Applying migration '…'" lines, then "Done."
Invoke-AzureDatabaseBootstrap.ps1 -Environment pegasus-prod …                 → 0
  Output: "Verified 544 catalogued permission/denial rows and 377 effective
           runtime DML rows."
```

**The first `efbundle` invocation failed**, omitting
`ConnectionStrings__Pegasus`: *"ConnectionStrings:Pegasus is required for the
Production runtime profile."* The host failed to construct, so **no migration
was applied** — the retry was clean, not partial.

Read-back from production SQL:

```
SELECT TOP 1 MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC
  → 20260829212237_GrantProviderSubmissionAcceptRecovery   (= manifest identity)
SELECT COUNT(*) FROM __EFMigrationsHistory              → 87   (was 76)
new tables present: AiJobs, CaseValuations, PrincipalApiCredentials,
                    ProviderSubmissions
  (NamedEstimates creates no table — it reshapes CaseRepairSpecifications)
row baseline: AiJobs 0, PrincipalApiCredentials 0, ProviderSubmissions 0,
              CaseValuations 0, against Cases 7
```

## 6. Provision

```
every *_SECRET_URI names pegasusprodkv252ow37g                 → 10 of 10, 0 exceptions
azd env set PEGASUS_WEB_IMAGE_DIGEST sha256:47f57ea5…          → 0
azd env set PEGASUS_WEB_REVISION_SUFFIX 0b3ec847aae4           → 0
Test-AzureDeploymentPlan.ps1 -Mode PreProvision …              → 0
  "Production Worker activation smoke passed (approved-live-worker)."
  "Worker Disabled settings render 'false'."
azd provision -e pegasus-prod --no-prompt                      → 0  (2m09s)
```

Read-back:

```
active revision  pegasus-prod-web-252ow37gij--0b3ec847aae4   created 15:21:37Z
image            …/pegasus/web@sha256:47f57ea5…368595b       (= manifest)
mode Single, traffic 100, replicas 1
replica          ready true, started true, restartCount 0
Features__AutomationMcp true ; Features__ProviderApi true
DocumentRequests__* count 15 ; LimitsVersion int-31-interim-v1 ;
  MaximumRequestBytes 10485760
```

## 7. Worker

```
az functionapp deployment source config-zip … --src worker.zip   → 0
  "Deployment was successful."
function list → 7: DueWorkSweep, InboxRecovery, PendingWorkRecovery,
  SentEvidencePoll, StagedArtifactReconciliation, UnifiedWork, UnifiedWorkPoison
AzureWebJobs.*.Disabled → all seven "false"
app state → Running / Normal / enabled / httpsOnly; root responds 200
```

## 8. Smoke and diagnostics

```
Invoke-ProductionSmoke.ps1 (15:25Z)  → 0  poll 15:25:02Z, subscription 2026-09-02 10:25:00Z
Invoke-ProductionSmoke.ps1 (15:33Z)  → 0  poll 15:30:03Z
GET /diagnostics/version → {"version":"0.1.0-alpha.1","sourceSha":"0b3ec847aae4…"}
GET /health/live 200 ; /health/ready 200 ; GET / 302
POST /api/provider/v1/submissions unauthenticated → 401   (live, admits nobody)
```

**Telemetry does not cover this release.** App Insights ingestion stopped at
12:41Z on the workspace's 0.5 GB daily cap (`RespectQuota`, resetting 03:00Z),
about 2h40m before the deploy. Every check above is therefore direct
observation. `AppExceptions` last saw data 2026-08-29T19:31Z.

## 9. Retention

`artifacts/releases/release-37-0b3ec847` in the primary checkout — the manifest,
`worker.zip` and `efbundle.exe`. The 1.4 GB `web-image.tar.gz` is not retained;
the image is in the ACR under tag `0b3ec847aae42ee1c1bee4fb99459f9192534dca`.
The retained manifest hashes to `5DC59E80…6CD1E7A7`, equal to the figure
recorded in `operations.md`.

## 10. Documentation

PR #637 → `dev`, then promotion-only to `main` at `fb3f07ac` under fresh
authorisation. Diff: four files, all under `docs/`, **zero non-docs files**, no
Azure write.

Independent verification confirmed 24 facts against the live estate and found
**six false statements** in the first drafts — all corrected. The detail is on
this ticket's `scratch/review.md`; the short version is that a current-state
document written by the agent that ran the deploy needs checking against
reality, and this one did.

## What is NOT proved

- **No provider has called the API in any environment.** The gate is open and
  no credential is issued; a live caller remains outstanding.
- **The upload-link surface has no live evidence** beyond composing. Nobody has
  uploaded through it in production.
- The two smoke poll timestamps are an uncorroborated operator self-report —
  telemetry is dark for that window and no smoke artifact was kept.
- Native inference on the deployed runtime remains unverified, unchanged by
  this release.
