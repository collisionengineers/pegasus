---
kind: proof-record
schema: 2
merged_sha: "d76de2534ec6651c1a434a55f76593b7b140bf1c"
environment: ".worktrees/verify-intk-065-d76de2534ec6651c1a434a55f76593b7b140bf1c; Windows x64, PowerShell 7; sole host verifier /root/agent_config_verifier"
verified_at: "2026-09-08T16:07:09.1887499Z"
result: PASS
receipts: []
attempts:
  - attempted_at: "2026-09-08T15:38:10.9274326Z"
    command: "dotnet restore ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --locked-mode"
    cwd: ".worktrees/verify-intk-065-d76de2534ec6651c1a434a55f76593b7b140bf1c"
    exit_code: 0
    result: PASS
    authority: supporting
    summary: "Locked Core restore passed."
  - attempted_at: "2026-09-08T15:38:20.2923516Z"
    command: "dotnet build ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-restore"
    cwd: ".worktrees/verify-intk-065-d76de2534ec6651c1a434a55f76593b7b140bf1c"
    exit_code: 0
    result: PASS
    authority: supporting
    summary: "Release Core build passed with zero warnings/errors."
  - attempted_at: "2026-09-08T15:38:57.6543208Z"
    command: "dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter \"FullyQualifiedName~PrincipalIdentificationCorpusTests\" --logger \"trx;LogFileName=intk-065-exactmerge-d76de253-20260908-1538.trx\" --results-directory ./artifacts/verification"
    cwd: ".worktrees/verify-intk-065-d76de2534ec6651c1a434a55f76593b7b140bf1c"
    exit_code: 0
    result: PASS
    authority: supporting
    summary: "Focused PrincipalIdentificationCorpusTests passed 7/7 with zero skipped."
  - attempted_at: "2026-09-08T15:39:09.7589368Z"
    command: "python -m unittest discover -s scripts/reference_data/tests -p test_build_principal_identification_corpus.py"
    cwd: ".worktrees/verify-intk-065-d76de2534ec6651c1a434a55f76593b7b140bf1c"
    exit_code: 0
    result: PASS
    authority: supporting
    summary: "Existing generator Python unit tests passed 2/2."
  - attempted_at: "2026-09-08T15:39:18.3555778Z"
    command: "pwsh -NoProfile -File ./scripts/Test-DocumentationLinks.ps1"
    cwd: ".worktrees/verify-intk-065-d76de2534ec6651c1a434a55f76593b7b140bf1c"
    exit_code: 0
    result: PASS
    authority: supporting
    summary: "All relative Markdown links resolved across 140 files."
  - attempted_at: "2026-09-08T15:39:29.1358160Z"
    command: "pwsh -NoProfile -File ./scripts/Test-TestMarkdownPlacement.ps1"
    cwd: ".worktrees/verify-intk-065-d76de2534ec6651c1a434a55f76593b7b140bf1c"
    exit_code: 0
    result: PASS
    authority: supporting
    summary: "Existing Markdown placement regressions passed."
  - attempted_at: "2026-09-08T15:39:48.2645880Z"
    command: "pwsh -NoProfile -File ./scripts/Test-MarkdownPlacement.ps1 -Base a1f0bfe260ea05df531df6e0ca3109141e7697da -Head d76de2534ec6651c1a434a55f76593b7b140bf1c"
    cwd: ".worktrees/verify-intk-065-d76de2534ec6651c1a434a55f76593b7b140bf1c"
    exit_code: 0
    result: PASS
    authority: supporting
    summary: "Markdown placement passed over actual merge-parent range."
  - attempted_at: "2026-09-08T15:42:38.5240248Z"
    command: "python -B ./artifacts/intk-065-exactmerge-verify.py"
    cwd: ".worktrees/verify-intk-065-d76de2534ec6651c1a434a55f76593b7b140bf1c"
    exit_code: 1
    result: FAIL
    authority: supporting
    failure_class: plan
    summary: "Verification harness incorrectly required 220 nested reference occurrences; observed 1538. This was an incorrect verification-count premise, not an established package defect."
  - attempted_at: "2026-09-08T16:07:09.1887499Z"
    command: "python -B ./artifacts/intk-065-exactmerge-verify.py"
    cwd: ".worktrees/verify-intk-065-d76de2534ec6651c1a434a55f76593b7b140bf1c"
    exit_code: 0
    result: PASS
    authority: authoritative
    summary: "Root-inspected verification-only premise correction; same merged code/package. Canonical bytes/hash, current five helper snapshots, every nested reference, seven historical objects, non-current snapshots, and only 26 approved reference replacements passed."
---

# Exact-merge verification

PR #706 is confirmed MERGED into configured integration branch dev at the SHA above. Root queried the declared pr.yml / verify / push contract before creating this clean detached worktree; the workflow is absent (HTTP404), so every scoped obligation used the ordinary authorized fallback and no receipt is invented.

Plan b7e0dfdbb97bcd01 and full exact-merge ledger scratch/verify.md@b5494af596718591 were read before this proof. The current-source-only evidence boundary is unchanged. Full original-input corpus regeneration is unavailable on this host and is not claimed as PASS; no original source, historical object or tracked package was regenerated or changed during this exact-merge verification.

## Preserved failure and corrected premise

The 15:42 command really exited 1 and remains the typed failed attempt above. Its failure_class is plan for the verification harness's incorrect counting premise: 220 was interpreted as a required reference occurrence count. It is not a transient-product claim and does not assert a shipped-code defect. The package and application source did not change between attempts.

Independent erratum scratch/review-erratum.md@689822490f3507d9 preserves the historical pre-merge attestation and explains the original census: 220 is the declared resolver-target universe, not references counted. Root inspected the complete corrected ignored harness (SHA-256 11973FB514CF477D473ECD6E7E8E1571B77510277511CB1A10E30C5C4589681A) and granted one execution. It retains all nested reference validation, strict hash format, fragment-stripped membership, canonical/helper comparison, historical equality and allowed-delta checks. No fixed occurrence count replaces the failed 220 premise.

The final command found 1,376 evidenceRef lists containing 1,538 references: 182 strict sha256 references and 1,356 non-hash references; every non-hash reference resolves against 220 unique declared target IDs. All five current snapshots match the existing generator helper; all seven historical sections and all non-current snapshots match the merge parent a1f0bfe260ea05df531df6e0ca3109141e7697da. Reversing the 26 approved reference substitutions and purpose/current-snapshot updates makes the JSON equal to that parent. Package SHA-256 stays 494e0a0f42ced164aab97cd50ebb497c1479c09eaf9f0a4db177949bbdc7c251.

## Documents and retained evidence

Root's exact parent-to-merge comparison confirms the index changes only its principal-profile destination, README adds only the approved five-line historical/current clarification, and relocated qdos.md is byte-identical (both Git blobs afad79d8a95768a903284fbcabf223b13e90a509). The plan's general byte-equality wording does not override its explicit README clarification.

Before disposable-worktree cleanup, the exact-merge Core TRX and corrected harness were copied to source-root ignored artifacts/verification/intk-065-d76de2534ec6651c1a434a55f76593b7b140bf1c/ and their SHA-256 values independently confirmed:
- intk-065-exactmerge-d76de253-20260908-1538.trx: 83AEA0E4A0DB98D6C1174A10FA48201C03FBBF16F7AB0CEEAF07E8FB083CEA6E.
- intk-065-exactmerge-verify.py: 11973FB514CF477D473ECD6E7E8E1571B77510277511CB1A10E30C5C4589681A.

The full corrected harness and exact output also remain in the ticket's verify ledger. Pre/post checks retained the exact detached HEAD, clean tracked/untracked state and no active verification process. Earlier pre-merge failures, superseded materialization and explicit report/command transcription corrections remain preserved in their records. Current baseline InaccessibleCase assertion ownership was subsequently corrected to DELIV-056, not an ENG-034 reopening.

PASS covers only the approved current-source inventory and documentation correction. It is not a full-original regeneration, broader application-suite PASS, deployment, main promotion or release acceptance.
