---
release: 35
sourceRevision: 3a1a017c8dea0cde21aa94cbbe15e82f07a6f54f
mainAfterPromotion: 3a1a017c8dea0cde21aa94cbbe15e82f07a6f54f
mainAfterDocsPromotion: 68adedafb9159772515b1b4fb9758f0ab2261fe7
manifestSha256: CA81E6F7D9A1A63C9CC8460614E728B601E206919CB6653E7CB5A681D9EF10CF
webImageDigest: sha256:694c562f9b686877b73e30015a65d35b52c05e5a4b0c455219388c157a0892c8
migrationIdentity: 20260827100901_ReactivateBoundApprovedMailboxes
docsPr: https://github.com/collisionengineers/pegasus/pull/578
disposition: PASS
---

# Proof — DELIV-029 (release 35)

Executed 2026-08-27, following `.agents/skills/pegasus-release/SKILL.md`
(full release route) and the DELIV-029 plan, steps 1–10. Written on merged
`main` after the promotion below, per repository convention.

## 1. Preflight

```
git fetch origin --prune                                             → 0
git rev-parse origin/main   → 1ec65dc894f121f4bb5b31ae82c818a401d08beb
git rev-parse origin/dev    → 3a1a017c8dea0cde21aa94cbbe15e82f07a6f54f
git merge-base --is-ancestor $mainSha $releaseSha                    → 0 (fast-forward valid)
git log --oneline --decorate main..dev   → PRs #577,#576,#571(via 61d80539),#575,#572,#573 (6 tasks: MAIL-017/018/019/020/021, INTK-044)
git diff --stat main..dev  → 26 files, +7547/-65 (application, infra, migration, config — full release route confirmed)
az account show            → subscription e6076573-23a5-46a8-acef-7e22d264e5db, tenant 858cf5b3-aa0a-47a6-9b40-4851fd0afa94   → 0
az containerapp revision list (web)  → active revision --1ec65dc894f1, image digest …b04bad2c…   → 0
az functionapp config appsettings list (worker)  → 7 functions enabled, ApprovedInboxPollSchedule=0 */5 * * * *   → 0
az monitor app-insights component billing show   → dataVolumeCap.cap = 0.1   → 0
az monitor log-analytics workspace show          → dailyQuotaGb = 0.1, RespectQuota   → 0
```

All preflight facts matched the plan's recorded preconditions exactly.

## 2. Promote

```
git push --atomic --force-with-lease=refs/heads/dev:$releaseSha origin \
  $releaseSha:refs/heads/main $releaseSha:refs/heads/dev             → 0
git fetch origin --prune                                             → 0
git rev-parse origin/main  → 3a1a017c8dea0cde21aa94cbbe15e82f07a6f54f
git rev-parse origin/dev   → 3a1a017c8dea0cde21aa94cbbe15e82f07a6f54f
```

Both refs equal the approved SHA. **MERGE AUTH GRANTED** (operator,
2026-08-27, recorded on the ticket) was in force for this exact SHA.

## 3. Build (detached worktree `../pegasus-worktrees/release-3a1a017c`)

```
git worktree add --detach $releaseRoot $releaseSha                   → 0
git status --porcelain (worktree)                                    → clean
pwsh ./scripts/Build-ReleaseArtifacts.ps1 -Version 0.1.0-alpha.1 -SourceRevision $releaseSha   → 0
pwsh ./scripts/Test-AzureDeploymentPlan.ps1 -Mode Local               → 0 ("Local; Worker Disabled settings render 'true'")
pwsh ./scripts/Test-AzureDeploymentPlan.ps1 -Mode Artifact -ManifestPath …  → 0
```

Manifest: `sourceRevision=3a1a017c8dea0cde21aa94cbbe15e82f07a6f54f`,
`sourceStatus=clean`, `migrationIdentity=20260827100901_ReactivateBoundApprovedMailboxes`,
`webImage.digest=sha256:694c562f9b686877b73e30015a65d35b52c05e5a4b0c455219388c157a0892c8`.
manifest SHA-256 = `CA81E6F7D9A1A63C9CC8460614E728B601E206919CB6653E7CB5A681D9EF10CF`.

## 4. Upload

```
pwsh ./scripts/Test-AzureDeploymentPlan.ps1 -Mode PreUpload -ManifestPath … -ManifestSha256 …   → 0
az acr login --expose-token; oras login; oras cp …                    → 0
oras manifest fetch … --descriptor  → digest sha256:694c562f9b686877b73e30015a65d35b52c05e5a4b0c455219388c157a0892c8
```

Remote digest equals `manifest.webImage.digest`. Digest match confirmed.

## 5. Migration

```
pwsh ./scripts/Test-AzureDeploymentPlan.ps1 -Mode PreMigration -Environment pegasus-prod -ManifestPath … -ManifestSha256 …   → 0
efbundle.exe --connection "Server=tcp:pegasus-prod-sql-252ow37gij.database.windows.net,1433;Database=pegasus;Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
  (run from src/Pegasus.Web with ASPNETCORE_ENVIRONMENT=Production, Runtime__Profile=Production,
   AzureIdentity__WebClientId=e801d141-e876-471a-8829-222e9759b933,
   TransportStorage__AccountName=pegtrans252ow37gij, CustodyStorage__AccountName=pegcustody252ow37gij,
   CustodyStorage__ServiceUri=https://pegcustody252ow37gij.blob.core.windows.net/,
   IntakeQueue__ServiceUri=https://pegtrans252ow37gij.queue.core.windows.net/,
   Box__BaseUri/UploadUri/RootFolderId, shape-valid placeholder Box__ConfigJson/Box__ClientSecret,
   Graph__BaseUri=https://graph.microsoft.com/v1.0/, Graph__TenantId=858cf5b3-aa0a-47a6-9b40-4851fd0afa94,
   Graph__ChangeNotificationClientState=<placeholder>, AZURE_TOKEN_CREDENTIALS=AzureCliCredential)   → 0
  Output: "Applying migration '20260827100901_ReactivateBoundApprovedMailboxes'. Done."
pwsh ./scripts/Invoke-AzureDatabaseBootstrap.ps1 -Environment pegasus-prod -ManifestPath … -ManifestSha256 …   → 0
  Output: "Verified 526 catalogued permission/denial rows and 359 effective runtime DML rows."
```

Direct SQL read-back (Entra token via `az account get-access-token`):

```sql
SELECT TOP 1 MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC
  → 20260827100901_ReactivateBoundApprovedMailboxes
SELECT COUNT(*) FROM ApprovedMailboxes → 1
SELECT * FROM ApprovedMailboxes
  → instructions@collisionengineers.co.uk, State=Approved, ActivatedAtUtc=2026-08-27 10:20:33 +00:00 (unchanged)
```

Migration head confirmed. `ActivatedAtUtc` unchanged from the pre-release
value, confirming the UPDATE matched zero rows as the plan expected.

## 6. Provision

```
azd env set PEGASUS_WEB_IMAGE_DIGEST sha256:694c562f9b686877b73e30015a65d35b52c05e5a4b0c455219388c157a0892c8 -e pegasus-prod   → 0
azd env set PEGASUS_WEB_REVISION_SUFFIX 3a1a017c8dea -e pegasus-prod   → 0
pwsh ./scripts/Test-AzureDeploymentPlan.ps1 -Mode PreProvision -Environment pegasus-prod -ManifestPath … \
  -WorkerActivation approved-live-worker -ExpectedLiveWorkerActivation approved-live-worker   → 0
azd provision -e pegasus-prod --no-prompt   → 0 (11 resources updated, 1m32s)
```

Read-back:

```
az containerapp revision list → pegasus-prod-web-252ow37gij--3a1a017c8dea,
  image digest …694c562f…, status RunningAtMaxScale, traffic 100
az containerapp show → mode Single, traffic [{latestRevision:true, weight:100}]
az monitor app-insights component billing show → dataVolumeCap.cap = 0.5
az monitor log-analytics workspace show → dailyQuotaGb = 0.5, RespectQuota
```

## 7. Worker deploy

```
az functionapp deployment source config-zip --src ./artifacts/releases/0.1.0-alpha.1/worker.zip   → 0
  "Deployment was successful."
az functionapp function list → 7 functions: DueWorkSweepFunction, InboxRecoveryFunction,
  PendingWorkRecoveryFunction, SentEvidencePollFunction, StagedArtifactReconciliationFunction,
  UnifiedWorkFunction, UnifiedWorkPoisonFunction
az functionapp config appsettings list → all 7 AzureWebJobs.*.Disabled=false,
  ApprovedInboxPollSchedule=0 */5 * * * * (unchanged)
```

## 8. Smoke

```
pwsh ./scripts/Invoke-ProductionSmoke.ps1 -BaseUri https://pegasus-prod-web-252ow37gij.ashymushroom-676209e5.uksouth.azurecontainerapps.io \
  -ExpectedSourceRevision 3a1a017c8dea0cde21aa94cbbe15e82f07a6f54f -ExpectedVersion 0.1.0-alpha.1 \
  -ResourceGroupName rg-pegasus-prod -SubscriptionId e6076573-23a5-46a8-acef-7e22d264e5db \
  -ExpectedWorkerActivation approved-live-worker   → 0
  "Production Worker activation smoke passed (approved-live-worker)."
  "Inbox intake liveness smoke passed (last poll 2026-08-27 19:45:12Z, subscription expires 2026-09-02 10:25:00Z)."
  "Production smoke passed."
az containerapp show (repeat) → mode Single, traffic 100% latest revision
```

Focused live behavioural check (SQL): `ApprovedMailboxSubscriptions` row for
subscription `09018cc2-99a5-4084-a374-397a9b7d4560`, `LifecycleState=Active`,
`ExpiresAtUtc=2026-09-02 10:25:00 +00:00`, `LastMaintainedAtUtc=2026-08-27
16:30:00 +00:00` — matches the smoke script's independently-read value.

Telemetry observation: `AppDependencies` query, `TimeGenerated >
2026-08-27T19:48:00Z` (14 minutes post Worker-deploy, run at 20:02Z):
223 Worker dependency records, all `Success = true`; workspace
`dataIngestionStatus = RespectQuota` (not `OverQuota`). No successful SQL
dependency rows appeared in the window, consistent with the deployed
`SqlDependencyTelemetryFilter`. This is a post-deploy observation only, not
a controlled before/after comparison against the unfiltered baseline (no
"before" window was captured under the new deploy's exact traffic shape).

Not proved by this release: the new 0.5 GB cap surviving a full working day
(PLAT-034, open); the INTK-044 Audit-allocation recovery path under a live
operator submission; the Mailboxes page's Activated/Subscription columns
under a live operator session (screenshot evidence is operator-supplied,
not captured by this agent).

## 9. Retain evidence

```
Copy-Item …/artifacts/releases/0.1.0-alpha.1 → C:/Users/Alex/Documents/GitHub/pegasus/artifacts/releases/release-35-3a1a017c   → 0
git worktree remove --force …/pegasus-worktrees/release-3a1a017c   → 0
```

## 10. Docs PR

`docs/operations.md`, `docs/current-architecture.md`, `docs/open-decisions.md`
updated in worktree `../pegasus-worktrees/deliv-029-release-35-docs` on
branch `task/deliv-029-release-35-docs` (from `origin/dev`). Commit
`cb2ab070`. PR [#578](https://github.com/collisionengineers/pegasus/pull/578)
opened against `dev` with the release evidence in the body. Not merged or
reviewed by this agent.

PR #578 was subsequently reviewed and merged into `dev` as `68adedaf`
(`repository-check` green: `changes`, `documentation`,
`local-development-scripts`, `reference-data` all SUCCESS; the code jobs
correctly SKIPPED for a docs-only diff).

## 11. Docs promotion-only pass — PERFORMED 2026-08-27

Authority: fresh operator **MERGE AUTH GRANTED** on 2026-08-27 for exactly
`68adedafb9159772515b1b4fb9758f0ab2261fe7`, given as approval of the
promotion-only plan. Section 3 of the release skill only — no build, no
Azure write, no redeploy, no application-code change in the range.

Preflight:

```
git fetch origin --prune                                             → 0
git rev-parse origin/main   → 3a1a017c8dea0cde21aa94cbbe15e82f07a6f54f
git rev-parse origin/dev    → 68adedafb9159772515b1b4fb9758f0ab2261fe7 (equals approved SHA)
git merge-base --is-ancestor $mainSha $releaseSha                    → 0 (fast-forward valid)
git diff --stat 3a1a017c..68adedaf
  → docs/current-architecture.md | 36 +-
    docs/open-decisions.md       | 19 +-
    docs/operations.md           | 57 +-
    3 files changed, 89 insertions(+), 23 deletions(-)   (docs only)
```

Promote:

```
git push --atomic --force-with-lease=refs/heads/dev:$releaseSha origin \
  $releaseSha:refs/heads/main $releaseSha:refs/heads/dev             → 0
  "3a1a017c..68adedaf  68adedaf… -> main"   (fast-forward; dev already at the SHA)
git fetch origin --prune                                             → 0
git rev-parse origin/main   → 68adedafb9159772515b1b4fb9758f0ab2261fe7
git rev-parse origin/dev    → 68adedafb9159772515b1b4fb9758f0ab2261fe7
```

Content read-back on `main` (no checkout of the mutable working copy):

```
git log --oneline origin/main -3 → 68adedaf, cb2ab070, 3a1a017c
git show origin/main:docs/operations.md      → "the estate currently serves **release 35**"
git show origin/main:docs/open-decisions.md  → Stale threshold row now reads
  "three missed `ApprovedInboxPollSchedule` recovery ticks at `0 */5 * * * *`"
```

Both current-state documents on `main` now match what was actually deployed.
The `open-decisions.md` row also satisfies [[MAIL-022]], whose correction
rode in `cb2ab070`.

## Stop predicates checked

None fired: origin/dev matched the approved SHA at preflight; every gate
script (`Local`, `Artifact`, `PreUpload`, `PreMigration`, `PreProvision`)
exited 0; uploaded digest equalled the manifest; migration head matched;
`azd provision` and the Worker `config-zip` deploy both succeeded; smoke
passed including the new intake-liveness assertion. At the promotion-only
pass, `origin/dev` still equalled the approved `68adedaf`, `origin/main` was
still an ancestor, and the range was docs-only.

## Disposition

**PASS.** All release-35 read-backs match the plan's expected values, and
steps 1–11 are complete: the docs PR (#578) merged into `dev` as `68adedaf`
and the promotion-only pass put that SHA on `main` under fresh operator
authority. Nothing from this ticket remains outstanding. The limits recorded
under step 8 (the 0.5 GB cap over a full working day, the INTK-044 live
operator path, the Mailboxes column screenshots) are unproved by this
release and are tracked separately, not deficiencies of this record.
