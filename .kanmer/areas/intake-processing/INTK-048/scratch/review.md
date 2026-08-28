Primary session authored the implementation; the blocking verdict below comes from fresh independent reviewer `review_pr601_independent`.

## Review — PR #601 at 14e0ad6f522a8b39c735f31535e842d8b0738fc8

### Changes

- `src/Pegasus.Core/Intake/ReconcileUnidentifiedDestinations.cs` treats the receipt's effective `CurrentCaseId` as an Instruction Case destination before original-decision eligibility and uses `CurrentCaseReference`.
- `tests/Pegasus.Core.Tests/Intake/ReconcileUnidentifiedDestinationsTests.cs` covers a still-eligible receipt with an active manual Case association.
- `tests/Pegasus.IntegrationTests/UnidentifiedReconciliationTests.cs` exercises the real manual-link path, sweep, persisted association/event, resolution history, and replay.

### Comments and dispositions

- **Blocking:** the supported `ReverseIntakeLink` path can clear `CurrentCaseId` after the new sweep permanently resolves the U-item. The resolved row is not reopened or resynchronized, registration replay returns it unchanged, and a later relink can leave the current resolution target pointing to the first Case. **Disposition:** filed as [[PR-069]], which blocks [[INTK-048]].
- **Non-blocking:** none.

### Gate checks

- The post-implementation report accurately lists all three changed files and their rationale.
- The implementation meets every recorded plan step, but the plan missed the reversible association lifecycle required by FRD-02 and linked [[INTK-029]].
- The simplification pass was honestly recorded: its test-claim gap was fixed and there are no undisposed simplification findings. The blocker is a correctness finding, not simplification feedback.
- No open-questions document exists; the questions-resolved gate is satisfied.
- GitHub reports the PR mergeable and all applicable checks successful; infrastructure is skipped by change classification.

### Verdict

**Needs changes.** Do not merge PR #601 until [[PR-069]] lands in this PR and an independent re-review passes at the new head SHA.
