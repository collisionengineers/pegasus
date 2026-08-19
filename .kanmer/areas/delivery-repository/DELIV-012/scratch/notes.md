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

## Lane results

### Repair-specification store wiring — DONE, PR #425 opened (2026-08-19 13:35Z)

Branch `task/deliv-012-wire-repair-spec-store`, head `2d410159`. Took the "register and route through the store" path rather than deleting the abstraction. The agent found — and I accept — that `StartDraftAsync` cannot be called literally from `EfCaseAssessmentStore.SaveAsync`: it requires an Engineer actor unconditionally while the implicit legacy-draft path is also exercised by the **Automation** actor (existing test `AutomationSaveIsUnconfirmedAttributedAndParityLoggedWithAStaffSave` proves it), and it opens its own transaction and bumps the workflow version, which would double-guard inside `SaveAsync`'s already-open serializable transaction. So the duplicated logic was extracted to `internal static` members (`DraftQuery`, `AcceptedQuery`, `NewLegacyDraft`) shared by both, and `EfCaseAssessmentStore` took a real `IRepairSpecificationStore` dependency for its read path. No optional parameters, no wrapper result types — the anti-patterns the repo rails name.

Evidence: Release build 0/0; Core 640/640; Architecture 97/97 (dependency direction intact); `AssessmentPersistenceIntegrationTests` + `RepairSpecificationMigrationTests` **7/7 against LocalDB** — real execution, not skipped. The TICK-093 migration file was confirmed untouched, so it cannot conflict with the grants lane.

### Mail destination policy — the "no caller" claim was wrong, lane redirected

The TICK-045 lane reported that `MailOperationalDestinationPolicy` has no production consumer to wire, because the categorised queue UI is capability UI-14 / MAIL-02, scheduled "Next / 0.3.0". That is true of UI-14 — but TICK-044's own `open-questions` records the operator's instruction verbatim: *"the retained mailbox viewer is meant to show this information… A policy referenced only by tests is incomplete and must not pass review as delivered."* The retained mailbox viewer (`/Inbox/{id}`, `Pages/Mail/Message.cshtml`) already exists, so the caller the operator asked for is available without building UI-14. Lane redirected to surface the policy-derived destination on that page, with the label taken from `OperatorLabels` rather than new literal copy, and a test so the caller cannot silently regress. It must report the label key it used so [[INTK-007]] can complete the "Needs sorting" → "Unidentified" rename when it merges last.

## Git hygiene done early (safe, all verified merged)

- Deleted local **and** remote branches for the seven merged tickets: `task/plat-006-shell-upload`, `task/tick-033-request-upload-reconciliation`, `task/tick-043-mailbox-identity`, `task/tick-044-classification-catalogue`, `task/tick-046-classification-history`, `task/tick-093-versioned-repair-spec` (remote+local) and `task/deliv-011-release-11` (local only — it was never pushed). Each verified `0` commits ahead of `origin/dev` first.
- Deleted the stray local branch `pr417check` (head `599bfe6d`, identical to INTK-006's tip, no unique commits, no ticket) — the review branch the research pass flagged.
- Remote branches now: `main`, `dev`, `kanmer-board`, the four `intk-00N-*` PR branches and `task/tick-045-shared-classification-policy`, plus the DELIV-012 working branches as they push.

## Board truthfulness corrections

- **PLAT-001** → `deployment: production`. Verified: PR #397's merge commit `5ab3b773` is an ancestor of `origin/main` (`d8de29cb`), so the Claude Design shell has been live since release 10.
- **TICK-211** → `deployment: n/a` (a zero-diff decision record).
- **PLAT-006, TICK-033, TICK-043, TICK-044, TICK-046** → `deployment: not-deployed`, recording the state positively instead of by an empty field. All five merged after release 10 and ship with release 12.
- **TICK-011** still to correct: its proof cites `ae6f0c2d` and `f7d99b18` as ancestors of the deployed commit, but both are unreachable from any ref — I confirmed the reachable delivery commits are `ef3eb4c7` and `ba65c1ed`, both ancestors of `origin/main`. Its `deployment: not-deployed` is also wrong: the ImageIntake source, migration, Web pages and tests are all in the deployed release-10 tree; what the ticket means is "no live caller", which is an activation fact, not a deployment one.

### Migration grants + CI guard + docs drift — lane returned, one NEW live defect found

Branch `task/deliv-012-grant-and-docs-fixes`, head `98c8b041`.

- **TICK-093 blocker fixed.** `GRANT SELECT, INSERT, UPDATE` + `DENY DELETE` on `CaseRepairSpecifications` to `pegasus_web_runtime_role`, added to `20260819112640_VersionedRepairSpecifications.cs` `Up()` with the same provider guard as `20260819104953`. Permissions justified per operation from `EfCaseAssessmentStore` (SELECT via `AnyAsync`, INSERT via `Add`) and `EfRepairSpecificationStore` (SELECT/INSERT/UPDATE, never `Remove`). No worker grant — the Worker reaches neither store; only `Pages/Cases/Assessment/Index.cshtml.cs` and `Mcp/AssessmentMcpTools.cs` do. Designer and snapshot untouched.
- **New CI guard** `scripts/Test-MigrationGrants.ps1`, wired into the `changes` job. It parses `CreateTable(name: "X")` out of each `Up()` and fails unless the file grants `X` or carries an explicit opt-out marker; it also recognises the older interpolated-helper grant style so it does not raise false positives. Self-tested against a synthetic ungranted table (failed as designed) and the real tree (passes, 48 files). 65 tables across 16 pre-least-privilege migrations were exempted — each confirmed present in `20260729199000_RuntimeRoleReconciliation.cs`'s own grant arrays, so they are covered elsewhere rather than ungranted.
- **NEW LIVE PRODUCTION DEFECT — `EvaHandoffDownloadOperations`.** The guard caught a table created by `20260811122654_CaseCustodyEvaRecovery.cs` with no grant anywhere in the tree. **I verified it against the production database directly** (read-only, `sys.database_permissions` joined to `sys.database_principals`):

  | Table | Web role | Worker role |
  |---|---|---|
  | `EvaHandoffOperations` | GRANT SELECT, GRANT INSERT, DENY DELETE | DENY DELETE |
  | `EvaHandoffRevisions` | GRANT SELECT, GRANT INSERT, DENY DELETE | DENY DELETE |
  | **`EvaHandoffDownloadOperations`** | **no permission rows at all** | **none** |

  The table exists in production, the migration is applied (head is later, `20260814094632`), and `EvaHandoffStore.cs:194` reads it while `:272` inserts into it, reached from `Pages/Cases/Vehicle.cshtml.cs`. So the EVA hand-off download path fails with a SQL permission error **in the currently deployed release 10** — this is not a release-12 risk, it is already broken. A follow-up migration is being added on the same branch to fix it in this release; because the original migration is applied, it cannot be edited in place.
- **Docs drift corrected.** `current-architecture.md:85` now states the `/Inbox/{id}` POST handler and the real Web grants; line ~423 was checked and is about a different table pair, so it was correctly left alone. `operations.md:278` "min 0 max 1 — cold start accepted" → "min 1 max 1 — no scale-to-zero, no cold start", verified against `infra/modules/platform.bicep:461-462`.
- The agent caught its own bug in self-review: a Python marker-insertion script had stripped the UTF-8 BOM from 16 migration files; restored before commit, so the diffs are comment-only.
- Evidence: Release build 0/0; `Test-MigrationGrants`, `Test-DocumentationLinks`, `Test-CiChangeFlags` pass; Core 640/640; Architecture 97/97; `IntakePersistenceIntegrationTests` **9/9 against LocalDB**, exercising the full migration chain including the new GRANT.

### RELEASE GATE BROKEN ON `dev` — found 2026-08-19 ~14:00Z

The renderer-container lane hit it and I reproduced it directly in the release worktree at `560f741c`:

```
pwsh ./scripts/Test-AzureDeploymentPlan.ps1 -Mode Local
Exception: Test-AzureDeploymentPlan.ps1:53
  Database bootstrap must account for grant-carrying migration
  20260819104953_MailClassificationCorrectionHistory.cs.
EXIT=1
```

`-Mode Local` and `-Mode Artifact` are mandatory steps of the release route, so **release 12 could not have proceeded from current `dev` at all.** TICK-046 (PR #418) merged a grant-carrying migration without updating `scripts/Invoke-AzureDatabaseBootstrap.ps1`'s expected census, and `Test-AzureDeploymentPlan.ps1` asserts every such migration is accounted for. Pre-existing on `dev`, not caused by any of my lanes. Assigned to the grants lane (same file, same defect class) with instructions to also sweep every other migration containing `GRANT` so we find them all in one pass rather than one per release attempt.

Worth noting what this says about the release route: the route's own preflight caught a defect that CI did not, because CI never runs `Test-AzureDeploymentPlan`. That is a gap worth a follow-up ticket after the release.

### Renderer container — DONE, Chromium proven in the image

Branch `task/deliv-012-renderer-container`, head `f1f439b8`.

- `Directory.Build.props` gains `<PlaywrightVersion>1.61.0</PlaywrightVersion>`; the Infrastructure `PackageReference` and the new `<ContainerBaseImage>mcr.microsoft.com/playwright/dotnet:v$(PlaywrightVersion)-noble</ContainerBaseImage>` in `Pegasus.Web.csproj` both derive from it, so a Playwright bump cannot silently desynchronise the package from the base image. No `ContainerUser`/port overrides — the built image proved it inherits `User: 1654` and `ExposedPorts: 8080/tcp` correctly.
- `Build-ReleaseArtifacts.ps1` ran end-to-end (exit 0): image digest `sha256:0772d6ee…`, `linux/amd64`, archive 1.36 GiB, total artefact footprint ≈1.9 GiB.
- **Chromium evidence**, since no Docker exists to run the image: `oras manifest fetch --oci-layout` → 14 layers (13 base + 1 app); the config blob confirms `Entrypoint=["dotnet","/app/Pegasus.Web.dll"]`, `ExposedPorts 8080/tcp`, `PLAYWRIGHT_BROWSERS_PATH=/ms-playwright`, `APP_UID=1654`; replaying `config.history` against `rootfs.diff_ids` identified the exact Chromium layer built by `playwright.ps1 install --with-deps` — digest `sha256:2c236c77…`, 776 MB.
- **Renderer proven against a real Chromium locally**: `AssessmentReportRendererTests` **6/6 passed** in 29 s after `playwright.ps1 install chromium`.
- **Sizing recommendation (bicep deliberately unchanged):** the Web Container App runs 0.5 vCPU / 1 GiB, min=max=1. The lane recommends 1.0 vCPU / 2 GiB — Container Apps hard-OOM-kills rather than throttling, and 1 GiB is tight for ASP.NET Core plus headless Chromium; ≈$15.77/month → ≈$31.54/month at UK South retail. **This is an operator cost decision and an `infra/` change that would alter the `azd provision --preview` expectations, so it is not being made unilaterally.**

### INTK-005 — DONE, all four blockers plus a gap the brief missed

Branch `intk-005-grouped-upload`, head `d70118b1`, PR #416 updated.

- Ordinal-0 token rewrite fixed (`GroupedIntake.cs:143` — parent token verbatim for ordinal 0). One-member groups redirect to `/Upload/Status/{id}` again; only multi-file groups go to `/Upload/Group/{groupId}` (`Upload.cshtml.cs:127-141`). GRANT SELECT, INSERT added for the Web role on both new tables, evidenced from `EfIntakeSubmissionGroupStore` (no UPDATE/DELETE) and a grep showing zero Worker references. Batch limit now derives from one `MaximumBatchFileCount` constant reused in the page copy. `data-auto-refresh` added to the group status page. Retry shape copied from `EfIntakeWorkStore.ReceiveWithRetryAsync` into both `AddMemberAsync` and `GetOrCreateAsync`. All five Codex comments applied.
- **The dev merge silently ate this branch's own migration id** — no conflict markers, because dev's newer ids landed on adjacent lines and git took dev's list wholesale. The lane caught it and re-inserted `20260819101344_GroupedIntakeSubmission`. Exactly the "nothing inadvertently overwritten" risk this ticket exists to prevent, and a warning for the remaining INTK merges.
- **Gap found beyond the brief:** `ListMembersAsync` hardcoded `IsDuplicate=false` and the replay branch never called `IIntakeSubmission`, so the duplicate notice stayed hidden even after the redirect fix. Fixed properly by tracking `IsDuplicate` per ordinal.
- Evidence: build 0/0; Core 644/644; Architecture 97/97; the four named integration classes 30 passed / 0 failed / 6 pre-existing QDOS-corpus skips; `GroupedIntakeWebTests` 1/1. Full-solution `dotnet test` honestly not run (~28 min) and left unticked. Checklist 7/33 → 28/38.
- Flagged for reconciliation: `intk-006` independently fixed the same redirect symptom by bypassing the group path for single files. INTK-005 kept every upload flowing through `IGroupedIntakeSubmission`. INTK-006 must adopt INTK-005's approach on rebase, not re-apply its own.

### TICK-045 — the MAIL-02 caller landed, with falsifiability proven

The lane wired `MailOperationalDestinationPolicy` into the retained mailbox viewer, which is the caller TICK-044's operator ruling asked for:

- `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs` — thin static `Destination(MailClassificationResult) => MailOperationalDestinationPolicy.Map(result)`, called from the view inside the existing "Classification evidence" panel, rendering an "Operational destination" row and the destination policy key/version. Pure derived value from the already-loaded dossier — no new persistence, no new panel style, reusing the page's existing `<dl class="detail-list">` convention.
- New `OperatorLabels.MailOperationalDestinationLabel(...)`. The abstention arm maps to the literal `"Needs sorting"` — deliberately the **same string the page already renders** via `QueueLabel` and `OutcomeLabel`, so no new operator-visible spelling was introduced ahead of INTK-007's vocabulary migration.

**What earns confidence here:** the lane proved its own tests can fail. It temporarily made `Destination()` return a hardcoded mapping, watched both new tests fail on the exact rendered `<dt>Operational destination</dt>` string, reverted (confirmed by `git diff` returning to a 9-line net addition) and reran green. That is the opposite of the original PR #422, whose test could not fail for the reason it claimed. Release build 0/0, Core 640/640, targeted integration 31/31.

**Reachability list handed to [[INTK-007]]** for the `Needs sorting` → `Unidentified` rename — `MailOperationalDestinationPolicy.cs:37` (abstention branch, pre-existing), the new `OperatorLabels.MailOperationalDestinationLabel` switch arm, the new `Message.cshtml.cs` helper, the live `Message.cshtml` row at `/Inbox/{id}`, and two test locations.

**Owed to [[TICK-044]] and not yet done:** the caller landed on TICK-045's branch, not TICK-044's, so TICK-044's checklist items — "Wire MailOperationalDestinationPolicy into the retained-mail projection", "Display both values in the retained mailbox viewer", and its acceptance-test item — are now satisfied by a diff on another ticket. I must reconcile TICK-044's checklist and `open-questions` against the merged TICK-045 diff **after** #422 merges, and record where the work actually landed. Until then TICK-044 must not clear `verifying`, per its own operator bar that a policy referenced only by tests must not pass review as delivered.

**Capabilities wording:** MAIL-02's row now names the real caller and the exact page and states explicitly that UI-14 (categorised queues) remains undelivered; UI-14 was not upgraded. MAIL-03's row stays limited to what its classification test proves. Note `docs/capabilities.md` is also touched by INTK-007, which merges last — expect a conflict there and resolve by keeping both rows' honest wording.

### CI diagnosis — a stale PR merge ref, not a code failure (2026-08-19 ~14:20Z)

PR #425's `changes` job failed twice, each time at almost exactly 5 minutes, taking every downstream lane to `skipping`. The full job log shows the failure is inside `actions/checkout@v7` itself:

```
[command] git -c protocol.version=2 fetch --no-tags --prune --no-recurse-submodules origin
  +refs/heads/*:refs/remotes/origin/* +refs/tags/*:refs/tags/*
  +73018a965f7ff3bbad596e904f24ec5771c89cc4:refs/remotes/pull/425/merge
13:37:20  (start)
13:42:20  ##[error]The operation was canceled.
```

Five minutes of no progress on the fetch, then cancellation — and `Complete job` had to terminate orphan `git`/`git-remote-https` processes. Nothing in the repository or the diff is involved; the same `changes` job passed in **22 s** on PR #416 at the same moment, so it was not runner contention either. The constant across both failures was the pull-request **merge ref** `73018a96…`, which the fetch could not resolve.

Fix: closed and reopened PR #425, which made GitHub recompute the merge commit (`73018a96…` → `9c2dc00a…`). A fresh run started immediately and `documentation` passed in 28 s. Re-running the failed jobs of the old run could never have worked, because that run was pinned to the unresolvable ref — worth remembering the next time a job dies inside checkout rather than inside a step we wrote.

Most likely trigger: the six merged remote branches deleted earlier in this session went out while that run's merge ref was being computed. No action needed, but if it recurs on another PR, close/reopen is the remedy rather than `gh run rerun`.
