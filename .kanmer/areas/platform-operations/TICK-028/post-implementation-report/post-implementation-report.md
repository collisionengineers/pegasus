## What shipped

Docs-only PR [#459](https://github.com/collisionengineers/pegasus/pull/459)
on branch `task/tick-028-ops-09-backup` (worktree
`../pegasus-worktrees/tick-028`, from `origin/dev`):

- `docs/runbook.md` — new `#### Point-in-time restore commands` subsection
  under the existing `### Production recovery` contract: exact `az sql db
  show`/`str-policy show`/`list-usages` inventory commands, the `az sql db
  restore` command (Entra-token connection, `--backup-storage-redundancy
  Geo`), verification via `Invoke-Sqlcmd -AccessToken` (reusing
  `scripts/Invoke-AzureDatabaseBootstrap.ps1`'s connection pattern) checking
  `__EFMigrationsHistory` head and row counts, and the app-smoke step. Closes
  with the approval boundary citing `#live-operation-approval-matrix`.
- `docs/operations.md` — `## Recovery` gained the measured posture: 7-day
  short-term retention, `Geo` backup storage redundancy, `S0` SKU, ~40 MB
  database size, and the honest documented-RPO/RTO assessment.
- Ticket `open-questions.md` — the restore drill parked under `## Parked
  (explicitly deferred)`, unticked, with the exact approval scope needed.

## Read-only Azure verification performed

`az sql db show`, `az sql db str-policy show`, `az sql db ltr-policy show`,
`az sql db list-usages`, `az sql server show` against
`pegasus-prod-sql-252ow37gij/pegasus` (sub
`e6076573-23a5-46a8-acef-7e22d264e5db`, rg `rg-pegasus-prod`). No Azure write
was made. Full readback recorded in `research.md`.

Key findings:

- Short-term retention (PITR window): 7 days. `earliestRestoreDate`
  2026-08-13, exactly 7 days before the 2026-08-20 readback — PITR is
  confirmed live, not just configured.
- `currentBackupStorageRedundancy`/`requestedBackupStorageRedundancy`: `Geo`.
  `zoneRedundant`: false. Long-term retention: not configured (all zero).
- SKU: Standard `S0` (10 DTU). Database size: ~39.5 MiB used / ~48 MiB
  allocated (max 250 GB).
- Server is Entra-only auth (`azureAdOnlyAuthentication: true`) — restore
  verification must use an `az`-issued access token, matching the existing
  `Invoke-Sqlcmd -AccessToken` convention rather than inventing a new one.

## RPO/RTO honesty check (documented, not assumed)

- RPO: Microsoft Learn ("Automated backups in Azure SQL Database") states
  transaction log backups occur approximately every 10 minutes; PITR restores
  to any point in the retention window via that log chain. This sits under
  the 15-minute target with roughly a 5-minute typical margin, but Microsoft's
  own wording ("approximately," "depends on compute size and activity") means
  it is a documented typical figure, not a guaranteed bound — the residual
  risk is only closed by measuring an actual drill.
- RTO: no fixed same-region PITR figure is published for Azure SQL Database
  (only zone-HA/failover-group/geo-restore rows appear in the RTO/RPO table).
  The documented restore-time factors (size, compute size, log volume,
  activity replayed) all favor this tiny (~40 MB) database — restore is
  expected to complete in minutes, comfortably inside 4 hours — but this is
  an inference from documented factors and database size, not a measured
  result.

## Parked (operator approval needed)

`open-questions.md`: approve a one-off point-in-time restore drill into a new
database `pegasus-restore-drill-<date>` (an Azure write) to measure actual
RPO/RTO end to end. Exact target: subscription
`e6076573-23a5-46a8-acef-7e22d264e5db`, resource group `rg-pegasus-prod`,
server `pegasus-prod-sql-252ow37gij`. This is the only remaining gap between
documented/inferred posture and measured proof; everything else this ticket's
checklist and body ask for (posture verification, documented procedure,
approval boundary) is complete.

## Simplification pass

n/a — docs-only (recorded in `plan.md`).

## Not merged

PR left open for independent review per lane instructions; ticket moved to
Review, not Done.
