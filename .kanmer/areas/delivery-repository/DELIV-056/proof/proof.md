---
kind: proof-record
schema: 2
merged_sha: "71a2d27c8836a44b639762469ec950f1c8c82802"
environment: ".worktrees/verify-deliv-056-71a2d27c8836a44b639762469ec950f1c8c82802; Windows PowerShell 7; .NET SDK 10.0.302"
verified_at: "2026-09-08T16:47:13.9778933Z"
result: PASS
receipts: []
attempts:
  - attempted_at: "2026-09-08T16:43:38.7829907Z"
    command: "dotnet restore ./Pegasus.slnx --locked-mode"
    cwd: ".worktrees/verify-deliv-056-71a2d27c8836a44b639762469ec950f1c8c82802"
    exit_code: 0
    result: PASS
    authority: authoritative
    summary: "Locked restore succeeded for seven projects."
  - attempted_at: "2026-09-08T16:43:53.7490572Z"
    command: "dotnet build ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-restore"
    cwd: ".worktrees/verify-deliv-056-71a2d27c8836a44b639762469ec950f1c8c82802"
    exit_code: 0
    result: PASS
    authority: authoritative
    summary: "Integration owning project Release build succeeded, zero warnings and errors."
  - attempted_at: "2026-09-08T16:45:58.4763047Z"
    command: "dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter \"(FullyQualifiedName=Pegasus.IntegrationTests.CustodyOutboxIntegrationTests.AnAuditCaseCompletesCustody|FullyQualifiedName=Pegasus.IntegrationTests.CustodyOutboxIntegrationTests.AnAutomaticAuditReachesReviewWithOneIdentityAndItsDocuments|FullyQualifiedName=Pegasus.IntegrationTests.CustodyOutboxIntegrationTests.ReevaluationReadsTheRetainedLogicalSourceAfterStagingWasDeleted|FullyQualifiedName=Pegasus.IntegrationTests.CustodyOutboxIntegrationTests.ReevaluationRejectsRetainedSourceIdentityDriftBeforeReplacingTheReceipt|FullyQualifiedName=Pegasus.IntegrationTests.CustodyOutboxIntegrationTests.CancellationSqlFaultAndLeaseLossUseExactTaxonomyAndRequireStaffRecovery|FullyQualifiedName=Pegasus.IntegrationTests.InstructionDraftWebTests.IdenticalBytesWithDifferentTokensPersistDistinctSourceIdentitiesWithMatchingHashes|FullyQualifiedName=Pegasus.IntegrationTests.SendToAiIntegrationTests.InaccessibleCaseCannotPostSendToClaude)\" --logger \"trx;LogFileName=deliv-056-seven-71a2d27c8836a44b639762469ec950f1c8c82802.trx\" --results-directory ./artifacts/verification"
    cwd: ".worktrees/verify-deliv-056-71a2d27c8836a44b639762469ec950f1c8c82802"
    exit_code: 0
    result: PASS
    authority: authoritative
    summary: "Seven exact corrected methods passed, no failures/skips; TRX C40AA13FD73EE29AD6F9B7FA43BA85F41796D7C4FD2FB9FA7473DF0BBB8A8811."
  - attempted_at: "2026-09-08T16:47:13.9778933Z"
    command: "dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter \"FullyQualifiedName=Pegasus.IntegrationTests.TestUiFocusedRenderTests.HeldLeaseConfirmationClearsOnlyTheCurrentLeaseThroughRazor\" --logger \"trx;LogFileName=deliv-056-held-lease-71a2d27c8836a44b639762469ec950f1c8c82802.trx\" --results-directory ./artifacts/verification"
    cwd: ".worktrees/verify-deliv-056-71a2d27c8836a44b639762469ec950f1c8c82802"
    exit_code: 0
    result: PASS
    authority: authoritative
    summary: "Separate integrated HeldLease caller passed 1/1, no failures/skips; TRX B25E44CD0E5A186F49B1EA3ADC1980F0F2FB653DE6E8CC16D9E5C76C0CF7BB5B."
---

# DELIV-056 exact-merge proof

PR #709 independently reviewed head 23ea02f310a790c9aa3224a10de40efacf5444eb and GitHub squash merge 71a2d27c8836a44b639762469ec950f1c8c82802 were confirmed before verification. The declared pr.yml/verify/push workflow lookup returned HTTP 404 before Git; there is no exact-merge receipt, so all bounded obligations were run locally by the sole host verifier. Source was clean and detached at the exact merge before and after execution.

The current plan's seven corrected methods and separate post-integration HeldLease caller all passed. This is not a broad-suite PASS or D5 candidate approval. No production code, policy, schema, source evidence or assertion was changed during verification.

## Retained history and limits

The original pre-merge 13-class run remains FAIL: 269 total, 261 PASS, 7 FAIL, 1 skip; hash 01E4E965C1203ECEBE13AB50DCF0DDCF16EBAD2A416F581D60119E76BF411C7F. Exact-six remained FAIL 4/2 (FC765D98532AABD76BCA6AE245E6BEFCBA53696BAC56217ADF5B2392C9A5D377), exact-seven remained FAIL 5/2 (55A836D0EB42E98826A5991B83A98E0675879D4319BCEA22989B01AAD61BD799), and the final pre-merge two-Audit delta passed (A44267FEB86853AEBD7039F53D2231B10CD51C86211476075DAA7957830B69D7). These different-input attempts remain in the full execution/report/scratch ledger; none is reclassified as exact-merge evidence.

PR-head optional browser/Test UI failures and SQL shard 3 failure are the unchanged UploadCaseSearchBrowserTests.CaseSearchComboboxIsKeyboardOperableAndCompletesTheAttachDecision and UploadConfirmationWebTests.AttachAddsAnUnmatchedInstructionUploadToTheChosenCaseAndReplaysSafely contract issue, explicitly deferred by independent review to [[INTK-066]]. They were not rerun or claimed passing here. Broader corrective-release candidate coverage and that operator decision remain outstanding.

The full command/setup/exit ledger is scratch/verify.md@501f0cae8cf72d07. The two exact-merge TRXs are retained outside the disposable worktree under artifacts/verification/deliv-056-71a2d27c8836a44b639762469ec950f1c8c82802/ with the hashes above. Closeout must also retain pre-merge failure artifacts before removing the implementation workspace.
