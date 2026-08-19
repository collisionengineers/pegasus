# Checklist — DELIV-012, release 12

## Preparation

- [x] Research: current estate (Azure, read-only)
- [x] Research: codebase evidence since release 10
- [ ] Research: recent tickets and PR comments
- [x] Read-only production preflight (duplicate canonical Message-IDs = 0; `CaseEstimateLines` = 0)
- [x] Operator decisions obtained and recorded in `open-questions/`
- [x] Plan written

## Wave A — defects on merged `dev`

- [ ] A1 grants for `CaseRepairSpecifications` + census closes the blind spot — PR merged
- [ ] A2 `MailOperationalDestinationPolicy` has a production caller — PR merged
- [ ] A3 `IRepairSpecificationStore` registered and used by the assessment path — PR merged
- [ ] A4.1 container route for Chromium proved locally (image built, inspected, renders)
- [ ] A4.2 operator entry point for a report draft
- [ ] A4.3 A4 PR merged
- [ ] A5 `current-architecture.md:85` and `operations.md:278` corrected — PR merged

## Wave B — INTK takeover

- [ ] Four tickets force-taken with a scratch note explaining the takeover
- [ ] INTK-005 failures fixed, grants added, checklist complete, CI green
- [ ] INTK-006 merged with `dev`, Q2 branches expressed, checklist complete, CI green
- [ ] INTK-008 assertion fixed, flake characterised, grants added, operator-notes per Q2, checklist complete, CI green
- [ ] INTK-007 rebased, conflicts resolved, grants added, `CLAUDE.md` invariant updated, checklist complete, CI green

## Wave C / merge order

- [ ] #422 TICK-045 merged
- [ ] Wave A PRs merged in order A1, A5, A2, A3, A4
- [ ] #416 merged
- [ ] #417 merged
- [ ] #423 merged
- [ ] #424 merged
- [ ] `dotnet ef migrations has-pending-model-changes` clean on the final `dev`
- [ ] `gh pr list --state open` empty

## Wave D — hygiene

- [ ] Every branch verified contained in `origin/dev` before deletion
- [ ] `.worktrees/intk-008/CONTEXT.md` content captured to the ticket before removal
- [ ] All task worktrees removed (never `.worktrees/kanmer`)
- [ ] Local branches reduced to `main`, `dev`, `kanmer-board`
- [ ] Remote branches reduced to `main`, `dev`, `kanmer-board`
- [ ] `git fetch --prune`, `git worktree prune` clean

## Wave E — release 12

- [ ] E1 CI green on the final `dev` head
- [ ] E2 preflight: `main` ancestor of `dev`, SHA recorded
- [ ] E3 `MERGE AUTH GRANTED` requested and the atomic fast-forward pushed
- [ ] E4 main-push run and history guard green
- [ ] E5 artefacts built and validated (Local + Artifact), manifest SHA-256 recorded
- [ ] E6 `azd env refresh` + PreUpload
- [ ] E7 read-only SQL preflight re-run at release time
- [ ] E8 image pushed to ACR, digest verified
- [ ] E9 PreMigration + `efbundle` applied, migration head read back
- [ ] E10 database bootstrap grant census passed
- [ ] E11 Sent-evidence approval applied (Q4)
- [ ] E12 PreProvision + `azd provision --preview` reviewed
- [ ] E13 `azd provision` executed
- [ ] E14 Worker package deployed by config-zip
- [ ] E15 production smoke exit 0

## Verification, docs, proof

- [ ] Backend verification complete (version, health, migrations, worker, poll advance, Sent exception stopped)
- [ ] Browser verification of every shipped UI change on production
- [ ] `docs/operations.md` + `docs/current-architecture.md` refreshed and merged
- [ ] `proof/proof.md` written from the deployment evidence
- [ ] Dependent tickets moved to done, one gated stage at a time
- [ ] DELIV-012 closed out

## Progress notes

- 2026-08-19 12:12Z — DELIV-012 created; DELIV-011 (held release 11) archived as superseded and its claim released.
- 2026-08-19 12:15Z — three research lanes dispatched. Estate and codebase lanes returned; the tickets/PR-comment lane hit a model limit and was relaunched on Sonnet.
- 2026-08-19 12:40Z — read-only production SQL preflight run: no duplicate canonical Message-IDs (TICK-043's unique index is safe), `CaseEstimateLines` empty (TICK-093's backfill is a no-op in production).
- 2026-08-19 12:45Z — CI re-run triggered for PR #422's SQL-timeout flake.
- 2026-08-19 12:55Z — five operator decisions recorded; scope enlarged to take over the four INTK PRs, make three orphaned surfaces live, and approve Sent-evidence polling.

## Progress notes — continued

- 2026-08-19 13:10Z — five tickets force-taken (INTK-005/006/007/008, TICK-045). Six remediation lanes dispatched.
- 2026-08-19 13:25Z — early git hygiene: 7 merged worktrees removed, their local+remote branches deleted, stray `pr417check` deleted. Free disk 7.0 → 28.1 GB, which the container work needed.
- 2026-08-19 13:30Z — board truthfulness: PLAT-001 → `production` (verified ancestor of deployed `main`), TICK-211 → `n/a`, five post-release-10 tickets → `not-deployed`. TICK-011's proof corrected: two of its three cited commits are unreachable objects with no refs; `deployment` corrected `not-deployed` → `production` with the honest "shipped, no live caller" qualification, and a retrospective `open-questions` created (it had none, so its gate had passed vacuously).
- 2026-08-19 13:35Z — **PR #425** opened: repair-specification store wired to a real caller. Independently reviewed — pass. Verified the new cross-context read happens *after* `transaction.CommitAsync`, so there is no stale read or lock contention.
- 2026-08-19 14:00Z — **release gate found broken on `dev`**: `Test-AzureDeploymentPlan -Mode Local` failed because TICK-046's grant-carrying migration was unaccounted for in the database bootstrap. Release 12 could not have run from `dev`.
- 2026-08-19 14:05Z — renderer container lane returned: Chromium proven in the produced image via `oras` layer analysis, renderer tests 6/6 against real Chromium locally. Sizing recommendation (0.5 vCPU/1 GiB → 1.0/2 GiB) left as an operator decision; `infra/` deliberately untouched.
- 2026-08-19 14:10Z — INTK-005 returned: four blockers fixed, five review comments applied, plus a gap the brief missed (duplicate-replay notice never surfaced). Its `dev` merge had silently dropped its own migration id with no conflict markers — caught and restored.
- 2026-08-19 14:20Z — diagnosed PR #425's repeated CI failure as an unresolvable GitHub **merge ref**, not a code fault (the hang is inside `actions/checkout`'s fetch; the same job passed in 22 s on another PR). Close/reopen regenerated it and a fresh run started.
- 2026-08-19 14:30Z — **PR #426** opened: release gate fixed, `CaseRepairSpecifications` grant, new migration for the live `EvaHandoffDownloadOperations` production defect, and `Test-MigrationGrants.ps1` guarding the whole class. I re-ran both gates myself on the branch: `-Mode Local` exit 0, guard 49 files pass.
- 2026-08-19 14:35Z — TICK-045 lane wired `MailOperationalDestinationPolicy` into the retained mailbox viewer and **proved its tests can fail** by temporarily breaking the helper. INTK-006 and INTK-007 lanes running; INTK-008 and the report-draft entry point still in flight.

## Wave A status — 2026-08-19 ~14:45Z

- [x] A1 grants for `CaseRepairSpecifications` — in PR #426
- [x] A1b **new**: `EvaHandoffDownloadOperations` grant migration — a live production defect, in PR #426
- [x] A1c **new**: release gate `Test-AzureDeploymentPlan -Mode Local` repaired (was failing on `dev`) — PR #426
- [x] A1d **new**: `scripts/Test-MigrationGrants.ps1` guard, wired into CI — PR #426
- [x] A1e **new**: `Test-AzureDeploymentPlan -Mode Local` added to the always-on `changes` job, so the release route's own preflight is now a CI gate. **Verified executing in CI**, not merely configured: run `32263089802`, job `changes`, steps "Migration runtime-grant check" and "Azure deployment plan (Local)" both `success`.
- [x] A2 `MailOperationalDestinationPolicy` has a production caller (`/Inbox/{id}`) — in PR #422
- [x] A3 `IRepairSpecificationStore` registered and used — PR #425
- [x] A4.1 container route for Chromium proved locally — PR #427 (`oras` layer evidence + renderer tests 6/6 against real Chromium)
- [x] A4.1b Web container raised to 1.0 vCPU / 2 GiB on the operator's decision; `az bicep build` verified — PR #427
- [x] A4.2 operator entry point for a report draft — branch pushed; ships reachable but disabled pending imported estimates (operator answer Q7)
- [x] A5 `current-architecture.md` and `operations.md` drift corrected — PR #426

## Wave B status

- [x] INTK-005 blockers fixed, CI fully green (#416)
- [x] INTK-005 bootstrap census entries added (by me, so INTK-006 inherits them)
- [ ] INTK-006 — lane running, rebasing onto the updated INTK-005
- [ ] INTK-007 — lane running; census block added and self-verified
- [ ] INTK-008 — lane running; census requirement and CASE-005 context sent
- [x] TICK-045 delivered a real caller and falsifiable tests (#422), CI fully green

## Follow-ups filed

- [x] **CASE-005** — SQL deadlock in parallel Qdos case allocation retries (pre-existing, evidenced on clean `dev`)
- [x] **ENG-002** — import repair estimates from external systems, MCP, and drag-and-drop (operator truth, quoted verbatim)
- [x] **CASE-003** — `/Cases/Create` without a receipt returns 500 (filed earlier)

## Owed before closeout

- [ ] Reconcile TICK-044's checklist — the caller its operator ruling demanded landed on TICK-045's diff
- [ ] `docs/capabilities.md` MAIL-04 still reads "Allocation only" although TICK-046 delivered it (that row belongs to TICK-046)
- [ ] Record the operator's estimate-import statement in `docs/operator-notes.md` (currently only in ENG-002 and this ticket's open questions)
- [ ] Follow-up ticket for the SDK-bearing Playwright base image, once tooling allows a minimal base

## Merge progress — 2026-08-19 evening

Merged into `dev` in the planned order, each after its own review entry in
`scratch/review` and with both gates (`-Mode Local`, `Test-MigrationGrants`)
re-verified on the resulting `dev` head:

| # | PR | Merge commit |
|---|---|---|
| 1 | #426 release gate + grants + guard | `2fa9c486` |
| 2 | #425 repair-spec store caller | `91a94471` |
| 3 | #422 TICK-045 MAIL-02 caller | `00a6787f` |
| 4 | #427 Chromium image + 1.0 vCPU / 2 GiB | `45b25bb5` |
| 5 | #416 INTK-005 grouped upload | `e18512a6` |
| 6 | #417 INTK-006 grouped image routing | `df194758` |

Still to merge, in order: **#423** INTK-008 (CI running on a fresh head after a
stale-merge-ref checkout hang — same signature as #425 earlier, same remedy),
**#428** report-draft entry point (CI running), **#424** INTK-007 (all green,
merges last by design — it owns the vocabulary migration).

`dev` history integrity checked: `main` is a strict ancestor; 18 first-parent
commits since release 10, every one a PR merge, no direct pushes.

- [x] #422 TICK-045 merged
- [x] Wave A PRs merged (A1 via #426, A3 via #425, A4.1 via #427; A2 rode #422; A5 via #426)
- [x] #416 merged
- [x] #417 merged
- [ ] #423 — CI
- [ ] #428 — CI (A4.2)
- [ ] #424 — green, waiting its turn

## Interim hygiene — 2026-08-19 21:45Z

Six more merged branches deleted remote **and** local after verifying `0` ahead
of `origin/dev`: `intk-005-grouped-upload`, `intk-006-grouped-image-routing`,
`task/deliv-012-grant-and-docs-fixes`, `task/deliv-012-wire-repair-spec-store`,
`task/deliv-012-renderer-container`, `task/tick-045-shared-classification-policy`.
Their six worktrees removed (each verified clean and 0 ahead first).

Remaining worktrees: main checkout, `.worktrees/kanmer` (never touched),
`.worktrees/intk-007`, `.worktrees/intk-008`, `../pegasus-worktrees/deliv-012-release-12`,
`../pegasus-worktrees/deliv-012-report-draft-entry-point` — the last four all
still carrying unmerged work or the release itself.

Board: INTK-005, INTK-006 and TICK-045 moved review → verifying (their PRs are
merged; proof waits on the deployment). **TICK-044's checklist reconciled**: the
five caller items are ticked with explicit provenance notes recording that the
work landed on TICK-045's diff under this ticket, and the one remaining item —
the authenticated production mailbox-viewer check — is what keeps it in
`verifying` until release 12 is proven.

## All PRs integrated — 2026-08-19 ~22:20Z

- [x] #423 INTK-008 merged (`a907ecd2`)
- [x] #428 report-draft entry point merged (`3de4f684`)
- [x] #424 INTK-007 merged **last** (`ed3be51c`) — the promotion head
- [x] `gh pr list --state open` → only #410, the dev→main CI vehicle (by design)
- [x] Hygiene at target end state: remote = `main`, `dev`, `kanmer-board`; local = those three + `task/deliv-012-release-12` (release's own, removed at closeout); worktrees = main checkout + `.worktrees/kanmer` + the release worktree (removed at closeout)
- [x] INTK-008 moved to `verifying`; INTK-007 to follow after the deployment proof

## Release preflight — running

- [x] E2: release worktree fast-forwarded to `ed3be51c95bc2a055606e5210131d37de9de2dd1`; `main` verified strict ancestor of `dev`
- [x] Gates at the promotion head: `-Mode Local` exit 0; `Test-MigrationGrants` 53 files; Release build 0 warnings / 0 errors
- [x] E7 read-only SQL preflight at release time: production head `20260814094632_DropBoxFileRequests` (8 migrations pending); duplicate canonical Message-ID groups = 0; `CaseEstimateLines` = 0; `IntakeReceipts` = 12 (INTK-007's backfill input is small)
- [ ] E1: PR #410 full lane set on `ed3be51c` — running
- [ ] E5: `Build-ReleaseArtifacts` at `ed3be51c` — running
- [ ] E3: **STOP — request MERGE AUTH GRANTED + Azure write approvals**
