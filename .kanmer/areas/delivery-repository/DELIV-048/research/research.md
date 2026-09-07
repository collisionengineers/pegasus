# DELIV-048 research

Baseline origin/dev 1d972f05c0f10c2ecf804f271a4fd3155242f1ef.
The material hole was which release callers enforced the obsolete workstation
restriction. Current user/EPIC-014 explicitly supersedes EPIC-013 Linux-only
and requires Windows plus Linux; no separate approval is owed for this edit.

- scripts/Build-ReleaseArtifacts.ps1:12-18 rejects Windows and hardcodes
  linux-x64/efbundle. Web, OCI and Worker publish separately target linux-x64.
- scripts/Test-AzureDeploymentPlan.ps1:120-145 repeats the Linux-only pair,
  calls GetUnixFileMode unconditionally; lines180-185 assert Windows absent.
- scripts/PegasusPlatform.ps1:Get-PegasusPlatform already resolves supported
  Windows/Linux. Reuse it through one small migration-bundle helper shared by
  builder and validator; no second runtime-pair list in production.
- Both database/admin bootstrap invoke the existing Artifact gate and do not
  execute a hardcoded migration filename. Release skill/reference is the actual
  migration execution consumer. .codex skill is a canonical .agents pointer.
- Test-PegasusPlatform.ps1 is existing cheap self-contained script acceptance;
  extend with release checks. WorkerActivationReleaseContractTests copies the
  validator fixture; copy its newly imported platform dependency too.
- Current platform guidance: AGENTS.md:184, runbook.md:30-34 and981,
  current-architecture.md:109-115, ADR0037 and ADR index; operations:49 records
  the prior WSL qualification, not proof for a future release.
- Project Web runtime IDs already include linux-x64 and win-x64. No package,
  framework, infrastructure, schema or deploy change is needed.

Sources: Microsoft [EF bundles](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying)
and [SDK container publishing](https://learn.microsoft.com/en-us/dotnet/core/containers/sdk-publish)
confirm explicit bundle runtimes and container archive publishing without a
Docker daemon. No project-specific sources were declared by get_sources.

Evidence limits: read-only source inspection; no artifact build, tests or live
write. The Linux execution bit will only be asserted on Linux; paired artifacts
must match the current release workstation to avoid executing an incompatible
bundle. Each release remains on one native platform from clean SHA to migration.
