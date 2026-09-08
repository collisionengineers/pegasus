2026-09-08 implementation handoff

Implemented the approved existing-helper extension in `tests/Pegasus.IntegrationTests/IntakeWebTestSupport.cs`: `CreateDefinitiveQdosInstructionDocument(...)` produces a real PDF document containing formal notification title plus QDOS extraction signals. It accepts scenario fields and optional extra lines; no production policy/parser/source change.

Updated the affected existing transport paths in CustodyOutbox, InstructionDraft, ImageViewing, ImageIntake shared case seed (therefore MailWorkspace/TriageQueues/UploadConfirmation/TestUi callers), MailboxIntake, MultiFormat, QdosTriage, Recovery, SendToAi and UploadConfirmation. Where a test’s asserted path was an actual MIME/PDF/DOCX/MSG/mailbox/upload flow, the formal content is an actual document attachment or structured document, not EmailBody. Direct display/setup continues to use its existing callers; no corpus gate or request bypass was introduced.

SendToAi retains absent-dialog and NotFound POST assertions, and removes only the stale disabled/gated-control markup expectation.

Static evidence only:
- `git diff --check`: PASS (no diagnostic; Git emitted only CRLF checkout warnings).
- Exact changed paths are a subset of the approved fourteen-file map: 11 test files, all under `tests/Pegasus.IntegrationTests/`; no production/docs/scripts/corpus changes.
- Remaining `QDOS instruction` / `Vehicle Registration:` markers are intentional negative/limit probes, a forwarded-address display string, or invalid field input that is mirrored into its attached formal instruction document.
- No build, test, browser, capture, package, deployment, commit, push, PR, merge, or verification run by this worker.

Await parent-coordinated independent review and designated host verification of the exact existing 13-class selection.

2026-09-08 parent static-review correction

Parent identified two helper-root-cause defects before verification. Fixed them:
- No fixture facts are defaulted. Registration and vehicle now default to null; their structural labels are blank only where the prior scenario had no corresponding fact. Callers with an original registration pass it explicitly. Removed introduced Ford Focus/default registration and the invented mailbox claim number.
- Additional document lines split on '\r' and '\n' characters with the existing repository pattern, so raw C# LF strings cannot reach PdfPig as multiline/control-character text.
- InstructionDraft now adds the formal title only and carries its own normalized QDOS/signature lines with addSignatureLines false, avoiding duplicate nonblank registration/vehicle content. Its added vehicle label is intentionally blank.

Repeated static git diff --check and approved-scope census: PASS (only CRLF checkout warnings). No build/test/browser/other host-owned verification was run.

## Sole-host verification — FAIL (2026-09-08)

Verifier: `/root/agent_config_verifier`
Queue lane: 4/4
Frozen worktree: `.worktrees/deliv-056`
Frozen branch: `DELIV-056-align-intake-regression-fixtures-with-definitive-instruction-evidence`
Frozen head: `7b6aa189c2112ab3cf8df2c2e337fc9f2b0dabae`

Preflight at `2026-09-08T14:52:57.7749337Z` matched the parent-frozen handoff exactly: 10 modified files, all under `tests/Pegasus.IntegrationTests`; 211 insertions/101 deletions; `git diff --check` exit 0 (repository LF→CRLF advisories only); binary diff hash `eb759743751ad38bcbf4fb1ee648594d36641127`. Only idle reusable MSBuild nodes were resident; no testhost/vstest or competing command was active. Parent also supplied an independent final static review of NO FINDINGS.

### Commands

1. `dotnet restore ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --locked-mode`
   - attempted_at: `2026-09-08T14:53:05.2279602Z`
   - exit_code: **0**
   - result: PASS
2. `dotnet build ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-restore`
   - attempted_at: `2026-09-08T14:53:14.3276237Z`
   - exit_code: **0**
   - result: PASS
   - summary: Build succeeded; 0 warnings, 0 errors.
3. `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~CustodyOutboxIntegrationTests|FullyQualifiedName~InstructionDraftWebTests|FullyQualifiedName~ImageViewingWebTests|FullyQualifiedName~MailWorkspaceWebTests|FullyQualifiedName~QdosTriageIntegrationTests|FullyQualifiedName~TriageQueuesWebTests|FullyQualifiedName~ImageIntakeWebTests|FullyQualifiedName~MailboxIntakeIntegrationTests|FullyQualifiedName~MultiFormatIntakeWebTests|FullyQualifiedName~RecoveryTests|FullyQualifiedName~SendToAiIntegrationTests|FullyQualifiedName~UploadConfirmationWebTests|FullyQualifiedName~TestUiFocusedRenderTests" --logger "trx;LogFileName=deliv-056-focused-host-20260908.trx" --results-directory artifacts/verification`
   - attempted_at: `2026-09-08T14:54:38.3006773Z`
   - exit_code: **1**
   - result: **FAIL**
   - observed totals: Failed 7, Passed 261, Skipped 1, Total 269, Duration 9m24s.
   - TRX: `artifacts/verification/deliv-056-focused-host-20260908.trx`
   - TRX SHA-256: `01E4E965C1203ECEBE13AB50DCF0DDCF16EBAD2A416F581D60119E76BF411C7F`

The live plan named 13 class-substring selectors, but this filter selected 269 cases rather than the parent's expected 44; `FullyQualifiedName~RecoveryTests` also selected one `QdosAllocationRecoveryTests` case, which skipped. This command-selection overbreadth is retained as verifier evidence and was not retried. All seven failures below are nevertheless inside the ticket's explicitly named authorized classes and are genuine failures:

- `CustodyOutboxIntegrationTests.AnAuditCaseCompletesCustody`: expected `CaseCreated`; actual `NeedsSorting`.
- `CustodyOutboxIntegrationTests.AnAutomaticAuditReachesReviewWithOneIdentityAndItsDocuments`: expected `CaseCreated`; actual `NeedsSorting`.
- `CustodyOutboxIntegrationTests.ReevaluationRejectsRetainedSourceIdentityDriftBeforeReplacingTheReceipt`: `InvalidOperationException` — accepted intake receipt cannot be changed through pre-case workflow.
- `CustodyOutboxIntegrationTests.ReevaluationReadsTheRetainedLogicalSourceAfterStagingWasDeleted`: same `InvalidOperationException`.
- `CustodyOutboxIntegrationTests.CancellationSqlFaultAndLeaseLossUseExactTaxonomyAndRequireStaffRecovery`: expected exact `DbUpdateException`; actual `NotSupportedException` — custody adapter does not retain instruction attachments.
- `InstructionDraftWebTests.IdenticalBytesWithDifferentTokensPersistDistinctSourceIdentitiesWithMatchingHashes`: expected 4; actual 3.
- `UploadConfirmationWebTests.AttachAddsAnUnmatchedInstructionUploadToTheChosenCaseAndReplaysSafely`: expected page substring `No existing case matched this` was absent.

Postcheck at `2026-09-08T15:04:27.8335379Z` retained the exact 10-file source status, clean diff check, and binary diff hash. Only idle reusable MSBuild nodes remained.

Disposition: **FAIL**. No fix, rerun, source write, full rail, assertion weakening, commit, push, PR, merge, or stage mutation was performed.

## 2026-09-08 corrective fixture addendum

Implemented the six approved corrective items within the existing DELIV-056 test-fixture change set. New edits were confined to `CustodyOutboxIntegrationTests.cs` and `InstructionDraftWebTests.cs`; the shared PDF builder was reused unchanged.

- Re-evaluation sources use a genuine attached PDF that truthfully says its work type is not yet classified.
- Audit custody fixtures now attach both a genuine Audit notification and a distinct original bodyshop report with the repairable assessment. The bodyshop report is attached first because the existing automatic-evidence seed deliberately selects the receipt's first retained attachment as the original report.
- The counting custody fake counts attachment-retention effects through the existing default lease-guard overload.
- The duplicate submission test now asserts one shared case, exactly one allocation, and the three truthful receipt-event rows.

No test/build was run and no commit, push, PR, or production action was performed; the sole host verifier remains responsible for the required existing selections. Static `git diff --check` passed (exit 0; Git emitted only LF-to-CRLF warnings). The complete dirty DELIV-056 set remains the original ten test files, with current whole-diff binary hash `f75a69cfdfd243d02d7e1210f39246f1f95bfb81`.
