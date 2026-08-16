# Plan — Board + temp-plan cleanup (Stage C, do last)

Runs last so plans this task still needs aren't removed early.

1. Fresh board scan (`list_items`, `search_items`): identify tickets that only
   restate capability rows with boilerplate and have no actionable
   defect/decision/deliverable. Archive them (`update_item archived: true`).
2. Reconcile any not-yet-applied consolidations from `kanmer-tickets/plan.md`
   (append `Migrated validation — [[ID]]` to owners; archive proof-only
   originals with a migration note).
3. Ownership-check and remove the orphan temp-plans (impact list), leaving
   this task's own plans until merge.

## Acceptance
Remaining board items are actionable; every retained temp-plan has a live
owner; archived items recoverable; no ticket content lost.

## Verify
`get_status`; `list_items include_archived: true`; activity review → zero
warnings, no lost content. `pwsh ./scripts/Test-DocumentationLinks.ps1`
(temp-plan README links still resolve).

**Held for user review.**
