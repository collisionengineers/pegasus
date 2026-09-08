---
kind: proof-record
schema: 2
merged_sha: "0a6ccca799eb670825e60b614cf24b846cdf4572"
environment: ".worktrees/verify-deliv-058-0a6ccca799eb670825e60b614cf24b846cdf4572; Windows x64, PowerShell 7; sole host verifier /root/agent_config_verifier"
verified_at: "2026-09-08T15:56:41.1141344Z"
result: PASS
receipts: []
attempts:
  - attempted_at: "2026-09-08T15:54:28.2881243Z"
    command: "dotnet restore ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --locked-mode"
    cwd: ".worktrees/verify-deliv-058-0a6ccca799eb670825e60b614cf24b846cdf4572"
    exit_code: 0
    result: PASS
    authority: supporting
    summary: "Locked Architecture restore passed."
  - attempted_at: "2026-09-08T15:54:41.0761891Z"
    command: "dotnet build ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-restore"
    cwd: ".worktrees/verify-deliv-058-0a6ccca799eb670825e60b614cf24b846cdf4572"
    exit_code: 0
    result: PASS
    authority: supporting
    summary: "Release Architecture build passed with zero warnings and zero errors."
  - attempted_at: "2026-09-08T15:56:27.7328141Z"
    command: "dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build --filter \"FullyQualifiedName~IntakeOrchestrationUsesOneExplicitExtractionPolicyBoundary|FullyQualifiedName~FunctionDependsOnTheCanonicalStagedArtifactReconciler\" --logger \"trx;LogFileName=deliv-058-exactmerge-focused-20260908-1556.trx\" --results-directory artifacts/verification"
    cwd: ".worktrees/verify-deliv-058-0a6ccca799eb670825e60b614cf24b846cdf4572"
    exit_code: 0
    result: PASS
    authority: supporting
    summary: "Both focused architecture tests passed, zero failed or skipped."
  - attempted_at: "2026-09-08T15:56:41.1141344Z"
    command: "dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build --logger \"trx;LogFileName=deliv-058-exactmerge-full-20260908-1556.trx\" --results-directory artifacts/verification"
    cwd: ".worktrees/verify-deliv-058-0a6ccca799eb670825e60b614cf24b846cdf4572"
    exit_code: 0
    result: PASS
    authority: authoritative
    summary: "All 116 architecture tests passed, zero failed or skipped."
---

# Exact-merge verification

PR #708 is confirmed MERGED into configured integration branch dev at the full SHA above (2026-09-08T15:47:28Z). Root read plan 128e84dc19caa5f6, effective gates and advisory reconciliation (no recommendation), then queried the declared pr.yml / verify / push contract for this SHA before any verification Git action. The workflow is absent (HTTP404); no qualifying receipt exists, so every scoped obligation used the ordinary detached fallback. Missing or failed optional CI is not represented as a green receipt.

The designated verifier's complete ledger is scratch/verify.md@70aad82913522fa9, read in full. Exact HEAD, clean detached state, absence of competing commands, and exactly two first-parent paths were confirmed before the checks. Both integrated test blobs equal their independently reviewed freeze: DependencyDirectionTests.cs 77ca689ea1d91d694429cdcdee74ffd4ab29daf4; StagedArtifactReconciliationFunctionTests.cs 80b74ef998d4ba905cd4b6dc05b10cea1945cd87. Postcheck at 2026-09-08T15:57:33.5612580Z retained those facts.

The scoped positive/negative extraction-selector boundary and exact ordered Worker pairing composition assertions passed. No runtime, constructor, policy, dependency, schema or other source path changed.

## Retained results

Before disposable-worktree cleanup, root copied both exact-merge TRXs to the source repository's ignored artifacts/verification/deliv-058-0a6ccca799eb670825e60b614cf24b846cdf4572/ directory and independently confirmed SHA-256 equality:
- deliv-058-exactmerge-focused-20260908-1556.trx: 65CE41645B4FD6AB04648806E7EB4E291104B37D950ED07116B5F7BC09571EFE.
- deliv-058-exactmerge-full-20260908-1556.trx: D9419290A3998CC026811762A7ECC8F22C1B19CE4E09A0A7AB496C9B10ECCC89.

Original two architecture failures and all optional pre-merge CI failures remain in research/review/scratch history. The pre-merge focused 2/full116 PASS is supporting history, not a substituted exact-merge receipt. No exact-merge verification attempt failed. This PASS is only DELIV-058 acceptance; it is not a converged whole-application gate, deployment or main promotion.
