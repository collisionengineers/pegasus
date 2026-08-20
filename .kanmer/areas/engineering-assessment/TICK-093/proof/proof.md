# Proof — TICK-093 (ENG-01)

## Merge

PR #420, merge commit `560f741c89cd109a0f28e53a4e8172fdc2d3c279` on `dev`/`main`.

## Deployment

Shipped in **release 12** (`ed3be51c95bc2a055606e5210131d37de9de2dd1`, deployed
2026-08-19 ~22:40–22:52Z). `560f741c` is a verified ancestor of `ed3be51c`.
See [[DELIV-012]] proof for the release-12 deployment readbacks.

## Production evidence

- Migration `20260819112640` (`VersionedRepairSpecifications`) applied to
  production as part of release 12's 8-migration batch.
- **Grant repair applied and read back**: per [[DELIV-012]] proof,
  `sys.database_permissions` readback on production shows
  `CaseRepairSpecifications` → Web SELECT/INSERT/UPDATE + DENY DELETE — the
  table's permission set this ticket's migration establishes.
- **The store's production caller landed via PR #425**
  (`task/deliv-012-wire-repair-spec-store`, merge commit
  `91a94471bd6315bebabf951b8b721755e6bcb0ea`, verified ancestor of
  `ed3be51c`) — TICK-093 itself implements the Core/Infrastructure aggregate
  and store; the real production wiring of that store to a live caller
  shipped on DELIV-012's own PR, not this ticket's branch.

## Qualification

TICK-093's own verification evidence is at the isolated Core/Infrastructure
tier (Core 45/45, focused SQL lifecycle 3/3, architecture 97/97); this proof
adds the missing production-deployment leg — the aggregate is live with real
grants, and its caller is wired per PR #425 — closing the gap between "built"
and "in production" honestly (the caller is a separate PR, not this ticket's
own diff).
