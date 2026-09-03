# Current auto run — EPIC-012

Run record: `automation/runs/20260902T203000Z-claude-fable.md`. Status:
**running** — Build wave 1, three of five merged.

Handoff document, refreshed 2026-09-04:
`C:\Users\PC\Downloads\Pegasus_EPIC-012_handoff.md`. Workflow scripts:
`C:\Users\PC\Downloads\Pegasus_EPIC-012_workflows\`
(`case-workspace-v2-build-2.js` is current).

## Wave 1

| Ticket | State | Merge SHA |
| --- | --- | --- |
| PLAT-070 | merged, Verifying | `60fc84dc` |
| DOCS-017 | merged, Verifying | `86ce276d` |
| ENG-035 | merged, Verifying | `ce027748` |
| PLAT-068 | PR #655, round-2 fixes pushed (`7a1efab7`), mergeable, CI running | — |
| AUTO-018 | PR #654, REQUEST CHANGES (blocker R1), held until PLAT-068 merges | — |

Still owed by wave 1: merge PLAT-068; AUTO-018's fix round, review and merge;
then one wave verification run at the final merge commit that writes every
merged ticket's proof and moves them to Done, then closeout.

Waves 2 to 5 are fully Prepared and unstarted. `origin/dev` `ce027748`;
`origin/main` `32f8679d`, two commits ahead of `dev` through direct pushes —
an administrator action, not a lane's.

## Carried forward, must not be lost

1. **Report generation is blocked on `dev`** until [[CASE-040]] wires the
   sign-off Engineer into `EfAssessmentReportProjectionSource` (wave 3). This
   was DOCS-017's declared and approved accepted risk, not a slip; CASE-040
   owns closing it and its proof must show a draft generating end to end from
   the production path. No release to `main` before then.
2. **AUTO-018 blocker R1**: `CK_AiJobs_MarketResearchResult` has unbalanced
   parentheses, so its migration cannot apply. R2–R6 and R9 ride the same
   round; R7 accepted.
3. **Migration ordering**: a lane whose migration predates another that has
   since merged must **regenerate** it so it sorts after `dev`'s tail, and
   reconcile the model snapshot and the applied-migrations assertion. PLAT-068
   hit this; every later migration lane will too.

## Testing policy (operator-agreed 2026-09-03)

Lanes run restore, Release build and the Core + Architecture projects only;
**GitHub CI is the pre-merge full-suite gate** (it shards the ~26-minute
integration suite three ways on every PR); reviews are diff-scoped; one full
local run per wave at the merge commit for the proof record. Every command
runs as `<cmd>; echo NAME_EXIT=$?`. Gate merges on the run conclusion, not on
`gh pr checks`, which interleaves superseded runs.
