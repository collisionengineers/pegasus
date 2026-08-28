# Checklist — CASE-025

- [x] Recover worktree from `task/case-025-cases-queues` (95f69958)
- [x] Audit 95f69958; verdicts recorded in research
- [x] Merge origin/dev (clean; no Migrations conflicts)
- [x] Page-model repairs (exclusive Missing, image files line, quick
      detail without string surgery, per-target filter retention, sort
      removal)
- [x] Index.cshtml three-pane markup per §1.4
- [x] TriageQueuesWebTests rewritten to the new contract
- [x] Release build green (compiler feedback)
- [x] Simplification pass over the branch diff (dated heading in plan)
- [x] Post-implementation report
- [x] PR open to dev; stop

## Verification notes

- Build only in this lane; `dotnet test`/snapshot scripts belong to the
  orchestrator's wave loop.
- Ticket-body verification items (queried counts; unidentified count
  excludes blocked; 1580/1100/760 no-clip) are proven by the wave loop's
  browser pass, not here.
- PR: https://github.com/collisionengineers/pegasus/pull/596
