# Post-implementation report — DELIV-054

## Outcome

The release artifact builder now creates Web and Worker ZIPs with
`System.IO.Compression.ZipFile::CreateFromDirectory`, retaining dot-prefixed
publish directories while keeping each publish root at the ZIP root. The
existing artifact validator requires the Worker `.azurefunctions/` root and
Web `.playwright/` root before OCI inspection. Its existing platform fixture
creates valid ZIPs and proves both named missing-root failures.

## Files changed

| Path | Change |
|---|---|
| `scripts/Build-ReleaseArtifacts.ps1` | Replaced the two glob-based `Compress-Archive` calls with root-preserving `ZipFile::CreateFromDirectory` calls. |
| `scripts/Test-AzureDeploymentPlan.ps1` | Added ZIP-root validation for Worker `.azurefunctions/` and Web `.playwright/`. |
| `scripts/Test-PegasusPlatform.ps1` | Replaced placeholder ZIP files with synthetic ZIP fixtures and negative missing-root assertions. |

## Governing docs

- ADR-0039 is preserved: migration-bundle workstation mapping, manifest schema
  3, and deployed Linux-x64 targets are unchanged.
- The release skill's four-artifact route remains unchanged. This correction
  affects only ZIP construction and artifact validation.

## Verification evidence

Independent host verification is recorded in `scratch/execution.md` against
the frozen three-file hashes:

- PowerShell parser check over all three scripts: exit 0.
- `pwsh -NoProfile -File ./scripts/Test-PegasusPlatform.ps1`: exit 0.
  It reported the release workstation/manifest contract and LocalDB
  classification as passed.
- `pwsh -NoProfile -File ./scripts/Test-AzureDeploymentPlan.ps1 -Mode Local`:
  exit 0. It reported `Azure deployment plan validation passed (Local; Worker
  Disabled settings render 'true').`
- The only observed warning was that a newer Bicep release exists; no upgrade
  was performed.
- Independent static/simplicity review: no findings, as reported by the parent
  controller.

## Scope and limits

No package build, application build/test/restore, browser/capture host, cloud
operation, deployment, actual release artifact, or post-merge verification ran.
The first immutable actual-release package build remains D6 work. This report
does not claim a post-merge PASS.

## Traceability

- Base SHA: `7b6aa189c2112ab3cf8df2c2e337fc9f2b0dabae`.
- Successor relationship: [[DELIV-048]] remains Verifying and unchanged.
- Provenance: unique ZIP correction from PR #676,
  `80acaf56e65d45c53f46bda75924f1a5f0dd3ed2`; no PR #676 state was changed.
- Implementation commit and draft PR: recorded after their creation in the
  ticket traceability and this report's handoff update.

## Handoff

The branch is ready for independent review on `dev`. Review should confirm
the three-file scope, the `ZipFile` root-layout argument, required
`.azurefunctions/` and `.playwright/` archive roots, and the absence of
unrelated workstation or release-procedure changes.
