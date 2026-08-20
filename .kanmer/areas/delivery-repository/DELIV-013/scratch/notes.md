2026-08-20 ~11:15Z progress:
- Release candidate a3c88a7b: local Release build 0 warnings / 0 errors; Test-AzureDeploymentPlan -Mode Local pass; Test-MigrationGrants 57 files pass.
- Pending migrations vs release 13 confirmed: 20260820034652_ImageIntakeSubmissionGroup, 20260820040337_SendToAiConnectorSettings, 20260820055900_ImageCaseCustody (all additive → previous-artifact rollback stays valid against migrated schema).
- Production confirmed serving 2325ed4a (diagnostics/version). release-13-2325ed4a artifacts retained (azd-preview.txt for byte-compare, worker.zip for rollback).
- PR #467 full green CI; PR #466 docs-only (docs/capabilities.md + frd-11), dotnet lanes legitimately path-skipped.
- 10 verification lanes running (8 ticket batches, UI-copy audit, merge-integrity sweep) over release-14 worktree.
- Runbook already records the 2026-08-19 Sent-evidence mailbox approval (release 12) — no docs gap there. Remaining owed docs: operations release-14 row + serving statement, current-architecture refresh, runbook "Previous-artifact rollback" procedure (closes TICK-029 gap). Rollback outline: web = re-pin previous digest/revision via azd env + preview-gated provision; worker = config-zip of retained previous worker.zip; DB = roll-forward only, additive migrations keep previous app valid, restore is Recovery-section territory with its own approvals.
