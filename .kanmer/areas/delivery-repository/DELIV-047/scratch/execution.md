2026-09-04 plan correction: Test-PegasusPlatform.ps1 is deliberately Windows-only LocalDB coverage. Removed it from scope; ORAS is instead proven by the Linux Offline Doctor and exact artifact build. ORAS install attempt 1 failed before installation because the checksum expected the publisher filename; attempt 2 verified the checksum and installed 1.3.4, then returned nonzero only because /tmp cannot use gio trash. Exact temporary files were subsequently unlinked and `oras version` passed.

2026-09-04 Step 1 initial checks: Test-AzureDeploymentPlan -Mode Local PASS. Cloud Doctor first attempt found ORAS 1.3.4 PASS but failed because the fresh worktree had not yet run npm ci or generated the Debug Playwright command; those are setup prerequisites, not release-tool regressions. Plan corrected from Offline to Cloud because release tools are in the Cloud profile.

## Transitions

- 2026-09-04T19:02:11.149Z lease-phase implementing → running-command (lease f9770d71-438d-4241-a126-a41b0880b5c4 rev 2; expires 2026-09-04T20:02:11.146Z)

2026-09-04 canonical rail attempt 1: restore PASS; Release build PASS (0 warnings/errors); Core 1225/1225 PASS; Integration 1264 PASS, 7 intentional skips, 0 failures in 39m48s; Architecture 99 PASS, 1 FAIL. Failure: isolated Local deployment-plan fixture did not copy scripts/Build-ReleaseArtifacts.ps1 after Local validation began inspecting it. Expanded only the architecture fixture file scope; assertions remain unchanged.
