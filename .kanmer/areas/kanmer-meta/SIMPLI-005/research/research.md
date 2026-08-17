# Research — Archive non-actionable board items + orphan plans

Root plan: `docs/temp-plans/retire-now-rewrite-agents.md`. Source of intent:
`docs/temp-plans/kanmer-tickets/plan.md` (the reconciliation plan that created
the SIMPLI tickets; much of it is already applied).

## Board baseline (get_status)
218 tickets: 192 todo, 20 in-progress, 0 review, 6 done, 18 archived.
The `kanmer-tickets/plan.md` mapping (complete 15 delivered tickets with
`Operator confirmed`; consolidate 16 proof-only tickets into owners; rework 6)
is largely reflected already — Stage C reconciles the remainder, it does not
re-run the whole plan blind. A fresh board scan is required at execution to
find tickets that only restate capability rows with boilerplate.

## Orphaned temp-plans (cross-referenced vs live `git worktree list`)
Owned (live worktree/branch — **do not remove**): `case-custody-eva-export`,
`case-edit-lease-continuity`, `qdos-audit-intake-inbox`,
`qdos-forward-intake-failure`, `upload-case-creation-and-inbox`,
`report-renderer-integration*` (14 files), `report-renderer-workspace-uplift`.
Owned by THIS task: `simplify/simplify.md`, `simplify/adr-consolidation.md`,
`retire-now-rewrite-agents.md` — keep until the task merges.

**Orphan candidates (no live task owner):**
- `keep-web-warm.md` — `task/keep-web-warm` already merged into `dev`.
- `mcp-assessment-toolset.md` — no live branch/worktree.
- `send-to-claude-channel-integration.md` — no live branch/worktree.
- `kanmer-tickets/plan.md` — becomes orphaned once this SIMPLI task merges.

Each candidate needs a final ownership check immediately before removal
(archive/delete per the temp-plans contract; git history retains them).
