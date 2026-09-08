---
kind: proof-record
schema: 2
merged_sha: "6509746913eda16d2c4440add20e7f6793500f0b"
environment: ".worktrees/verify-deliv-054-6509746913eda16d2c4440add20e7f6793500f0b; Windows PowerShell 7; CEALEX-May25"
verified_at: "2026-09-08T14:22:08.230106Z"
result: PASS
receipts: []
attempts:
  - attempted_at: "2026-09-08T14:21:53.758059Z"
    command: "pwsh -NoProfile -Command '$files = @(\"scripts/Build-ReleaseArtifacts.ps1\", \"scripts/Test-AzureDeploymentPlan.ps1\", \"scripts/Test-PegasusPlatform.ps1\"); foreach ($file in $files) { $tokens = $null; $errors = $null; [void][System.Management.Automation.Language.Parser]::ParseFile((Resolve-Path -LiteralPath $file), [ref]$tokens, [ref]$errors); if ($errors.Count -ne 0) { $errors | ForEach-Object { Write-Error (\"{0}: {1}\" -f $file, $_.Message) }; exit 1 }; Write-Output (\"PARSE PASS: {0}\" -f $file) }'"
    cwd: ".worktrees/verify-deliv-054-6509746913eda16d2c4440add20e7f6793500f0b"
    exit_code: 0
    result: PASS
    authority: authoritative
    summary: "All three changed scripts emitted PARSE PASS."
  - attempted_at: "2026-09-08T14:22:00.253693Z"
    command: "pwsh -NoProfile -File ./scripts/Test-PegasusPlatform.ps1"
    cwd: ".worktrees/verify-deliv-054-6509746913eda16d2c4440add20e7f6793500f0b"
    exit_code: 0
    result: PASS
    authority: authoritative
    summary: "Release workstation/manifest contract (win-x64), Windows/Linux mappings, hidden-root positive and negative ZIP fixtures, and LocalDB classification passed."
  - attempted_at: "2026-09-08T14:22:08.230106Z"
    command: "pwsh -NoProfile -File ./scripts/Test-AzureDeploymentPlan.ps1 -Mode Local"
    cwd: ".worktrees/verify-deliv-054-6509746913eda16d2c4440add20e7f6793500f0b"
    exit_code: 0
    result: PASS
    authority: authoritative
    summary: "Local deployment plan validation passed; Worker Disabled settings render true. Informational Bicep update warning; no upgrade performed."
---

# DELIV-054 exact-merge verification

PR https://github.com/collisionengineers/pegasus/pull/703 was squash-merged into dev on 2026-09-08 at 13:58:20 UTC. The exact merge SHA above, not the author SHA, was verified by the sole host verifier /root/agent_config_verifier. Root independently confirmed the clean detached HEAD. The verification interval ended at 2026-09-08T14:22:33.6963623Z; attempted_at values are the original invocation timestamps.

## Receipt classification

The configured contract is pr.yml / verify / push. Its exact-SHA lookup exited 1 with HTTP 404 because that workflow is absent; the current ci.yml does not supply that configured dev-push receipt. No receipt was fabricated or treated as a failing implementation test. All packet obligations were missing and were executed locally through the kanmer-verify fallback. The pull_request run is not a qualifying receipt.

## Scope and evidence

All three packet obligations passed sequentially on unchanged merged source. Full commands, observed output, input hashes and original timestamps are retained in scratch/verify.md. No post-merge check failed. Pre-merge supporting evidence remains in scratch/execution.md and is not substituted for exact-merge evidence.

This proves the bounded three-script correction and synthetic archive validation, not an actual release package or deployment. Real artifact packaging remains D6; PR #676 deployment provenance and DELIV-048 historical evidence remain separate. No application build, migration, cloud mutation, browser host or release packaging ran. All verifier processes exited before host-slot handoff.
