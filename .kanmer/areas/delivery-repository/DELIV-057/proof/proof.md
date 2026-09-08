---
kind: proof-record
schema: 2
merged_sha: "3370e58f40de205986fb9642e62b329f06f91a4f"
environment: ".worktrees/verify-deliv-057-3370e58f40de205986fb9642e62b329f06f91a4f; Windows x64, PowerShell 7; sole host verifier /root/agent_config_verifier"
verified_at: "2026-09-08T15:47:12.9991498Z"
result: PASS
receipts: []
attempts:
  - attempted_at: "2026-09-08T15:45:36.9287577Z"
    command: "dotnet restore ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --locked-mode"
    cwd: ".worktrees/verify-deliv-057-3370e58f40de205986fb9642e62b329f06f91a4f"
    exit_code: 0
    result: PASS
    authority: supporting
    summary: "Locked affected-project restore succeeded."
  - attempted_at: "2026-09-08T15:45:49.1585955Z"
    command: "dotnet build ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-restore"
    cwd: ".worktrees/verify-deliv-057-3370e58f40de205986fb9642e62b329f06f91a4f"
    exit_code: 0
    result: PASS
    authority: supporting
    summary: "Release build succeeded with zero warnings and zero errors."
  - attempted_at: "2026-09-08T15:47:12.9991498Z"
    command: 'dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~VehicleLookupBackfillTests"'
    cwd: ".worktrees/verify-deliv-057-3370e58f40de205986fb9642e62b329f06f91a4f"
    exit_code: 0
    result: PASS
    authority: authoritative
    summary: "All three historical vehicle-lookup migration tests passed; zero failed or skipped."
---

# Exact-merge verification

PR #707 is confirmed MERGED into the configured integration branch dev at the full SHA above. The current declared verification contract is pr.yml / verify / push. Its exact-SHA lookup returned HTTP 404 because that workflow is absent; there is no qualifying receipt. All scoped obligations therefore used the ordinary authorized detached fallback, not a CI waiver or fabricated green run.

Root read plan 68ba2647c344d5da, effective gates and the advisory reconciliation (no recommendation). Only after the receipt lookup did root create and confirm the exact, clean detached worktree. The designated verifier's full ledger is scratch/verify.md@c0d5db824fe389c3, read in full before this proof.

At 15:45:19 UTC, preflight confirmed exact HEAD, parent d76de2534ec6651c1a434a55f76593b7b140bf1c, clean detached state and no active verification process. The first-parent diff contains only tests/Pegasus.IntegrationTests/VehicleLookupBackfillTests.cs. Its existing seed gains the two required historical confirmation columns and two true values; test names and transition/backfill/idempotency assertions are unchanged. No production schema or runtime compatibility behavior is restored.

The 15:48:19 UTC postcheck retained exact HEAD and a clean worktree. Reusable build-server nodes were distinguished from active test commands. No additional test attempt or failure occurred at this merge SHA.

Original PR700 and subsequent optional pre-merge CI failures remain in research/review/scratch history, including the three NULL InstructionConfirmedByStaff setup failures. Cancelled, pending and non-required sibling CI are not qualifying receipts and are not relabelled PASS. This proof covers only DELIV-057, not the full release candidate, deployment, or main promotion.
