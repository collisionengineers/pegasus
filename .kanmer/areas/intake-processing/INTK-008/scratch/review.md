## INTK-008 PR review — 2026-08-19

Reviewer/author disclosure: Codex implemented this ticket and is recording the
initial review; an independent human review remains required.

Review set:
- PR #423, current branch commits through 855160b7.
- Diff checked against current origin/dev after a non-force rebase/merge.
- Research, files.md, plan, checklist, open-questions, and post-implementation
  report read.
- Governing-doc section in plan covers all linked FRDs and the new ADR decision.
- Scope respects the two-origin model: no principal-less formal Case/PO row,
  no second allocator/store, and conflicting/no-readable groups remain INTK-007.

Findings:
- Core lifecycle state/event projection, serializable replay/CAS transitions,
  reverse pairing merge history, staff closure authorization, UI state/search
  vocabulary, ADR-0029, and VRM-keyed custody adapter are present.
- Existing stale “pre-Case only” language was corrected in FRD-01/02/06/12,
  design, CONTEXT, operator notes, capabilities, and index.
- Release builds and 582 full Core tests pass locally; focused ImageIntake
  coverage is 40 tests.
- PR CI is currently pending after the final terminology commit; no review
  verdict or merge is recorded until all required checks finish.

Simplification pass:
- Reused ImageIntakeStore, existing receipt association/history, existing Case
  history, StaffActorFactory, and the existing Box/local custody adapter.
- No duplicate formal Case allocator, Box client, matcher, or runtime boundary
  added.
- Generated EF migration designer is required by the repository migration
  convention; no hand-written replacement was introduced.

Open review risks:
- SQL/browser/integration CI must verify the additive migration and Razor action.
- The custody root is exposed through the guarded adapter but is intentionally
  not invoked by local registration; external custody dispatch remains the
  existing queued boundary.
- A future independent reviewer should inspect operation-key conflict semantics
  and the merge event's formal Case history projection.

Provisional verdict: pending CI and independent review; no blocking finding yet.
