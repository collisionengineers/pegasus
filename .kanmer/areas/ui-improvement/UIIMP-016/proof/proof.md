---
kind: proof-record
merged_sha: "90a7591846883dcf23ce8aef6d76bec66bb6edb4"
environment: "Ubuntu WSL, detached worktree .worktrees/verify-uiimp-016-90a7591846883dcf23ce8aef6d76bec66bb6edb4; .NET 10; package-pinned Chromium; loopback-only pinned SQL Server container"
verified_at: "2026-09-04T19:49:20+01:00"
result: PASS
attempts:
  - attempted_at: "2026-09-04T19:37:00+01:00"
    command: "pwsh -NoLogo -NoProfile -File ./scripts/Test-DocumentationLinks.ps1"
    cwd: ".worktrees/verify-uiimp-016-90a7591846883dcf23ce8aef6d76bec66bb6edb4"
    exit_code: 0
    result: PASS
    summary: "All relative Markdown links resolve (125 files checked)."
  - attempted_at: "2026-09-04T19:37:00+01:00"
    command: "pwsh -NoLogo -NoProfile -File ./scripts/Test-MarkdownPlacement.ps1 -Base 90a7591846883dcf23ce8aef6d76bec66bb6edb4^ -Head 90a7591846883dcf23ce8aef6d76bec66bb6edb4"
    cwd: ".worktrees/verify-uiimp-016-90a7591846883dcf23ce8aef6d76bec66bb6edb4"
    exit_code: 0
    result: PASS
    summary: "Markdown placement passed for the merge commit."
  - attempted_at: "2026-09-04T19:37:01+01:00"
    command: "targeted rg for remaining Narrator or mandatory screen-reader evidence wording"
    cwd: ".worktrees/verify-uiimp-016-90a7591846883dcf23ce8aef6d76bec66bb6edb4"
    exit_code: 0
    result: PASS
    summary: "Only the three explicit non-claim passages remain; no mandatory screen-reader evidence wording remains."
  - attempted_at: "2026-09-04T19:37:01+01:00"
    command: "git diff --check 90a7591846883dcf23ce8aef6d76bec66bb6edb4^ 90a7591846883dcf23ce8aef6d76bec66bb6edb4"
    cwd: ".worktrees/verify-uiimp-016-90a7591846883dcf23ce8aef6d76bec66bb6edb4"
    exit_code: 0
    result: PASS
    summary: "No whitespace errors."
  - attempted_at: "2026-09-04T19:37:01+01:00"
    command: "dotnet restore ./Pegasus.slnx --locked-mode"
    cwd: ".worktrees/verify-uiimp-016-90a7591846883dcf23ce8aef6d76bec66bb6edb4"
    exit_code: 0
    result: PASS
    summary: "Locked restore completed successfully."
  - attempted_at: "2026-09-04T19:37:03+01:00"
    command: "dotnet build ./Pegasus.slnx --configuration Release --no-restore"
    cwd: ".worktrees/verify-uiimp-016-90a7591846883dcf23ce8aef6d76bec66bb6edb4"
    exit_code: 0
    result: PASS
    summary: "Release build completed successfully."
  - attempted_at: "2026-09-04T19:38:12+01:00"
    command: "dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter Category=Browser&Category!=Corpus -- xUnit.MaxParallelThreads=2"
    cwd: ".worktrees/verify-uiimp-016-90a7591846883dcf23ce8aef6d76bec66bb6edb4"
    exit_code: 0
    result: PASS
    summary: "124 passed, 0 failed, 0 skipped in 10m56s against a fresh loopback-only pinned SQL Server container; cleanup confirmed."
---

# Proof — UIIMP-016

PASS at the exact GitHub merge SHA. The selected Chromium evidence is internally consistent across the changed authorities and the executable Browser lane passes. This proof does not claim Narrator or other screen-reader interoperability, complete WCAG conformance, subjective usability, or operator acceptance.

PR: https://github.com/collisionengineers/pegasus/pull/662

Merged at: 2026-09-04T18:35:55Z
