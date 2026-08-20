# Plan — INTK-012: ordinal-0 member-token resolution

Branch `task/intk-012-ordinal-token` from origin/dev (c91215fd), worktree `../pegasus-worktrees/intk-012`, PR → `dev`. Small diff; the plan is proportional.

## Steps

1. **Token convention (one owner).** `GroupedIntakeMemberToken.ParentTokenCandidates(memberToken)` in Core: yields the stripped parent for a strict `:{ordinal>=1}` suffix (`NumberStyles.None` — no signs, no whitespace; `:0` is not a member-token shape because `Create` never emits it), then always the bare token itself (INTK-005's ordinal-0 shape). `FindForMemberSourceAsync` becomes a loop over these candidates calling the existing `FindAsync` — the store stops parsing tokens entirely.
2. **Scope the group decision table explicitly.** `IntakeSubmissionGroup.HasSiblingMembers` (`ExpectedMemberCount > 1`) in Core; `ImageIntakeAutomation.TryApplyGroupAsync` and `ReconcileGroupedImageIntake` both consult it and step aside for one-member groups — the single-image rule (exact-wins, operator-directed 2026-08-03) governs a lone image, exactly as the routing policy's doc already scopes itself ("more than one image"). Without this, making the lookup truthful would silently flip single-file uploads onto group fail-closed association semantics.
3. **Tests.** Core: token-candidate shapes in `GroupedIntakeTests`; one-member-group-keeps-single-rule in `AutomaticImageIntakeTests` (exact-among-fuzzy still associates). Integration: ordinal-0 (and ordinal-1) members of a real uploaded group resolve to their group via `FindForMemberSourceAsync`; unrelated token resolves to nothing (`GroupedIntakeWebTests`). Focused runs: `GroupedIntakeTests`, `AutomaticImageIntakeTests` filter, `GroupedIntakeWebTests`, `GroupedImageIntakeConcurrencyTests` (callers' suite), plus full Core.Tests; `Test-MigrationGrants.ps1` and `Test-AzureDeploymentPlan.ps1 -Mode Local` stay green (no migration).

## Acceptance

- Every member ordinal — 0 included — resolves to its group in both `FindForMemberSourceAsync` callers.
- The token convention has exactly one owner (`GroupedIntakeMemberToken`); the store carries no token knowledge.
- Single-file uploads keep the single-image association rule (regression-pinned); multi-image groups keep group semantics.
- Build zero-warning; focused suites green.

## Interplay with INTK-015 (recorded)

INTK-015 (PR #447) rewrites the body of `TryApplyGroupAsync` below the group lookup; this ticket adds an early return beside the lookup itself and touches no INTK-015 file regions otherwise. Whichever merges second should re-run `GroupedImageIntakeConcurrencyTests` (CI does). Composed behaviour: an ordinal-0 trigger takes the group path and the group registers once; INTK-015's adopt-by-origin branch remains as the convergence safety net.

## Simplification pass — 2026-08-20

Run with the `code-simplifier` agent over `git diff origin/dev...HEAD`; behaviour-preserving only; build zero-warning, focused Core suites (32) green after applying. Commit `3e6a452f`.

Applied:
- `GroupedIntakeMemberToken.ParentTokenCandidates` — iterator (`yield return`) replaced with a returned candidate list, so the `ThrowIfNullOrWhiteSpace` guard fires at the call instead of at first enumeration (no caller/test observes the deferred path; the sole production caller enumerates immediately).
- `IIntakeSubmissionGroupStore.FindForMemberSourceAsync` XML doc — repointed at `GroupedIntakeMemberToken` as the token-shape owner and states the ordinal-zero case (the old doc restated the very belief this ticket corrects).

Not applied (with reasons):
- Up-to-two-DbContext lookups per candidate in the store — inherent to the fix (an ordinal-0 token is shape-indistinguishable from a non-group token, so no cheap pre-filter exists); collapsing candidates into one query would fold `FindAsync`'s composition back in and lose the owner-defined candidate order. Recorded as an observation: `ReconcileGroupedImageIntake` now pays one query per non-group needs-sorting receipt where the shape-gate cost zero.
- The one-member-group rationale appearing at three sites — the rule has one owner (`HasSiblingMembers`); each comment adds a local consequence.
- Fully-qualified `NumberStyles`/`CultureInfo` — both conventions exist in the codebase; no churn.
- Test-preamble duplication in `GroupedIntakeWebTests` — second copy, deliberately divergent (skips processing); threshold is a third copy.
