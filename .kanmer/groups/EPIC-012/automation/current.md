# Current auto run — EPIC-012

Run record: `automation/runs/20260902T203000Z-claude-fable.md`. Status:
**running** — Build wave 1, Workflow run `wf_7f9d3816-920`, script
`case-workspace-v2-build-2.js`.

Prepare is complete for all 20 build tickets (Prepare-3 finished ENG-036 and
CASE-043; the five escalated questions were resolved by the controller on
2026-09-03 and both tickets pass `leave-preparing`).

## Testing policy, operator-agreed 2026-09-03

The full solution test run costs about 26 minutes, almost all of it
`Pegasus.IntegrationTests`. The build script had it running three or four
times per ticket. It now runs **once per lane and once per wave**:

| Step | Runs |
| --- | --- |
| Codex implementation | restore, Release build, and the Core and Architecture test projects only (~30 s). Explicitly forbidden from running the integration suite |
| Wrapper, after implementation | the one authoritative full-suite run for the lane, plus snapshot regeneration and verify for a routed page change and `Test-MigrationGrants.ps1` for a migration |
| After the simplification pass | build plus Core and Architecture only — the pass is behaviour-preserving; snapshot verify only if it touched a routed page |
| Cross-model PR review | scoped to the diff, with snapshot verify and migration grants still unscoped; falls back to the full filter if the scope cannot be justified honestly |
| Wave verification | one detached checkout at the head of `origin/dev`, the full suite plus the union of the wave's plan-named acceptance commands, once. Every ticket's merge SHA is first proved an ancestor of that wave SHA, and each proof cites that SHA and the shared log |

A single non-zero exit fails the whole wave: every ticket gets a FAIL proof
and stays in Verifying. No ticket reaches Done on a red run.

Agents are also forbidden from polling a background command with filler
turns; they wait for the completion notification.

## Wave 1 restarts

Two deliberate restarts, no work lost. The first run exposed a shared-lock
bug: ENG-035 was classified non-serial because its owned-path text said
"migration" in lower case while the test was case-sensitive, so a migration
lane could have run concurrently with three others against the linear EF
chain. The test is now case-insensitive and also catches
`docs/design/README.md`. ENG-035 keeps its taken record, branch and worktree
and resumes them under the resumed-execution-packet rule; its uncommitted
work (a migration and fifteen edited files, one full passing test run and an
applied simplification pass) is intact.

Lane order: ENG-035 (resumed) → PLAT-070 → DOCS-017 → PLAT-068 → AUTO-018,
serial because every one touches a capacity-one shared path.

Remaining: wave 2 CASE-038; wave 3 CASE-039, CASE-040, CASE-041, CASE-029,
CASE-042 (blocked by CASE-032), PLAT-069, CASE-009, then ENG-034 serial last;
wave 4 ENG-036, ENG-031, ENG-029, DOCS-018, CASE-043 serial; wave 5
UIIMP-014 → DELIV-030, then the adversarial claims.

Git: `origin/dev` 07ac7f1b; `origin/main` 32f8679d, two commits ahead of
`dev` through direct pushes. Lanes branch from `origin/dev` and must not
reconcile that divergence; it is an administrator action.
