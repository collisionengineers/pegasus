# DELIV-048 implementation report

## Result and source

Source frozen in .worktrees/deliv-048 on DELIV-048-portable-release from
1d972f05c0f10c2ecf804f271a4fd3155242f1ef. Uncommitted; awaiting root verification
before commit/PR/Review. Fourteen scoped files, roughly 270 added/changed lines
including 114 cheap script-test lines and the new ADR. No product code,
packages, schemas, CI lanes, snapshots, cloud state or shared checkout changed.
PLAT-028 untouched and its lease renewed.

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

## Required root validation

Focused C# filter:
FullyQualifiedName~WorkerActivationReleaseContractTests

Existing script: pwsh -NoProfile -File ./scripts/Test-AzureDeploymentPlan.ps1
-Mode Local (Bicep compilation; root owns this).

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
No live or promotion/merge write has been performed by this implementation.

## Stop

Await root evidence before commit/PR/Review. Retain taken lease/worktree.
