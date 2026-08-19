## Baseline snapshot — 2026-08-19 12:15Z (before any action)

- `origin/main` = d8de29cb (release 10, deployed 2026-08-18); `origin/dev` = 4ba63888 (35 commits ahead, main is an ancestor). First-parent merges on dev since main: PRs 407 408 409 411 412 413 414 415 418 419 421.
- Open PRs: #416 INTK-005, #417 INTK-006, #420 TICK-093, #422 TICK-045, #423 INTK-008, #424 INTK-007 (all → dev); #410 dev→main (release CI vehicle, head feda958f — stale vs dev).
- Remote branches beyond main/dev/kanmer-board: intk-005/006/007/008, task/plat-006-shell-upload, task/tick-033-request-upload-reconciliation, task/tick-043-mailbox-identity, task/tick-044-classification-catalogue, task/tick-045-shared-classification-policy, task/tick-046-classification-history, task/tick-093-versioned-repair-spec.
- Local branches: the same plus task/deliv-011-release-11 (never pushed). Worktrees: main checkout (dev, `.codex/config.toml` modified — not ours), `.worktrees/kanmer` (board), `.worktrees/intk-005..008`, `../pegasus-worktrees/{deliv-011-release-11, plat-006-shell-upload, tick-033, tick-043-mailbox-identity, tick-044-classification-catalogue, tick-045-shared-classification-policy, tick-046-classification-history, tick-093-versioned-repair-spec}`.
- Board: review = INTK-005/006/007/008, TICK-045, TICK-093 (all taken); verifying = TICK-046, TICK-043, PLAT-006 + 2 others; implementing = TICK-015 (not ours, untaken) and DELIV-011 (now archived/superseded).
- Research lanes dispatched 12:15Z: current-estate (Azure R/O), codebase-evidence (git/gh R/O), recent-tickets (Kanmer/gh R/O).

## Read-only production preflight checks — 2026-08-19 12:40Z (run by me, not a subagent)

`Invoke-Sqlcmd` against `pegasus-prod-sql-252ow37gij/pegasus` with an Entra token, SELECT only:

| Check | Result | Why it matters |
|---|---|---|
| Duplicate `(MailboxId, UPPER(TRIM(InternetMessageIdentity)))` groups in `RetainedMailboxMessages` | **0** (10 rows total) | TICK-043's migration `20260819093019` creates a **unique** filtered index on exactly that pair — a non-empty result would fail the bundle mid-apply. Safe to apply. |
| `CaseEstimateLines` rows / distinct cases | **0 / 0** | TICK-093's migration `20260819112640` backfills one `Draft/LegacyUnresolved` `CaseRepairSpecifications` row per case with estimate lines — in production that backfill is a **no-op**, so the migration itself cannot fail on data. |

Consequence: the missing `GRANT` on `CaseRepairSpecifications` does **not** break the migration; it breaks the **runtime** Web path. `EfCaseAssessmentStore.SaveAsync` (origin/dev, lines ~117-135) does `context.CaseRepairSpecifications.AnyAsync(...)` (SELECT) and `.Add(specification)` (INSERT) whenever an assessment is saved with estimate lines. Under per-table least privilege `pegasus_web_runtime_role` has no permission on that table, so the first real assessment save in production would throw a SQL permission error. Confirmed blocker for release 12; remediation = a new migration granting SELECT/INSERT/UPDATE (+ DENY DELETE, per the convention in `20260819104953_MailClassificationCorrectionHistory.cs:100-105`) and extending the census in `scripts/Invoke-AzureDatabaseBootstrap.ps1`.

Note: `Invoke-AzureDatabaseBootstrap.ps1` builds its expected census by parsing `20260729199000_RuntimeRoleReconciliation.cs` plus hard-coded additions — a table with **no** grants appears in neither the expected nor the actual set, so the bootstrap assertion cannot catch this class of omission. Same for the CI census test. Worth recording in the release docs.
