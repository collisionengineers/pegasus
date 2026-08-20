# Files — INTK-012 ordinal-0 member-token ambiguity

Verified read-only in `../pegasus-worktrees/intk-012` at origin/dev (c91215fd).

## Verified facts

- `EfIntakeSubmissionGroupStore.FindForMemberSourceAsync` (src/Pegasus.Infrastructure/Persistence/EfIntakeSubmissionGroupStore.cs:37-53) returns null for any token without a `:{int}` suffix — an ordinal-0 member (bare parent token, `GroupedIntakeMemberToken.Create` returns the submission token verbatim for ordinal 0) can NEVER find its group. Its parse is also loose: `int.TryParse` default styles accept a signed suffix, and `:0` (a shape `Create` never produces) strips as if it were a member suffix.
- Callers affected (both mis-handle first members today): `ImageIntakeAutomation.TryApplyGroupAsync` (src/Pegasus.Core/ImageIntake/ImageIntakeAutomation.cs:136) — an ordinal-0 trigger falls to the single-receipt path; `ReconcileGroupedImageIntake` (src/Pegasus.Core/Intake/ReconcileGroupedImageIntake.cs:78) — an ordinal-0 straggler is skipped as "not a grouped upload".
- `GroupedIntakeMemberToken` (src/Pegasus.Core/Intake/GroupedIntake.cs:103-107) is the one owner of the token convention (its own doc records the drifted-second-copy incident this ticket descends from).
- **Scope guard the fix must add:** `ImageIntakeGroupRoutingDecision`'s doc scopes the group decision table to "a manual upload [that] contains more than one image", and FRD-02's single-image rule (exact confirmed match wins over a one-character fuzzy candidate, operator-directed 2026-08-03) differs from the group rule (raw eligible-case count > 1 fails closed even past an exact match). Every manual upload — including a single file — is a one-member `IntakeSubmissionGroup` (INTK-005). Today the lookup miss accidentally kept one-member groups on the single-image path; once the lookup is truthful, that routing choice must become explicit or single-file uploads change association behaviour.

## Change set

- `src/Pegasus.Core/Intake/GroupedIntake.cs` — `GroupedIntakeMemberToken.ParentTokenCandidates(memberToken)`: the `:{ordinal>=1}` strip (strict `NumberStyles.None`) first, then the bare token itself (the ordinal-0 shape). The convention keeps exactly one owner. Plus `IntakeSubmissionGroup.HasSiblingMembers => ExpectedMemberCount > 1` — the one owner of "a one-member group is not a multi-image group".
- `src/Pegasus.Infrastructure/Persistence/EfIntakeSubmissionGroupStore.cs` — `FindForMemberSourceAsync` tries each candidate parent token via the existing `FindAsync`; no token parsing of its own.
- `src/Pegasus.Core/ImageIntake/ImageIntakeAutomation.cs` — `TryApplyGroupAsync` returns null for a group without sibling members: the single-image decision table (exact-wins) governs a lone image, per the routing policy's own scope; group semantics stay for >= 2.
- `src/Pegasus.Core/Intake/ReconcileGroupedImageIntake.cs` — skip groups without sibling members (no sibling can ever change the receipt's outcome — the same reason the non-grouped skip already states).

## Tests

- `tests/Pegasus.Core.Tests/Intake/GroupedIntakeTests.cs` — `ParentTokenCandidates` shapes: bare token; `:1`/`:12` suffix (strip first, bare second); `:0` and `:+1` and `:abc` literals (bare only).
- `tests/Pegasus.Core.Tests/ImageIntake/AutomaticImageIntakeTests.cs` — a one-member group keeps the single-image rule: exact candidate among a fuzzy pair still associates (the distinguishing behaviour the group table would refuse).
- `tests/Pegasus.IntegrationTests/GroupedIntakeWebTests.cs` — the test INTK-011 could not write: every member of a real uploaded group — ordinal 0 included — resolves to its group through `FindForMemberSourceAsync` by its own source identity; an unrelated token resolves to nothing.

## Out of scope

- The one-registration-per-group collapse is [[INTK-015]] (PR #447, same lane). The two compose: with both merged, an ordinal-0 trigger takes the group path and the group registers once; until INTK-015 merges, the ordinal-0 trigger simply joins its siblings' group evaluation (per-member registration, INTK-011 behaviour).
