2026-08-20 ~11:15Z progress:
- Release candidate a3c88a7b: local Release build 0 warnings / 0 errors; Test-AzureDeploymentPlan -Mode Local pass; Test-MigrationGrants 57 files pass.
- Pending migrations vs release 13 confirmed: 20260820034652_ImageIntakeSubmissionGroup, 20260820040337_SendToAiConnectorSettings, 20260820055900_ImageCaseCustody (all additive → previous-artifact rollback stays valid against migrated schema).
- Production confirmed serving 2325ed4a (diagnostics/version). release-13-2325ed4a artifacts retained (azd-preview.txt for byte-compare, worker.zip for rollback).
- PR #467 full green CI; PR #466 docs-only (docs/capabilities.md + frd-11), dotnet lanes legitimately path-skipped.
- 10 verification lanes running (8 ticket batches, UI-copy audit, merge-integrity sweep) over release-14 worktree.
- Runbook already records the 2026-08-19 Sent-evidence mailbox approval (release 12) — no docs gap there. Remaining owed docs: operations release-14 row + serving statement, current-architecture refresh, runbook "Previous-artifact rollback" procedure (closes TICK-029 gap). Rollback outline: web = re-pin previous digest/revision via azd env + preview-gated provision; worker = config-zip of retained previous worker.zip; DB = roll-forward only, additive migrations keep previous app valid, restore is Recovery-section territory with its own approvals.

2026-08-20 ~11:55Z — dev moved during verification: PR #468 (TICK-064, MAIL-23 folder bindings) merged by its owner → dev fb42ce15. Reviewed it post-merge myself (record on TICK-064 scratch/review): safe, read-only, invariants hold; one convention regression (inline operation key) fixed on the release branch. Release now carries FOUR pending migrations: ImageIntakeSubmissionGroup, SendToAiConnectorSettings, ImageCaseCustody, ApprovedMailboxLogicalFolderBindings — census 58/58 + bootstrap census verified. Copy-fix PR #472 (merged with dev head; architecture suite 98/98 green, mailbox suites green) awaiting CI, then it becomes the release cut. Posted hold-merge comments on in-flight #469/#470/#471. PLAT-015 filed for structural copy debt.

2026-08-20 ~12:4xZ — RELEASE 14 DEPLOYED. Route transcript (all gates passed in order):
- Cut d91fd7d7835af116c0c769b75fd4ccae56ca377b (origin/dev; includes verified PRs #437–#468, #471, #472).
- Build-ReleaseArtifacts 0.1.0-alpha.1 @ cut; manifest SHA-256 87667CB7D015FA42765DBCA8942B1B89E26DF738ACF42C04FEE47FEB25D622FB.
- Test-AzureDeploymentPlan: Local, Artifact, PreUpload, PreMigration, PreProvision all passed (azd env reconstructed in worktree from main checkout + refresh; AZURE_SUBSCRIPTION_ID/TENANT_ID re-set).
- oras push → pegasusprodacr252ow37gij/pegasus/web:d91fd7d7…; digest sha256:949797d4922f8030401e4f4974b30aeb450d0aa2be4ea9cb14228a52b5a19f36 == manifest digest.
- efbundle (Production host env incl. new Graph__BaseUri; Box shape-valid placeholders): applied 20260820034652_ImageIntakeSubmissionGroup, 20260820040337_SendToAiConnectorSettings, 20260820055900_ImageCaseCustody, 20260820100056_ApprovedMailboxLogicalFolderBindings; head readback confirmed.
- Invoke-AzureDatabaseBootstrap: 501 catalogued permission/denial rows + 337 effective runtime DML rows verified.
- azd provision --preview byte-identical to stored release-13 preview except revisionSuffix 2325ed4a31d7→d91fd7d7835a (container/app-setting diffs collapsed by what-if, same as prior releases). Provisioned: revision pegasus-prod-web-252ow37gij--d91fd7d7835a, image @sha256:949797d4…, Running.
- Worker config-zip: "Deployment was successful."
- Invoke-ProductionSmoke: PASSED incl. worker activation approved-live-worker. /diagnostics/version serves sourceSha d91fd7d7….
- Artifacts retained at artifacts/releases/release-14-d91fd7d7 (main checkout).
Next: post-deploy verification of operator issues (SQL + browser), docs refresh PR, dev→main promotion, closeout.
