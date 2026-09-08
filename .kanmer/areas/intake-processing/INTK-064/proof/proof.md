---
kind: proof-record
merged_sha: "96777888bfa7ee7f85d63979a4a09ae10cda7d13"
environment: ".worktrees/verify-intk-064-96777888bfa7ee7f85d63979a4a09ae10cda7d13; Windows x64, PowerShell 7, .NET 10, SQL Server LocalDB; root sole heavy verifier"
verified_at: "2026-09-08T06:32:40Z"
result: PASS
attempts: [
  {
    "attempted_at": "2026-09-08",
    "command": "dotnet restore ./Pegasus.slnx --locked-mode",
    "cwd": ".worktrees/intk-064",
    "exit_code": 0,
    "result": "PASS",
    "summary": "Root session72700, seven projects restored. No second-precision timestamp recorded."
  },
  {
    "attempted_at": "2026-09-08",
    "command": "dotnet build ./Pegasus.slnx --configuration Release --no-restore",
    "cwd": ".worktrees/intk-064",
    "exit_code": 1,
    "result": "FAIL",
    "summary": "Session72700; 15.92s; CS8602 TriageLifecycle.cs:106. Nullable evaluator result dereferenced; no tests ran. Corrected by non-null UniqueMatch property pattern, not suppression or test relaxation."
  },
  {
    "attempted_at": "2026-09-08",
    "command": "dotnet build ./Pegasus.slnx --configuration Release --no-restore",
    "cwd": ".worktrees/intk-064",
    "exit_code": 0,
    "result": "PASS",
    "summary": "Session39568; corrected source, 91.22s; zero warnings/errors."
  },
  {
    "attempted_at": "2026-09-08T05:55:54.3281705Z",
    "command": "dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter \"FullyQualifiedName~TriageReplayTests|FullyQualifiedName~AddTriageNoteTests|FullyQualifiedName~ImmediateExternalPublicationTests\" --results-directory ./artifacts/verification --logger \"trx;LogFileName=intk-064-core.trx\"",
    "cwd": ".worktrees/intk-064",
    "exit_code": 0,
    "result": "PASS",
    "summary": "32 Core executed/passed, 90ms; no skips."
  },
  {
    "attempted_at": "2026-09-08T05:55:57.3119103Z",
    "command": "dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter \"FullyQualifiedName~QdosTriageIntegrationTests.AutomaticPairingRechecksCurrentIdentityLeaseVersionAndManualIntent|FullyQualifiedName~QdosTriageIntegrationTests.GenuineFormalInstructionLinksTriageInEitherArrivalOrderWithoutChangingItsWorkflow|FullyQualifiedName~QdosTriageIntegrationTests.CaseAssociationUsesCanonicalWorkflowVersionAndActiveCaseLease|FullyQualifiedName~QdosTriageIntegrationTests.NamedMutationRetriesReturnHistoricalResultsAndRetainConflictAndStateGates|FullyQualifiedName~TriageFromIntakeIntegrationTests|FullyQualifiedName~AzureSqlRuntimeRoleMigrationTests.WorkerRuntimeAutomaticallyLinksTriageAndReplaysWithoutFindingsPermission|FullyQualifiedName~ConcurrencyTokenPersistenceTests.FreshLocalDbCaseAcceptanceAndTriageInsertUpdateGenerateTokensAndRejectStaleWrites|FullyQualifiedName~StagedArtifactReconciliationFunctionIntegrationTests.TimerCallsTheBoundedReconcilerAndLogsEveryResultField\" --results-directory ./artifacts/verification --logger \"trx;LogFileName=intk-064-integration.trx\"",
    "cwd": ".worktrees/intk-064",
    "exit_code": 0,
    "result": "PASS",
    "summary": "14 integration executed/passed,59s; both genuine arrival orders and restricted Worker included."
  },
  {
    "attempted_at": "2026-09-08T05:57:00Z",
    "command": "pwsh -NoProfile -File ./scripts/Test-DocumentationLinks.ps1",
    "cwd": ".worktrees/intk-064",
    "exit_code": 0,
    "result": "PASS",
    "summary": "127 files; time recorded to minute."
  },
  {
    "attempted_at": "2026-09-08",
    "command": "gh pr view 699 --repo collisionengineers/pegasus --json state,mergeCommit,url",
    "cwd": ".",
    "exit_code": 0,
    "result": "PASS",
    "summary": "MERGED at exact96777888bfa7ee7f85d63979a4a09ae10cda7d13; exact shell start not recorded. Normal fetch and exact detached worktree add exit0; SHA assertion, detached symbolic-ref expected exit1 and clean status assertions passed in session79811."
  },
  {
    "attempted_at": "2026-09-08",
    "command": "dotnet restore ./Pegasus.slnx --locked-mode",
    "cwd": ".worktrees/verify-intk-064-96777888bfa7ee7f85d63979a4a09ae10cda7d13",
    "exit_code": 0,
    "result": "PASS",
    "summary": "Exact merged checkout, session79811, seven projects, longest2.61s."
  },
  {
    "attempted_at": "2026-09-08",
    "command": "dotnet build ./Pegasus.slnx --configuration Release --no-restore",
    "cwd": ".worktrees/verify-intk-064-96777888bfa7ee7f85d63979a4a09ae10cda7d13",
    "exit_code": 0,
    "result": "PASS",
    "summary": "Session79811,126.45s,zero warnings/errors; completed before merged test start."
  },
  {
    "attempted_at": "2026-09-08T06:29:58.7366124Z",
    "command": "dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter \"FullyQualifiedName~TriageReplayTests|FullyQualifiedName~AddTriageNoteTests|FullyQualifiedName~ImmediateExternalPublicationTests\" --results-directory ./artifacts/verification --logger \"trx;LogFileName=intk-064-96777888-core.trx\"",
    "cwd": ".worktrees/verify-intk-064-96777888bfa7ee7f85d63979a4a09ae10cda7d13",
    "exit_code": 0,
    "result": "PASS",
    "summary": "32/32 Core,106ms,zero skips; finish06:30:00.3735826Z."
  },
  {
    "attempted_at": "2026-09-08T06:30:01.7546529Z",
    "command": "dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter \"FullyQualifiedName~QdosTriageIntegrationTests.AutomaticPairingRechecksCurrentIdentityLeaseVersionAndManualIntent|FullyQualifiedName~QdosTriageIntegrationTests.GenuineFormalInstructionLinksTriageInEitherArrivalOrderWithoutChangingItsWorkflow|FullyQualifiedName~QdosTriageIntegrationTests.CaseAssociationUsesCanonicalWorkflowVersionAndActiveCaseLease|FullyQualifiedName~QdosTriageIntegrationTests.NamedMutationRetriesReturnHistoricalResultsAndRetainConflictAndStateGates|FullyQualifiedName~TriageFromIntakeIntegrationTests|FullyQualifiedName~AzureSqlRuntimeRoleMigrationTests.WorkerRuntimeAutomaticallyLinksTriageAndReplaysWithoutFindingsPermission|FullyQualifiedName~ConcurrencyTokenPersistenceTests.FreshLocalDbCaseAcceptanceAndTriageInsertUpdateGenerateTokensAndRejectStaleWrites|FullyQualifiedName~StagedArtifactReconciliationFunctionIntegrationTests.TimerCallsTheBoundedReconcilerAndLogsEveryResultField\" --results-directory ./artifacts/verification --logger \"trx;LogFileName=intk-064-96777888-integration.trx\"",
    "cwd": ".worktrees/verify-intk-064-96777888bfa7ee7f85d63979a4a09ae10cda7d13",
    "exit_code": 0,
    "result": "PASS",
    "summary": "14/14 integration,61s,zero skips; finish06:31:05.3617031Z. Same focused acceptance at exact merge, session49002."
  },
  {
    "attempted_at": "2026-09-08",
    "command": "pwsh -NoProfile -File ./scripts/Test-DocumentationLinks.ps1",
    "cwd": ".worktrees/verify-intk-064-96777888bfa7ee7f85d63979a4a09ae10cda7d13",
    "exit_code": 0,
    "result": "PASS",
    "summary": "Commande3e0a4,127 files,3.054s. No generated UI change; no capture required."
  }
]
---

# Proof — INTK-064

PR [699](https://github.com/collisionengineers/pegasus/pull/699) merged into
configured integration branch dev at 2026-09-08T06:16:37Z.
Exact author head 1e4f20e5718ec97abf269563fbba1fd944b944a4 received independent
pack_reconcile PASS, whole attestation236cd47ab3845f07; public review5138050741.
Root did not author the implementation. Plan87b4d11fd251180e,
mapf05f000a8b26f9be, reportd314ff39a7935cca and checklist2b038e2bac1c41b6
were read in full. Empty task-PR checks are not a hosted CI PASS.
All failed and successful author attempts above remain part of this record.

## Accepted behavior

Existing creation/replay, formal acceptance/replay and reconciliation timer
reach one normally registered TriageCasePairing owner. The existing Core
matcher and both context-bound index queries recheck complete current identity
and replacement ownership inside the serializable write transaction.
Automatic linking keeps Triage's own reference, state and findings and creates
no extra Case/PO. Both arrival orders use the same retained genuine QDOS
original SHA2563063ff9ecb31878f582fb439047d999a41a7c6fe5b978cfbee5c7e7f277553b4;
the pre-existing Triage arrangement is not evidence that this formal original
was classified as Triage.

Actual focused SQL tests cover corrected current identity/new competitor,
cross-principal replacement, cancellation, Completed association without
reopening, manual unlink/relink protection, current versions/live staff lease,
replay/history uniqueness, dynamic pending selection and persisted tokens.
Actual restricted Worker calls pairing/replay without TriageFindings access;
the existing timer fixture verifies caller and bounded failure log fields.
Core recovery tests keep failed work retryable and continue unrelated work.
No new queue, timer, schema, grant or matcher; original manual authorization
and image pairing are unchanged.

## Retained artifacts

All four TRXs were copied and hashes rechecked before any cleanup, under
pegasus_pack/current/proofs/INTK-064/. manifest.json records names, lengths,
SHA256s and exact merge. Original timestamps are +01:00; attempts above convert
the test times to UTC exactly. Shell attempts lacking an exact time retain date
precision rather than inventing seconds.

| Artifact | SHA256 |
| --- | --- |
| intk-064-core.trx | 6AC9B8A24C302230F659FEF561656BA13FB393880562E96E5F877784CBBED7FC |
| intk-064-integration.trx | F47E0DA8540ECB09C8636A1B925A95DAD937B2BC51497BB63751AADC8C767AA1 |
| intk-064-96777888-core.trx | BBBBB2B3568B01DDE3768C92C11FF8DEAB3B2E5E7056273081F5EB7A89A31567 |
| intk-064-96777888-integration.trx | 731719E5BA76FDA8EFA35DF111B1035F51E72E9FA8F6CC746D7CF787B7FD9EF7 |

## Boundaries and closeout handoff

PASS is exact integrated acceptance, not deployment/provider notification
success. No live mailbox/cloud/resource/data write or broad corpus run occurred.
EPIC-014 still owns the final converged release and live lifecycle proof.
ENG-029 received only the recorded equal-value assertion-method handoff after
integration; historical claims remain untouched. No material review findings
or omitted ticket acceptance remain. Root may move Verifying to Done after
fresh gates, then perform owned closeout only after full proof readback.
