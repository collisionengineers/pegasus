---
kind: proof-record
merged_sha: "783b537f189ead88553f940d03df0d1f9558ef75"
environment: "Windows PowerShell 7, .NET 10 Release; C:/Users/Alex/Documents/GitHub/pegasus/.worktrees/verify-intk-061-783b537f189ead88553f940d03df0d1f9558ef75; existing real SQL integration fixtures"
verified_at: "2026-09-07T21:57:29Z"
result: PASS
attempts:
  - attempted_at: "2026-09-07"
    command: "gh pr view 679 --json state,mergeCommit,mergedAt,url,headRefOid,baseRefName"
    cwd: "."
    exit_code: 0
    result: PASS
    summary: "MERGED to dev at 2026-09-07T21:12:02Z; exact mergeCommit 783b537f189ead88553f940d03df0d1f9558ef75."
  - attempted_at: "2026-09-07"
    command: "git fetch origin 783b537f189ead88553f940d03df0d1f9558ef75"
    cwd: "."
    exit_code: 0
    result: PASS
    summary: "Exact merge commit fetched."
  - attempted_at: "2026-09-07"
    command: "git rev-parse 783b537f189ead88553f940d03df0d1f9558ef75^{tree} 3d58d8c58fe57aac5af6091f7ebf0e0b81b5b560^{tree}"
    cwd: "."
    exit_code: 1
    result: FAIL
    summary: "PowerShell parsed unquoted braces before Git. Corrected quoting in following attempt; not an application failure."
  - attempted_at: "2026-09-07"
    command: "git rev-parse '783b537f189ead88553f940d03df0d1f9558ef75^{tree}' '3d58d8c58fe57aac5af6091f7ebf0e0b81b5b560^{tree}'"
    cwd: "."
    exit_code: 0
    result: PASS
    summary: "Comparison completed and found unequal trees. Full-source/binary reuse was rejected, not certified."
  - attempted_at: "2026-09-07"
    command: "Test-Path -LiteralPath 'C:/Users/Alex/Documents/GitHub/pegasus/.worktrees/verify-intk-061-783b537f189ead88553f940d03df0d1f9558ef75'"
    cwd: "."
    exit_code: 0
    result: PASS
    summary: "Returned False before worktree creation; no existing path overwritten."
  - attempted_at: "2026-09-07"
    command: "git worktree add --detach .worktrees/verify-intk-061-783b537f189ead88553f940d03df0d1f9558ef75 783b537f189ead88553f940d03df0d1f9558ef75"
    cwd: "."
    exit_code: 0
    result: PASS
    summary: "Created exact deterministic detached verification workspace."
  - attempted_at: "2026-09-07"
    command: "git rev-parse HEAD"
    cwd: ".worktrees/verify-intk-061-783b537f189ead88553f940d03df0d1f9558ef75"
    exit_code: 0
    result: PASS
    summary: "Equals exact merge SHA."
  - attempted_at: "2026-09-07"
    command: "git symbolic-ref --short -q HEAD"
    cwd: ".worktrees/verify-intk-061-783b537f189ead88553f940d03df0d1f9558ef75"
    exit_code: 1
    result: PASS
    summary: "Expected exit 1 and empty output proves detached HEAD; not a failed check."
  - attempted_at: "2026-09-07"
    command: "git status --short --branch"
    cwd: ".worktrees/verify-intk-061-783b537f189ead88553f940d03df0d1f9558ef75"
    exit_code: 0
    result: PASS
    summary: "Only HEAD (no branch); clean tracked/untracked status before execution."
  - attempted_at: "2026-09-07"
    command: "git diff --check 783b537f^ 783b537f"
    cwd: ".worktrees/verify-intk-061-783b537f189ead88553f940d03df0d1f9558ef75"
    exit_code: 0
    result: PASS
    summary: "No whitespace errors."
  - attempted_at: "2026-09-07"
    command: "pwsh -NoProfile -File ./scripts/Test-MarkdownPlacement.ps1 -Base '783b537f189ead88553f940d03df0d1f9558ef75^' -Head 783b537f189ead88553f940d03df0d1f9558ef75"
    cwd: ".worktrees/verify-intk-061-783b537f189ead88553f940d03df0d1f9558ef75"
    exit_code: 0
    result: PASS
    summary: "Markdown placement passed."
  - attempted_at: "2026-09-07"
    command: "dotnet restore ./Pegasus.slnx --locked-mode"
    cwd: ".worktrees/verify-intk-061-783b537f189ead88553f940d03df0d1f9558ef75"
    exit_code: 0
    result: PASS
    summary: "Root ran locked restore at exact merge SHA; passed."
  - attempted_at: "2026-09-07"
    command: "dotnet build ./Pegasus.slnx --configuration Release --no-restore"
    cwd: ".worktrees/verify-intk-061-783b537f189ead88553f940d03df0d1f9558ef75"
    exit_code: 0
    result: PASS
    summary: "Root ran exact merged-source Release build: zero warnings/errors, 64.31 seconds."
  - attempted_at: "2026-09-07"
    command: "dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter 'FullyQualifiedName~IntakeOcrTests|FullyQualifiedName~PollApprovedInboxTests|FullyQualifiedName~ProcessIntakeTests|FullyQualifiedName~ReconcileUnidentifiedDestinationsTests'"
    cwd: ".worktrees/verify-intk-061-783b537f189ead88553f940d03df0d1f9558ef75"
    exit_code: 0
    result: PASS
    summary: "Root actual execution: 114 passed, 0 failed; 334 ms test duration."
  - attempted_at: "2026-09-07"
    command: "dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter 'FullyQualifiedName~OcrIntakeRecoveryTests|FullyQualifiedName~QdosAllocationRecoveryTests.DestinationFailureStaysDurableAndRetriesTheSameEvaluation|FullyQualifiedName~QdosAllocationRecoveryTests.LiveQueuedProcessingRunsMailAssociationBeforeAllocation|FullyQualifiedName~QdosAllocationRecoveryTests.CompletedQueuedReplayRunsMailAssociationBeforeAllocation|FullyQualifiedName~QdosAllocationRecoveryTests.UniqueExistingCaseAssociationBypassesNewAllocationExactlyOnce|FullyQualifiedName~UnreadableImageGroupHasOneUnidentifiedOutcomeAndLeavesTheSweep|FullyQualifiedName~IntakeCustodyContinuationTests'"
    cwd: ".worktrees/verify-intk-061-783b537f189ead88553f940d03df0d1f9558ef75"
    exit_code: 0
    result: PASS
    summary: "Root actual execution: 17 passed, 0 failed; 89 seconds. IntakeCustodyContinuationTests does not exist and matched no tests; no coverage is attributed to that term."
  - attempted_at: "2026-09-07"
    command: "dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter 'FullyQualifiedName~AzureSqlRuntimeRoleMigrationTests.WebRuntimeCanInsertPairedOcrWorkRowsButCannotProcessOcr' --logger 'console;verbosity=normal'"
    cwd: ".worktrees/verify-intk-061-783b537f189ead88553f940d03df0d1f9558ef75"
    exit_code: 0
    result: PASS
    summary: "Root actual execution: named test passed, 1/1; 25 seconds test time, 26.8259 seconds total. Exercises actual restricted Worker custody store, wrong receipt and duplicate claim."
  - attempted_at: "2026-09-07"
    command: "git status --short --branch"
    cwd: ".worktrees/verify-intk-061-783b537f189ead88553f940d03df0d1f9558ef75"
    exit_code: 0
    result: PASS
    summary: "Post-run at 21:57 UTC: only HEAD (no branch); workspace remains clean."
  - attempted_at: "2026-09-07"
    command: "Get-FileHash -Algorithm SHA256 src/Pegasus.Core/bin/Release/net10.0/Pegasus.Core.dll,src/Pegasus.Infrastructure/bin/Release/net10.0/Pegasus.Infrastructure.dll,tests/Pegasus.Core.Tests/bin/Release/net10.0/Pegasus.Core.Tests.dll,tests/Pegasus.IntegrationTests/bin/Release/net10.0/Pegasus.IntegrationTests.dll | Select-Object Hash,Path | ConvertTo-Json"
    cwd: ".worktrees/verify-intk-061-783b537f189ead88553f940d03df0d1f9558ef75"
    exit_code: 0
    result: PASS
    summary: "Read-only post-execution exact-merge artifact manifest recorded below at 21:57 UTC."
---

# Merged verification — INTK-061

PR: https://github.com/collisionengineers/pegasus/pull/679
Merged to the configured integration branch dev at 2026-09-07T21:12:02Z.
Reviewed author head: 3d58d8c58fe57aac5af6091f7ebf0e0b81b5b560.
Independent reviewer PASS: scratch/review.md@895481a6c184fd34.
Implementation report: post-implementation-report/post-implementation-report.md
version ada2fff40a1790d6; plan version 68da14c493e97e39.

## Evidence attribution and precision

Root is the sole heavy verification owner and actually ran the merged restore,
build and test commands above in this exact detached workspace. This worker
performed the recorded Git/placement checks, assembled root's command/exit
evidence and read the binary hashes. These are new exact-merge executions,
not relabeled pre-merge tests or a claimed CI run.

Attempt timestamps use ISO date precision because individual start instants
were not recorded. All attempts occurred on 7 September 2026 UTC; root's
merged checks completed before this proof timestamp. No midnight or invented
second-precision start times are implied. Setup checks occurred about
21:43–21:46 UTC; post-run status and artifact reads at 21:57 UTC.

Merged execution passed 114 Core plus 17 SQL plus one named restricted-role
SQL case. The nonexistent IntakeCustodyContinuationTests filter term provided
zero coverage. Its intended custody claim is instead backed by the separately
executed actual AzureSqlRuntimeRoleMigrationTests case, not by the unmatched
name. No provider call, production write, deployment, full solution test rail,
soak suite or full v1 acceptance is claimed. Root retains the converged solution
rail and release ownership after the separately owned remediation lanes.

## Source comparison and rejected reuse shortcut

Merge tree: b3d49005cd317389a9ec5382714aced7606d2ee1.
Author tree: 7aed1b06bdb2da06651c8c08136a1fce6c969785.
They differ: MAIL-036 and documentation/operating changes landed first.
The read-only Git blob census completed exit 0: 30 of the author's 31 changed
files are byte-identical at merge, including every changed production file and
both FRDs. PollApprovedInboxTests is the one differing author file, due to
MAIL-036 notification tests. Fifteen further non-author files differ.

This is not full-source equality and cannot justify reusing the old build as
a merged-source PASS. Root therefore built this exact merge and executed
actual intake/OCR/destination callers, including PollApprovedInboxTests for
the MAIL-036 interaction. Earlier per-file-identical focused group tests remain
historical supplementary evidence only. Original artifact hashes and the
comparison procedure remain in scratch/verify-preparation.md@7c7e2f4333136340;
those hashes were not represented as an earlier recorded execution binding.

## Historical pre-merge attempts preserved

These were on .worktrees/intk-061, not the detached merge workspace. Individual
start times and every expanded historical filter were not retained, so they
are not fabricated as new commands in the merged attempt list.

1. Locked restore passed. Release build 1 exited 1 after 18 seconds with
   27 parser diagnostics from one contextual LINQ identifier, group.
   Renaming it to submissionGroup fixed the syntax; no tests ran.
2. Release build 2 exited 1 after 15.8 seconds with CA1310 on StartsWith.
   Using the existing EF-translatable Like convention fixed it; no tests ran.
3. Release build 3 passed with zero warnings/errors. Focused Core then exited
   1: 139 passed, one failed. Whole-record equality compared ActionActor
   references. The corrected assertion explicitly retains origin, canonical
   reason, safe detail, operation key, exact time, actor identity and roles.
4. Own-review identified an AlreadyAssociated race; root approved both helper
   receipt refreshes and targeted live/replay/unique-match concurrency assertions.
5. Frozen SQL exited 1 after 2m47s: 46 passed, two failed of 48. Both were
   missing exact table cases in the existing CountAsync test helper, in
   UnreadableImageGroupHasOneUnidentifiedOutcomeAndLeavesTheSweep and
   DestinationFailureStaysDurableAndRetriesTheSameEvaluation. Required
   assertions were not weakened. All other selected cases, including actual
   Worker custody, OCR and old-group reconciliation, passed.
6. Corrected frozen-source Release build passed, exit 0, zero warnings/errors,
   30.72 seconds.
7. Targeted corrected-source Core passed 1/1, exit 0, 55 ms. Integration passed
   5/5, exit 0, 51 seconds: the two previous fixture failures and all three
   unique-match/live-mail/completed-mail association paths.

There were two failed compiler attempts, not three: build 3 was PASS followed
by a failed Core run. This record retains all known failures and their actual
dispositions. The later green runs do not erase them.

## Acceptance and production callers

- ProcessIntake -> RetainIncomingArtifact -> existing EF retention claim
  routes custody by operation identity. The exact-merge restricted Worker
  test executes the actual store without broader grants and rejects wrong
  receipt/repeated claims.
- UnifiedWorkFunction -> ProcessQueuedIntake retains the same evaluation and
  durable work until required destination writes complete. Exact-merge SQL
  tests prove transient destination and unique-association failure recovery,
  no duplicate Case allocation, and current receipt after concurrent association
  in both live and completed replay paths.
- Queue acknowledgment alone is not the fix. Existing FailProcessingAsync
  persists RetryScheduled/due time and clears the lease; the existing pending
  dispatcher republishes due work. Host interruptions retain expiring leases.
  No new queue, timer, Worker permissions, schema or runtime was introduced.
- Existing grouped-image automation/sweep owns the canonical group origin and
  reason. Exact-merge SQL proves one group U outcome and exclusion after
  completion; prior byte-identical focused tests prove conflict reason and
  oldest eligible group progress without unrelated-row starvation.
- External-work -> ProcessIntakeOcr retains completed output and paired Pending
  work until analysis applies. Exact-merge Core and SQL prove output replay
  after analysis failure with no second provider submission.

The canonical FRD-02/FRD-05 and known callers agree with these bounded changes.
Principal domain activation, formal Triage linking, handoff and deployment
remain separate owners; this proof does not certify those unfinished outcomes.

## Exact-merge post-run artifact manifest

SHA256 hashes read from this detached workspace after root's execution.
These are provenance for this build, not claims of equality with author DLLs.

| Binary | SHA256 |
| --- | --- |
| Pegasus.Core.dll | e9a816d7bc6e8f997ca40b51a768975bed03c63373f0156ee61f47313637a24c |
| Pegasus.Infrastructure.dll | 9ada0177665f52d26ac2ffebb42ac5f41a3cd1501a6478e810e40f9aa4b293a6 |
| Pegasus.Core.Tests.dll | 9f5c0aaa1bff6a5d851c409fbd5d86a92f919a95eedb202cdf00356e71280e86 |
| Pegasus.IntegrationTests.dll | f8a4c5b778a23bc5741d9a242aae57e20f8700a780ecb68d7da82883d030c1ec |

## Disposition

PASS for INTK-061's bounded integrated implementation. Not deployed.
Await root's proof readback and authorization before Verifying -> Done and
validated closeout. The retained implementation claim and both worktrees
remain intact until that authorization; no cleanup or release has occurred.
