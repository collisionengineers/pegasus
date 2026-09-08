---
kind: proof-record
merged_sha: "498144b0bb55b68fd53b9a31ffc89ef90622c73a"
environment: ".worktrees/verify-eng-037-498144b0bb55b68fd53b9a31ffc89ef90622c73a; Windows x64, PowerShell 7, .NET 10"
verified_at: "2026-09-08T03:32:57.6587861Z"
result: PASS
attempts:
  - attempted_at: "2026-09-08"
    command: "dotnet restore ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --locked-mode"
    cwd: ".worktrees/verify-eng-037-498144b0bb55b68fd53b9a31ffc89ef90622c73a"
    exit_code: 0
    result: PASS
    summary: "Session88787; Core219ms, tests283ms. UTC day recorded; separate process start timestamp not captured."
  - attempted_at: "2026-09-08"
    command: "dotnet build ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-restore"
    cwd: ".worktrees/verify-eng-037-498144b0bb55b68fd53b9a31ffc89ef90622c73a"
    exit_code: 0
    result: PASS
    summary: "After restore in guarded session88787;7.99s, zero warnings/errors. Separate process start timestamp not captured."
  - attempted_at: "2026-09-08T03:32:56.1463791Z"
    command: "dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter 'FullyQualifiedName~AssessmentPolicyTests|FullyQualifiedName~AssessmentReportProjectionTests' --logger 'trx;LogFileName=eng-037-merged-dates.trx' --results-directory ./artifacts/verification"
    cwd: ".worktrees/verify-eng-037-498144b0bb55b68fd53b9a31ffc89ef90622c73a"
    exit_code: 0
    result: PASS
    summary: "78/78 PASS,97ms, zero failed/notExecuted/inconclusive. Exact test start from TRX; guarded script exit0."
---

# ENG-037 exact merged verification

GitHub PR695 independently reviewed and normally merged by intake_audit at
2026-09-08T03:29:23Z into configured integration branch dev. Exact merge SHA
above, not the source head or later moving branch tip. Root is author and
runtime verifier, not its independent reviewer or merge actor.

Read full plan7acbd02e0af054f3, filesf24bccc84a440a9e, checklist3a235bf92e61d944,
report9c85672f44891b2c and final review870b3f76ab646c07.
Public independent review5136969812 binds
7c86e9bc91f12f03a37a9a7c33dd1f5afcf2e524. No open findings.
Fresh gates preceded Review to Verifying at03:29:54.714Z.

Exact commit fetch and SHA-named detached worktree creation passed. HEAD
equals498144b0bb55b68fd53b9a31ffc89ef90622c73a; symbolic-ref exited1 with no
branch, and porcelain status was empty before/after checks. Shared checkout
and other ticket trees were untouched. Session88787 ran only after CASE-031
session29817 finished; root was the sole heavy verifier.

The existing AssessmentPolicy normalizer now explicitly parses Gregorian ISO
dates under invariant culture. SaveAssessment and EfCaseAssessmentStore are
existing production callers. All writable date fields preserve ordinary and
leap-day values under th-TH, ar-SA and en-GB, refuse invalid/minimum/alternate
formats, and restore test culture. Existing report invariant-date regression
also passes unchanged. No second parser, UI, schema, provider or timezone work.

Author session12906 earlier passed locked restore, build17.14s and78 tests129ms.
No author or merged build/test failure occurred. Earlier read-only filename/
glob search errors are retained in the report, not application failures.

## Artifacts and handoff

- Author .worktrees/eng-037/artifacts/verification/eng-037-dates.trx:
  E70AEAE038C0D62BEDD05104026ACCC5BBC6D2F024B16F4B5227231CBBD3B4D1.
- Detached artifacts/verification/eng-037-merged-dates.trx:
  F15A3CF63E189077F1B07671D13EF2A56F3473E06AA17365549136BEC0783820.

Main read all merged TRX counters, exact start/finish and SHA256. Preserve both
hash-verified artifacts under pegasus_pack/current/proofs/ENG-037 before owned
cleanup. No full-suite CI, native Linux runtime or deployment claim; final
converged solution/release verification remains under EPIC-014.

PASS permits fresh gates and Verifying to Done, then kanmer-closeout record
keeping, exact owned Git cleanup and claim release last. No unrelated branch,
worktree or evidence may be removed.


## Closeout retention — 8 September 2026

[PR695](https://github.com/collisionengineers/pegasus/pull/695) merged at
2026-09-08T03:29:23Z; verified Done at03:39 UTC. Both named TRXs have been
copied without overwrite to pegasus_pack/current/proofs/ENG-037 and their
SHA256 values exactly match the originals above. MANIFEST.sha256 names both.
The current ticket records the merged SHA, PR, integrated/dev and not-deployed.
Owned temporary Git cleanup and release are pending, with artifacts safe.


## Closeout completed — 8 September 2026

All two retained TRXs remain hash-identical; MANIFEST.sha256 SHA256 is
D32AD64065DF409D3D044D8AAB25D4997D18EA54FDF2C27A38513C5AB56EBAC6.
Fresh MERGED readback and complete694-item ownership census found ENG-037
alone on the recorded branch/worktree. Both explicit resolved cleanup targets
were inside the intended .worktrees directory and clean. Normal git worktree
remove deleted only the SHA-named detached tree and .worktrees/eng-037.
Normal git branch -d and remote branch deletion succeeded; no force used.
Fetch/prune passed; worktree prune dry-run found nothing before its no-op run.
Both directories and local/remote branch are confirmed absent. The source is
recoverable from merged PR695; retained evidence is outside those directories.
No unrelated worktree, shared checkout, board Git or corpus was removed.
The claim is released last after this final record and checklist are read back.
