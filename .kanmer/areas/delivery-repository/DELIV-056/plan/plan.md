# Plan — DELIV-056: Correct six formal-evidence fixture regressions

## Objective

Correct the six D56-caused, in-scope failing methods without changing product policy, without undoing the prior conversion to real accepted documents, and without absorbing the unchanged UploadConfirmation/browser manual-attach contract defect.

## Starting state

The frozen worktree is .worktrees/deliv-056 on branch DELIV-056-align-intake-regression-fixtures-with-definitive-instruction-evidence. The sole-host broad regression command recorded in scratch/notes.md selected 269 cases (261 passed, 7 failed, 1 skipped; exit 1); it remains failure inventory evidence and is not narrow proof. The parent-reviewed root-cause packet is scratch/investigation.md@bf0ac77e9156b498. Evidence: research/research.md@6946d05dd2e760a3; files/files.md@5d3eccccfa0eaf23; prior plan/plan.md@6015cf1970066490; prior checklist/checklist.md@5ae85a2a1bc45166.

Existing D56 real-document fixture edits remain in the worktree. This correction pass adds no changes outside the two paths below and does not alter those prior transport fixtures.

## Governing docs

- docs/frd/frd-02-intake-and-source-identity.md — **Meets.** Definitive authorised intake allocates one case idempotently; an exact existing-case match does not allocate a duplicate. Ambiguous/unidentified material stays on its truthful destination rather than being disguised as a positive instruction. The formal Audit shape must be evidence-backed before allocation.
- docs/engineering.md — **Meets.** The implementation does not run checks in this planning phase. After implementation, one designated host runs the exact focused methods; the retained broad command remains a failure record rather than being represented as focused evidence.

## Required changes

Four root-cause classes cover the six D56 failures:

1. Audit two-document evidence: CustodyOutboxIntegrationTests.AnAuditCaseCompletesCustody and AnAutomaticAuditReachesReviewWithOneIdentityAndItsDocuments each need both the formal Audit notification and a distinct, real original report carrying exactly one valid assessment. Preserve their current custody/review assertions.
2. Honest pre-case source: CustodyOutboxIntegrationTests.ReevaluationReadsTheRetainedLogicalSourceAfterStagingWasDeleted and ReevaluationRejectsRetainedSourceIdentityDriftBeforeReplacingTheReceipt need a dedicated real PDF whose text truthfully identifies it as a QDOS source awaiting work-type classification. It must have no generated Engineer/Audit notification title. It is not a fake EmailBody route token or a document crafted to conceal a formal instruction; its purpose is the existing pre-case reevaluation contract.
3. Attachment-aware custody test double: CancellationSqlFaultAndLeaseLossUseExactTaxonomyAndRequireStaffRecovery needs CountingCustody to count/delegate RetainAcceptedIntakeAttachmentAsync, allowing the current formal attachment to complete remote custody effects and the test’s injected SQL completion fault to be observed.
4. Duplicate-source semantics: InstructionDraftWebTests.IdenticalBytesWithDifferentTokensPersistDistinctSourceIdentitiesWithMatchingHashes must prove two independent receipt identities share one case through UniqueMatch/current-case association, with one allocation event and two receipt-recorded events. It must not replace the observed count with a weaker literal-only assertion.

Reuse IntakeTestEvidence.CreateDefinitiveQdosInstructionDocument directly for the neutral original-report and clearly labelled pre-case PDFs when it can express their actual content without an accepted work-type title. Do not edit IntakeWebTestSupport.cs merely for convenience. If implementation establishes that it cannot create a real, plainly labelled neutral document without accidental category text, stop and obtain parent approval before a minimal extension to that existing helper.

## Expected files

| Action | Repo-root-relative path | Responsibility |
|---|---|---|
| Modify | tests/Pegasus.IntegrationTests/CustodyOutboxIntegrationTests.cs | Correct the two Audit fixture pairs, isolate the two honest pre-case reevaluation sources, and make CountingCustody delegate/count attachment retention. |
| Modify | tests/Pegasus.IntegrationTests/InstructionDraftWebTests.cs | Replace stale event total with explicit distinct-receipt / shared-case / UniqueMatch / exact-event-type proof. |
| Inspect only; modify only after parent approval if the current helper cannot express truthful neutral PDFs | tests/Pegasus.IntegrationTests/IntakeWebTestSupport.cs | Reuse the existing real PDF construction; do not introduce a fixture framework or defaults. |

## Do not modify

- tests/Pegasus.IntegrationTests/UploadConfirmationWebTests.cs and tests/Pegasus.IntegrationTests/Browser/UploadCaseSearchBrowserTests.cs. Their body-only manual-attach assumptions now resolve to Unidentified rather than an open attach card; they are baseline current-contract work, not a D56 green-up.
- tests/Pegasus.IntegrationTests/TestUiFocusedRenderTests.cs. Its HeldLease scenario is satisfied by D56’s already-modified SendToAi shared seed once D56 integrates.
- All other existing D56-modified test paths: ImageIntakeWebTests.cs, ImageViewingWebTests.cs, MailboxIntakeIntegrationTests.cs, MailWorkspaceWebTests.cs, MultiFormatIntakeWebTests.cs, QdosTriageIntegrationTests.cs, RecoveryTests.cs, and SendToAiIntegrationTests.cs.
- src/**, docs/**, infra/**, scripts/**, package/dependency files, migrations, corpus/**, production policy, routing, allocation and UI behavior.

## Constraints

- Reuse the exact recorded D56 branch/worktree; do not retake the ticket or create a worktree/branch.
- Actual documents remain actual PDFs through the existing MIME/receipt route. No body-keyword fallback, compatibility path, product-policy adjustment or source-parser workaround.
- A standalone Audit requires two distinct document attachments and exactly one qualifying unnegated Repairable or Total Loss report outcome; do not combine the generated instruction and original report.
- The pre-case documents must state their true pre-classification role in visible text, not imitate a formal document while exploiting classifier gaps.
- Preserve the existing custody, source-retention, idempotency, automatic allocation, role, replay, review and destination assertions.
- The broad 13-class command is known overbroad. It stays in scratch as original failure evidence; it must not be called an exact six-method regression command.
- No implementation, restore, build, test, browser, commit, push, PR, merge, release or deployment occurs until root reads and approves this revised plan/checklist.

## Ordered steps

### Step 1 — Build the two real Audit fixture pairs

- Preconditions: Root approval of this revised packet; current CustodyOutbox source remains unchanged from the frozen D56 worktree.
- Files: tests/Pegasus.IntegrationTests/CustodyOutboxIntegrationTests.cs; tests/Pegasus.IntegrationTests/IntakeWebTestSupport.cs
- Symbols: AnAuditCaseCompletesCustody; AnAutomaticAuditReachesReviewWithOneIdentityAndItsDocuments; IntakeTestEvidence.CreateDefinitiveQdosInstructionDocument.
- Change: In each Audit scenario retain the formal Audit notification attachment and add a distinct actual original-report PDF with exactly one unnegated assessment. Use the existing PDF helper if it can create plainly named neutral report content; otherwise stop for approval before any helper edit.
- Preserved behaviour: Each receipt reaches CaseCreated and its existing custody/review assertions remain intact.
- Forbidden: One-document Audit evidence, an Engineer notification in the report, a synthetic non-PDF report, product policy changes, or assertion removal.
- Negative cases: A missing/ambiguous/negated report outcome must still fail closed under the current Core policy.
- Tests: CustodyOutboxIntegrationTests.AnAuditCaseCompletesCustody; CustodyOutboxIntegrationTests.AnAutomaticAuditReachesReviewWithOneIdentityAndItsDocuments.
- Commands: Deferred to designated verifier: exact six-method command in Commands.
- Expected output: Both selected Audit methods pass; their existing custody consequences remain asserted.
- Done when: Each method has two semantically distinct retained document attachments and passes all original claims.
- Deviation stop: Stop if the current helper cannot make an honest neutral report, if any added report creates a second classification candidate, or if a product path would need change.

### Step 2 — Isolate truthful pre-case reevaluation sources

- Preconditions: Step 1 complete or independently reviewable; no change to CreateSource() has been made.
- Files: tests/Pegasus.IntegrationTests/CustodyOutboxIntegrationTests.cs; tests/Pegasus.IntegrationTests/IntakeWebTestSupport.cs
- Symbols: ReevaluationReadsTheRetainedLogicalSourceAfterStagingWasDeleted; ReevaluationRejectsRetainedSourceIdentityDriftBeforeReplacingTheReceipt; CreateSource; new narrowly named pre-case source helper if needed; IntakeTestEvidence.CreateDefinitiveQdosInstructionDocument.
- Change: Add/use a separate source creator only for the two reevaluation tests. Its actual PDF represents source material awaiting work-type classification and has no Engineer/Audit notification. Leave the shared definitive CreateSource() and every automatic-acceptance caller unchanged.
- Preserved behaviour: The tests still retain/re-read the exact logical source and still exercise drift rejection before receipt replacement.
- Forbidden: Restoring body-token classification, changing automatic custody sources to pre-case, hiding a formal notification title, or changing reevaluation production behavior.
- Negative cases: An already accepted receipt must remain rejected by the pre-case reevaluation API.
- Tests: CustodyOutboxIntegrationTests.ReevaluationReadsTheRetainedLogicalSourceAfterStagingWasDeleted; CustodyOutboxIntegrationTests.ReevaluationRejectsRetainedSourceIdentityDriftBeforeReplacingTheReceipt.
- Commands: Deferred to designated verifier: exact six-method command in Commands.
- Expected output: Both selected methods pass while their receipt has no pre-existing Case association before reevaluation.
- Done when: The new source’s content is honest, actual and pre-case, and no automatic custody caller changes.
- Deviation stop: Stop if achieving NeedsSorting requires policy/source-parser changes or a deceptive fixture.

### Step 3 — Complete attachment handling in the existing custody test double

- Preconditions: Formal source attachment remains part of the test’s accepted case.
- Files: tests/Pegasus.IntegrationTests/CustodyOutboxIntegrationTests.cs
- Symbols: CancellationSqlFaultAndLeaseLossUseExactTaxonomyAndRequireStaffRecovery; CountingCustody; ICaseCustody.RetainAcceptedIntakeAttachmentAsync.
- Change: Add the same effect-counting/pass-through behavior for attachment retention that CountingCustody already supplies for source/root/audit operations.
- Preserved behaviour: The injected completion write still throws DbUpdateException after custody effects, recovery/retry semantics and exact taxonomy remain asserted.
- Forbidden: Suppressing attachment retention, changing the fake to swallow errors, changing the SQL interceptor, or using a body-only source to skip the operation.
- Negative cases: An adapter that does not implement attachment custody must remain fail-closed outside this test double.
- Tests: CustodyOutboxIntegrationTests.CancellationSqlFaultAndLeaseLossUseExactTaxonomyAndRequireStaffRecovery.
- Commands: Deferred to designated verifier: exact six-method command in Commands.
- Expected output: The observed failure point returns to the asserted injected DbUpdateException and later recovery assertions pass.
- Done when: CountingCustody reports attachment effects and delegates them to the inner real custody adapter.
- Deviation stop: Stop if attachment retention changes the expected fault ordering or exposes a product defect rather than the intended injected transaction failure.

### Step 4 — Prove one allocation across two identities

- Preconditions: Existing formal InstructionDraft transport fixture and identity/hash assertions remain intact.
- Files: tests/Pegasus.IntegrationTests/InstructionDraftWebTests.cs
- Symbols: IdenticalBytesWithDifferentTokensPersistDistinctSourceIdentitiesWithMatchingHashes; IntakeReceiptEvents; CaseMatchDecision; IntakeAllocationState.
- Change: Retain evidence of separate receipt IDs/tokens and matching bytes/assets. Add assertions that the two receipts associate to one Case, the second records UniqueMatch/current Case, exactly one allocation-succeeded event exists, and exactly two receipt-recorded events exist.
- Preserved behaviour: The first receives automatic allocation; the second never duplicates a Case; visible source identities remain distinct.
- Forbidden: Changing source bytes/tokens to avoid the unique match, removing identity/hash checks, or replacing four with three without semantic proof.
- Negative cases: A second allocation event or a second Case is a failure.
- Tests: InstructionDraftWebTests.IdenticalBytesWithDifferentTokensPersistDistinctSourceIdentitiesWithMatchingHashes.
- Commands: Deferred to designated verifier: exact six-method command in Commands.
- Expected output: The selected method passes only by proving the 2 receipt + 1 case + 1 allocation model.
- Done when: The test’s event expectations name event type and receipt/case relationships, not only a total.
- Deviation stop: Stop if the existing persistence/query surface cannot observe event type or association without changing production code.

### Step 5 — Static scope review and designated focused verification handoff

- Preconditions: Steps 1–4 complete; no deferred UploadConfirmation/browser source change has been made.
- Files: tests/Pegasus.IntegrationTests/CustodyOutboxIntegrationTests.cs; tests/Pegasus.IntegrationTests/InstructionDraftWebTests.cs; tests/Pegasus.IntegrationTests/IntakeWebTestSupport.cs
- Symbols: Not applicable; file-only reconciliation.
- Change: Run static diff/scope review, record exact changed paths and hand the named test command to the one host verifier. Record any failure exactly; do not broaden or retry the legacy 13-class filter.
- Preserved behaviour: Unchanged UploadConfirmation/browser failure stays recorded as a baseline current-contract issue; HeldLease is separately demonstrated after integration through its existing SendToAi seed.
- Forbidden: Build/test/browser execution by this implementation worker, treating 269 selected tests as narrow proof, or altering deferred UI tests to green the run.
- Negative cases: Any path outside the two correction files (or approved helper) is a scope failure.
- Tests: the six exact failed methods above; their direct changed helper consumers are those same Audit/reevaluation methods, the CountingCustody cancellation method, and the InstructionDraft method. No extra consumer is silently added.
- Commands: git diff --check; git diff --name-only; then host-owned test command below.
- Expected output: Static checks pass; designated verifier reports six exact outcomes independently of the retained broad run.
- Done when: Root has the implementation report and designated verifier handoff, not a local test result.
- Deviation stop: Stop on a new file, helper expansion, non-zero static result, a conflicting worktree change, or unavailable host verification authority.

## Acceptance checks

- The six named failures are corrected through real fixture/test behavior, not Core/Web policy changes.
- Both Audit scenarios use distinct attachment identities and one valid original-report assessment; their existing custody/review claims remain.
- Both reevaluation scenarios begin genuinely pre-case and retain the existing logical-source/drift claims.
- CountingCustody delegates real attachment custody and the SQL-fault method retains its exact exception/recovery proof.
- The InstructionDraft method demonstrates two receipt identities, one Case, a UniqueMatch association, one allocation-succeeded event, and two receipt-recorded events.
- UploadConfirmation Attach and browser case search remain visible as failures/deferred current-contract work; no pass is claimed for them.
- HeldLease requires no direct test change: after integration it exercises the existing D56 formal SendToAi seed.
- No dependency, application, schema, documentation, corpus, route or policy change is introduced.

## Commands

Implementation worker, only after approval:
- git diff --check
- git diff --name-only

Designated host verifier, after its required sequential build and with the frozen D56 head:
- dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "(FullyQualifiedName=Pegasus.IntegrationTests.CustodyOutboxIntegrationTests.AnAuditCaseCompletesCustody|FullyQualifiedName=Pegasus.IntegrationTests.CustodyOutboxIntegrationTests.AnAutomaticAuditReachesReviewWithOneIdentityAndItsDocuments|FullyQualifiedName=Pegasus.IntegrationTests.CustodyOutboxIntegrationTests.ReevaluationReadsTheRetainedLogicalSourceAfterStagingWasDeleted|FullyQualifiedName=Pegasus.IntegrationTests.CustodyOutboxIntegrationTests.ReevaluationRejectsRetainedSourceIdentityDriftBeforeReplacingTheReceipt|FullyQualifiedName=Pegasus.IntegrationTests.CustodyOutboxIntegrationTests.CancellationSqlFaultAndLeaseLossUseExactTaxonomyAndRequireStaffRecovery|FullyQualifiedName=Pegasus.IntegrationTests.InstructionDraftWebTests.IdenticalBytesWithDifferentTokensPersistDistinctSourceIdentitiesWithMatchingHashes)" --logger "trx;LogFileName=deliv-056-corrections-host.trx" --results-directory artifacts/verification

The command is intentionally exact-method equality, not a class-substring filter. UploadConfirmation Attach/browser and the PR706 HeldLease integration check are separate commands after their respective approval/integration conditions.

## Failure and deviation rules

Stop and report instead of improvising if the live worktree differs unexpectedly, a neutral PDF requires a production policy change, an audit report creates another category, any test requires a body-token fallback, an assertion cannot observe the claimed durable behavior, helper modification becomes necessary, a path outside Expected files changes, or a designated host result fails. Do not weaken an assertion, mutate the UploadConfirmation/browser baseline contract, rerun the historical broad filter as a substitute, merge, or start another ticket.

## Stop condition

Stop after root reads and approves this entire revised plan/checklist. Until that approval, do not edit source or run tests/builds. After approval, execute only these bounded steps in the already recorded D56 worktree, then hand off static evidence and the exact six-method verifier command. Do not merge, release, deploy, or absorb the deferred UploadConfirmation/browser work.

## Corrective verification addendum — existing automatic Audit evidence

### Addendum step 1 — Replace obsolete test seeding in the two Audit custody methods

**Retained failure evidence.** `scratch/verify.md@401565b62deb4e8a` records the designated-host exact-six result: 4 PASS / 2 FAIL. Both failures reach `AllocationTestData.SeedAutomaticAuditEvidenceAsync` in `CustodyOutboxIntegrationTests.AnAuditCaseCompletesCustody` and `CustodyOutboxIntegrationTests.AnAutomaticAuditReachesReviewWithOneIdentityAndItsDocuments`, where its synthetic insert violates the one-row-per-receipt `StandaloneAuditEvidence` constraint. The two real Audit fixture corrections are otherwise retained and are not reopened.

**One bounded change after root approval.** In those two methods only, replace the obsolete `AllocationTestData.SeedAutomaticAuditEvidenceAsync(services, receipt.Id)` call with the production-recorded evidence query: resolve `IStandaloneAuditEvidenceQueries` from `services`, call `GetForReceiptAsync(receipt.Id, CancellationToken.None)`, assert a non-null typed evidence result, and pass its existing `Id` to the current `AcceptAsync` call. No upsert/seed helper change is permitted.

**File.** `tests/Pegasus.IntegrationTests/CustodyOutboxIntegrationTests.cs` only.

**Preserve.** Both methods retain their existing `ProcessIntake`, `AcceptAsync`, custody, review, identity, and document assertions. The already-passing reevaluation, attachment-custody and duplicate-source corrections stay unchanged.

**Do not modify.** `AllocationTestData.SeedAutomaticAuditEvidenceAsync`; any production Core/Web/Infrastructure policy, guard, allocation route or schema; `InstructionDraftWebTests.cs`; `IntakeWebTestSupport.cs`; all other D56 files. Do not suppress the unique constraint or manually create a second evidence row.

**Acceptance.** Each Audit test consumes the actual evidence created by `ProcessIntake`, so it reaches its unchanged acceptance/custody assertions without a duplicate `StandaloneAuditEvidence` insert. The designated host reruns only the same exact six-method command after this source change; no implementation-worker test/build, commit, push, PR, merge, release or deployment occurs.

**Stop condition.** Stop after this plan addendum is recorded and root has reviewed its version. Do not edit source until root explicitly authorizes exactly the two call replacements.

## SendToClaude display-correction addendum

**Observed current contract.** In `src/Pegasus.Web/Pages/Cases/Shared/_CaseEstimate.cshtml`, the only interactive Send to Claude launcher is the anchor carrying `data-dialog-open="send-to-claude-dialog"`; its mutation form exists only in the separately rendered `data-dialog="send-to-claude-dialog"` dialog. When `AssessmentIsReadOnly` is true, the partial does not render the button row at all. `DetailsModel` fails an absent access answer closed to `AssessmentIsReadOnly = true`, and the existing `InaccessibleCaseCannotPostSendToClaude` proof already retains both dialog absence and a `404` for the POST.

**One bounded change after root approval.** In `tests/Pegasus.IntegrationTests/SendToAiIntegrationTests.cs`, method `InaccessibleCaseCannotPostSendToClaude` only, replace the deleted stale positive gated-control regex with `Assert.DoesNotContain("data-dialog-open=\"send-to-claude-dialog\"", html, StringComparison.Ordinal)`. This is a specific negative assertion over the interactive launcher—not an absence assertion over the visible Send to Claude label, which can legitimately occur on a disabled control in other access states.

**Preserve.** Keep the current dialog absence assertion and POST `HttpStatusCode.NotFound` assertion unchanged. Do not change the test fixture, Core read-only/access decision, Razor markup, endpoint guard, AI job recording seam, helper, seed, or any other D56 file. The Audit evidence-query correction remains at full-diff hash `14351132c034f809e4c6496b92430b75a46053a4` and has not yet been rerun.

**Focused verifier handoff.** The next designated-host command is the existing exact six-method equality filter extended with exactly `Pegasus.IntegrationTests.SendToAiIntegrationTests.InaccessibleCaseCannotPostSendToClaude` (seven named methods total). The older 269-case result included the method after the stale regex deletion but is not proof of this stronger named assertion. The active INTK verifier owns the merge lane; this worker performs no build/test, commit, push, PR, merge, release or deployment.

**Stop condition.** Stop after root reviews this plan addendum and its companion checklist item. Do not edit the test until root explicitly authorizes this one negative assertion.

## Audit acceptance-version correction addendum

**Root cause.** `ProcessIntake.ExecuteCoreAsync` stores and returns the receipt, then calls `RecordAutomaticAuditEvidenceAsync` and returns the earlier receipt object. The automatic evidence store checks that receipt version, increments `IntakeReceipts.Version`, and persists that resulting version on the evidence row. Therefore the two Audit tests’ default `AcceptAsync(... expectedVersion: 0)` is stale after they correctly obtain the automatically recorded evidence. It is a deterministic optimistic-concurrency mismatch at `EfCaseAcceptanceStore.AcceptOnceAsync`’s `receipt.Version != request.ExpectedIntakeVersion` guard—not allocation replay, a generic race, or a reason to add a production retry.

**Authoritative existing token.** The already-approved `IStandaloneAuditEvidenceQueries.GetForReceiptAsync` result is the Core `StandaloneAuditEvidence` record. Its public `ReceiptVersion` field is mapped from the persistence row’s `ResultingReceiptVersion`; it is the version created by that evidence recording. Acceptance explicitly rejects evidence whose persisted resulting version is later than the requested expected version, so using `evidence.ReceiptVersion` preserves the guard and still fails closed if any later receipt mutation occurs. No separate receipt refresh is required or permitted.

**One bounded change after root approval.** In `tests/Pegasus.IntegrationTests/CustodyOutboxIntegrationTests.cs`, add only `expectedVersion: evidence.ReceiptVersion` to each existing `AcceptAsync` call in `AnAuditCaseCompletesCustody` and `AnAutomaticAuditReachesReviewWithOneIdentityAndItsDocuments`. Keep the evidence query/type assertion, existing case acceptance, custody/review/identity/document assertions, and all other D56 changes exactly as they are.

**Do not modify.** `AcceptAsync`, `ProcessIntake`, `EfCaseAcceptanceStore`, `EfStandaloneAuditEvidenceStore`, the automatic evidence query, the standalone evidence seed helper, allocation route, concurrency/retry policy, runtime code, helpers, schema, or any other test method/file. Do not seed/upsert another evidence row, refresh the receipt version, or suppress the conflict.

**Focused verifier handoff.** After the source change, the designated host performs its required incremental Release build and then runs exactly the two affected Audit methods: `CustodyOutboxIntegrationTests.AnAuditCaseCompletesCustody` and `CustodyOutboxIntegrationTests.AnAutomaticAuditReachesReviewWithOneIdentityAndItsDocuments`. The 5 stable PASS results from the prior seven-method run on full-diff hash `bfa477f890e1da5fb570c0bdecfd0825459a3f7c` remain recorded evidence because their sources are unchanged; both earlier failed TRXs remain preserved. This two-method delta run supersedes the proposed seven-method rerun only for the new expected-version additions.

**Stop condition.** Stop after root reviews this addendum and its companion checklist item. Do not edit source, build, or test until root explicitly authorizes exactly these two named arguments.
