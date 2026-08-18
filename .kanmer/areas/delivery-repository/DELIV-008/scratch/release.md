## Release 9 transcript — 2026-08-18 (claude-code)

Candidate SHA `f1e116c6eb939f901f32e5f89d58d1d8a4701851` = origin/dev after PRs #393 (TICK-026), #402 (DELIV-007), #403 (AUTO-001) merged. PR #400 checks re-running on it.

Release worktree `../pegasus-worktrees/deliv-008-release-9` @ f1e116c6, clean.

- C1 `dotnet build ./Pegasus.slnx -c Release` → 0 warnings, 0 errors (57 s).
- C2 `Test-AzureDeploymentPlan.ps1 -Mode Local` → pass.
- C3 `Build-ReleaseArtifacts.ps1 -Version 0.1.0-alpha.1 -SourceRevision f1e116c6…` → `artifacts/releases/0.1.0-alpha.1/{web.zip, web-image.tar.gz, worker.zip, efbundle.exe, release-manifest.json}`.
  Manifest: sourceStatus clean; migrationIdentity `20260814094632_DropBoxFileRequests`; webImage `pegasus/web:f1e116c6…` digest `sha256:63e863242479326d6ef359ef37c0d82ed3db894b6facfe93f2c36d8c489bdd13` (linux/amd64, OCI); tools dotnet 10.0.302 / az 2.88.0 / azd 1.29.0.
- C4 `-Mode Artifact` → pass. Manifest SHA-256 `67A9C17A8ED42F577F3C8F5ACC0184FA454F4D839E7770E86254B86A3B66E324`.
- Pre-checks: ACR authentication-as-arm `enabled`; Worker nine `Disabled` settings all `false`; KV secret `automation-mcp-client-secret` versioned id `…/68ff4a6b656d4768a3d83313f8d80ca9`, Web identity has Key Vault Secrets User on it; azd env refreshed (output keys present).
