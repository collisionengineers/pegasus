2026-09-04 plan correction: Test-PegasusPlatform.ps1 is deliberately Windows-only LocalDB coverage. Removed it from scope; ORAS is instead proven by the Linux Offline Doctor and exact artifact build. ORAS install attempt 1 failed before installation because the checksum expected the publisher filename; attempt 2 verified the checksum and installed 1.3.4, then returned nonzero only because /tmp cannot use gio trash. Exact temporary files were subsequently unlinked and `oras version` passed.

2026-09-04 Step 1 initial checks: Test-AzureDeploymentPlan -Mode Local PASS. Cloud Doctor first attempt found ORAS 1.3.4 PASS but failed because the fresh worktree had not yet run npm ci or generated the Debug Playwright command; those are setup prerequisites, not release-tool regressions. Plan corrected from Offline to Cloud because release tools are in the Cloud profile.

## Transitions

- 2026-09-04T19:02:11.149Z lease-phase implementing → running-command (lease f9770d71-438d-4241-a126-a41b0880b5c4 rev 2; expires 2026-09-04T20:02:11.146Z)

2026-09-04 canonical rail attempt 1: restore PASS; Release build PASS (0 warnings/errors); Core 1225/1225 PASS; Integration 1264 PASS, 7 intentional skips, 0 failures in 39m48s; Architecture 99 PASS, 1 FAIL. Failure: isolated Local deployment-plan fixture did not copy scripts/Build-ReleaseArtifacts.ps1 after Local validation began inspecting it. Expanded only the architecture fixture file scope; assertions remain unchanged.

2026-09-04 final-head evidence at 287fc2e46aeee4999c8bab18349ea44f32b40b4d: focused regression PASS; exact artifact build PASS for 0.1.0-alpha.947; manifest schema 3, source exact/clean, migration runtime linux-x64, executable efbundle mode 755 (401196753 bytes), linux/amd64 OCI image digest sha256:da45a80c6bb049231baa6bf620116b97ecc29d7d79faf405d699bb67843470a0; Artifact validation PASS; Local validation PASS; documentation links PASS (126 files); Markdown placement PASS; diff check PASS. Targeted stale-wording census found only the deliberate Windows-only email-eval exception and retained historical release evidence in operations.md.

Canonical final-head rail against a fresh loopback-only pinned SQL Server container: locked restore PASS; Release build PASS with 0 warnings and 0 errors; Core 1225/1225 PASS; Architecture 100/100 PASS; Integration 1264 PASS, 7 intentional skips, 0 failures in 37m34s. Exit code 0. Container and temporary credential file were removed by the cleanup trap; post-run container census was empty.

Simplification pass completed and recorded in plan/plan.md. No application, schema, infrastructure template, CI, cloud or production state was changed.
