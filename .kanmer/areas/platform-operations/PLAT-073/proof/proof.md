---
kind: proof-record
merged_sha: "a33896724339591d07862bd5223f9d689a355aa7"
environment: "detached worktree .worktrees/verify-plat-073-a33896724339591d07862bd5223f9d689a355aa7 on WSL Linux"
verified_at: "2026-09-04T19:07:41+01:00"
result: PASS
attempts:
  - attempted_at: "2026-09-04T18:04:00+01:00"
    command: "pwsh ./scripts/Invoke-Doctor.ps1 -Profile Offline"
    cwd: ".worktrees/verify-plat-073-a33896724339591d07862bd5223f9d689a355aa7"
    exit_code: 1
    result: FAIL
    summary: "Fresh detached checkout lacked its repository-local Azurite install and generated Playwright installer; Doctor correctly reported both."
  - attempted_at: "2026-09-04T18:04:05+01:00"
    command: "pwsh ./scripts/Invoke-Doctor.ps1 -Profile Cloud"
    cwd: ".worktrees/verify-plat-073-a33896724339591d07862bd5223f9d689a355aa7"
    exit_code: 1
    result: FAIL
    summary: "Same pre-initialization repository-local payload omissions; all host Cloud tools including sqlcmd v1.10.0 passed."
  - attempted_at: "2026-09-04T18:04:10+01:00"
    command: "dotnet restore ./Pegasus.slnx --locked-mode"
    cwd: ".worktrees/verify-plat-073-a33896724339591d07862bd5223f9d689a355aa7"
    exit_code: 0
    result: PASS
    summary: "Locked restore completed."
  - attempted_at: "2026-09-04T18:05:00+01:00"
    command: "dotnet build ./Pegasus.slnx --configuration Release --no-restore"
    cwd: ".worktrees/verify-plat-073-a33896724339591d07862bd5223f9d689a355aa7"
    exit_code: 0
    result: PASS
    summary: "Release build succeeded with zero warnings and errors."
  - attempted_at: "2026-09-04T18:05:45+01:00"
    command: "dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build --filter FullyQualifiedName~MainBranchHistoryGuardTests|FullyQualifiedName~WorkerActivationReleaseContractTests"
    cwd: ".worktrees/verify-plat-073-a33896724339591d07862bd5223f9d689a355aa7"
    exit_code: 0
    result: PASS
    summary: "22 passed, 0 failed."
  - attempted_at: "2026-09-04T18:06:00+01:00"
    command: "pwsh ./scripts/Test-DocumentationLinks.ps1"
    cwd: ".worktrees/verify-plat-073-a33896724339591d07862bd5223f9d689a355aa7"
    exit_code: 0
    result: PASS
    summary: "All relative links resolve across 125 Markdown files."
  - attempted_at: "2026-09-04T18:06:05+01:00"
    command: "pwsh ./scripts/Test-MarkdownPlacement.ps1 -Base HEAD^ -Head HEAD"
    cwd: ".worktrees/verify-plat-073-a33896724339591d07862bd5223f9d689a355aa7"
    exit_code: 0
    result: PASS
    summary: "Markdown placement passed."
  - attempted_at: "2026-09-04T18:06:10+01:00"
    command: "git diff --check HEAD^"
    cwd: ".worktrees/verify-plat-073-a33896724339591d07862bd5223f9d689a355aa7"
    exit_code: 0
    result: PASS
    summary: "No whitespace errors."
  - attempted_at: "2026-09-04T18:06:15+01:00"
    command: "task-root Kanmer get_status invoked from detached repository cwd"
    cwd: ".worktrees/verify-plat-073-a33896724339591d07862bd5223f9d689a355aa7"
    exit_code: 1
    result: FAIL
    summary: "The ad-hoc Node client could not resolve the MCP SDK from the repository cwd; server or repository code did not run."
  - attempted_at: "2026-09-04T19:05:00+01:00"
    command: "pwsh ./scripts/Initialize-LocalDevelopment.ps1"
    cwd: ".worktrees/verify-plat-073-a33896724339591d07862bd5223f9d689a355aa7"
    exit_code: 0
    result: PASS
    summary: "Installed repository-local pinned payloads; embedded Offline Doctor passed."
  - attempted_at: "2026-09-04T19:06:00+01:00"
    command: "pwsh ./scripts/Invoke-Doctor.ps1 -Profile Offline"
    cwd: ".worktrees/verify-plat-073-a33896724339591d07862bd5223f9d689a355aa7"
    exit_code: 0
    result: PASS
    summary: "All Linux Offline prerequisites passed after required initialization."
  - attempted_at: "2026-09-04T19:06:10+01:00"
    command: "pwsh ./scripts/Invoke-Doctor.ps1 -Profile Cloud"
    cwd: ".worktrees/verify-plat-073-a33896724339591d07862bd5223f9d689a355aa7"
    exit_code: 0
    result: PASS
    summary: "All Linux Cloud prerequisites passed without authentication or external write."
  - attempted_at: "2026-09-04T19:07:00+01:00"
    command: "task-root Kanmer get_status invoked from the pinned Kanmer tool cwd with detached repo-root"
    cwd: "/home/pguser/tools/kanmer"
    exit_code: 0
    result: PASS
    summary: "Kanmer v0.4.1 reports repo.upToDate true; only compensated board-config information remains."
  - attempted_at: "2026-09-04T18:03:00+01:00"
    command: "GitHub repository-check / Test UI"
    cwd: "GitHub Actions run 33897937098 at exact PR head edb42e325b24e1c66de84e3e1dc1fb22b8fefa56"
    exit_code: 1
    result: FAIL
    summary: "Initial Test UI job exceeded its explicit 35-minute budget after 120/120 browser captures passed while non-browser capture continued."
  - attempted_at: "2026-09-04T18:03:05+01:00"
    command: "GitHub rerun failed jobs / Test UI"
    cwd: "GitHub Actions run 33897937098 at unchanged PR head edb42e325b24e1c66de84e3e1dc1fb22b8fefa56"
    exit_code: 0
    result: PASS
    summary: "Unchanged-head rerun passed; all applicable PR checks green before merge."
---

# Verification outcome

PASS. The exact GitHub merge commit is detached, clean and matches the recorded SHA. Initial failures were prerequisite ordering and verifier-client resolution errors, followed by the same checks passing without code changes. Hosted Test UI's timeout and unchanged-head successful rerun are retained. No application, cloud or production operation was performed.
