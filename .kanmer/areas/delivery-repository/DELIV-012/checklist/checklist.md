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
