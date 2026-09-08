## Focused integration failure dispositions and joint static correction — 8 September 2026

All 15 failures from the retained `cc826889407b97dc2d951219c70b59e619de70f2` focused Integration run remain recorded above; none is erased by this local correction. The source dispositions are:

1. `TriageQueuesWebTests.AwaitingAttachMovesTheImageIntakeToAnExistingCase`: update the caller to the existing two-step Prepare (200) then reviewed CaseId/CaseVersion confirmation contract.
2. `UploadOutcomeQueriesTests.CompletedGroupedImageWithoutASettledDestinationIsStillProcessing`: preserve image-only grouped pending truth; fixture is image material.
3. `NoUsableVrmImageGroupRoutedToUnidentifiedIsReportedForReview`: preserve group Unidentified review precedence; fixture is image material.
4. `ResolvedGroupedUnidentifiedItemIsReportedWithoutPollingOrAnotherDecision`: report settled Resolved before any manual proposal.
5. `ImageIntakeWebTests.ConfidentReadAutoRegistersAndAutoAssociatesTheUnambiguousCase`: child fixture slice uses the genuine supported non-manual Mailbox route.
6. `UploadConfirmationWebTests.AttachGroupAddsEveryOpenMemberToTheChosenCase`: viable destination lookup no longer rejects persisted Case type codes.
7. `ManualUploadWithAUniqueImageMatchStillRequiresStaffConfirmation`: the viable destination read returns the recorded existing Case for explicit confirmation.
8. `AttachAddsAnUnmatchedInstructionUploadToTheChosenCaseAndReplaysSafely`: the visible message states `Choose a case destination` despite the Pending chip.
9. `ImageIntakePersistenceTests.GroupRegistrationAndInterruptedPairingPreserveEveryMember(staffOverride: True)`: child fixture slice uses genuine supported group Mailbox material.
10. The same grouped persistence test with `staffOverride: False`: same child fixture correction.
11. `AttachMergesARegisteredImageGroupIntoACaseTypedByReference`: viable persisted destinations no longer fail on lower-case type codes.
12. `CasesIndexWebTests.AwaitingImageSelectionCarriesTheExactOriginReceiptIntoConfirmation`: the existing confirmation route receives a viable destination rather than a false destination rejection.
13. `CaseCreateWebTests.CreateReplaysTheCommittedAddressBeforeRetryingAcceptance`: continuation uses `ExpectedReceiptVersion + 1` after correction and one further address advancement, including replay.
14. `ImageIntakePersistenceTests.ReceiptLinkEnforcesEligibilityOnceAnImageIntakeExists`: child fixture preserves the current shared conflict boundary while proving the receipt remains unassociated/unmerged.
15. `CaseCreateWebTests.RepeatedCreateSubmissionWithTheSameOperationIdAllocatesOneReference`: allocation receives the stable continuation and normalized posted inspection date, not a mutable replay snapshot.

Joint local source slices reviewed by root:

- Main source/caller slice: `EfIntakeAssociationDestinations.cs`, `UploadOutcome.cs`, `Cases/Create.cshtml.cs`, `UploadOutcomeQueriesTests.cs`, and `TriageQueuesWebTests.cs`. It removes only the irrelevant Case.Type parse/projection; uses existing outcome kinds/components; no new port, journal, schema, dependency, or fallback.
- Exclusive child fixture slice (not edited by the main author): `ImageIntakeWebTests.cs` and `ImageIntakePersistenceTests.cs`; root accepted its real Mailbox durable single/group setup and preserved automatic/replay/reversal/eligibility assertions.

Root reviewed the complete seven-file joint diff. `git diff --check` passed (only line-ending warnings). Static-only authorization: no host check, build, test, push, or PR was run. Local commit authorized; freeze resulting exact head for independent verification.

- 2026-09-08T19:53:04.243Z lease-phase implementing → running-command (lease eef8e3c2-cb07-4588-a7f9-5cb09a3433fc rev 22; expires 2026-09-08T20:53:04.232Z)

## Sole host re-grant — INTK-066 focused integration correction

Root read canonical IDLE c8b61ca457a80335, ready resumed packet, reviewed both exclusive seven-file correction slices, and confirmed actual clean HEAD cca2c76cb9d4acc19e68a1a776719d5dc701f8d3 in unchanged .worktrees/INTK-066 / INTK-066-manual-upload-confirmation. Prior cc826 build/Core passes and15 focused failures remain retained with concrete dispositions. Both authors idle. Sole verifier /root/agent_config_verifier ACTIVE; lease renewed running-command60min.

Run original build, then Core --no-build, then exact focused nonbrowser Integration queue with one necessary affected-caller addition: (FullyQualifiedName~UploadConfirmationWebTests|FullyQualifiedName~UploadOutcomeQueriesTests|FullyQualifiedName~CaseCreateWebTests|FullyQualifiedName~GroupedIntakeWebTests|FullyQualifiedName~ImageIntake|FullyQualifiedName~CasesIndexWebTests|FullyQualifiedName~MailWorkspaceWebTests|FullyQualifiedName~TriageQueuesWebTests.AwaitingAttach)&Category!=Browser. This explicitly includes the changed blank-reason caller test rather than relying on method-name ImageIntake substring. Only after all pass, run Category=Browser&FullyQualifiedName~UploadCaseSearchBrowserTests with xUnit.MaxParallelThreads=1 and documented browser setup. Preserve all failures; no weakening or source/snapshot changes; first genuine failure stops remainder/no autonomous retry.

Fresh process census required; prior final census was empty. Exact invocation-owned reusable MSBuild nodes may be recorded and stopped after their parent build exits using PID/start/parent/expected command validation, as preceding grant; no foreign/name-based termination. Local disposable SQL only, no live/cloud/Outlook/Box/push/PR. Record commands/exits and explicit canonical IDLE on completion/stop. Later remaining full cohorts, responsive and four-scope snapshot obligations are not waived.

## Focused-integration corrected head — build/Core pass, focused integration fail — 2026-09-08

Grant inputs initially read at ticket revision `rev1:cd6a7f62f4f36235`, lease revision 22, exact base/head `9ae9db753e3a3ecce1d9735d5c2fbe6fb5b0ff2c` / `cca2c76cb9d4acc19e68a1a776719d5dc701f8d3`. The first evidence append was rejected with REVISION_CONFLICT after a concurrent root update advanced the ticket to `rev1:119f6a9c51703007` / lease revision 24; nothing was overwritten, and this append preserves that update.

Preflight at `2026-09-08T19:53:43.0309831Z` exited 0: exact clean branch/head, base ancestry, and empty dotnet/MSBuild/testhost/vstest census.

### Build — PASS

`dotnet build Pegasus.slnx` ran `19:53:55.1622127Z`–`19:55:38.6768183Z`, exit 0: 0 warnings, 0 errors, elapsed 00:01:43.08.

After parent PID 17232 exited, six exact invocation-created reusable MSBuild nodes were validated and stopped: PIDs 25204, 8876, 22656, 30760, 21132 and 20468; created `19:53:55.777319Z`–`19:53:55.782216Z`; all expected Program Files dotnet `MSBuild.dll /nodemode:1 /nodeReuse:true`. Cleanup exited 0 with none remaining at `19:56:05.0281741Z`.

### Core — PASS

`dotnet test tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --no-build` ran `19:56:12.9585336Z`–`19:56:20.1397209Z`, exit 0: 1,955 passed, 14 skipped, 0 failed, 1,969 total.

### Amended focused non-browser integration — FAIL

`dotnet test tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --no-build --filter "(FullyQualifiedName~UploadConfirmationWebTests|FullyQualifiedName~UploadOutcomeQueriesTests|FullyQualifiedName~CaseCreateWebTests|FullyQualifiedName~GroupedIntakeWebTests|FullyQualifiedName~ImageIntake|FullyQualifiedName~CasesIndexWebTests|FullyQualifiedName~MailWorkspaceWebTests|FullyQualifiedName~TriageQueuesWebTests.AwaitingAttach)&Category!=Browser"` ran `19:56:37.8598213Z`–`20:02:06.0961379Z`, exit 1: 119 passed, 8 failed, 0 skipped, 127 total, duration 5m25s.

Failures:
1. `UploadConfirmationWebTests.AttachGroupAddsEveryOpenMemberToTheChosenCase` line 475 — expected HTTP Found, actual OK.
2. `UploadConfirmationWebTests.AttachAddsAnUnmatchedInstructionUploadToTheChosenCaseAndReplaysSafely` line 125 — expected HTTP Found, actual OK.
3. `UploadConfirmationWebTests.RegisterGroupCreatesOneVehicleImageCaseFromTheStaffTypedRegistration` line 427 — expected non-null value, actual null.
4. `TriageQueuesWebTests.AwaitingAttachMovesTheImageIntakeToAnExistingCase` line 534 — expected HTTP Found, actual OK.
5. `UploadConfirmationWebTests.AnUndecidedGroupShowsOneSubmissionDecisionInsteadOfPerFileOffers` line 374 — rendered HTML lacked `This submission`.
6. `UploadConfirmationWebTests.AttachMergesARegisteredImageGroupIntoACaseTypedByReference` line 212 — expected HTTP Found, actual OK.
7. `CasesIndexWebTests.AwaitingManualImageGroupLinksToItsSubmissionConfirmation` line 123 — expected non-null value, actual null.
8. `CasesIndexWebTests.AwaitingImageSelectionCarriesTheExactOriginReceiptIntoConfirmation` line 75 — expected HTTP Found, actual OK.

No TRX/logger or durable rendered response was configured; stdout exposed only the ordinary HTML document prefix and the absent text for failure 5. All previous failures remain retained.

Per stop-first-failure, the UploadCaseSearch browser command is **NOT RUN**. No retry, source/assertion/filter change, snapshot update, live action, push or PR occurred.

Postcheck at `2026-09-08T20:02:24.1103209Z` exited 0: exact branch/head remained clean and no dotnet, MSBuild, testhost or vstest process remained.

- 2026-09-08T20:04:14.575Z lease-phase running-command → implementing (lease eef8e3c2-cb07-4588-a7f9-5cb09a3433fc rev 25; expires 2026-09-08T20:34:14.567Z)

## Primary disposition — cca2 eight failures and remaining UI evidence

Root reread exact canonical IDLE e053f70fdefb75d6 and full eight-failure record fb3673c15111b1a7; fresh execution packet ready. Preserve all build/Core passes and SQL failures. Attachment failures 1,2,4,6,8 share the two lease keys exceeding CaseCommandSeamRules.ValidateAcquire's 100-character limit. Use the already computed deterministic decision operationKey under a lease prefix at BOTH acquisition sites; do not change the limit, hash policy or assertions. Failures 3,5,7 share manual image open-Unidentified NeedsReview having no Attach payload, so Group Load excludes it from the reviewed open roster and registration silently returns. Add the existing bound Attach payload only for eligible manual image NeedsReview, retaining its U/NeedsReview state, pre-reconcile Working and resolved behavior, existing suggestions/viability and no pure-image new-Case proposal. Add focused outcome assertions without deleting current state expectations.

Authorize primary author source-only correction in UploadCaseDecision.cs, UploadOutcome.cs and direct UploadOutcomeQueriesTests.cs. Also authorize already-required UI evidence preparation in existing UploadCaseSearchBrowserTests.cs and QdosAllocationRecoveryBrowserTests.cs: smallest 1580/1100/760 theory amendments for actual confirmation and editable Create states; retain existing keyboard, ARIA, mutation and Audit assertions, no new driver. TestUiSnapshotTests.cs exact upload-status--needs-decision matcher may use current rendered Choose a case destination phrase so existing scoped capture identifies the intended new state, not historical copy. No generated HTML edits; snapshot script owns those later. All six files are within the existing packet. No host tests/build/scripts/browser, live actions, commit/push/PR until root inspects diff. Return readiness and stop. Host stays IDLE; later grant must freeze actual new head.

## 2026-09-08 — implementation correction committed

Committed `500b86a9b9f7e2d8d12b0b2a5a1ef3072b27d6aa` (`Fix manual upload confirmation decisions`) on `INTK-066-manual-upload-confirmation`. The correction is limited to the reviewed six-file scope.

No verification was run at this new head. Retained prior evidence: build and Core checks passed on the earlier frozen head; the focused integration run reported 119 passed and 8 failed. Those eight failures remain intentionally retained and must be rerun against `500b86a9b9f7e2d8d12b0b2a5a1ef3072b27d6aa`: AttachGroup, unmatched Attach, RegisterGroup null, Triage Awaiting Attach, an undecided group missing submission, registered image group typed-reference attach, CasesIndex manual group null, and CasesIndex exact-origin attach.

Worktree is frozen for an independent fresh verifier; no runtime, browser, script, HTML, push, or PR activity was performed.

Correction: the full implementation commit is `500b86a9b21adbd7a8fe56a65ddd52782630ce21`; the earlier full SHA in this scratch entry was transcribed incorrectly. This correction supersedes that identifier.

- 2026-09-08T20:11:34.248Z lease-phase implementing → running-command (lease eef8e3c2-cb07-4588-a7f9-5cb09a3433fc rev 27; expires 2026-09-08T21:11:34.237Z)

## Sole host re-grant — INTK-066 lease-key and open-image decision correction

Root reread canonical IDLE e053f70fdefb75d6, fresh ready packet, and actual clean HEAD 500b86a9b21adbd7a8fe56a65ddd52782630ce21 in .worktrees/INTK-066 / INTK-066-manual-upload-confirmation. Root read all six changed files, including shared CanOffer guard and actual editable-field browser assertions. Previous cca2 build/Core passes and eight SQL failures are retained with dispositions. Author idle. Sole CEALEX-May25 owner /root/agent_config_verifier ACTIVE; lease running-command rev27 for60min.

Run original dotnet build Pegasus.slnx, Core --no-build, and SAME amended focused nonbrowser Integration filter (UploadConfirmationWebTests|UploadOutcomeQueriesTests|CaseCreateWebTests|GroupedIntakeWebTests|ImageIntake|CasesIndexWebTests|MailWorkspaceWebTests|TriageQueuesWebTests.AwaitingAttach, all FullyQualifiedName~ predicates grouped then &Category!=Browser). After all PASS, browser --no-build filter Category=Browser&(FullyQualifiedName~UploadCaseSearchBrowserTests|FullyQualifiedName~QdosAllocationRecoveryBrowserTests), xUnit.MaxParallelThreads=1, using documented installed Chromium/setup. New theories prove actual confirmation and editable Create at1580/1100/760; keep all unrelated existing tests in these classes. First genuine failure stops remainder and returns IDLE; no autonomous retry or source/assertion/filter fixes.

Fresh exact-head/process census required. After original build parent exits, record/revalidate PID/start/parent/expected command of only this invocation's reusable MSBuild nodes then native exact-ID cleanup permitted, as prior grants. No foreign/name-based termination. Local disposable SQL only. No snapshot generation, source edits, push/PR, live/cloud/Outlook/Box. Remaining full Release rails and four-scope capture/verification are separate later obligations, not waived. Record exact commands/exits and explicit canonical IDLE on finish.

## Exact-head verification PASS — 500b86 correction

Verifier `/root/agent_config_verifier` ran the freshly granted queue in `.worktrees/INTK-066` on branch `INTK-066-manual-upload-confirmation`. Initial census proved exact HEAD `500b86a9b21adbd7a8fe56a65ddd52782630ce21`, a clean worktree and no `dotnet`, MSBuild, testhost or vstest processes.

1. `dotnet build Pegasus.slnx` — exit 0. Build succeeded in 00:01:47.75 with 0 warnings and 0 errors.
2. After the build parent PID 30660 had exited, six exact child reusable nodes were revalidated as `dotnet.exe ... MSBuild.dll /nodemode:1 /nodeReuse:true` with parent 30660 and stopped by exact PID only: 6844, 9788, 1820, 17244, 26452, 31924. Follow-up census found none of those PIDs remaining.
3. `dotnet test tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --no-build` — exit 0: 1,955 passed, 14 skipped, 0 failed (1,969 total).
4. `dotnet test tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --no-build --filter "(FullyQualifiedName~UploadConfirmationWebTests|FullyQualifiedName~UploadOutcomeQueriesTests|FullyQualifiedName~CaseCreateWebTests|FullyQualifiedName~GroupedIntakeWebTests|FullyQualifiedName~ImageIntake|FullyQualifiedName~CasesIndexWebTests|FullyQualifiedName~MailWorkspaceWebTests|FullyQualifiedName~TriageQueuesWebTests.AwaitingAttach)&Category!=Browser"` — exit 0: 127 passed, 0 skipped, 0 failed in 4m45s. This closes all eight retained failures from `cca2c76c...`.
5. `dotnet test tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --no-build --filter "Category=Browser&(FullyQualifiedName~UploadCaseSearchBrowserTests|FullyQualifiedName~QdosAllocationRecoveryBrowserTests)" -- xUnit.MaxParallelThreads=1` — exit 0: 10 passed, 0 skipped, 0 failed in 1m48s. This includes the actual confirmation and editable Create checks at 1580/1100/760.

Final postcheck remained at exact clean HEAD `500b86a9b21adbd7a8fe56a65ddd52782630ce21`; no `dotnet`, MSBuild, testhost or vstest process remained. No retry, source/snapshot edit, live action, push or PR occurred. Remaining full Release rails and four-scope snapshot work remain separate obligations.

- 2026-09-08T20:24:14.922Z lease-phase running-command → implementing (lease eef8e3c2-cb07-4588-a7f9-5cb09a3433fc rev 30; expires 2026-09-08T20:54:14.915Z)

- 2026-09-08T20:25:32.365Z lease-phase implementing → running-command (lease eef8e3c2-cb07-4588-a7f9-5cb09a3433fc rev 31; expires 2026-09-08T22:25:32.356Z)

## Sole host re-grant — INTK-066 full Release regression complement

Root reread canonical IDLE 02f95cb2f70d217a and exact-head initial PASS51facfb252070af6; ready packet unchanged, source remains frozen at500b86a9b21adbd7a8fe56a65ddd52782630ce21 in .worktrees/INTK-066 / INTK-066-manual-upload-confirmation. Author idle. CEALEX-May25 sole owner /root/agent_config_verifier ACTIVE; fresh running-command lease120min. The plan requires fullsolution evidence because this change touches shared cross-channel allocation/association; this is not a prose-only full build.

Using PowerShell7/Windows and local disposable LocalDB (no external SQL override), run sequential existing docs/runbook.md complement rails:
1. dotnet restore ./Pegasus.slnx --locked-mode
2. dotnet build ./Pegasus.slnx --configuration Release --no-restore
3. dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build
4. dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build
5. dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "Category!=Corpus&Category!=Browser"
6. pwsh ./tests/Pegasus.IntegrationTests/bin/Release/net10.0/playwright.ps1 install chromium
7. dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "Category=Browser&Category!=Corpus" -- xUnit.MaxParallelThreads=2

Require fresh exact-head, clean-tree and other-context/host-process preflight. Record/revalidate only this exact restore/build invocation's reusable MSBuild PID/start/parent/expected nodemode command, then stop those exact owned nodes AFTER their parent exits; no foreign or broad name-based termination. Stop remaining commands at first genuine failure, retain it, report/no autonomous retry or fix. Browser installation is pinned local test runtime only, not an application dependency change.

No source/snapshot edits, capture, packaging, cloud/live/Outlook/Box action, push/PR. Four-scope generated snapshots and documentation checks follow under a separate grant. Record every command/exit/count (skips distinct), postcheck exact frozen clean input and empty owned-host activity, then explicit canonical IDLE. Root retains earlier failures; new passes do not erase them.

- 2026-09-08T20:48:22.306Z lease-phase running-command → implementing (lease eef8e3c2-cb07-4588-a7f9-5cb09a3433fc rev 35; expires 2026-09-08T21:18:22.287Z)
