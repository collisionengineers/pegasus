## Exact-merge verification preparation — 7 September 2026

Root assigned lightweight preparation only; root remains sole heavy verifier.
PR682 was read from GitHub as MERGED at
522e67f270ab4d6086d9fba04095988db3598888, reviewed head
1bf9ac613a2b7610d2ddc23e8acfd9f4b462ef79. Independent PASS
scratch/review@fdf4270802857212, plan@b71b55e37264f8dc and
post-implementation-report@08e9663b3940a805 are retained.

Detached workspace created and checked:
.worktrees/verify-docs-020-522e67f270ab4d6086d9fba04095988db3598888.
Implementation .worktrees/docs-020 and DOCS-020-report-consistency remain
untouched and retained. Lease renewed in verifying through
2026-09-07T22:23:39.922Z, revision8.

### Executed preparation checks

Recorded inspection snapshot: 2026-09-07T21:54:27Z, Windows/PowerShell7.

- gh pr view 682 --json state,mergeCommit,url,headRefOid: exit0, MERGED and
  exact full SHA above.
- Exact commit fetch and git worktree add --detach <exact workspace above>
  522e67f270ab4d6086d9fba04095988db3598888: exit0.
- git rev-parse HEAD: exit0, exact merge; git symbolic-ref --short -q HEAD:
  exit1 and empty (expected detached); git status --porcelain: exit0 empty.
  Guarded assertion command exited0.
- Scope enumerated by git diff --name-only --no-renames
  2e50fde474ce35eb32eff8677eb2327cb6aad272
  1bf9ac613a2b7610d2ddc23e8acfd9f4b462ef79: exit0, 15 paths.
  git diff --exit-code <reviewed head> <merge SHA> -- <those 15 paths>:
  exit1, one shared test file differs. This source comparison result is
  preserved; it is not a failing runtime assertion or full-scope equality.
- Full inspected diff of AzureSqlRuntimeRoleMigrationTests.cs adds only
  the already-merged INTK-061 using, real Worker custody-claim assertions
  and ConnectedContextFactory. DOCS-020 grant expectation edits remain.
  Explicit git diff --exit-code on the other 14 DOCS-020 changed paths:
  exit0; all product/FRD/grant-migration/bootstrap/report test changes match.
- Full-tree git diff --name-status --no-renames <reviewed head> <merge SHA>:
  exit0, 95 paths differ. Reviewed tree5886f6eca7cc9df3610b7930f99437d044ff74c3
  is NOT merged treecc15ddf4e821b7eb38af4578b86504de7140ab25.
- git fetch origin dev: exit0. git merge-base --is-ancestor
  522e67f270ab4d6086d9fba04095988db3598888 origin/dev: exit0, reachable.

### Root checks queued, not executed here

Use existing Release Integration filters in the exact detached workspace:
FullyQualifiedName~CaseReportGenerationPersistenceTests|FullyQualifiedName~AssessmentReportDraftWebTests|FullyQualifiedName~AzureSqlRuntimeRoleMigrationTests.LatestMigrationGivesFoundationTablesTheirExactRuntimePermissions|FullyQualifiedName~CaseArtifactCustodyRecoveryTests

Complementary report-plan coverage:
FullyQualifiedName~CaseReportDeliveryPreparationPersistenceTests|FullyQualifiedName~DocumentCustodyDurabilityTests|FullyQualifiedName~StaffAccountAdministrationPersistenceTests|FullyQualifiedName~AssessmentPersistenceIntegrationTests.ReportDraftGenerationThroughProductionProjectionResolvesSignOffAndFailsClosedWithoutIt

Root schedules a proportionate cohort after PLAT-028 and current intake/
estimate checks; it need not repeat the broad 148-case premerge batch merely
because this scratch lists its original evidence owners. Record actual
command/exit/count at the exact merge. The report's original compiler,
harness and missing-capture failures stay preserved alongside corrected
passes. No new build/test/capture/CI/cloud/email was run in this preparation.
No Done, cleanup, deployed correctness, visual PASS or full-tree equivalence
is claimed. Whole-file proof remains pending root's executable evidence.

7 September22:43 UTC: exact merged restore/build PASS139.38s, focused53 test cohort52 PASS/1 FAIL134s. Whole FAIL proof50597d092c61d794 read back; returned same ticket to Implementing. Source checkout is clean. `git fetch origin dev` passed; `git merge --ff-only origin/dev` refused with `fatal: Not possible to fast-forward, aborting` because integrated squash history differs from author branch. Wrapper later reads exited0, so that is not the merge's exit code or a merge PASS; no source changed. Preserve existing branch/history with ordinary merge of origin/dev instead of reset/rebase. Only planned follow-up behavior is the exact pending migration expectation.

## Follow-up exact-merge preparation — 2026-09-07

PR685 independently passed review4db9227104d0a60e/public5135623992 and merged at22:53:39Z as 4d7ad4a0d2593300fd02527838aa2f1cf6555860; fresh gates returned Review→Verifying. Original proof50597d092c61d794 remains FAIL, unmodified, and retains PR682's 52 passes/one failure. No Done/cleanup claim.

Created .worktrees/verify-docs-020-4d7ad4a0d2593300fd02527838aa2f1cf6555860 from the exact fetched GitHub merge. Resolved root/common Git, detached branch, exact HEAD and tracked/untracked cleanliness all checked; merge-base --is-ancestor to fetched origin/dev exited0. Author and old failed verification workspaces retained.

`git diff --exit-code 2b1d700ba70336560934b171fa623d8c66df5559 4d7ad4a0d2593300fd02527838aa2f1cf6555860 -- tests/Pegasus.IntegrationTests/CaseWorkflowMigrationTests.cs` exit0. Full reviewed/merged trees differ in exactly four INTK-062 public-upload paths, not whole-tree equality. Reviewed tree1f1b1777d9aca8c7b9b0b8b6b373bb09d7c70112; merged tree5c283cf2712d16add80c5613dcb38e57fdf3e955.

Versus original tested522e67f270ab4d6086d9fba04095988db3598888, the full change census is only those four public-upload paths plus the one corrected migration expectation. Core/Infrastructure, report tests, runtime-role tests, custody recovery tests and integration project inputs are unchanged (scoped diff exit0); no report application behavior was changed by the correction or this merge. This supports root's explicit one-failed-case rerun, not repeating the unchanged52 tests.

Root focused merged filter: FullyQualifiedName=Pegasus.IntegrationTests.CaseWorkflowMigrationTests.CustodyEvidenceOrdinalsAndOperationsMigrateFromPreviousSchemaWithoutIdentityLoss. Use a distinct merged TRX/log name. No build/test/CI was run during preparation, and no premerge result has been relabelled as a merged PASS. Final whole proof must retain all original and correction attempts.
