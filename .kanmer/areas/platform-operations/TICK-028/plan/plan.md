## Plan (docs-only, proportional to a documentation diff)

1. **`docs/runbook.md` — extend `### Production recovery`.** Add a new
   `#### Point-in-time restore commands` subsection immediately after the
   existing 8-step contract (reuses that contract's steps 2–7 rather than
   restating them): exact `az sql db show`/`str-policy show` inventory
   commands, the `az sql db restore --dest-name pegasus-restore-drill-<date>
   --time ... --backup-storage-redundancy Geo` restore command, and
   verification steps reusing the `Invoke-Sqlcmd -AccessToken` pattern from
   `scripts/Invoke-AzureDatabaseBootstrap.ps1` (row counts on a representative
   table, `__EFMigrationsHistory` head, then the named real-caller smoke
   journey per the existing contract's step 6). Close with the approval
   boundary sentence citing `#live-operation-approval-matrix`'s "Deploy,
   restore, fail over, or retire" row — this is a write, not a read-only
   check.
2. **`docs/operations.md` — extend `## Recovery`.** Add the measured posture
   (7-day short-term retention/PITR window, `Geo` backup storage redundancy,
   `S0`/Standard SKU, ~40 MB database size, earliest restore point observed
   2026-08-13) as a dated current-state fact, without changing the existing
   "deferred, gates no release" and "no exercise has completed" statements —
   posture is now measured; the exercise itself is still unrun.
3. **Park the drill.** Write `open-questions.md` with the exact unticked
   line the orchestrator specified, citing the approval matrix.
4. **Ticket docs to walk stages**: this plan + files + research to leave
   Preparing; post-implementation-report to enter Review; proof stays
   unwritten (ticket does not reach Done in this pass — review is the target
   stage per orchestrator instruction).

## Reuse

- Extends the existing `docs/runbook.md#production-recovery` contract and
  `docs/operations.md#recovery` section rather than creating new sections or
  a second recovery procedure (one-list-per-concept: the 8-step contract
  stays the single production recovery procedure).
- Reuses `scripts/Invoke-AzureDatabaseBootstrap.ps1`'s
  `Invoke-Sqlcmd -AccessToken (az account get-access-token ...)` connection
  pattern for verification queries, since the server is Entra-only
  (`azureAdOnlyAuthentication: true`) — no new SQL-auth convention invented.
- Cites the existing `#live-operation-approval-matrix` row for the approval
  boundary rather than writing a new approval rule.

## Out of scope / explicitly deferred

- No restore drill executed (Azure write, not approved this pass) — parked
  as an open question.
- No change to `docs/capabilities.md`'s OPS-09 "deferred, non-blocking, gates
  no release" status — that decision stands; only the documented posture and
  procedure are new.
- No application or infrastructure code change.

## Simplification pass — 2026-08-20

n/a — docs-only.
