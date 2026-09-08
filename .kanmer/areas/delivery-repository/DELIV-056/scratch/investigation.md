## Parent-requested correction investigation — no source edits (2026-09-08)

Scope: classify the seven failures from the sole-host focused run before any correction is approved. Evidence is the retained TRX artifacts/verification/deliv-056-focused-host-20260908.trx (exit 1; 269 total / 261 passed / 7 failed / 1 skipped), the frozen D56 source diff, and static path/policy inspection. This note neither changes source nor reruns verification.

### D56 failure inventory and bounded corrections

1. CustodyOutboxIntegrationTests.AnAuditCaseCompletesCustody — failure assertion at tests/Pegasus.IntegrationTests/CustodyOutboxIntegrationTests.cs:2389: expected CaseCreated, actual NeedsSorting.

2. CustodyOutboxIntegrationTests.AnAutomaticAuditReachesReviewWithOneIdentityAndItsDocuments — identical failure at CustodyOutboxIntegrationTests.cs:2460.

   Shared cause / correction: each scenario changed its old fake bodyshop report into one real PDF titled AUDIT REPORT NOTIFICATION (lines 2361–2374 and 2432–2445), but it is only the generated Audit instruction. PrincipalMailClassificationPolicy.EvaluateStandaloneAuditReport requires at least two distinct document attachments, exactly one carrying the Audit title, and exactly one other document containing precisely one unnegated Repairable or Total Loss assessment (src/Pegasus.Core/Intake/Classification/PrincipalMailClassificationPolicy.cs:316–354). ProcessIntake deliberately downgrades an Audit with no qualifying report to NeedsSorting (src/Pegasus.Core/Intake/ProcessIntake.cs:205–215).

   Preserve both custody scenarios by attaching: (a) the current formal Audit notification PDF and (b) a second, actual original-report PDF whose content says one valid assessment, with no Engineer or Audit notification title. Do not use the Engineer-notification helper for the report: it adds an Inspection classification candidate. The smallest map is CustodyOutboxIntegrationTests.cs plus a narrowly named real-PDF report helper in IntakeWebTestSupport.cs only if the existing builder cannot express a neutral report honestly. The later SeedAutomaticAuditEvidenceAsync does not repair the intake-time classification.

3. CustodyOutboxIntegrationTests.ReevaluationReadsTheRetainedLogicalSourceAfterStagingWasDeleted — failure begins at reevaluation call CustodyOutboxIntegrationTests.cs:69.

4. CustodyOutboxIntegrationTests.ReevaluationRejectsRetainedSourceIdentityDriftBeforeReplacingTheReceipt — failure begins at CustodyOutboxIntegrationTests.cs:122.

   Shared cause / correction: both invoke shared CreateSource() (lines 41 and 112). D56 converted that source to a formal classified attachment (CustodyOutboxIntegrationTests.cs:2722–2745), so queued processing automatically allocates/accepts it; the pre-case reevaluation API correctly refuses the later mutation with “An accepted intake receipt cannot be changed through the pre-case intake workflow.”

   Add a distinct, clearly labelled pre-case reevaluation source used only by these two tests. It must be an actual PDF with truthful QDOS identifying/field content but explicitly no generated work-type notification, representing material awaiting classification—not a hidden classifier-signature trick nor a restored body-token fake. It should settle unclassified/NeedsSorting, retain the logical source, and remain reevaluatable. Keep CreateSource() formal for custody/automatic-allocation tests. Map: CustodyOutboxIntegrationTests.cs and, only if needed for the clearly labelled genuine pre-case document, IntakeWebTestSupport.cs.

5. CustodyOutboxIntegrationTests.CancellationSqlFaultAndLeaseLossUseExactTaxonomyAndRequireStaffRecovery — assertion at CustodyOutboxIntegrationTests.cs:1074 expected the injected DbUpdateException; actual was NotSupportedException: This custody adapter does not retain instruction attachments.

   Cause / correction: the D56 formal source has a retained instruction attachment. The real processor retains attachments after the source (src/Pegasus.Infrastructure/Persistence/EfQueuedCustodyProcessor.cs:279–304). The test CountingCustody only delegates/counts root, source and audit operations (CustodyOutboxIntegrationTests.cs:2981–3017); ICaseCustody intentionally default-throws for adapters that omit attachment custody (src/Pegasus.Core/Custody/CustodyContracts.cs:126–150). Add a counting/delegating RetainAcceptedIntakeAttachmentAsync implementation to that test double. This preserves the actual accepted-document test and reaches the intended post-effect SQL fault; do not revert this one source to body-only content. Map: CustodyOutboxIntegrationTests.cs only.

6. InstructionDraftWebTests.IdenticalBytesWithDifferentTokensPersistDistinctSourceIdentitiesWithMatchingHashes — InstructionDraftWebTests.cs:119 expects four IntakeReceiptEvents, observed three.

   Cause / correction: this is a stale semantic assertion after formal automatic allocation, not an asset-count failure. Receipt persistence records one event per receipt (EfIntakeReceiptStore.cs:633–640). The first of the two byte-identical but token-distinct sources allocates and records one intake_allocation_succeeded event (EfIntakeAllocationStore.cs:154–157,241–244). The second sees the first Case as a unique match and automatic allocation returns null when CurrentCaseId is present or the match is unique (DurableIntake.cs:985–993, IntakeAllocation.cs:241–246): total 2 recorded receipt events + 1 allocation event.

   Keep the existing two-distinct-identities / same-hash / asset assertions and replace the bare count proof with: two distinct receipt IDs, both receipts point to the one Case, the second has UniqueMatch/the same current Case as the first, exactly one allocation-succeeded event, and two recorded-receipt events (total three). This proves the intended replay/association semantics rather than weakening to 3. Map: InstructionDraftWebTests.cs only.

7. UploadConfirmationWebTests.AttachAddsAnUnmatchedInstructionUploadToTheChosenCaseAndReplaysSafely — page assertion at UploadConfirmationWebTests.cs:99 did not find “No existing case matched this.”

   Root cause / disposition: the fixture is unchanged by D56: it remains a generic body-only QDOS EML (UploadConfirmationWebTests.cs:82–101; git diff origin/dev against this file is empty). Its lack of an accepted formal work-type classification is enough to settle it as NeedsSorting (ProcessIntake.cs:890–901), which is routed to Unidentified (ProcessIntake.cs:504–515,533–538). UploadOutcomeQueries checks an open Unidentified result before its ReadyToCreate branch (src/Pegasus.Web/Presentation/UploadOutcome.cs:254–268 before 309–324), so the asserted no-match/create-or-attach copy is absent. This is a genuine baseline behaviour/test-contract defect, not caused by D56 source changes.

   Do not make this a formal unmatched instruction: normal allocation would create a Case, eliminating the open attach decision. The test’s business purpose is the staff attach/replay flow, so its owner needs an approved current open-decision fixture (most plausibly the existing supported ambiguous-match route) and must update the asserted message to that truthful state while retaining the attach/replay assertions. That needs a separately agreed scope/change map; no D56 correction is proposed here.

### PR706/browser and HeldLease baseline

- tests/Pegasus.IntegrationTests/Browser/UploadCaseSearchBrowserTests.cs:19–94 uses the same generic body-only QDOS upload and then waits for details.upload-attach > summary at line 53. It therefore shares the UploadConfirmation baseline outcome: no open attach card is produced, so the browser timeout is expected. It is outside D56’s modified path map and must not be fixed by turning the upload into a definitive document (which auto-allocates). It needs the same approved, current manual-attach scenario decision as item 7.

- TestUiFocusedRenderTests.HeldLeaseConfirmationClearsOnlyTheCurrentLeaseThroughRazor calls SendToAiIntegrationTests.SeedAcceptedCaseAsync. On PR706 alone that helper still builds generic body-only QDOS input and its CaseCreated assertion fails. D56 already changes this shared helper to attach a formal QDOS document (tests/Pegasus.IntegrationTests/SendToAiIntegrationTests.cs:55–63); the held-lease test passed in the D56 focused run. No TestUiFocusedRenderTests.cs edit is required once D56 is integrated.

### Proposed correction map, approval boundary, and focused proof

Candidate D56 correction paths only: tests/Pegasus.IntegrationTests/CustodyOutboxIntegrationTests.cs, tests/Pegasus.IntegrationTests/InstructionDraftWebTests.cs, and only if necessary tests/Pegasus.IntegrationTests/IntakeWebTestSupport.cs for honest neutral-report/pre-case real-PDF construction. No Core/Infrastructure/Web product policy path changes.

Not approved / needs owner decision: UploadConfirmationWebTests.cs and Browser/UploadCaseSearchBrowserTests.cs require an explicit current-contract decision for the manual attach entry state; they should not be folded into the six fixture/test corrections merely to make the focused run green.

After root packet approval, run the narrow existing test methods for the six corrected failing scenarios plus their immediate helper consumers as a single host-owned focused selection. Separately run UploadConfirmation Attach and the browser case-search test only after its manual-attach contract is decided. Re-run HeldLease after D56 integration to demonstrate the formal shared seed resolves PR706 setup. Retain the original 13-class broad-filter failure as evidence; do not treat its 269-case result as narrow proof.
