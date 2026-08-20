# Post-implementation report — INTK-012

Branch `task/intk-012-ordinal-token` from origin/dev (c91215fd), worktree `../pegasus-worktrees/intk-012`, 2 commits `fe48c239..3e6a452f`, PR → `dev`.

## What shipped

- **The token convention keeps one owner and gains its inverse.** `GroupedIntakeMemberToken.ParentTokenCandidates(memberToken)` (src/Pegasus.Core/Intake/GroupedIntake.cs) names the parent tokens a member token can carry, in precedence order: a strict `:{ordinal>=1}` suffix (`NumberStyles.None` — the only suffix shape `Create` emits; `:0` is never emitted so it is not a member suffix) strips to its parent, and the bare token itself is always a candidate because an ordinal-0 member carries the parent token verbatim (INTK-005).
- `EfIntakeSubmissionGroupStore.FindForMemberSourceAsync` now just tries each candidate through the existing `FindAsync` — the store carries no token knowledge, and ordinal-0 members resolve to their group in both callers (`ImageIntakeAutomation.TryApplyGroupAsync`, `ReconcileGroupedImageIntake`). The lookup's stale contract doc (which restated the exact belief this ticket corrects) was repointed at the convention owner.
- **The truthful lookup required an explicit scope decision.** Every manual upload is a submission group (INTK-005), so single-file uploads are one-member groups; before this fix the lookup miss accidentally kept them on the single-image path. The group decision table scopes itself to "a manual upload [that] contains more than one image", and its fail-closed eligible-case count differs from the operator-directed (2026-08-03) single-image exact-match rule — so `IntakeSubmissionGroup.HasSiblingMembers` (`ExpectedMemberCount > 1`) is now the one owner of that distinction, and both callers step aside for one-member groups. Single-file behaviour is unchanged and regression-pinned; multi-member groups now include their ordinal-0 member on every evaluation path.

## Test evidence (exact counts)

- Core: full `Pegasus.Core.Tests` **724/724**, including the new `ParentTokenCandidates` theory (7 shapes) + round-trip test in `GroupedIntakeTests` and `AOneMemberGroupKeepsTheSingleImageAssociationRule` in `AutomaticImageIntakeTests` (exact-among-fuzzy still associates for a lone image — the assertion the group table would fail).
- Integration (focused): `GroupedIntakeWebTests` — new `EveryMemberResolvesToItsGroupByItsOwnSourceIdentity` (the test INTK-011 could not write: ordinal 0 and ordinal 1 of a real uploaded group both resolve via `FindForMemberSourceAsync`; an unrelated token resolves to nothing) — plus `GroupedImageIntakeConcurrencyTests`, `UploadOutcomeQueriesTests`, `ImageIntakePersistenceTests`: **21/21**.
- `Pegasus.ArchitectureTests` **97/97**; `Test-MigrationGrants.ps1` pass (55 files; no migration in this change); `Test-AzureDeploymentPlan.ps1 -Mode Local` pass; build 0 warnings.

## Deliberately left out / for the reviewer

- **INTK-015 interplay (PR #447, same lane):** that branch rewrites `TryApplyGroupAsync` below the group lookup; this one adds a guard beside the lookup. Whichever merges second should re-run `GroupedImageIntakeConcurrencyTests` (CI's sql-integration lane covers it). Composed behaviour: an ordinal-0 trigger takes the group path and the group registers once; INTK-015's adopt-by-origin branch remains as the convergence safety net rather than the primary mechanism.
- Cost observation (from the simplification pass): `ReconcileGroupedImageIntake` now pays one `FindAsync` query per non-group needs-sorting receipt where the old shape-gate cost zero — inherent to the fix, since an ordinal-0 token is shape-indistinguishable from a non-group token; bounded at 50 receipts per sweep tick.
- No FRD change: the behaviour this restores is what FRD-02 already specifies (group membership durable and resolvable per member; single-image rule for a lone image).

## Governing docs

- None changed; `docs/frd/frd-02-intake-and-source-identity.md` already governs and is unmodified here.
