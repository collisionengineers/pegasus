---
kind: proof-record
merged_sha: "70a9f94f1e07dfee8a3ff746e83334c55d648d1a"
environment: "detached WSL/Linux x64 worktree .worktrees/verify-deliv-047-70a9f94f1e07dfee8a3ff746e83334c55d648d1a; Docker 29.8.0; pinned loopback SQL Server container"
verified_at: "2026-09-04T22:54:11+01:00"
result: INCONCLUSIVE
failure_class: inconclusive
attempts:
  - attempted_at: "2026-09-04T21:09:00Z"
    command: "gh pr view 667 --json state,mergeCommit,url"
    cwd: "."
    exit_code: 0
    result: PASS
    summary: "PR 667 is MERGED with exact merge SHA 70a9f94f1e07dfee8a3ff746e83334c55d648d1a."
  - attempted_at: "2026-09-04T21:09:10Z"
    command: "git worktree add --detach .worktrees/verify-deliv-047-70a9f94f1e07dfee8a3ff746e83334c55d648d1a 70a9f94f1e07dfee8a3ff746e83334c55d648d1a; detached/clean/SHA assertions"
    cwd: "."
    exit_code: 0
    result: PASS
    summary: "Worktree is clean, detached and at the exact GitHub merge SHA."
  - attempted_at: "2026-09-04T21:10:00Z"
    command: "npm ci && pwsh ./scripts/Invoke-Doctor.ps1 -Profile Cloud"
    cwd: ".worktrees/verify-deliv-047-70a9f94f1e07dfee8a3ff746e83334c55d648d1a"
    exit_code: 1
    result: FAIL
    summary: "Fresh worktree lacked the generated Playwright launcher; all other Cloud tools including ORAS 1.3.4 passed. npm reported the existing 12 audit findings (11 moderate, 1 high)."
  - attempted_at: "2026-09-04T21:11:00Z"
    command: "dotnet restore ./Pegasus.slnx --locked-mode; dotnet build ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --no-restore; pwsh ./tests/Pegasus.IntegrationTests/bin/Debug/net10.0/playwright.ps1 install chromium"
    cwd: ".worktrees/verify-deliv-047-70a9f94f1e07dfee8a3ff746e83334c55d648d1a"
    exit_code: 0
    result: PASS
    summary: "Applied the Doctor-provided fresh-worktree repair; build passed with zero warnings/errors and the pinned Chromium payload is installed."
  - attempted_at: "2026-09-04T21:12:00Z"
    command: "pwsh ./scripts/Invoke-Doctor.ps1 -Profile Cloud"
    cwd: ".worktrees/verify-deliv-047-70a9f94f1e07dfee8a3ff746e83334c55d648d1a"
    exit_code: 0
    result: PASS
    summary: "Cloud Doctor passed every prerequisite, including Linux platform, Docker, Azure tools, ORAS 1.3.4, browser, sqlcmd and PowerShell modules; it grants no external-operation approval."
  - attempted_at: "2026-09-04T21:12:30Z"
    command: "pwsh ./scripts/Build-ReleaseArtifacts.ps1 -Version 0.1.0-alpha.947 -SourceRevision 70a9f94f1e07dfee8a3ff746e83334c55d648d1a"
    cwd: ".worktrees/verify-deliv-047-70a9f94f1e07dfee8a3ff746e83334c55d648d1a"
    exit_code: 0
    result: PASS
    summary: "Exact clean merge-SHA artifacts built: schema 3, linux-x64 efbundle, four hash-bound artifacts and linux/amd64 OCI digest sha256:c4fbfe6f7e76d298ed565b43011d0fa1da1368bce85218d653b6991b71fe1d70."
  - attempted_at: "2026-09-04T21:14:00Z"
    command: "pwsh ./scripts/Test-AzureDeploymentPlan.ps1 -Mode Local; pwsh ./scripts/Test-AzureDeploymentPlan.ps1 -Mode Artifact -ManifestPath ./artifacts/releases/0.1.0-alpha.947/release-manifest.json"
    cwd: ".worktrees/verify-deliv-047-70a9f94f1e07dfee8a3ff746e83334c55d648d1a"
    exit_code: 0
    result: PASS
    summary: "Local and Artifact validation passed at the exact merge SHA."
  - attempted_at: "2026-09-04T21:14:20Z"
    command: "chmod u-x efbundle; run Artifact validation expecting rejection; restore chmod u+x"
    cwd: ".worktrees/verify-deliv-047-70a9f94f1e07dfee8a3ff746e83334c55d648d1a/artifacts/releases/0.1.0-alpha.947"
    exit_code: 0
    result: PASS
    summary: "Underlying Artifact validation exited nonzero and reported that the Linux bundle must be executable by its owner; permission was restored to 755."
  - attempted_at: "2026-09-04T21:15:00Z"
    command: "pwsh ./scripts/Test-DocumentationLinks.ps1; pwsh ./scripts/Test-MarkdownPlacement.ps1 -Base 70a9f94f1^ -Head HEAD; git diff --check 70a9f94f1^...HEAD"
    cwd: ".worktrees/verify-deliv-047-70a9f94f1e07dfee8a3ff746e83334c55d648d1a"
    exit_code: 0
    result: PASS
    summary: "All 126 Markdown files linked correctly; placement and diff checks passed."
  - attempted_at: "2026-09-04T21:16:00Z"
    command: "dotnet restore ./Pegasus.slnx --locked-mode; dotnet build ./Pegasus.slnx --configuration Release --no-restore; dotnet test ./Pegasus.slnx --configuration Release --no-build --filter Category!=Corpus"
    cwd: ".worktrees/verify-deliv-047-70a9f94f1e07dfee8a3ff746e83334c55d648d1a"
    exit_code: 0
    result: PASS
    summary: "Restore passed; Release build passed with zero warnings/errors; Core 1225/1225, Architecture 100/100, Integration 1267 passed / 7 skipped / 0 failed in 40m39s against fresh loopback SQL Server. Container and temporary credential file were removed."
  - attempted_at: "2026-09-04T22:54:11+01:00"
    command: "authorised dev-to-main promotion, production migration/upload/provision/deploy, full smoke, and current-state documentation refresh"
    cwd: "authorised Linux release terminal"
    exit_code: null
    result: INCONCLUSIVE
    summary: "Not run: Azure CLI and azd need fresh authentication; no exact-target production cloud/database write approval or immediate MERGE AUTH GRANTED was supplied."
---

# Verification proof — DELIV-047

## Result

The implementation merged to `dev` is fully verified locally at the exact GitHub merge SHA. Linux artifact identity, permission failure handling, release-plan validation, documentation, build and tests all pass. The initial fresh-worktree Doctor failure and successful identical retry are retained above.

The ticket remains **INCONCLUSIVE** rather than Done because its acceptance criterion explicitly requires an authorised production release and smoke. Completion requires fresh Azure authentication, operator approval for the exact production targets and writes, and the exact phrase `MERGE AUTH GRANTED` immediately before promoting `dev` to `main`. After the live route passes, both current-state documents must be refreshed before this proof can become PASS.
