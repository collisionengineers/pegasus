# Research — DELIV-054: hidden runtime directories in release ZIPs

## Question

Why current release ZIP construction can omit required hidden runtime directories,
and what smallest correction restores those entries without changing the
portable workstation, OCI image, or manifest contracts.

## Findings

- Current `scripts/Build-ReleaseArtifacts.ps1` uses
  `Compress-Archive -Path (Join-Path <publish-root> '*')` for both
  `web.zip` and `worker.zip`. The wildcard excludes dot-prefixed source
  directories on Linux.
  - Source: current builder, read 2026-09-08.
- PR #676 commit `80acaf56e65d45c53f46bda75924f1a5f0dd3ed2` replaces those
  two calls with `System.IO.Compression.ZipFile::CreateFromDirectory` with
  `includeBaseDirectory = $false`, preserving the ZIP-root layout while
  including dot-prefixed entries.
  - Source: `git show 80acaf56 -- scripts/Build-ReleaseArtifacts.ps1`.
- The current branch already carries ADR-0039's portable workstation
  migration-bundle mapping and schema-3 manifest behavior. The ZIP correction
  is absent; reinstating the old Linux-only guard would conflict with that ADR.
  - Source: `scripts/Build-ReleaseArtifacts.ps1`;
    `docs/adr/0039-windows-and-linux-release-workstations.md`.
- Retained release-36 archives contain root-prefixed
  `.azurefunctions/` Worker entries and `.playwright/` Web entries.
  - Source: read-only `tar -tf` inventory of
    `artifacts/releases/release-36-84132d01/worker.zip` and `web.zip`.
- Current artifact-manifest validation verifies manifest identity and OCI
  metadata but does not inspect either ZIP's entries. Its existing lightweight
  fixture currently supplies text files in place of ZIPs.
  - Source: `scripts/Test-AzureDeploymentPlan.ps1` and
    `scripts/Test-PegasusPlatform.ps1`.

## Implications

Use the established `ZipFile::CreateFromDirectory` replacement for both ZIPs
with no base directory. Add archive-entry checks for the Worker
`.azurefunctions/` and Web `.playwright/` roots to the existing artifact
validator, and make its existing fixture real ZIP archives with those hidden
directories. This confines the change to the release builder and its present
contract test; it keeps the native bundle, OCI, and manifest behavior intact.

## Open questions

- None. The ticket and ADR-0039 set the required scope and workstation policy.
