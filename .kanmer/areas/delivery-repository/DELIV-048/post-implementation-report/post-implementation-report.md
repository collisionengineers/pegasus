# DELIV-048 implementation report

## Current follow-up — 8 September 2026

The original PR681 was squash-merged into dev at
1c1d7a0a45555604bafd3e732bd606bf083b804a. Exact-merge proof2462b18082379607
found one implementation omission: actual Windows Get-PegasusRepairHint oras
still demanded Linux (command07e9d2,exit1,1.512s). Full proof was read;
Verifying returned to Implementing. The original evidence below is retained.

Root resumed the same recorded branch/worktree through a ready packet under
planea1fca1e3f50f1fb. The clean normal baseline merge initially conflicted
only on ADR index's newly added0040 row. Root retained accepted dev's row
using apply_patch, then proved the complete staged tree identical to
96777888bfa7ee7f85d63979a4a09ae10cda7d13 before merge commit59c43f567.
No source was discarded, no force/stash/rebase and no shared ref changed.

The follow-up modifies exactly two already mapped files (+6/-2):
PegasusPlatform.ps1 now gives Windows the same existing official pinned
ORAS1.3.4 installation guidance; Test-PegasusPlatform's existing two-host loop
supplies Kind and checks the actual repair hint for both Windows and Linux.
The production caller is Invoke-Doctor.ps1:547. No new installer, package,
convention, abstraction, application code, schema or cloud action.

Actual command3e756e passed exit0,2.2845s: git diff --check,
pwsh -NoProfile -File ./scripts/Test-PegasusPlatform.ps1, and native
Get-PegasusRepairHint oras. All original manifest/negative/LocalDB assertions
remain and pass. No .NET build, native Linux claim or new artifact validation.
Root authored this follow-up; a different agent must independently review it.
Final exact integrated release-artifact validation remains required before Done.
This corrects an existing ADR0039 convention, not a new command convention.

## Retained original PR681 report

## Result and source

Source frozen in .worktrees/deliv-048 on DELIV-048-portable-release from
1d972f05c0f10c2ecf804f271a4fd3155242f1ef. Root's focused verification passed; commit
b6ffdeda1f8eee7f71e86ca033260de20483cb60 is pushed in PR #681 to dev:
https://github.com/collisionengineers/pegasus/pull/681. Fourteen scoped files, roughly 270 added/changed lines
including 114 cheap script-test lines and the new ADR. No product code,
packages, schemas, CI lanes, snapshots, cloud state or shared checkout changed.
PLAT-028 source remained untouched by this lane.

Builder and validator use the same Get-PegasusMigrationBundle mapping through
existing Get-PegasusPlatform. Actual Windows/Linux x64 guard; exact matching
runtime and filename; Linux-only permission API. Web/Worker Linux RID and
OCI linux/amd64 preserved. Existing bootstrap consumers still validate the
manifest. The release skill's migration command resolves its executable from
that validated manifest. No copied compatibility route or Docker dependency.

ADR-0039 supersedes0037; historic0037 decision body and deployed operations
history preserved. Current AGENTS, runbook, as-built tooling, canonical skill
and .codex forwarding agree. The docs skill required append-only ADR
supersession; release skill supplied the retained route and permission gates.

## Changed files

| Action | Path | Purpose |
| --- | --- | --- |
| Modify | scripts/PegasusPlatform.ps1 | Existing platform helper; one release bundle identity. |
| Modify | scripts/Build-ReleaseArtifacts.ps1 | Host-matching EF bundle; keep Linux deployed artifacts. |
| Modify | scripts/Test-AzureDeploymentPlan.ps1 | Host/pair validation, Linux-only mode check; keep safety gates. |
| Modify | scripts/Test-PegasusPlatform.ps1 | Cheap existing script acceptance, actual/mocked supported host cases. |
| Modify | tests/Pegasus.ArchitectureTests/WorkerActivationReleaseContractTests.cs | Validator fixture imports required platform file. |
| Modify | AGENTS.md | Portable release convention, outside managed section only. |
| Modify | docs/runbook.md | Native Windows/Linux release path and exact bundle selection. |
| Modify | docs/current-architecture.md | Repository tooling paragraph, no deployment claim. |
| Modify | docs/adr/0037-linux-authorised-release-workstation.md | Supersession metadata/status only. |
| Add | docs/adr/0039-windows-and-linux-release-workstations.md | User-approved workstation decision; no new deploy route. |
| Modify | docs/adr/README.md | Decision index. |
| Modify | .agents/skills/pegasus-release/SKILL.md | Canonical current release procedure. |
| Modify | .agents/skills/pegasus-release/references/database-migration.md | Manifest-named migration executable. |
| Modify | .codex/skills/pegasus-release/SKILL.md | Preserve canonical forwarding; name portable route. |

## Checks

PASS: PowerShell AST parse all four changed scripts (exit0).
PASS: pwsh -NoProfile -File ./scripts/Test-PegasusPlatform.ps1 (exit0, about2s).
This exercised Windows native manifest/hash/OCI-stub acceptance, Windows and
Linux mapping branches, wrong filename/path, crossed pair/other-host rejection,
corrupt hash rejection before ORAS, and existing eight LocalDB classifications.
Linux owner-execute assertion is conditional and was NOT executed on Windows.
No compiler, database, Docker, ORAS binary or cloud service ran for this check.
PASS: git diff --check (separate command exit0; line-ending notices only).
Read-only discovery had one rg missing old Test-RepositoryPolicy.ps1 path;
not a validation failure and no action taken on the absent old script.

## Root verification (pre-merge)

Root supplied completed exit-0 evidence from this Windows worktree; tool
command output is retained in the parent run, not a separate log file. No
worker repeated any heavy command.

| Check | Result |
| --- | --- |
| dotnet restore ./Pegasus.slnx --locked-mode | PASS. |
| dotnet build ./Pegasus.slnx --configuration Release --no-restore | PASS; 0 warnings, 0 errors; 59.58 seconds. |
| Architecture Release --no-build, filter below | PASS; 17/17 tests; 35 seconds. |
| pwsh -NoProfile -File ./scripts/Test-AzureDeploymentPlan.ps1 -Mode Local | PASS; Bicep update-available warning only. |

## Remaining integrated validation

Focused C# filter:
FullyQualifiedName~WorkerActivationReleaseContractTests

Completed Local script: pwsh -NoProfile -File
./scripts/Test-AzureDeploymentPlan.ps1 -Mode Local (root PASS above).

Final clean integrated release, once only: Build-ReleaseArtifacts.ps1 -Version
<approved-version> -SourceRevision <exact-clean-head>, then existing
Test-AzureDeploymentPlan.ps1 -Mode Artifact -ManifestPath <manifest>.
Do not build/restore independently for this lane. No UI snapshot scope.
Native Linux script/artifact behavior remains explicitly unexercised locally;
mocked mappings are not native execution or deployment evidence.

## Traceability and deferred boundary

Removed old DELIV-047 blocking relation on root authorization because its
Linux-cutover prerequisite does not govern the new cross-platform brief;
retained DELIV-047 as related context. EPIC-013 original body/group preserved,
EPIC-014 added and current brief appended. Profile fix selected for release
consequence. Existing ADR0007 linked. New ADR0039 exists only in the isolated
worktree; link_doc rejects it against the stale shared checkout. Keep docs_todo
until the new file is visible to board validation after integration; do not
copy it into the protected shared checkout to bypass the check.

OCR root lane will separately amend Test-AzureDeploymentPlan's deferred Azure
services assertion; this diff touched only platform/bundle source assertions.
No live cloud or promotion/deployment write has been performed by this
implementation. The ticket PR is authorised separately and does not perform
a release.

## Stop

Root supplied the required focused evidence and authorised a separate PR to
dev with [skip ci], then Implementing to Review after live gates. Retain the
taken lease/worktree; independent kanmer-review owns exact-head review and
merge. Final clean integrated release packaging and required CI remain owed
once at the coordinated head. No required check is waived and no post-merge
proof is claimed.
