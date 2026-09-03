# Current auto run — EPIC-012

Run record: `automation/runs/20260902T203000Z-claude-fable.md`. Status:
**running autonomously to completion** (operator goal set 2026-09-04: finish
waves 1–5, every ticket Done on `dev`; no status pauses).

Handoff: `C:\Users\PC\Downloads\Pegasus_EPIC-012_handoff.md`. Scripts:
`C:\Users\PC\Downloads\Pegasus_EPIC-012_workflows\case-workspace-v2-build-2.js`.

## Wave 1 — four of five merged

| Ticket | State | Merge SHA |
| --- | --- | --- |
| PLAT-070 | merged, Verifying | `60fc84dc` |
| DOCS-017 | merged, Verifying | `86ce276d` |
| ENG-035 | merged, Verifying | `ce027748` |
| PLAT-068 | merged, Verifying | `3f0cb45e` |
| AUTO-018 | PR #654, fix round in flight (blocker R1 unbalanced CHECK SQL) | — |

`origin/dev` is `3f0cb45e`; its migration tail is
`20260903225331_StaffAccountSignOff`. AUTO-018 must regenerate its migration
after that tail.

## Wave 2 — running

CASE-038 (single-scroll Case frame) launched 2026-09-04, one lane, Opus high;
PLAT-070's merge unblocked it. Its shells stay heading-only: the Assessment
handler surface moves with ENG-034 in wave 3 (option B).

## Still to do

Wave 1: AUTO-018 merge, then the wave verification run (one full suite at the
final merge SHA, ancestry-checked, writing every merged ticket's proof) and
closeout. Waves 3, 4, 5 as scheduled — wave 3 ends with ENG-034 serial on the
`Details.cshtml.cs` lease; wave 4 has CASE-043 serial on the migration lock;
wave 5 is UIIMP-014 then DELIV-030, then the adversarial claims.

## Carried forward

1. **Report generation is blocked on `dev`** until [[CASE-040]] wires the
   sign-off Engineer into `EfAssessmentReportProjectionSource` (wave 3).
   DOCS-017's declared and approved accepted risk. CASE-040's proof must show
   a draft generating end to end from the production path. No release to
   `main` before then, and release needs explicit `MERGE AUTH GRANTED`.
2. **Migration ordering**: any lane whose migration predates a since-merged
   one must regenerate it after `dev`'s tail and reconcile the model snapshot
   and the applied-migrations assertion. PLAT-068 and AUTO-018 both hit this.
3. `origin/main` `32f8679d` is two commits ahead of `dev` through direct
   pushes; reconciling that is an administrator action, not a lane's.

## Testing policy (operator-agreed)

Lanes run restore, Release build and the Core + Architecture projects only;
GitHub CI is the pre-merge full-suite gate (it shards the ~26-minute
integration suite three ways per PR); reviews are diff-scoped; one full local
run per wave at the merge commit for proof. Every command runs as
`<cmd>; echo NAME_EXIT=$?`. Gate merges on the run conclusion
(`gh run list --branch <b> --limit 1`), never on `gh pr checks`, which
interleaves superseded runs.
