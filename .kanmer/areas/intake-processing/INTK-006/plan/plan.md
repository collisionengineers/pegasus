# Plan — INTK-006

## Approach

Using the durable membership contract from the [[INTK-005]] PR branch, make the group—not an individual image—the routing unit. Wait until every image member has terminal recognition evidence, aggregate accepted VRMs, reuse one exact eligible-case matcher, and commit one idempotent outcome: associate all members to one existing Case or create one documented Image-initiated Case and attach all members.

## Governing docs

Modifies:
- `docs/operator-notes.md` (protected; operator confirmation is recorded in this ticket)
- `docs/prd/pegasus-product.md`
- `docs/frd/frd-01-case-identity-and-lifecycle.md`
- `docs/frd/frd-02-intake-and-source-identity.md`
- `docs/frd/frd-06-vehicle-and-engineering-evidence.md`
- `docs/frd/frd-12-operator-experience.md`
- `docs/design/README.md`
- `docs/capabilities.md` and `docs/index.md` where ownership/navigation changes

The INTK-005 branch is the implementation base and is not a merge prerequisite. Governing-document reconciliation remains an explicit parallel/review workstream: do not silently change protected or normative docs in this branch, and do not invent Case reference, principal, lifecycle, or resolution semantics. Reuse the existing Case owner and record any unresolved policy mismatch in the report for review/rebase resolution.

## Implementation steps

1. **Prepare the non-blocking dependency and policy seam.**
   - Confirm the INTK-005 PR branch `intk-005-grouped-upload` is available locally/remotely and use it as this ticket's worktree base. Do not wait for PR merge; review changes will be reconciled by rebasing this branch later.
   - Record the exact existing Case creation/acceptance owner and its supported inputs before editing. Do not call EF directly or create a second policy owner.
   - Treat governing-document reconciliation as parallel review work: link or update only documents authorized by the ticket scope, preserve protected operator truth, and surface any semantics that cannot yet be represented by the existing Case owner.
   - Do not stop implementation solely because INTK-005 has not merged; stop only if the existing owner cannot safely express the requested outcome without inventing product policy, and record that precise boundary in the report.

2. **Define one canonical group-routing policy in Core.**
   - Add an enum/record only if needed for these mutually exclusive states: WaitingForMembers, WaitingForRecognition, AssociateExistingCase, CreateImageInitiatedCase, TechnicalFailure.
   - Inputs are ordered group members, each terminal recognition result, normalized accepted suggestions, and eligible Case candidates.
   - Ignore below-bar text for automatic identity. Preserve it as evidence only.
   - Compute distinct accepted normalized VRMs across every member.
   - Select AssociateExistingCase only when the set contains exactly one VRM and the shared candidate owner returns exactly one eligible Case with no contradiction.
   - Select the existing-case association only for exactly one usable accepted VRM and exactly one eligible Instruction-initiated Case. Route zero/ambiguous/conflicting VRMs to INTK-007; route a usable VRM with no unique match to the existing ImageIntake owner, whose Image-initiated lifecycle is completed by INTK-008.
   - Never split a completed group or leave it in an unreasoned generic Needs sorting result; no-valid/conflicting VRM uses INTK-007 Unidentified, while the usable-VRM/no-match path uses the existing ImageIntake owner.

3. **Make recognition completion explicit.**
   - Change `OnnxVrmRecognitionEngine` result mapping so detector-empty and recognizer-empty are distinguishable safe results.
   - Preserve detector and recognizer model/version/hash evidence.
   - Treat no-plate, unreadable-crop, and below-bar suggestion as terminal evidence; treat dependency/model/IO faults through existing retry/terminal failure rules.
   - Add non-sensitive telemetry counters/spans for detector outcome and recognizer outcome. Include ids/model versions/counts only; exclude pixels/crops/raw registration text.
   - Add engine tests for detector-empty, recognizer-empty, suggested-below-bar, accepted suggestion, and technical failure.

4. **Orchestrate at group completion.**
   - Replace the per-receipt early-return path in `ImageIntakeAutomation.ApplyAsync` with lookup of the member's group through the INTK-005 Core port.
   - Load every group member and its image assets/recognition state in ordinal order.
   - If any member is unprocessed or retryable, persist/return Waiting without routing.
   - Once all members are terminal, evaluate the group policy exactly once.
   - Replays from any member must load the persisted group outcome and no-op/reconcile rather than reevaluate into a different destination.

5. **Persist an idempotent group outcome.**
   - Add one outcome row keyed uniquely by group id with decision, selected normalized VRM when applicable, target Case id, reason code, created time, operation/evaluation identity, and version.
   - Enforce one group→one target Case. Associate all image member origins within the same transaction as outcome finalization where the existing store boundary permits.
   - Use serializable transaction/replay conventions already used by case acceptance and Image Intake registration.
   - On concurrency conflict, reload the existing outcome and verify it matches; never create a second Case.
   - Preserve each receipt, filename, source identity, suggestion, Image Intake reference (if governing docs retain it), and association history.

6. **Implement the existing-Case branch.**
   - Reuse/refactor `ImageIntakeCasePairing` as the single eligible-candidate selector.
   - Require exact documented normalized registration and exactly one eligible pre-report candidate.
   - Register/persist required Image Intake evidence for every group member through the existing lifecycle/store.
   - Associate every member with the selected Case and record group-level reason/history.
   - Test that a readable overview plus an unreadable damage close-up both appear on the same Case.

7. **Implement the Image-initiated Case branch through the sole Case owner.**
   - Extend the exact Case acceptance/allocation use case named after governing-doc reconciliation; do not call EF directly.
   - Pass the persisted group id and all immutable origins so replay identity is the group, not one arbitrary member.
   - Populate only fields authorized by the updated FRD. Do not invent principal, registration, instruction, claimant, or Case type defaults.
   - Create exactly one Case, then attach/register every member in ordinal order and persist the one group outcome.
   - Use the documented reference/lifecycle semantics exactly. If the docs require a nonstandard reference, add it through the existing sequence owner rather than reusing Image Intake/U/Audit sequences.
   - Test no-match and multi-match groups with one usable VRM create one—not N—Image-initiated Case; zero-VRM, low-confidence, or conflicting-VRM groups follow the INTK-007 Unidentified contract.

8. **Expose honest status and history.**
   - Extend the INTK-005 group status view to show Waiting for all images, Associated with Case <reference>, Created Image-initiated Case <reference>, or named technical failure.
   - Link to the existing Case details route and show every original filename/origin.
   - Add one presentation mapping in `OperatorLabels.cs`; do not emit raw enum/snake_case values.
   - Ensure receipt detail and history identify the shared group outcome without implying a low-confidence VRM was accepted.

9. **Run the complete routing matrix.**
   - One image, accepted VRM, one eligible Case → associate.
   - Overview accepted VRM + close-up no plate, one eligible Case → associate both.
   - Accepted VRM, zero eligible Cases → one Image-initiated Case.
   - Accepted VRM, two eligible Cases → one Image-initiated Case, no existing association.
   - Two distinct accepted VRMs → one INTK-007 Unidentified group, no fabricated VRM reference.
   - All no-plate/unreadable/below-bar → one INTK-007 Unidentified group, no fabricated VRM reference.
   - One member retryable/processing → no final outcome yet.
   - Technical terminal failure → documented failure/Unidentified path after INTK-007 semantics, never silent Case creation.
   - Replay, reverse completion order, and two concurrent finalizers → same one outcome/Case.
   - Duplicate filenames and byte-identical distinct occurrences remain distinct origins.

10. **Verify and simplify.**
   - Run `dotnet restore`, Release build, focused recognition/Core/persistence/web/browser tests, and full `dotnet test`.
   - Run migration tests from an existing database and a clean database.
   - Perform the four required simplification lenses. Reject duplicate matching rules, duplicate reason taxonomies, direct Case writes, and group polling abstractions without a caller.
   - Record dated simplification findings/dispositions in this plan and update checklist/report.

## Verification

- Durable evidence proves both recognition layers' distinguishable outcomes without sensitive logs.
- Every completed vehicle-image group reaches exactly one existing-Case association, existing ImageIntake hand-off, or INTK-007 Unidentified outcome.
- No group is split, no formal fallback Case is fabricated here, no VRM reference is fabricated for conflicting/no-readable evidence, and low-confidence/conflicting text never attaches to an existing Case.
- Original filenames, receipts, identities, order, suggestions, and history remain attributable.
- The implementation uses INTK-005 group identity and the existing Case/Image Intake owners.
- Governing docs and code agree on reference/principal/lifecycle semantics.
- Release build, migrations, and all tests pass.

## Risks and controls

- **Current docs may require reconciliation for the requested Case fallback:** keep the implementation on the existing Case owner, preserve the non-blocking branch dependency, and surface any remaining policy decision explicitly for review/rebase rather than silently inventing semantics.
- **Concurrent last-member completion:** unique group outcome plus transactional replay.
- **Premature finalization:** explicit per-member terminal recognition state.
- **Wrong association:** one shared exact matcher; conflicts create fallback rather than attach.
- **Sensitive telemetry:** outcome/count/version only, never image or raw candidate text.


## Parallel-branch execution note — 2026-08-19

This ticket is intentionally implemented from the INTK-005 PR branch before PR merge. Record the exact base SHA in execution scratch and ticket notes. When INTK-005 is reviewed, rebase this branch onto the reviewed INTK-005 result and resolve any conflicts before its PR is finalized. INTK-005 review/merge coordination is not an execution blocker.

## Execution boundary and simplification pass — 2026-08-19

- Implemented from INTK-005 PR branch SHA `ed04f498`; the dependency is intentionally branch-based and not a merge blocker. Rebase onto the reviewed INTK-005 result before final merge.
- Reused the existing `IIntakeReceiptQueries`, `IIntakeSubmissionGroupStore`, `IImageIntakeCaseCandidates`, `TryRegisterAndAssociateAsync`, and image-intake suggestion store. No duplicate candidate matcher, generic workflow abstraction, or direct EF Case mutation was introduced.
- Group routing now aggregates all members and safely associates every member only for one accepted VRM and one eligible existing Case. Detector-empty and recognizer-empty outcomes are recorded with distinct failure codes.
- The Image-initiated Case branch is not fabricated: the existing Case acceptance owner requires a real principal and immutable Case identity. INTK-007/updated governing documents must supply that authorized contract; until then the group remains available for the documented Unidentified path. This is a review/rebase policy seam, not an INTK-005 branch dependency blocker.
- Release build passed with 0 warnings/errors. Focused Core tests (19) and VRM integration tests (5) passed.

## Clarified product model — 2026-08-19

The operator has clarified that Pegasus has two Case-origin types:

- **Instruction-initiated Case (main/formal):** starts from an accepted official instruction document such as PDF or Word. It uses the existing Principal, Case type, and Case/PO allocation rules. Images may be absent at initial creation; missing images make the Case incomplete/Not ready, not unidentified.
- **Image-initiated Case (secondary/pre-instruction):** starts from retained vehicle images before formal instructions arrive. It has no Case/PO because Principal/formal instruction may be unknown. It uses an immutable VRM reference with a per-VRM sequence: `AB12ABC-01`, then `AB12ABC-02`, `AB12ABC-03`, with no reuse. It is a separate source-origin record until a later Instruction-initiated Case is matched.

VRM matching is the bridge: when exactly one eligible Instruction-initiated Case matches the group VRM without overlap or contradictory identity, all group images associate to that Case while both origins and history remain attributable. Otherwise the Image-initiated Case remains the single destination for the complete group.

This ticket now includes the governing-document amendment needed to remove the current pre-Case-only conflict. Required reconciliation targets: `docs/operator-notes.md`, `docs/prd/pegasus-product.md`, `docs/frd/frd-01-case-identity-and-lifecycle.md`, `docs/frd/frd-02-intake-and-source-identity.md`, `docs/frd/frd-06-vehicle-and-engineering-evidence.md`, `docs/frd/frd-12-operator-experience.md`, `docs/design/README.md`, `docs/capabilities.md`, `CONTEXT.md`, and any ADR/index wording that must be superseded or relocated. The final docs must define the Image-initiated reference sequence, lifecycle, conversion/association rules, origin/history retention, and its exclusion from Case/PO, Audit, and Unidentified references.

## Files mapping is a required amendment input — 2026-08-19

Before changing code or governing documentation, read INTK-006 files.md in full. It is the authoritative inventory for this ticket and records the exact repository conflicts, existing ImageIntake reuse points, and the paths that must be amended.

1. Amend files.md first whenever research discovers a new source file, migration, route, query, custody boundary, or governing-document conflict.
2. Use files.md to drive the documentation reconciliation: operator notes, PRD, FRD-01/02/06/12, design, capabilities, index, CONTEXT.md, and a superseding ADR for ADR-0013.
3. Reuse the existing ImageIntake aggregate/reference owner for Image-initiated Cases. Do not add a principal-less formal Cases row, weaken Case/PO invariants, or create a second allocator.
4. Implement the confirmed outcomes: one usable VRM/no unique match creates one Image-initiated Case; conflicting valid VRMs route the whole group to INTK-007 with the explicit conflicting_vrms marker; no-readable members follow a readable group sibling.
5. Implement lifecycle semantics: searchable Image-initiated Cases retain original filenames and Box custody under the VRM reference, can be staff-closed with a reason, and on later matching are closed/converted as merged or subsumed with permanent cross-case history.
6. Keep the existing staff authorization boundary; do not introduce a new permissions model.
7. Before entering review, verify that files.md, the code diff, the governing docs, and this plan agree. Any path not represented in files.md is an incomplete plan and must be added before implementation continues.

## Follow-on boundary — 2026-08-19

Read and amend files.md before implementation. It is the authoritative file map and conflict audit. INTK-006 implements the grouped recognition/diagnostic/unique existing-Case association seam. INTK-008 reuses the existing ImageIntake aggregate for the usable-VRM/no-match Image-initiated Case, then adds its searchable lifecycle, Box custody presentation, staff closure, and merge/subsumption history. INTK-007 owns grouped Unidentified and conflicting_vrms. No principal-less formal Cases row or second allocator may be added.

## Takeover remediation — 2026-08-19 (claude-code, DELIV-012)

Taken over by operator decision to finish PR #417. Merged `origin/intk-005-grouped-upload` (head `d70118b1`) into this branch, resolved the fail-closed blocker (per-member association overruling the group decision), and closed the review backlog. Pushed as `bfacefeb`.

### Merge

Two real conflicts: `EfIntakeSubmissionGroupStore.cs` (kept INTK-006's `FindForMemberSourceAsync` plus INTK-005's non-`async` `GetOrCreateAsync` signature) and `IntakePersistenceIntegrationTests.cs`'s expected-migration list (took INTK-005's full list). The migration list was independently verified against `src/Pegasus.Infrastructure/Persistence/Migrations/` by diffing a generated file list against the extracted string literals — not trusted from the clean merge, per the explicit warning that this exact merge silently dropped a migration id once before with no conflict markers. Confirmed exactly one copy of `20260819101344_GroupedIntakeSubmission` survived, carrying INTK-005's GRANT statements.

### Blocker fix — group decision must gate association

`src/Pegasus.Core/ImageIntake/ImageIntakeAutomation.cs`, `TryRegisterAndAssociateAsync` (now ~L367-451): added an `ImageIntakeGroupRoutingDecision? groupDecision` parameter. `associationAllowed = groupDecision is null or AssociateExistingCase`. The per-member candidate search (`FindEligibleByRegistrationAsync` + the one-character-missing/exact-match tiebreak) only runs when `associationAllowed`; for every other group decision (`HandOffToImageIntake` in particular) the member still registers into ImageIntake but the candidate search and `TryAssociateAsync` are skipped entirely — the group's own ambiguous/no-match decision can no longer be overruled by a per-member exact match among an otherwise-ambiguous candidate set. Reused the existing `IImageIntakeCaseCandidates`/`TryAssociateAsync`/`registerImageIntake` ports; no second matcher.

Test: `AmbiguousGroupEligibilityHandsOffDespiteAPerMemberExactMatch` (`tests/Pegasus.Core.Tests/ImageIntake/AutomaticImageIntakeTests.cs`) builds a 2-member group where both members read the same VRM, and the group holds two raw candidates for it — one an exact match, one a one-character-longer fuzzy match — so the group-level count is 2 (ambiguous → `HandOffToImageIntake`). Asserts `MutationStore.AutoLinks` is empty and both members still register.

### A second bug found while wiring the fix — ordinal-0 token drift

`GroupedIntakeMemberToken` (new, in `src/Pegasus.Core/Intake/GroupedIntake.cs`): `SubmitGroupedIntake` submits ordinal 0 under the *bare* submission token (INTK-005's single-file-token fix), but `ImageIntakeAutomation.TryApplyGroupAsync`'s per-member receipt lookup unconditionally queried `{token}:{ordinal}`, including for ordinal 0. Any real multi-member group (2+ files) could therefore never find its own first member's receipt via that lookup and would report `waiting_for_members` forever — the whole grouped-routing feature would never complete for a real multi-image upload. Extracted the one child-token shape (`ordinal == 0 ? token : $"{token}:{ordinal}"`) out of `SubmitGroupedIntake`'s private `ChildToken` into a shared public `GroupedIntakeMemberToken.Create`, used by both `SubmitGroupedIntake` and `ImageIntakeAutomation`. One list of the shape, not two.

### Other fixes (mapped to the 13 open PR review comments)

1. **Persist the expected member count before routing (P1)** — FIXED. `IntakeSubmissionGroup.ExpectedMemberCount`, threaded through `IIntakeSubmissionGroupStore.GetOrCreateAsync`, persisted on `IntakeSubmissionGroupEntity` (check constraint `>= 1`), set once from `SubmitGroupedIntake`'s `files.Length` and never revised on replay. `TryApplyGroupAsync` now returns `waiting_for_members` when `members.Count < group.ExpectedMemberCount`, before it even tries to resolve member receipts — closing the tautological "expected == actual because both come from the same row count" gap the reviewer identified. New migration `20260819140113_ImageIntakeGroupExpectedMemberCount` (generated with `dotnet ef migrations add`, default value corrected to `1` so the `AddColumn` step never transiently violates its own check constraint). Test: `AGroupWithFewerStagedMembersThanDeclaredNeverFinalizes`.
2. **Raise the request limit for multi-file uploads (P1)** — already fixed by the INTK-005 merge (`IntakeEnvelopeLimits.MaximumBatchContentLength` wired into `Program.cs`'s `MultipartBodyLengthLimit`). Verified by reading the merged code; no action needed.
3. **Refresh the grouped status while files are pending (P2)** — already fixed by the INTK-005 merge (`data-auto-refresh` present in `UploadGroupStatus.cshtml`). Verified it survived the merge rather than fixing it twice.
4. **Exclude non-image receipts from image-group recognition (P2)** — FIXED. `TryApplyGroupAsync` filters to `receipts.Where(IsImageOnly)` (reusing the existing predicate) before scanning; a non-image member (e.g. a PDF in the same batch) still counts toward the declared-membership check but is never scanned and never blocks the image members' own decision. Test: `ANonImageGroupMemberIsExcludedFromRecognitionAndDoesNotBlockTheDecision`.
5. **Preserve duplicate feedback for replayed groups (P2)** — already fixed by the INTK-005 merge (`isDuplicateByOrdinal` tracked and stamped onto members in `SubmitGroupedIntake.ExecuteAsync`, commit `d70118b1`).
6. **Avoid rescanning the whole group for every receipt (P2)** — FIXED. `ScanAsync` now loads `suggestionStore.ListForReceiptAsync` first and reuses a recorded suggestion (matched by asset id + storage key + content hash) instead of calling the engine again. Test: `RecognitionRunsOnceEvenWhenTheGroupIsTriggeredTwice` (two `ApplyAsync` triggers, engine called twice total across a 2-member group, not four times).
7. **Specify grouped routing in the owning FRD (P1)** — FIXED. Added "Grouped image-intake routing" to `docs/frd/frd-02-intake-and-source-identity.md` (membership/completeness, non-image exclusion, distinct-VRM aggregation, the associate-or-hand-off precedence table, the group-not-per-member fail-closed rule, scan-once). Added a paragraph to `docs/frd/frd-06-vehicle-and-engineering-evidence.md` on the detector/recognizer diagnostic distinction and scan-once behaviour, cross-referencing the FRD-02 section. Registered both under `INT-28`/`INT-17` in `docs/capabilities.md` rather than minting a new capability ID, since this generalises an existing capability (single-image → group) rather than adding a new one. `scripts/Test-TestMarkdownPlacement.ps1` and `scripts/Test-DocumentationLinks.ps1` both pass locally (the `documentation` CI job's two steps).
8. **Register confident groups even without an eligible case (P1)** — already correct in the current code: `HandOffToImageIntake` is not in `TryApplyGroupAsync`'s early-return skip list, so registration still runs for it; only association is skipped (now correctly gated by the blocker fix above, item 11). Verified by reading the code and by the blocker-fix test asserting `Register.Requests.Count == 2` while `AutoLinks` stays empty.
9. **Update single-upload callers for the group landing route (P1)** — already fixed: a one-member group redirects to the pre-existing `/UploadStatus` route with the bare receipt id (`Upload.cshtml.cs`, `result.Members.Count == 1` branch), not a new group-landing route. Verified by the green `QdosIntakeWebTests`/`IntakeWebNegativeTests` integration run (34 passed, 0 failed).
10. **Retry incomplete group registration and association (P1)** — **not fixed; recorded disposition.** A recoverable failure partway through the per-member registration/association loop is swallowed inside `TryRegisterAndAssociateAsync` and the loop continues; if the failing member is the *last* one to complete, nothing later re-triggers that group's evaluation. Registration (`image-intake-register:{receiptId}`) and association (`image-intake-associate:{receiptId}`) are each idempotent by receipt-scoped operation key, so any *later* re-trigger of the same group (another member arriving, a manual reprocess) safely completes a previously-failed member — the actual gap is narrow (last member fails, nothing subsequently re-triggers). A real fix needs a durable per-group routing-attempt/outcome record so a partial failure can be detected and replayed; this ticket's own plan already delegates "durable Image-initiated lifecycle outcome persistence" to [[INTK-008]]. Not invented here to avoid a second, competing outcome/retry mechanism ahead of that design. Recommend INTK-008 (or a follow-up) close this alongside its durable outcome work.
11. **Honor the handoff decision before associating members (P1)** — FIXED. This is the ticket's headline blocker; see above.
12. **Keep one replay identity across upload cardinalities (P2)** — FIXED by removing the single-file bypass in `Upload.cshtml.cs` (the takeover's explicit instruction): every upload, 1 file or many, now flows through `IGroupedIntakeSubmission`, so the child-operation-key shape (`manual-upload:{token}:{ordinal}`) and the group identity `(channel, token)` no longer change shape by cardinality. Also fixed the `GroupedIntakeMemberToken` drift bug discovered while verifying this (see above) — the bare-vs-suffixed token now agrees everywhere.
13. **Make concurrent member insertion idempotent (P2)** — already fixed by the INTK-005 merge: `AddMemberWithRetryAsync`/`AddMemberCoreAsync` in `EfIntakeSubmissionGroupStore.cs` retry on a unique-index violation and re-read the winning row on retry, verifying it names the same staged receipt before returning it (mirrors `EfIntakeWorkStore.ReceiveWithRetryAsync`, reused rather than duplicated, matching the takeover instruction).

### Simplification pass — 2026-08-19 (dated, per-diff)

Reviewed the full takeover diff (`git diff bfacefeb~1 bfacefeb`, 14 files, ignoring the generated migration Designer/snapshot bulk) against the four lenses:

- **Reuse.** `GroupedIntakeMemberToken` replaces a private duplicate (`SubmitGroupedIntake.ChildToken`) with one shared, public definition used by both callers — a genuine de-duplication that also fixed a real bug, not a new abstraction added speculatively (two concrete callers already existed). `IsImageOnly`, `ListForReceiptAsync`, `IImageIntakeCaseCandidates`, and the existing INTK-005 retry shape were reused as directed; no second matcher, no second retry mechanism, no direct EF Case write.
- **Simplification.** Considered inlining `associationAllowed` as a direct `groupDecision != HandOffToImageIntake` check instead of a named local; kept the named boolean and the `is null or AssociateExistingCase` form because it reads directly as "when is a per-member match search even permitted," and a future decision value doesn't need re-deriving the allow-list.
- **Efficiency.** The `ScanAsync` cache and the `imageReceipts` filter both remove real wasted work (redundant ONNX inference; scanning non-image bytes) rather than adding it; no new N+1 query introduced — `ListForReceiptAsync` is one call per `ScanAsync` invocation, same shape as the existing per-member `FindBySourceIdentityAsync` loop.
- **Altitude.** `ExpectedMemberCount` is a plain persisted int on an existing entity, not a new state machine or workflow abstraction; the check constraint mirrors the existing `Ordinal >= 0`/`AttemptCount >= 0` convention rather than inventing a new validation style.

No findings required a follow-up ticket beyond the recorded item 10 disposition above, which is itself already covered by INTK-008's existing scope.
