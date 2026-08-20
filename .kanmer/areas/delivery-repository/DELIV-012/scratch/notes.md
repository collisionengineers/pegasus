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

### A systemic gap, not a one-off: grant-carrying migrations vs the bootstrap census

Fixing TICK-046's unaccounted migration was not the end of it. `Test-AzureDeploymentPlan.ps1 -Mode Local` requires **every** migration containing a `GRANT` to be named in `scripts/Invoke-AzureDatabaseBootstrap.ps1`. Checking the branches queued for this release:

| Branch | Migration | Carries GRANT | Census entry |
|---|---|---|---|
| `intk-005` (#416) | `20260819101344_GroupedIntakeSubmission` | yes (2) | **missing** |
| `intk-006` (#417) | shares INTK-005's | yes | inherits |
| `intk-008` (#423) | `20260819112914_ImageInitiatedLifecycle` | yes, once its blocker is fixed | **missing** |
| `intk-007` (#424) | `20260819115323_UnidentifiedWork` | yes, once its blocker is fixed | **missing** |

So merging any of them as-is would re-break the release gate the moment it landed — each one individually, in the same way TICK-046 did. And **CI would not have caught any of it**, because CI never ran `Test-AzureDeploymentPlan` at all.

Two things done about it:

1. **Added `-Mode Local` to the always-on `changes` job in CI** (`ci.yml`, on the #426 branch, commit `5c24e61e`). It needs no cloud credentials — only `az bicep build` — so it belongs there. From now on a PR that adds a grant-carrying migration without its census entry fails CI instead of silently poisoning the next release. This closes the gap that let the release route's own preflight sit broken on `dev` with nobody knowing.
2. **Added INTK-005's census entries myself** (`intk-005-grouped-upload` → `0f71ee60`): Web `SELECT`/`INSERT` on `IntakeSubmissionGroups` and `IntakeSubmissionGroupMembers`, nothing for the Worker, evidenced by `EfIntakeSubmissionGroupStore` doing no UPDATE or DELETE and no Worker reference existing. Done on that branch rather than in #426 because each PR must carry its own entries, and because INTK-006 rebases onto INTK-005 and needed it in place first.

Messaged the INTK-006, INTK-007 and INTK-008 lanes with the new requirement, the exact format, and the caveat that `-Mode Local` will keep failing on the *unrelated* `20260819104953` name until #426 merges — so they do not chase someone else's defect.

**Forced merge order, consequently:** #426 first. Nothing else can verify its own gate until the `dev` baseline is repaired.

### CI failure triage — the same allocation test, twice more

PR #426's `sql-integration (2)` failed on `QdosAllocationRecoveryTests.DistinctParallelRetriesResolveToOneCaseAggregate` with the *assertion* symptom (`1 out of 2 items did not pass`), while PR #425 failed the same test with the *deadlock* symptom. #416, #422 and #425 (on re-run) all passed it. Two symptoms, one underlying concurrency defect, and it predates every release-12 branch — filed as **CASE-005** with the run evidence. #426 touches only migrations, scripts and docs, so it cannot be implicated. Re-running rather than treating it as a blocker, and recording honestly that a re-run was needed.

### INTK-006 — DONE, and it found a second real bug

Branch `intk-006-grouped-image-routing`, head `caef9dff`, PR #417.

**The blocker is fixed properly.** `TryRegisterAndAssociateAsync` now takes the group's routing decision and only runs the per-member candidate search when the decision is `AssociateExistingCase` (or there is no group decision). Test `AmbiguousGroupEligibilityHandsOffDespiteAPerMemberExactMatch` builds a two-member group whose accepted VRM has both an exact and a fuzzy candidate — so the group-level count is ambiguous and the decision is hand-off — and asserts no auto-link is written while both members still register. That is the product invariant: ambiguity fails closed to hand-off and a per-member fuzzy match can no longer overrule it.

**A second bug, found while verifying the first.** `SubmitGroupedIntake` submits ordinal 0 under the **bare** token (INTK-005's fix), but `TryApplyGroupAsync`'s member lookup always queried `{token}:{ordinal}`. So a real multi-member group could never find its own first member and would **wait forever**. Fixed by extracting one shared `GroupedIntakeMemberToken.Create` used by both callers, and proved by `OneEligibleCaseAssociatesEveryGroupMember` (both members register *and* associate). This is a good argument for the sequencing decision: INTK-006 was made to adopt INTK-005's single-path approach rather than keep its own bypass, and the incompatibility surfaced immediately instead of in production.

**The merge-list warning earned its place.** The lane did not trust the clean merge: it generated a file list from `Migrations/` and diffed it against the string literals in `IntakePersistenceIntegrationTests`, confirming an exact match at 49 migrations after the merge and 50 after adding its own, with exactly one copy of `20260819101344_GroupedIntakeSubmission` surviving and carrying INTK-005's GRANTs. Two real conflicts were resolved by hand (`EfIntakeSubmissionGroupStore.cs`, the migration list).

Other fixes: single-file bypass removed so every upload flows through `IGroupedIntakeSubmission`; recognition now reuses a recorded suggestion instead of re-running ONNX per trigger (`RecognitionRunsOnceEvenWhenTheGroupIsTriggeredTwice` — engine called twice total for two members across two triggers, not four); grouped-routing decision table written into `frd-02` with diagnostics in `frd-06` and registered in `capabilities.md`; a new migration `20260819140113_ImageIntakeGroupExpectedMemberCount`.

**Dispositions:** 12 of 13 reviewer comments fixed or verified already-correct; #10 (retry incomplete group registration) explicitly **not fixed**, with a recorded reason — it needs a durable per-group outcome record, delegated to INTK-008. That is an honest unapplied finding rather than a silent gap.

Checklist 26/41 → 30/41, every remaining item carrying a Progress note.

Evidence: build 0/0; Core 653/653; Architecture 97/97; filtered integration 34 passed / 6 pre-existing skips / 0 failed. `-Mode Local` still fails only on the pre-existing `20260819104953` name — correctly identified as not theirs.

Note its `infrastructure` CI job fails for the same pre-existing reason, which incidentally shows the gate *was* already wired into that path-gated job; what was missing is that the job does not run for every change set, which is why TICK-046's migration slipped through. The new always-on `changes` step closes that.

### Q4 Sent-evidence approval — how it will actually be applied (decided before the release window)

The plan's §3 assumed a SQL data write. Checking the code first changed that:

- `PollSentEvidence.ExecuteAsync` (`PollSentEvidence.cs:213-219`) asks `approvedMailboxPolicy.IsApprovedAsync(mailbox, ApprovedMailboxRouteScope.SentEvidence)` and throws if false.
- `ApprovedMailboxAdministration` (`:144-157`) **fails closed**: an `Approved` row with the `SentEvidence` scope and **no `SentFolderIdentity` is refused**. Production's row has `AllowSentEvidence=0` and `SentFolderIdentity=NULL`, so a bare `UPDATE ... SET AllowSentEvidence=1` would produce exactly the inconsistent state the policy forbids.
- There is a **real administration surface**: `/Administration/Mailboxes` (`Pages/Administration/Mailboxes.cshtml.cs`, `OnPostUpdateAsync` with `SelectedRouteScopes` and `SentFolderIdentity`), which goes through `EfApprovedMailboxStore` with version/replay checks and writes the approval history.

So the approval is applied **through the application**, as a signed-in administrator action on production during §7 verification — not by raw SQL. That keeps the change inside the product's own validation, records the actor and time in the mailbox history, and is itself a verification of the admin page on the newly deployed build. The Sent folder identity to enter is the one the Worker already runs with (`Graph__SentFolderId`, present in the Function App settings); I will read it from the live settings at the time rather than guess.

Expected observable effect afterwards: the once-a-minute `UnauthorizedAccessException` from `SentEvidencePollFunction` stops, and `ApprovedSentPollStates` advances. That is the proof line for Q4.

### Release worktree staged

`../pegasus-worktrees/deliv-012-release-12` now carries the azd environment (`.azure/config.json`, `.azure/pegasus-prod/{.env,.env.lock,config.json}`) copied from the main checkout — **placed correctly this time** (not nested), 48 values readable via `azd env get-values`, and `git status` clean because `.azure/pegasus-prod` is ignored. It will be fast-forwarded to the final `dev` head at preflight.

## Docs-refresh draft for release 12 (placeholders {SHA}, {SHA12}, {DIGEST}, {MSHA}, {DATE} filled at release time)

**operations.md release table new row:**
`| 12 | {DATE} | \`{SHA8}…\` | \`sha256:{DIGEST8}…\` | \`pegasus-prod-web-252ow37gij--{SHA12}\` | \`20260819093019_RetainedMailboxInternetMessageIdentity\`, \`20260819101344_GroupedIntakeSubmission\`, \`20260819104953_MailClassificationCorrectionHistory\`, \`20260819112640_VersionedRepairSpecifications\`, \`20260819112914_ImageInitiatedLifecycle\`, \`20260819115323_UnidentifiedWork\`, \`20260819140113_ImageIntakeGroupExpectedMemberCount\`, \`20260819180000_GrantEvaHandoffDownloadOperations\` |`
(Eight migrations — re-derive the exact set at release time as `__EFMigrationsHistory` head → folder diff; the six-figure estimate from planning grew to eight with INTK-006's ExpectedMemberCount and the EvaHandoff grant migration.)

**"currently serves" sentence** → release 12.

**"What release 12 proved beyond smoke" bullet must include:** first release on the Playwright/dotnet Chromium base (image ~13× larger; revision start pull time observed to be recorded); Web container raised to 1.0 vCPU / 2 GiB; eight migrations applied in one bundle incl. two grant-repair migrations (one fixing a live production defect on `EvaHandoffDownloadOperations` — verified broken pre-release via sys.database_permissions, verified granted post-release); Sent-evidence approval applied through `/Administration/Mailboxes` (Q4) and the once-a-minute `UnauthorizedAccessException` stream stopping; the renderer executing in the deployed container (report-draft panel reachable; actual PDF generation blocked only by the estimate-import gap, ENG-002); Upload multi-file group flow, Unidentified queue with `U<n>` references, Image-initiated Case lifecycle pages live.

**current-architecture.md:** release sentence; renderer paragraph updated from "locally verified source" to deployed-with-caller (still gated on estimate import for real output; keep the honest qualification).

**runbook:** the "SentEvidencePollFunction stays disabled unless separately approved" sentence updated to record the 2026-08-19 operator approval and the applied mailbox state.

### The 423×424 semantic conflict — how the two vocabularies were merged (2026-08-19 ~22:00Z)

Merging `dev` (now carrying INTK-008) into INTK-007 produced the conflict the pairwise analysis predicted in `frd-02`/`frd-12`, plus a trivial usings conflict in `DurableIntake.cs`. The FRD hunks were **semantic**: each side carried one operator-confirmed vocabulary — INTK-008's "Image-initiated Case projection" with the two-outcome ruling, INTK-007's "Unidentified" replacing "Needs sorting". Auto-picking either side would have destroyed the other's confirmed truth.

Resolution, by hand:
- **frd-02**: dev's paragraph structure (Image-initiated Case projection, merge/staff-close, the verbatim two-outcome operator ruling) with its two remaining "Needs sorting" phrases migrated to the Unidentified vocabulary, followed by INTK-007's full "Unidentified destination and reference" section. Asserted no stale term survived in the merged hunk.
- **frd-12**: dev's "Image-initiated Cases" term in the dashboard-count sentence combined with INTK-007's "The Unidentified count is the exact count of open Unidentified items and links to that queue."
- Migration list: union, verified **53/53** against the folder.

Gates after the merge, all green: build 0 errors; `-Mode Local` pass; `Test-MigrationGrants` 53 files; `Test-DocumentationLinks` 205 files; the 142 focused Core tests (Unidentified + MailOperationalDestination + ImageIntake) pass. Pushed `b9a25a68`; #424 CI running on that head.

#423 merged at `a907ecd2` after 10/10 green (its own `dev` merge earlier needed a real constructor resolution: INTK-006 added `IIntakeSubmissionGroupStore? groupStore`, INTK-008 added `IImageIntakeCasePairing casePairing` — combined with `casePairing` required and `groupStore` optional-last, one positional call site fixed, 91 image-intake Core tests green). #428 rebased onto post-423 dev cleanly and is in CI.

## RELEASE 12 EXECUTION LOG — 2026-08-19 ~22:45Z onward

Operator authorisations received via the question tool:
- **"MERGE AUTH GRANTED"** for exactly `ed3be51c95bc2a055606e5210131d37de9de2dd1`.
- **All five Azure writes approved** for the exact stated targets (ACR push; efbundle + bootstrap on `pegasus-prod-sql-252ow37gij/pegasus`; `azd provision` with exactly two expected changes; worker config-zip; the Q4 Sent-evidence approval via `/Administration/Mailboxes`).

| Step | Result |
|---|---|
| E1 | PR #410 **11/11 SUCCESS** on `ed3be51c` |
| E2 | `origin/dev` == PR head == `ed3be51c`; `main` strict ancestor |
| E3 | `git push --atomic --force-with-lease=refs/heads/dev:ed3be51c origin ed3be51c:refs/heads/main ed3be51c:refs/heads/dev` → `d8de29cb..ed3be51c main`; readback **main == dev == ed3be51c** |
| E5 | Artifacts at `ed3be51c`: digest `sha256:6dcf3ca134052ebf4f52d5062f1e28944b47615332e555e5146b2ac838626034`, manifest SHA-256 `863602260A58FA421C9150122B417721B6C03BABE7BCE3D810013DC936AFFAA7`, migrationIdentity `20260819180000_GrantEvaHandoffDownloadOperations`; web-image.tar.gz 1.36 GiB, efbundle 346 MB, web/worker zips ~101 MB. Local + Artifact modes pass |
| E6 | `azd env refresh` OK (48 values); PreUpload pass |
| E7 | Prod head `20260814094632` (8 pending); duplicate canonical Message-IDs 0; `CaseEstimateLines` 0; `IntakeReceipts` 12 |
| E4 | main-push `repository-check` run `32309456172` — watching |
| E8 | `oras cp` to `pegasusprodacr252ow37gij` — running |

### Release execution continued

| Step | Result |
|---|---|
| E4 | main-push `repository-check` run `32309456172` — completed (see next readback) |
| E8 | `oras cp` succeeded; ACR digest readback `sha256:6dcf3ca1…` — **matches the manifest exactly** |
| E9a | PreMigration pass |
| E9b | `efbundle` applied all 8 migrations; transcript saved to `artifacts/releases/0.1.0-alpha.1/migration-transcript.txt`. Two false starts, both environmental and instructive: (1) `azd env get-value` for a key that does not exist (`AZURE_WEB_CLIENT_ID`) returns the CLI's update-notice text, which then failed Guid validation — the real key is `WEB_IDENTITY_CLIENT_ID`; values were pinned literally after reading `azd env get-values`. (2) The runbook's "shape-only placeholder" for `Box__ConfigJson` must actually be shape-valid Box JWT JSON (`boxAppSettings.{clientID,clientSecret,appAuth.{publicKeyID,privateKey,passphrase}}` + `enterpriseID`) — a bare `{"placeholder":true}` fails host construction. Worth adding the exact placeholder to the runbook in the docs refresh. |
| E9c | Readback: head `20260819180000_GrantEvaHandoffDownloadOperations`, all 8 new ids present. **Both grant fixes verified live in production**: `CaseRepairSpecifications` = Web SELECT/INSERT/UPDATE + DENY DELETE; `EvaHandoffDownloadOperations` = Web SELECT/INSERT + DENY DELETE, Worker DENY DELETE — the previously zero-permission table is fixed. |
| E10 | `Invoke-AzureDatabaseBootstrap` — **Verified 496 catalogued permission/denial rows and 332 effective runtime DML rows.** Exit 0. |
| E12 | env set (digest / suffix `ed3be51c95bc` / activations); PreProvision pass ("Worker Disabled settings render 'false'" = enabled estate). `azd provision --preview` diffed against **release 10's own preview**: byte-identical property changes except `revisionSuffix d8de29cb94f3 → ed3be51c95bc`. The cpu/memory raise is inside the `* properties.template.containers` entry both previews carry; every other Modify line is the standing what-if normalization noise release 10 also showed. Stop condition satisfied with evidence, not judgement. |
| E13 | `azd provision` running |

## Post-release operator review — 2026-08-19/20, three defect reports, all triaged

The operator reviewed the deployed release and reported, in sequence:
1. *"Admin page at the very least is clearly showing a deployment regression"* → investigated; the Approved-mailboxes giant-row layout is real but **verified pre-existing** (page untouched between d8de29cb and ed3be51c; release-12 CSS diff is shell/upload only; identical DOM+CSS ⇒ identical render). Filed and dispatched **PLAT-009** (layout + that page's copy).
2. *"Its a giant white box… enormous amount of UI narration and copy… extremely bad and unprofessional"* → the design authority already mandates the opposite at `docs/design/README.md:160`; the estate drifted (audit: Assessment 33 prose blocks, _CaseWorkflow 24, Intake/Details 21, …). Filed and dispatched **PLAT-010** (estate-wide copy strip; per-page disposition table as the review artefact).
3. *"the unidentified page is a ton of slop… no clear answer as to what is going on"; "intake", "custody detail", "Intake receipt — 2b49d9d3-…"; "Unidentified should be a tab within queues… seperate filters for images and emails"; "The 'Not Ready' queue is also supposed to have seperate filters for instructions and image initiated cases"* → the vocabulary leaks violate the **recorded 2026-08-04 operator decision** banning "intake" operator-facing (design README :161) and the internal-identifiers rule (:168) — INTK-007/008's surfaces shipped in breach of the binding authority, which the release review (mine included) failed to catch on the UI-copy axis. Filed and dispatched **INTK-009** (Unidentified as a Queues tab with image/e-mail filters; Not-ready origin filters; row content that answers "what is it"; vocabulary purge). PLAT-010 told to exclude `Pages/Unidentified/*` (INTK-009 owns them) and to treat identifier/vocab leaks as first-class on every page it sweeps.

Honest note for the record: my production verification confirmed the features *work* and *are reachable*, and checked design compliance on the pages the release changed for structure (panels, no inline styles, colour rules) — but did not audit copy volume or the banned-terms list page-by-page. The operator's review caught what mine missed; the three tickets carry their verbatim words. Lanes running: PLAT-009, PLAT-010, INTK-009 — all open PRs for review, none merges itself. A release 13 (web-only, no migrations) will be proposed to the operator once they are green.

## Post-release lifecycle closeout — 2026-08-20

Closed out 16 of the 17 tickets whose proof depended on releases 12/13 (this ticket's proof cited as [[DELIV-012]] throughout each). Verified every cited PR merge commit is a git ancestor of its release SHA (`ed3be51c`/`2325ed4a`) before writing proof. Moved to `done`: PLAT-006, TICK-043/044/045/046, TICK-093, INTK-005/006/007/008/009/010/011, PLAT-009, PLAT-010 (INTK-007 needed two moves, review→verifying→done, since it started in `review`). All got `deployment: production` and a proof doc citing this ticket's release-12/13 readbacks plus each ticket's own PR-specific production evidence, with honest qualifications recorded where a ticket's own PIR flagged something incomplete (e.g. INTK-009/010's un-run manual 1920px visual pass, PLAT-010's two un-fixed GUID leaks, TICK-044/TICK-093's caller landing on a sibling ticket's PR, INTK-006's split-race defect closed by INTK-011, INTK-007's surface later rebuilt by INTK-009). CASE-003 (backlog, no docs, fixed inline inside INTK-010's PR #433) got a proof doc and `deployment: production` but only moved backlog→preparing — its `fix` profile gates `leave-preparing` on `files`+`plan`, which don't exist and weren't authorized to be written in this pass; it is stopped in `preparing`. SIMPLI-014 (already `done`) got an appended proof addendum noting its renderer now has a reachable production caller (the Report draft panel) since release 12, still gated on ENG-002 estimate import — stage untouched. Did not move DELIV-012 itself.
