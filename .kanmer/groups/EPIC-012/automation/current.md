# Current auto run — EPIC-012

Run record: `automation/runs/20260902T203000Z-claude-fable.md`. Status:
**running** — Build wave 1, Workflow run `wf_fc12d926-deb`, script
`case-workspace-v2-build-2.js`.

Prepare is complete for all 20 build tickets.

## Testing policy, operator-agreed 2026-09-03

The integration suite costs about 26 minutes run serially on the workstation.
GitHub CI (`.github/workflows/ci.yml`) already runs it on **every** pull
request — `on: pull_request` with no branch filter, so PRs to `dev` included —
sharded three ways at roughly four minutes a shard, alongside the Core and
Architecture projects, the browser lane and a shard-coverage job that proves
the shards reassemble into the lane's whole filter. The reviewer already
blocks the merge on those checks. Running the same suite locally was
therefore a slower duplicate of a gate we wait for regardless.

| Step | Runs |
| --- | --- |
| Codex implementation | restore, Release build, Core and Architecture test projects (~30 s). Forbidden from running the integration suite |
| Wrapper, after implementation | the same fast checks, run independently rather than trusted from Codex, plus snapshot regeneration and verify for a routed page change and `Test-MigrationGrants.ps1` for a migration. No local integration run |
| After the simplification pass | build plus Core and Architecture only; the pass is behaviour-preserving |
| Pull request | **GitHub CI is the pre-merge full-suite gate.** The reviewer watches the checks and merges only when they are green |
| Cross-model PR review | diff-scoped tests only, with snapshot verify and migration grants unscoped; no local full suite |
| Wave verification | one detached checkout at the head of `origin/dev`, the full suite plus the union of the wave's plan-named acceptance commands, once. Every ticket's merge SHA is proved an ancestor of that wave SHA first, and each proof cites that SHA and the shared log |

Every command is run as `<cmd>; echo NAME_EXIT=$?` and its exit code quoted:
an exit code that was not captured is not evidence. A single non-zero exit at
wave verification fails the whole wave — every ticket gets a FAIL proof and
stays in Verifying. Agents never poll a background command with filler turns.

## Wave 1 restarts

Three deliberate restarts, no work lost, each for a policy change or a bug:

1. Per-wave verification replaced per-ticket verification.
2. A shared-lock bug: ENG-035 was classified non-serial because its owned-path
   text said "migration" in lower case while the test was case-sensitive, so a
   migration lane could have run concurrently with three others against the
   linear EF chain. The test is now case-insensitive and also catches
   `docs/design/README.md`.
3. The local full suite moved to CI, as above.

ENG-035 keeps its taken record, branch and worktree throughout and resumes
them under the resumed-execution-packet rule; its uncommitted work (a
migration, fifteen edited files, an applied simplification pass and a full
passing local suite from before the change) is intact.

Lane order: ENG-035 (resumed) → PLAT-070 → DOCS-017 → PLAT-068 → AUTO-018,
serial because every one touches a capacity-one shared path.

Remaining: wave 2 CASE-038; wave 3 CASE-039, CASE-040, CASE-041, CASE-029,
CASE-042 (blocked by CASE-032), PLAT-069, CASE-009, then ENG-034 serial last;
wave 4 ENG-036, ENG-031, ENG-029, DOCS-018, CASE-043 serial; wave 5
UIIMP-014 → DELIV-030, then the adversarial claims.

Git: `origin/dev` 07ac7f1b; `origin/main` 32f8679d, two commits ahead of
`dev` through direct pushes. Lanes branch from `origin/dev` and must not
reconcile that divergence; it is an administrator action.
