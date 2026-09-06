# Validated common Foundation handoff

Common base D: 3284f93fc3ea9fd3bbbea9405ec92dc7818378f2.
Validated F: dc3cfd908348b38d69b5d5357c6d5899bfe5f812.
Providing branch: origin/task/pegasus-v1-platform.
Shared range consists of d819034ca6b4716e5f9a2e9a94f309edd14371c6,
5713d9b584a36433cf235a305d0aa3f18a5143cf and dc3cfd908348b38d69b5d5357c6d5899bfe5f812.

B/C adoption is now authorized: fetch origin, then on the owning clean branch
run `git merge --ff-only dc3cfd908348b38d69b5d5357c6d5899bfe5f812`; verify
`git merge-base --is-ancestor dc3cfd908348b38d69b5d5357c6d5899bfe5f812 HEAD`. Do not cherry-pick.
Both remote domain branches were created at D; their owners perform this FF.
No domain edits were placed on B/C by A. Record consumption SHA in this ticket.

Foundation is an incomplete development checkpoint. New domain ports are
passive until their named A/B/C implementations and A-authored composition
arrive. All C# signatures are authoritative; docs/engineering.md maps consumers.
Logical reads select exactly one full DocumentId/VersionId pair OR IntakeAssetId;
nullable Case/receipt contexts must match persisted authorized associations.
Glass's is the human brand; GlassRepairEstimate technical names remain.

Validation in ../pegasus-worktrees/v1-platform, Windows PowerShell:
- locked solution restore: PASS exit0.
- solution Release build --no-restore: PASS exit0, zero warnings/errors.
- ordered SQL migration + restricted Web/Worker representative writes/denials
  + administrator leaseclear exactreplay: PASS 3/3.
- runtime grant/lease generation/concurrency suite: 18 PASS, 1 historical
  assertion failure; corrected fixture scope without changing its original
  expectations. F-specific exact permission assertion added.
- corrected historical grant assertion and latest F permissions: PASS 2/2.
- Core CaseEditAuthorityTests: PASS 19/19.
- ArchitectureTests: PASS 100/100.
- Test-MigrationGrants.ps1: PASS,95 migration files.
- Test-AzureDeploymentPlan.ps1 -Mode Local: PASS, Worker disabled.
- git diff --check: PASS. Final F worktree clean.

Earlier failed seed SQL, dynamic DENY SQL, fixture compilation and historical
matrix assertions remain recorded in PLAT-075 scratch/foundation; their fixes
are in this shared range. No full v1/corpus/provider/deployment proof claimed.
No cloud/Box/mail/Glass's/EVA live writes or dev/main merge occurred.

A continues A01/A04. B/C may now implement their streams using the same F
commit objects. Later shared corrections follow common G protocol; concrete
domain-only DI patches remain A-authored against each recorded branch head.
