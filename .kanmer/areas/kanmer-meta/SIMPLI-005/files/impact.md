# Impact — Board + temp-plan cleanup

## Changes
- **Kanmer board** — `update_item archived: true` on non-actionable tickets
  (never `delete_item`); consolidate proof-only tickets into owners per
  `kanmer-tickets/plan.md` (append migration notes, archive originals).
- **`docs/temp-plans/`** — remove the orphan-candidate files after a final
  ownership check: `keep-web-warm.md`, `mcp-assessment-toolset.md`,
  `send-to-claude-channel-integration.md`, and `kanmer-tickets/plan.md`
  (last, once this task merges).

## Guards
- Archive, don't delete, Kanmer items — recoverable.
- Re-read each ticket/plan immediately before mutation; skip anything
  concurrently changed or taken (e.g. SIMPLI-001 is taken by another agent).
- Do the temp-plan deletions as the task's own maintenance step so no other
  task's material is touched.
