# Release 14 plan

Release SHA candidate: a3c88a7b (origin/dev, merge of PR #467). Previous release: 13 = 2325ed4a. In-flight lanes excluded and untouched: TICK-064/PR-013 (PR #468 open), AUTO-004/005, PLAT-014, TICK-053, PLAT-005.

## 1. Verification pass (before any deploy step)
- 10 parallel read-only verification lanes over a detached worktree `../pegasus-worktrees/release-14` @ a3c88a7b: 8 ticket batches covering all 36 verifying tickets (requirement coverage, live caller chain, copy compliance, merge-loss), 1 UI-copy audit of the full release diff vs docs/design/README.md, 1 merge-integrity + dark/orphaned-code sweep (incl. migration census vs Migrations folder, bicep↔config key parity).
- Local `dotnet restore` + Release build on the worktree (dev pushes have no CI; PR CI was green per merge).
- Findings adjudicated by orchestrator; fixes (if any) land as small PRs to dev before the release cut; the cut SHA is then re-recorded.

## 2. Deployment (runbook route, reusing release-13 mechanics)
Build-ReleaseArtifacts → Test-AzureDeploymentPlan (Local/Artifact/PreUpload/PreMigration/PreProvision) → oras push web image to pegasusprodacr252ow37gij → efbundle applies pending migrations (since release 13: ImageIntakeSubmissionGroup, SendToAiConnectorSettings, ImageCaseCustody) → Invoke-AzureDatabaseBootstrap (census) → azd provision --preview compared against release-13 stored preview: EXPECTED diffs = new web revision suffix/digest, worker PendingWorkDispatchSchedule '*/15 * * * * *' (INTK-015), Web Graph__BaseUri (MAIL-002); anything else = stop → azd provision → worker config-zip deploy → Invoke-ProductionSmoke. Azure writes pre-approved by operator directive 2026-08-20 for exactly these targets (rg-pegasus-prod estate).

## 3. Post-deploy verification
Live checks of each operator issue: badge=rows, mailbox-only email counter, mailbox admin without identifiers, assessment summary chip, upload confirmation options, image galleries, Box folders for image cases, worker abort silence, PollSentEvidence completions, Unidentified sweep resolution, AU17SEO consolidation path, QDOS26002 re-extraction. Browser (verification account) + read-only az/SQL.

## 4. Docs refresh (this ticket's PR to dev)
docs/operations.md release-14 row + serving statement; docs/current-architecture.md; docs/runbook.md OPS-14 previous-artifact rollback procedure section (closes TICK-029's gap); record the 2026-08-19 Sent-evidence mailbox approval. Reuses the release-13 row format verbatim.

## 5. Promotion (docs correct on BOTH branches — operator requirement)
After the docs PR merges to dev: exact-SHA atomic push of the dev head to main per docs/engineering.md#branches-and-delivery (`git push --atomic --force-with-lease=refs/heads/dev:$SHA origin $SHA:refs/heads/main $SHA:refs/heads/dev`). MERGE AUTH GRANTED given in the operator directive 2026-08-20. Stop if dev moved (e.g. PR #468 merged) — re-verify before promoting. Production then serves an ancestor of main (code SHA); main and dev identical including docs.

## 6. Closeout
Per verifying ticket: proof written from deployment + live evidence, move to done, release claim; remove only release-scope worktrees/branches (plat-002 + tick-098 worktrees, remote task/plat-002-staff-page-root + task/tick-098-rpt-03-audit-report-parity). End state: remote branches main/dev/kanmer-board + in-flight lanes only; 0 open release-scope PRs.

Simplification pass: n/a — release/deployment chore, no product code authored by this ticket (docs-only PR).
