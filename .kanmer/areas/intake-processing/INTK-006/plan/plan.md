# Plan — INTK-006

## Approach

Using the durable membership contract from the [[INTK-005]] PR branch, make the group—not an individual image—the routing unit. Wait until every image member has terminal recognition evidence, aggregate accepted VRMs, reuse one exact eligible-case matcher, and commit one idempotent outcome: associate all members to one existing Case or create one documented Image-Only Case and attach all members.

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
   - Add an enum/record only if needed for these mutually exclusive states: WaitingForMembers, WaitingForRecognition, AssociateExistingCase, CreateImageOnlyCase, TechnicalFailure.
   - Inputs are ordered group members, each terminal recognition result, normalized accepted suggestions, and eligible Case candidates.
   - Ignore below-bar text for automatic identity. Preserve it as evidence only.
   - Compute distinct accepted normalized VRMs across every member.
   - Select AssociateExistingCase only when the set contains exactly one VRM and the shared candidate owner returns exactly one eligible Case with no contradiction.
   - Select CreateImageOnlyCase for zero accepted VRMs, more than one accepted VRM, zero eligible matches, or multiple eligible matches.
   - Never return an unhandled/NeedsSorting third path for a completed vehicle group.

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

7. **Implement the Image-Only Case branch through the sole Case owner.**
   - Extend the exact Case acceptance/allocation use case named after governing-doc reconciliation; do not call EF directly.
   - Pass the persisted group id and all immutable origins so replay identity is the group, not one arbitrary member.
   - Populate only fields authorized by the updated FRD. Do not invent principal, registration, instruction, claimant, or Case type defaults.
   - Create exactly one Case, then attach/register every member in ordinal order and persist the one group outcome.
   - Use the documented reference/lifecycle semantics exactly. If the docs require a nonstandard reference, add it through the existing sequence owner rather than reusing Image Intake/U/Audit sequences.
   - Test zero-VRM, low-confidence, conflicting-VRM, no-match, and multi-match groups all create one—not N—Image-Only Case.

8. **Expose honest status and history.**
   - Extend the INTK-005 group status view to show Waiting for all images, Associated with Case <reference>, Created Image-Only Case <reference>, or named technical failure.
   - Link to the existing Case details route and show every original filename/origin.
   - Add one presentation mapping in `OperatorLabels.cs`; do not emit raw enum/snake_case values.
   - Ensure receipt detail and history identify the shared group outcome without implying a low-confidence VRM was accepted.

9. **Run the complete routing matrix.**
   - One image, accepted VRM, one eligible Case → associate.
   - Overview accepted VRM + close-up no plate, one eligible Case → associate both.
   - Accepted VRM, zero eligible Cases → one Image-Only Case.
   - Accepted VRM, two eligible Cases → one Image-Only Case, no existing association.
   - Two distinct accepted VRMs → one Image-Only Case, no existing association.
   - All no-plate/unreadable/below-bar → one Image-Only Case.
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
- Every completed vehicle-image group reaches exactly one association-or-Image-Only Case outcome.
- No group is split, no fallback Case is duplicated, and low-confidence/conflicting text never attaches to an existing Case.
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
- The Image-Only Case branch is not fabricated: the existing Case acceptance owner requires a real principal and immutable Case identity. INTK-007/updated governing documents must supply that authorized contract; until then the group remains available for the documented Unidentified path. This is a review/rebase policy seam, not an INTK-005 branch dependency blocker.
- Release build passed with 0 warnings/errors. Focused Core tests (19) and VRM integration tests (5) passed.

## Clarified product model — 2026-08-19

The operator has clarified that Pegasus has two Case-origin types:

- **Instruction-initiated Case (main/formal):** starts from an accepted official instruction document such as PDF or Word. It uses the existing Principal, Case type, and Case/PO allocation rules. Images may be absent at initial creation; missing images make the Case incomplete/Not ready, not unidentified.
- **Image-initiated Case (secondary/pre-instruction):** starts from retained vehicle images before formal instructions arrive. It has no Case/PO because Principal/formal instruction may be unknown. It uses an immutable VRM reference with a per-VRM sequence: `AB12ABC-01`, then `AB12ABC-02`, `AB12ABC-03`, with no reuse. It is a separate source-origin record until a later Instruction-initiated Case is matched.

VRM matching is the bridge: when exactly one eligible Instruction-initiated Case matches the group VRM without overlap or contradictory identity, all group images associate to that Case while both origins and history remain attributable. Otherwise the Image-initiated Case remains the single destination for the complete group.

This ticket now includes the governing-document amendment needed to remove the current pre-Case-only conflict. Required reconciliation targets: `docs/operator-notes.md`, `docs/prd/pegasus-product.md`, `docs/frd/frd-01-case-identity-and-lifecycle.md`, `docs/frd/frd-02-intake-and-source-identity.md`, `docs/frd/frd-06-vehicle-and-engineering-evidence.md`, `docs/frd/frd-12-operator-experience.md`, `docs/design/README.md`, `docs/capabilities.md`, `CONTEXT.md`, and any ADR/index wording that must be superseded or relocated. The final docs must define the Image-initiated reference sequence, lifecycle, conversion/association rules, origin/history retention, and its exclusion from Case/PO, Audit, and Unidentified references.
