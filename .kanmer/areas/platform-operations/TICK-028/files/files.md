## Files touched (docs-only)

- `docs/runbook.md` — extend `#production-recovery` (owns operational
  procedure) with an exact-command point-in-time restore procedure: `az sql
  db restore`, verification (row counts, `__EFMigrationsHistory` head, app
  smoke) using the existing `Invoke-Sqlcmd -AccessToken` pattern, and the
  approval boundary citing the live-operation approval matrix.
- `docs/operations.md` — record the measured posture (retention days,
  backup storage redundancy, earliest restore point, database size, SKU) in
  `#recovery` (current-state facts; owns no procedure).
- `.kanmer/**/TICK-028/open-questions.md` — new, park the restore-drill
  approval question, unticked.
- Ticket pipeline docs (`research.md`, `files.md`, `plan.md`,
  `post-implementation-report.md`, `proof.md`) under this ticket's own
  Kanmer folder.

No application/infrastructure code changes. No new top-level directory,
migration, or ADR — this documents an existing runbook contract
(`docs/runbook.md#production-recovery`) and existing capability entry
(`docs/capabilities.md` OPS-09); it does not invent new product scope.
