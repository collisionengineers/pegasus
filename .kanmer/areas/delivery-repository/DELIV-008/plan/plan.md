# Plan — DELIV-008 (release 9)

## Premises (verified read-only 2026-08-18)

- `origin/main` `2b0df78c` is an ancestor of `origin/dev`; PR #400 (dev→main)
  is a CI vehicle only. Promotion is the exact-SHA atomic push of
  `docs/engineering.md#branches-and-delivery`.
- No CI deploy exists (`ci.yml` has no azd/az step). Web is deployed by
  `azd provision` from `PEGASUS_WEB_IMAGE_DIGEST` + `PEGASUS_WEB_REVISION_SUFFIX`;
  `azd deploy web` is prohibited; Worker uses `azd deploy worker --from-package`.
- Live web `/diagnostics/version` = `aecad247…`; prod DB migration head
  `20260813025241_StandaloneAuditReportDecision`; `BoxFileRequests` exists with 0 rows.
- Only infra diff since deployed SHA: `platform.bicep` removes `webIntakeQueueSender`.
- Local `.azure/pegasus-prod/.env` lacks the deployment-output keys the
  `PreMigration`/`PreProvision` modes need → `azd env refresh` first.
- Tooling: dotnet 10.0.302, azd 1.29.0, az + bicep, oras 1.3.0, SqlServer PS module.

## Steps (worktree `../pegasus-worktrees/deliv-008-release-9` at the promoted SHA)

Promotion (B): fetch; `merge-base --is-ancestor origin/main origin/dev`; record
`$SHA=origin/dev`; PR #400 checks all SUCCESS on `$SHA`;
`git push --atomic --force-with-lease=refs/heads/dev:$SHA origin $SHA:refs/heads/main $SHA:refs/heads/dev`;
fetch and require both heads == `$SHA`; main-push run green incl. history guard.

Release (C) — reuse existing scripts only, no new mechanism:
C1 restore/build Release · C2 `Test-AzureDeploymentPlan -Mode Local` ·
C3 `Build-ReleaseArtifacts -Version 0.1.0-alpha.1 -SourceRevision $SHA` ·
C4 `-Mode Artifact` + manifest SHA-256 · C5 `azd env refresh` ·
C6 `-Mode PreUpload` · C7 ACR auth-as-arm status (stop if not enabled) ·
C8 `oras cp --from-oci-layout web-image.tar.gz:$SHA → <acr>/pegasus/web:$SHA` ·
C9 registry digest == manifest digest · C10 `-Mode PreMigration` + history readback ·
C11 `efbundle.exe --connection "…Authentication=Active Directory Default…"` ·
C12 `Invoke-AzureDatabaseBootstrap` · C13 history head readback ·
C14 `azd env set PEGASUS_WORKER_ACTIVATION approved-live-worker` + `-Mode PreProvision -ExpectedLiveWorkerActivation approved-live-worker` ·
C15 `azd env set PEGASUS_WEB_IMAGE_DIGEST/REVISION_SUFFIX/ACTIVATION` ·
C16 `azd provision --preview` (stop unless new revision + role-assignment removal only) ·
C17 `azd provision` · C18 container app readback + health ·
C19 `azd deploy worker --from-package worker.zip` · C20 `Invoke-ProductionSmoke` full ·
C21 App Insights / worker watch.

Docs: `docs/operations.md` release-9 row + "serves release 9" + fold the
un-numbered post-release-8 note; `docs/current-architecture.md:43`;
runbook Worker-activation text to the enabled readback. PR to `dev`
(docs-only), independent review, merge.

## Simplification pass — 2026-08-18

n/a — release execution and docs refresh; no application code.
