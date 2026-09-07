## Exact-merge verification preparation — 7 September 2026

Root assigned lightweight preparation only; root remains sole heavy verifier.
PR680 was read from GitHub as MERGED at
987988e0b984ad63c1de4be3ad189f3afeb2928c, reviewed head
d2bf633ec8ddc5b08b4052554d1d4e79f3930682. Independent delta PASS
scratch/review@556f03971e0d6572 carries F-001 fixed; plan@b7ea993913c84058
and post-implementation-report@6cdb9dc68164e418 retain prior evidence.

Detached workspace created and checked:
.worktrees/verify-plat-028-987988e0b984ad63c1de4be3ad189f3afeb2928c.
Implementation .worktrees/plat-028 and PLAT-028-principal-customer remain
untouched and retained. Lease renewed in verifying through
2026-09-07T22:23:39.854Z, revision10.

### Executed preparation checks

Recorded inspection snapshot: 2026-09-07T21:54:27Z, Windows/PowerShell7.
Commands ran from the existing ticket worktree or this exact detached root.

- gh pr view 680 --json state,mergeCommit,url,headRefOid: exit0, MERGED and
  exact full SHA above.
- git fetch origin 987988e0b984ad63c1de4be3ad189f3afeb2928c
  522e67f270ab4d6086d9fba04095988db3598888: exit0; exact commits fetched.
- git worktree add --detach <exact workspace above>
  987988e0b984ad63c1de4be3ad189f3afeb2928c: exit0, detached creation.
- git rev-parse HEAD: exit0, exact merge; git symbolic-ref --short -q HEAD:
  exit1 and empty (the expected detached result); git status --porcelain:
  exit0 and empty. Guarded assertion command exited0.
- Reviewed scope enumerated with git diff --name-only --no-renames
  3da60bd0c270111d5168dc17246dc831882108ea
  d2bf633ec8ddc5b08b4052554d1d4e79f3930682: exit0, 41 paths.
  git diff --exit-code <reviewed head> <merge SHA> -- <those 41 paths>:
  exit0, exact content and removal equality across this PR's scope.
- Full-tree git diff --name-status --no-renames <reviewed head> <merge SHA>:
  exit0, 58 other paths differ. Reviewed tree196c2b57ce05138a14645e1bcbc1913b831a058b
  is NOT merged tree0fc6da760fd5e3461c496b56ee85b83da649da09.
  This is a measured difference, not full-tree or binary equivalence.
- git fetch origin dev: exit0. git merge-base --is-ancestor
  987988e0b984ad63c1de4be3ad189f3afeb2928c origin/dev: exit0, reachable.

### Root checks queued, not executed here

Use normal Release project commands in the exact detached workspace:
Core filter FullyQualifiedName~Cases.OrganizationAdministrationTests.
Integration filter FullyQualifiedName~OrganizationAdministrationWebTests|FullyQualifiedName~OrganizationAdministrationPersistenceTests|FullyQualifiedName~OrganizationDirectoryWebTests|FullyQualifiedName~PrincipalCredentialPersistenceTests|FullyQualifiedName~ProviderApiSubmissionTests.
F-001 browser filter:
(FullyQualifiedName~AccessibilityTests.RealAuthenticatedRouteHasNoAxeViolationsAndNoInlineStyleAttribute&DisplayName~Administration/Principals)|FullyQualifiedName~QdosAllocationRecoveryBrowserTests.FailedAllocationShowsSafeRecoveryWithoutRawIdentifiers

Root decides the proportionate exact-merge cohort and records its actual
command/exit/count. Premerge test evidence may support only its original
head; the complete merged tree contains other lanes and no exact-merge
runtime PASS has yet been observed. No new build/test/capture/CI/cloud/email
command ran in this preparation. All 41 scope paths, including affected
snapshots and catalogue, are unchanged from the independently reviewed head;
that is source comparison, not a new capture or visual PASS.

Do not move Done or clean up yet. Root requested PLAT-028 priority so its
overlapping composition ownership can subsequently close before TICK-035.
Whole-file proof is pending the root checks, not asserted by this scratch.
