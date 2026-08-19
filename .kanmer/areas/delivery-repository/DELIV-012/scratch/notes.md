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

## Execution log — 2026-08-19 13:10–13:35Z

**Board takeover** (operator decision Q1): INTK-005, INTK-006, INTK-007, INTK-008 and TICK-045 force-taken from `Codex`/`codex-mcp-client` by `claude-code`, each left in `review`. Their existing pushed branches are kept because the open PRs point at them.

**Disk reclaim before the renderer work.** `C:` had only **7.0 GB** free and there is no Docker/Podman on this workstation, so the container route had to be measured rather than assumed. Measured the candidate bases with `oras manifest fetch` (no pull): `mcr.microsoft.com/playwright/dotnet:v1.61.0-noble` 13 layers / **1.32 GB compressed**, `mcr.microsoft.com/dotnet/aspnet:10.0` 0.10 GB, `mcr.microsoft.com/playwright:v1.61.0-noble` 0.93 GB. Fetched the Playwright .NET image's config blob: it carries **`DOTNET_VERSION=10.0.9`, `ASPNET_VERSION=10.0.9`, `PLAYWRIGHT_BROWSERS_PATH=/ms-playwright`, `ASPNETCORE_HTTP_PORTS=8080`, `APP_UID=1654`** — so it is a viable `ContainerBaseImage` for a .NET 10 ASP.NET app with browsers already installed, and the SDK-container route can produce a Chromium-capable image without Docker and without the prohibited `az acr build`.

Then ran Wave D's worktree cleanup early for the seven merged worktrees (each verified `0` commits ahead of `origin/dev` and clean): `deliv-011-release-11`, `plat-006-shell-upload`, `tick-033`, `tick-043-mailbox-identity`, `tick-044-classification-catalogue`, `tick-046-classification-history`, `tick-093-versioned-repair-spec`. Their branches remain on `origin` and are deleted later in Wave D. The release-11 artefacts in `deliv-011-release-11` (506 MB) were **not** copied out: release 11 never deployed, so they prove nothing — the record lives in the archived ticket. Cleared the NuGet HTTP cache. Free space **7.0 GB → 28.1 GB**.

**Agents dispatched** (all Sonnet, each in its own worktree, none may merge or open a PR):

| Lane | Branch | Scope |
|---|---|---|
| Grants + guard + docs | `task/deliv-012-grant-and-docs-fixes` | GRANT on `CaseRepairSpecifications`; census entry in `Invoke-AzureDatabaseBootstrap.ps1`; new `scripts/Test-MigrationGrants.ps1` CI guard so a `CreateTable` without a matching GRANT fails; `current-architecture.md:85` and `operations.md:278` drift |
| TICK-045 | `task/tick-045-shared-classification-policy` | Give `MailOperationalDestinationPolicy` a production caller; make the test drive the **registered** policy instead of seeding a fabricated result; remove the fabricated `claims@collisionengineers.co.uk`; make the MAIL-03 capability note match the evidence |
| INTK-005 | `intk-005-grouped-upload` | 4 blockers (ordinal-0 token rewrite, unconditional group redirect, missing GRANTs, migration list) + 4 unaddressed review comments |
| Repair-spec store | `task/deliv-012-wire-repair-spec-store` | Register `IRepairSpecificationStore` and route `EfCaseAssessmentStore`'s inline access through it, or remove the abstraction |
| Renderer container | `task/deliv-012-renderer-container` | `ContainerBaseImage` on the Playwright .NET base, real `Build-ReleaseArtifacts` run, `oras` evidence that Chromium is in the produced image, local renderer tests, sizing recommendation (no `infra/` edit) |

Still to dispatch: the report-draft operator entry point (the renderer's missing caller — there is currently **no** projection from a real case/assessment to `AssessmentReportSnapshot`, so this is genuine RPT-01/DOCS-001 scope), then INTK-006, INTK-008, INTK-007.
