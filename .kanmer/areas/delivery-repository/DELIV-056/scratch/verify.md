## Correction-freeze sole-host verification — FAIL (2026-09-08)

Verifier: `/root/agent_config_verifier`
Worktree: `.worktrees/deliv-056`
Branch: `DELIV-056-align-intake-regression-fixtures-with-definitive-instruction-evidence`
HEAD: `7b6aa189c2112ab3cf8df2c2e337fc9f2b0dabae`
Plan version: `b285fa245635a7af`

The earlier broad attempt remains unchanged and authoritative as historical failure inventory: exit 1, 269 selected, 7 failed, 261 passed, 1 skipped. It was not rerun.

Preflight at `2026-09-08T15:27:15.0122740Z` found no active testhost/vstest or competing verification command. Source remained the original ten-file packet with the two-file correction included, 283 insertions/104 deletions. `git diff --check` exited 0 with repository LF→CRLF advisories only. Full binary diff hash exactly matched `f75a69cfdfd243d02d7e1210f39246f1f95bfb81`. No dependency/build-input file differed; the existing locked restore assets were present from the prior locked restore at `2026-09-08T14:53:05Z`, so no fresh restore ran.

### Commands

1. `dotnet build ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-restore`
   - attempted_at: `2026-09-08T15:27:22.4894893Z`
   - exit_code: **0**
   - result: PASS
   - summary: Build succeeded; 0 warnings, 0 errors.
2. `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "(FullyQualifiedName=Pegasus.IntegrationTests.CustodyOutboxIntegrationTests.AnAuditCaseCompletesCustody|FullyQualifiedName=Pegasus.IntegrationTests.CustodyOutboxIntegrationTests.AnAutomaticAuditReachesReviewWithOneIdentityAndItsDocuments|FullyQualifiedName=Pegasus.IntegrationTests.CustodyOutboxIntegrationTests.ReevaluationReadsTheRetainedLogicalSourceAfterStagingWasDeleted|FullyQualifiedName=Pegasus.IntegrationTests.CustodyOutboxIntegrationTests.ReevaluationRejectsRetainedSourceIdentityDriftBeforeReplacingTheReceipt|FullyQualifiedName=Pegasus.IntegrationTests.CustodyOutboxIntegrationTests.CancellationSqlFaultAndLeaseLossUseExactTaxonomyAndRequireStaffRecovery|FullyQualifiedName=Pegasus.IntegrationTests.InstructionDraftWebTests.IdenticalBytesWithDifferentTokensPersistDistinctSourceIdentitiesWithMatchingHashes)" --logger "trx;LogFileName=deliv-056-corrections-host-20260908-1528.trx" --results-directory artifacts/verification`
   - attempted_at: `2026-09-08T15:28:21.6109611Z`
   - exit_code: **1**
   - result: **FAIL**
   - summary: Failed 2, Passed 4, Skipped 0, Total 6, Duration 53s.
   - TRX: `artifacts/verification/deliv-056-corrections-host-20260908-1528.trx`
   - TRX SHA-256: `FC765D98532AABD76BCA6AE245E6BEFCBA53696BAC56217ADF5B2392C9A5D377`.

Both failures are the corrected Audit fixtures:
- `CustodyOutboxIntegrationTests.AnAuditCaseCompletesCustody`: SQL unique-index violation inserting a duplicate `StandaloneAuditEvidence` row for the same `IntakeReceiptId`; failure reaches `AllocationTestData.SeedAutomaticAuditEvidenceAsync` and the test at line 2397.
- `CustodyOutboxIntegrationTests.AnAutomaticAuditReachesReviewWithOneIdentityAndItsDocuments`: the same duplicate `StandaloneAuditEvidence.IntakeReceiptId` violation; test line 2474.

Postcheck at `2026-09-08T15:29:33.4049402Z` retained the exact ten-file source status, clean diff check, and full binary diff hash. Only idle reusable MSBuild nodes remained; no testhost/vstest or active verification command remained.

Disposition: **FAIL**. No fix, retry, broad rerun, UploadConfirmation/browser command, source write, commit, push, PR, merge, or cleanup was performed.

## Final seven-method correction freeze — FAIL — 2026-09-08

Verifier: `/root/agent_config_verifier`
Worktree: `.worktrees/deliv-056`
Branch: `DELIV-056-align-intake-regression-fixtures-with-definitive-instruction-evidence`
HEAD: `7b6aa189c2112ab3cf8df2c2e337fc9f2b0dabae`
Plan/checklist: `5cbc29b767beb13e` / `7d6015e99b4c1fda`

The retained broad result (269 selected, 7 failed) and earlier exact-six result (2 failed, 4 passed) remain unchanged and non-PASS. Neither was rerun.

At `2026-09-08T15:49:27.0472541Z`, `dotnet build-server shutdown` exited 0 and left no scoped verification processes. Preflight at `2026-09-08T15:50:18.3949323Z` then confirmed the recorded branch/HEAD, exactly the same ten modified test files, `git diff --check` exit 0 with only LF→CRLF advisories, no dependency/build-input delta, existing locked restore assets, no competing process, and exact full binary-diff Git object hash `bfa477f890e1da5fb570c0bdecfd0825459a3f7c` (292 insertions, 108 deletions).

### Commands

1. `dotnet build ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-restore`
   - attempted_at: `2026-09-08T15:50:27.2497278Z`
   - exit_code: **0**
   - result: build succeeded; 0 warnings, 0 errors.
2. `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "(FullyQualifiedName=Pegasus.IntegrationTests.CustodyOutboxIntegrationTests.AnAuditCaseCompletesCustody|FullyQualifiedName=Pegasus.IntegrationTests.CustodyOutboxIntegrationTests.AnAutomaticAuditReachesReviewWithOneIdentityAndItsDocuments|FullyQualifiedName=Pegasus.IntegrationTests.CustodyOutboxIntegrationTests.ReevaluationReadsTheRetainedLogicalSourceAfterStagingWasDeleted|FullyQualifiedName=Pegasus.IntegrationTests.CustodyOutboxIntegrationTests.ReevaluationRejectsRetainedSourceIdentityDriftBeforeReplacingTheReceipt|FullyQualifiedName=Pegasus.IntegrationTests.CustodyOutboxIntegrationTests.CancellationSqlFaultAndLeaseLossUseExactTaxonomyAndRequireStaffRecovery|FullyQualifiedName=Pegasus.IntegrationTests.InstructionDraftWebTests.IdenticalBytesWithDifferentTokensPersistDistinctSourceIdentitiesWithMatchingHashes|FullyQualifiedName=Pegasus.IntegrationTests.SendToAiIntegrationTests.InaccessibleCaseCannotPostSendToClaude)" --logger "trx;LogFileName=deliv-056-final-seven-host-20260908-1551.trx" --results-directory artifacts/verification`
   - attempted_at: `2026-09-08T15:51:24.3646985Z`
   - exit_code: **1**
   - result: **FAIL** — 2 failed, 5 passed, 0 skipped, total 7.
   - TRX: `artifacts/verification/deliv-056-final-seven-host-20260908-1551.trx`
   - TRX SHA-256: `55A836D0EB42E98826A5991B83A98E0675879D4319BCEA22989B01AAD61BD799`.

Both Audit methods still fail, now after the obsolete duplicate-evidence seed has been removed:

- `CustodyOutboxIntegrationTests.AnAuditCaseCompletesCustody` — `Pegasus.Core.Intake.IntakeVersionConflictException: The intake or case changed after it was loaded.`; failure at `AcceptAsync`, test line 2400.
- `CustodyOutboxIntegrationTests.AnAutomaticAuditReachesReviewWithOneIdentityAndItsDocuments` — the same `IntakeVersionConflictException`; failure at `AcceptAsync`, test line 2479.

The other five named methods passed, including the newly added `SendToAiIntegrationTests.InaccessibleCaseCannotPostSendToClaude` negative-launcher assertion.

Postcheck at `2026-09-08T15:52:41.5384930Z` retained the exact ten-file status, clean diff check, and exact binary-diff hash. Only three idle reusable MSBuild node processes remained; no testhost/vstest or active verification command remained.

Disposition: **FAIL**. Per the stop rule there was no retry, source fix, broad rerun, browser command, commit, push, PR, merge, proof, stage move, release, deployment, or cleanup.
