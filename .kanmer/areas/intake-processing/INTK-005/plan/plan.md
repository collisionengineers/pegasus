# Plan — INTK-005

## Approach

Add the smallest normalized submission-group boundary around the existing per-file `IIntakeSubmission`. Every staff form submission receives one durable group token and ordered members; every member still receives its own immutable source identity, staged receipt, artifact, and work item. The Web UI accepts multiple files and presents every outcome. This is the required producer for [[INTK-006]].

## Governing docs

Meets:
- `docs/frd/frd-02-intake-and-source-identity.md`
- `docs/frd/frd-12-operator-experience.md`
- `docs/design/README.md`

No ADR is required: the existing Core/Infrastructure/Web boundaries carry the change and no runtime/store/deployment boundary is added.

## Implementation steps

1. **Start from current integration state.**
   - Confirm PLAT-006 is merged into `origin/dev`; if not, stop and record the dependency rather than recreating its dropzone.
   - Create/take the ticket worktree exactly as repository workflow requires.
   - Read EPIC-007 context and the complete ticket folder.
   - Run the current focused Upload/intake tests and record the baseline in scratch.

2. **Define Core group contracts without changing one-file policy.**
   - In `DurableIntake.cs`, add records for group identity, ordered member identity, group detail/result, and per-member submission result.
   - Define a store port with exact operations: find group by manual-upload submission token; create/replay the group; add or resolve a member by ordinal; list ordered members by group id; resolve group by staged receipt id.
   - Define a group submission use case that accepts the form token, actor, received time, and an ordered list of `IntakeSource` values.
   - Validate non-empty group, unique non-negative ordinal, supported manual-upload channel, and maximum token lengths using the existing constants/conventions.
   - Derive each child external receipt token and operation key deterministically from the form token plus ordinal; do not use filename/hash as identity.
   - Call the existing `IIntakeSubmission.ExecuteAsync` sequentially once per member. Do not duplicate hashing, staging, work-item creation, or retry policy.
   - Return one result entry per input ordinal, preserving safe original filename and accepted staged receipt id or a safe failure classification.

3. **Add normalized EF persistence.**
   - Add `IntakeSubmissionGroupEntity` with GUID id, source channel, unique external submission token, actor, received/created timestamps.
   - Add `IntakeSubmissionGroupMemberEntity` with group id, ordinal, staged receipt id, and created timestamp.
   - Configure required lengths and indexes: unique `(SourceChannel, ExternalSubmissionToken)`; unique `(GroupId, Ordinal)`; unique `StagedReceiptId`; FK member→group and member→staged receipt; restrict destructive cascade from staged receipt.
   - Implement the Core store port with explicit transactions and existing EF execution/retry conventions. Concurrent same-token creation must converge on the same group; conflicting member identity must fail closed.
   - Generate a migration and snapshot. Include only caller grants consistent with adjacent Azure SQL migrations.
   - Add DI registration in `Program.cs`.

4. **Prove Core and persistence semantics before UI work.**
   - Unit-test empty groups, one-member group, ordered three-member group, duplicate filenames, deterministic child tokens, and no duplicated one-file policy.
   - Integration-test first submission, exact replay, concurrent replay, different bytes under the same child identity, unique membership constraints, ordered query, and receipt→group lookup.
   - Test a simulated later-member failure: earlier accepted members remain returned and replaying the same token does not duplicate them.

5. **Change the authenticated Upload form.**
   - Change the page-model binding from one file to a supported file collection while retaining `ExternalReceiptToken`.
   - In markup, add `multiple` and keep the native input, label, hint, validation summary, and no-JavaScript submit path.
   - Extend PLAT-006 JavaScript only through its existing data attributes. Render selected names in ordinal order and permit duplicate names.
   - Validate the entire collection before invoking Core: at least one file, no empty file, every file no larger than `IntakeEnvelopeLimits.MaximumContentLength`, supported multipart request, and safe filename.
   - Do not allocate a new form token after validation failure; the browser retry must preserve the original group identity.
   - Open/copy/dispose one member stream at a time and pass ordered sources to the new group use case.

6. **Present every member outcome.**
   - Add the smallest group result/status page if a single POST response cannot safely refresh all member statuses.
   - Query the group and then the existing `IQueuedIntakeStatusQueries` for every staged receipt; never calculate worker state separately.
   - Show original filename, received/processing/complete/failed state, and link to the existing receipt/status/detail route for each member.
   - Show partial success explicitly: accepted files remain accepted; failed files name a safe reason and retry instruction.
   - Ensure one-member groups produce the same useful status without a separate code path.

7. **Extend web/integration/browser tests.**
   - Update multipart helpers to submit 1, 2, and many files under the same field name and token.
   - Assert each member produces its own staged receipt, source hash, original filename, source identity, artifact, and work item.
   - Assert the group has exactly those receipts in input order.
   - Assert duplicate filenames remain two distinct receipts.
   - Assert empty selection, empty member, oversized member, unsupported request, and aggregate request rejection produce no silent loss.
   - Assert exact replay returns the original group/receipts and a conflicting replay fails closed.
   - Assert the result page exposes all statuses/actions and remains usable without JavaScript and by keyboard.

8. **Run verification and simplification.**
   - Run formatting if required, `dotnet restore`, `dotnet build --configuration Release`, focused Core/integration/browser tests, then full `dotnet test`.
   - Inspect the branch diff through the four required simplification lenses. Specifically reject duplicate intake validation, duplicate status mapping, an unnecessary batch queue, and abstractions with no second caller.
   - Record dated simplification findings/dispositions in this plan before PR creation.
   - Update checklist and write the post-implementation report only after implementation evidence exists.

## Verification

- Several selected files create one durable group and one independent receipt/work item per file.
- Original filenames and selection order survive persistence and display.
- One-file submission is a one-member group.
- Same-token replay and concurrent replay never duplicate group or receipts.
- A failed later member never hides or rolls back earlier retained members.
- Existing per-file limits and source-identity conflict behavior remain unchanged.
- [[INTK-006]] can resolve all group members from any staged receipt using the Core query port.
- Release build and all tests pass.

## Risks and controls

- **Partial acceptance:** expose every member result and make replay idempotent; do not pretend an HTTP batch can roll back retained artifacts.
- **Memory pressure:** process one file at a time and retain request/aggregate limits.
- **Identity collision:** deterministic ordinal child tokens plus database uniqueness.
- **PLAT-006 overlap:** require merged dependency and extend its conventions.
- **Over-abstraction:** one group aggregate and one consumer port only; no generic batching framework.

## Simplification pass — 2026-08-19

- Reused the existing `IIntakeSubmission` for hashing, staging, replay, work-item creation, and per-file limits; no duplicate intake implementation was introduced.
- Kept group persistence in one focused store and one Core orchestration use case; no generic batching framework or second queue was added.
- Reused existing single-receipt status queries from the group result page rather than creating a second status taxonomy.
- Extended PLAT-006's existing dropzone attributes and native input instead of replacing the Upload interaction.
- No unapplied behavior-preserving simplification findings remain. The full integration suite was attempted; its test host crashed after 61 passed tests, while the focused grouped web test, focused Core tests, architecture suite, and Release build passed.


## Parallel-branch execution note — 2026-08-19

INTK-006 is intentionally allowed to execute before this PR merges. Its implementation worktree is based on this PR branch (`intk-005-grouped-upload`), not `origin/main` or `origin/dev`. Review feedback or merge conflict resolution for INTK-005 will be reconciled by rebasing INTK-006 later; this is planned coordination, not a blocking dependency.
