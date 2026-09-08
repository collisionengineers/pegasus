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

## Audit acceptance-version delta — PASS — 2026-09-08

Verifier: `/root/agent_config_verifier`
Worktree/branch: `.worktrees/deliv-056` / `DELIV-056-align-intake-regression-fixtures-with-definitive-instruction-evidence`
HEAD: `7b6aa189c2112ab3cf8df2c2e337fc9f2b0dabae`
Plan/checklist: `0b0fcbe6c75216b6` / `ee299342275d936e`

The broad 269/7-fail, exact-six 4-pass/2-fail, and exact-seven 5-pass/2-fail attempts remain retained. This delta reran only the two methods changed after the exact-seven attempt; the other five stable passes remain bound to prior full-diff hash `bfa477f890e1da5fb570c0bdecfd0825459a3f7c`.

At `2026-09-08T15:59:15.4963593Z`, `dotnet build-server shutdown` exited 0 and left no scoped process. Preflight at `2026-09-08T15:59:32.2936366Z` confirmed the same branch/HEAD and ten test files, `git diff --check` exit 0 with LF→CRLF advisories only, no dependency/build-input delta, no competing process, and exact full binary-diff Git object hash `bdbe80e82a6569a4a79ee57f44998f98f679eef2` (294 insertions, 108 deletions). Static inspection found exactly two `expectedVersion: evidence.ReceiptVersion` arguments, at the two authorized Audit call sites.

### Commands

1. `dotnet build ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-restore`
   - attempted_at: `2026-09-08T15:59:40.4613296Z`
   - exit_code: **0**
   - result: build succeeded; 0 warnings, 0 errors.
2. `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "(FullyQualifiedName=Pegasus.IntegrationTests.CustodyOutboxIntegrationTests.AnAuditCaseCompletesCustody|FullyQualifiedName=Pegasus.IntegrationTests.CustodyOutboxIntegrationTests.AnAutomaticAuditReachesReviewWithOneIdentityAndItsDocuments)" --logger "trx;LogFileName=deliv-056-audit-version-host-20260908-1600.trx" --results-directory artifacts/verification`
   - attempted_at: `2026-09-08T16:00:36.0218425Z`
   - exit_code: **0**
   - result: 2 passed, 0 failed, 0 skipped.
   - TRX: `artifacts/verification/deliv-056-audit-version-host-20260908-1600.trx`
   - TRX SHA-256: `A44267FEB86853AEBD7039F53D2231B10CD51C86211476075DAA7957830B69D7`.

Postcheck at `2026-09-08T16:01:33.0263207Z` retained the exact ten-file status, clean diff check, and full binary-diff hash. Only three idle reusable MSBuild nodes remained; no testhost/vstest or active verification command remained.

Disposition: **PASS** for the two-method expected-version delta. Together with the five unchanged-source passes from the immediately prior exact-seven run, every named D56 correction method now has focused passing evidence at its applicable frozen diff. This is not a broad-suite PASS and does not erase any earlier failure. No additional test, source mutation, retry, proof, stage move, commit, push, PR, merge, cleanup, release, deployment, or ENG check occurred.

## Exact-merge verification runtime — PASS — 2026-09-08

Target: GitHub merge commit `71a2d27c8836a44b639762469ec950f1c8c82802`.
Environment: `C:\Users\Alex\Documents\GitHub\pegasus\.worktrees\verify-deliv-056-71a2d27c8836a44b639762469ec950f1c8c82802`, Windows PowerShell 7 / .NET SDK 10.0.302.
Receipt classification preceded Git: the declared `pr.yml` / `verify` / `push` exact-SHA lookup returned HTTP 404. Therefore every obligation was missing, no receipt was rejected, and this local fallback has `receipts: []`.

Ordered setup/runtime:

1. At 2026-09-08T16:42:45.6723370Z the canonical path was absent and the worktree census showed no ownership collision.
2. 2026-09-08T16:42:54.7700768Z–2026-09-08T16:42:55.7814978Z — `git fetch origin`, exit 0; `origin/dev` advanced from `05995d325` to `71a2d27c8`.
3. 2026-09-08T16:43:08.0913944Z–2026-09-08T16:43:09.9225609Z — `git worktree add --detach .worktrees/verify-deliv-056-71a2d27c8836a44b639762469ec950f1c8c82802 71a2d27c8836a44b639762469ec950f1c8c82802`, exit 0.
4. 2026-09-08T16:43:25.7701119Z–2026-09-08T16:43:26.5499344Z — exact preflight PASS: `git rev-parse HEAD` returned target, `git symbolic-ref --short -q HEAD` exit 1/empty, `git status --short --branch` returned only `## HEAD (no branch)`, and scoped active process count was zero.
5. 2026-09-08T16:43:38.7829907Z–2026-09-08T16:43:42.5012614Z — `dotnet restore ./Pegasus.slnx --locked-mode`, exit 0; all seven projects restored.
6. 2026-09-08T16:43:53.7490572Z–2026-09-08T16:45:33.0254159Z — `dotnet build ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-restore`, exit 0: build succeeded, 0 warnings, 0 errors, elapsed 1m38.81s. Native session `92317` was retained through three polls and its final exit.
7. 2026-09-08T16:45:58.4763047Z–2026-09-08T16:46:57.6223541Z — `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "(FullyQualifiedName=Pegasus.IntegrationTests.CustodyOutboxIntegrationTests.AnAuditCaseCompletesCustody|FullyQualifiedName=Pegasus.IntegrationTests.CustodyOutboxIntegrationTests.AnAutomaticAuditReachesReviewWithOneIdentityAndItsDocuments|FullyQualifiedName=Pegasus.IntegrationTests.CustodyOutboxIntegrationTests.ReevaluationReadsTheRetainedLogicalSourceAfterStagingWasDeleted|FullyQualifiedName=Pegasus.IntegrationTests.CustodyOutboxIntegrationTests.ReevaluationRejectsRetainedSourceIdentityDriftBeforeReplacingTheReceipt|FullyQualifiedName=Pegasus.IntegrationTests.CustodyOutboxIntegrationTests.CancellationSqlFaultAndLeaseLossUseExactTaxonomyAndRequireStaffRecovery|FullyQualifiedName=Pegasus.IntegrationTests.InstructionDraftWebTests.IdenticalBytesWithDifferentTokensPersistDistinctSourceIdentitiesWithMatchingHashes|FullyQualifiedName=Pegasus.IntegrationTests.SendToAiIntegrationTests.InaccessibleCaseCannotPostSendToClaude)" --logger "trx;LogFileName=deliv-056-seven-71a2d27c8836a44b639762469ec950f1c8c82802.trx" --results-directory ./artifacts/verification`, exit 0: 7 passed, 0 failed, 0 skipped, duration 56s. Native session `19793` retained through final exit. TRX SHA-256: `C40AA13FD73EE29AD6F9B7FA43BA85F41796D7C4FD2FB9FA7473DF0BBB8A8811`.
8. 2026-09-08T16:47:13.9778933Z–2026-09-08T16:47:50.8653936Z — `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName=Pegasus.IntegrationTests.TestUiFocusedRenderTests.HeldLeaseConfirmationClearsOnlyTheCurrentLeaseThroughRazor" --logger "trx;LogFileName=deliv-056-held-lease-71a2d27c8836a44b639762469ec950f1c8c82802.trx" --results-directory ./artifacts/verification`, exit 0: 1 passed, 0 failed, 0 skipped, duration 33s. Native session `51132` retained through final exit. TRX SHA-256: `B25E44CD0E5A186F49B1EA3ADC1980F0F2FB653DE6E8CC16D9E5C76C0CF7BB5B`.
9. Postcheck 2026-09-08T16:48:07.6038821Z–2026-09-08T16:48:07.9920907Z: status remained only `## HEAD (no branch)`; scoped active process count zero.

The complete earlier non-PASS/convergence inventory remains authoritative history and is not erased or called broad PASS:

- original 13-class run: exit 1, 269 total / 261 pass / 7 fail / 1 skip; SHA-256 `01E4E965C1203ECEBE13AB50DCF0DDCF16EBAD2A416F581D60119E76BF411C7F`;
- exact-six: exit 1, 4 pass / 2 Audit fail; SHA-256 `FC765D98532AABD76BCA6AE245E6BEFCBA53696BAC56217ADF5B2392C9A5D377`;
- exact-seven: exit 1, 5 pass / 2 Audit version-conflict fail; SHA-256 `55A836D0EB42E98826A5991B83A98E0675879D4319BCEA22989B01AAD61BD799`;
- final two-method Audit delta: exit 0, 2/2 pass; SHA-256 `A44267FEB86853AEBD7039F53D2231B10CD51C86211476075DAA7957830B69D7`.

The unchanged/deferred `UploadConfirmationWebTests.AttachAddsAnUnmatchedInstructionUploadToTheChosenCaseAndReplaysSafely` and `UploadCaseSearchBrowserTests.CaseSearchComboboxIsKeyboardOperableAndCompletesTheAttachDecision` failures remain owned by INTK-066/F-001. They were not run, folded into D56, or represented as D56 failures. This exact-merge result is PASS only for D56's seven corrected methods and the post-integration HeldLease obligation. The final D5 converged coverage obligation remains separately required.

No source edit, broad rerun, snapshot, proof, stage move, cleanup, release, deployment or production action occurred.
