## Scope for this pass

Read-only verification of the actual Azure SQL backup/PITR posture for
`pegasus-prod-sql-252ow37gij/pegasus` (sub `e6076573-23a5-46a8-acef-7e22d264e5db`,
rg `rg-pegasus-prod`), and documentation of the restore procedure. A live
restore drill (creating a restored database) is an Azure write and is **not**
approved in this pass — it is parked as an open question for operator
approval. This ticket's checklist (`checklist.md`, migrated from [[TICK-190]]
and [[TICK-191]]) frames the "backup a template database" / "LocalDB
reclamation" checks in local-dev terms; those are already covered by the
existing [Local recovery](../../../../docs/runbook.md#local-recovery)
procedure and `LocalDbTemplateDatabaseTests` (see runbook "Locked restore,
build, and test"). This pass targets the still-open half of OPS-09: the
**production** Azure SQL posture and procedure, which is what the ticket body,
its capability entry (`docs/capabilities.md` OPS-09), and
`docs/runbook.md#production-recovery` actually gate on.

## Read-only Azure readback (2026-08-20, `az` CLI, sub e6076573-23a5-46a8-acef-7e22d264e5db)

`az sql db show -g rg-pegasus-prod -s pegasus-prod-sql-252ow37gij -n pegasus`:

- `edition`/`sku`: Standard, `S0` (10 DTU)
- `maxSizeBytes`: 268435456000 (250 GB)
- `currentBackupStorageRedundancy` / `requestedBackupStorageRedundancy`: `Geo`
- `zoneRedundant`: false
- `earliestRestoreDate`: `2026-08-13T05:31:50.870670+00:00`
- `creationDate`: `2026-08-01T20:46:49.587000+00:00`
- `defaultSecondaryLocation`: `ukwest` (primary `uksouth`)
- `status`: Online

`az sql db str-policy show` (short-term retention = PITR window):

- `retentionDays`: 7
- `diffBackupIntervalInHours`: 24

`az sql db ltr-policy show` (long-term retention):

- weekly/monthly/yearly retention all `PT0S` — LTR is **not configured**. Only
  the 7-day PITR window exists; there is no monthly/yearly backup retained
  beyond that.

`az sql db list-usages` (current size):

- `database_size`: 41,418,752 bytes (~39.5 MiB)
- `database_allocated_size`: 50,331,648 bytes (~48 MiB)

`az sql server show`: Entra-only authentication
(`azureAdOnlyAuthentication: true`), admin `digital@collisionengineers.co.uk`,
no SQL password login. Matches `scripts/Invoke-AzureDatabaseBootstrap.ps1`'s
existing `Invoke-Sqlcmd -AccessToken (az account get-access-token
--resource https://database.windows.net/)` connection pattern — restore
verification must use the same Entra access-token pattern, not a SQL login.

Cross-check: `earliestRestoreDate` (2026-08-13) is exactly 7 days before the
readback date (2026-08-20), consistent with the 7-day `retentionDays` policy
and confirming PITR is live and the retention window is real, not just
configured-but-inactive.

## Documented RPO/RTO figures (Microsoft Learn, cited not assumed)

- **Backup cadence** (Azure SQL Database, single database — "Automated
  backups in Azure SQL Database"): full backup weekly, differential every 12
  or 24h (this database: 24h, matching the DTU-model default), **transaction
  log backups approximately every 10 minutes**. PITR restores by replaying
  the log chain to any second within the retention window.
- There is no published fixed RPO number for same-region single-database PITR
  in the Business Continuity RTO/RPO table (that table only states figures for
  zone-redundant HA, failover groups/active geo-replication, and geo-restore).
  The 10-minute figure comes from the backup-frequency doc, not the BC/DR
  table, and Microsoft states the log-backup interval "depends on the compute
  size and the amount of database activity" — it is a documented typical
  figure, not a contractual SLA bound.
- **Honest RPO verdict**: the ~10-minute typical log-backup interval sits
  under the 15-minute RPO target with roughly a 5-minute margin, but is not a
  hard guarantee for every failure instant (Microsoft's own wording is
  "approximately"). Meets the target under documented typical behavior;
  worst-case log-interval drift is the residual risk, which only a measured
  drill (parked, see open-questions) can bound empirically for this
  database's actual activity pattern.
- **RTO factors** (Microsoft "Recovery time" for Azure SQL Database restore):
  database size, compute size, transaction-log volume, activity replayed,
  cross-region bandwidth (N/A — same-region restore), and concurrent restore
  requests in the target region (limit: 30 concurrent per subscription). "For
  a large or very active database... the restore might take several hours."
  This database is ~40 MB (tiny) at S0 — none of the size/activity factors
  that drive multi-hour restores apply. A same-region PITR restore of a
  database this size is expected to complete in minutes, comfortably inside
  the 4-hour RTO target, but this is an inference from documented factors and
  database size, not a measured result — the actual wall-clock restore time
  is the other half of what only the parked drill measures.

## Existing repo material reused

- `docs/runbook.md#production-recovery` already states the 8-step production
  recovery contract (approval, inventory, preserve-source-restore-into-new-target,
  migrations, reconciliation, health checks, evidence recording, retention) and
  the 15-min/4-hour objectives. This pass adds the missing **exact commands**
  under that existing contract rather than inventing a second procedure.
- `scripts/Invoke-AzureDatabaseBootstrap.ps1` supplies the working
  `Invoke-Sqlcmd -AccessToken` pattern for verification queries against the
  Entra-only server — reused rather than inventing a new connection method.
- `docs/operations.md#recovery` already states no exercise has completed and
  the proof is deferred/non-blocking (2026-08-03) — this pass records the
  measured *posture* (retention, redundancy, restore point) there without
  changing that deferred status, since the drill itself remains unrun.
- `docs/runbook.md#live-operation-approval-matrix` already covers "Deploy,
  restore, fail over, or retire" as a live-approval-gated action — the new
  procedure text cites this row rather than inventing a second approval rule.

## What this pass does NOT do (parked)

- No database is restored. No Azure write of any kind was made.
- The drill ("Approve a one-off point-in-time restore drill into a new
  database... to evidence RTO/RPO end to end") is recorded as an open,
  unticked question — it requires operator approval per the live-operation
  approval matrix (`docs/runbook.md#live-operation-approval-matrix`, "Deploy,
  restore, fail over, or retire" row).
