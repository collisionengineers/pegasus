# Current auto run — EPIC-012

Run record: `automation/runs/20260902T203000Z-claude-fable.md`. Status:
**running autonomously to completion** (operator goal, 2026-09-04: finish
waves 1–5, every ticket Done on `dev`).

Handoff: `C:\Users\PC\Downloads\Pegasus_EPIC-012_handoff.md`. Script:
`C:\Users\PC\Downloads\Pegasus_EPIC-012_workflows\case-workspace-v2-build-2.js`.

## Wave 1 — COMPLETE (all five Done and closed out)

Verified together at wave SHA `80f0ca262b0fe2ca354a5dfb18933dc3f105b917`,
each ticket's merge SHA proved an ancestor first; one shared command log.

| Ticket | PR | Merge SHA |
| --- | --- | --- |
| PLAT-070 | #649 | `60fc84dc` |
| DOCS-017 | #651 | `86ce276d` |
| ENG-035 | #648 | `ce027748` |
| PLAT-068 | #655 | `3f0cb45e` |
| AUTO-018 | #654 | `80f0ca26` |

Worktrees removed, branches deleted locally and on `origin`, tickets
released, Outcome sections filled.

Note for future verification runs: `scripts/Test-MarkdownPlacement.ps1` takes
mandatory `-Base`/`-Head` and fails with a parameter-binding error when called
bare; the gate CI actually wires is `Test-TestMarkdownPlacement.ps1`
(`.github/workflows/ci.yml:90-92`).

## Wave 2 — CASE-038 in review, CI running

PR #656, head `c9a7bb7b`. Two blockers were found and fixed in-lane, both
recorded as named dependencies in its PR because each is a one-place fix that
would otherwise serialise the wave:

1. The Test UI snapshot normalizer rewrote every non-catalogued URL to `#`,
   including `<img src>`, so the offline verify could never load a case
   document image. It now rewrites a non-catalogued **image** source to an
   inline placeholder pixel and leaves everything else as `#`.
2. The `test-ui` CI step timed out at 35 minutes (capture passed 123 tests in
   19m13s, then verify hit the cap). The single-scroll Case record renders
   every section on one page, so the capture legitimately grew; the step cap
   is now 55 minutes and the job 65.

## Remaining

Wave 3 (after CASE-038 merges): CASE-039, CASE-040, CASE-041, CASE-029,
CASE-042 (blocked by CASE-032 — skip and log if still blocked), PLAT-069,
CASE-009, then **ENG-034 serial last** on the `Details.cshtml.cs` lease.
Wave 4: ENG-036, ENG-031, ENG-029, DOCS-018, plus **CASE-043 serial**
(migration). Wave 5: UIIMP-014 then DELIV-030, then the adversarial claims.

## Carried forward

1. **Report generation is blocked on `dev`** until [[CASE-040]] wires the
   sign-off Engineer into `EfAssessmentReportProjectionSource`. DOCS-017's
   declared and approved accepted risk; CASE-040's proof must show a draft
   generating end to end from the production path. No release to `main`
   before then, and release needs explicit `MERGE AUTH GRANTED`.
2. **Migration ordering**: a lane whose migration predates a since-merged one
   must regenerate it after `dev`'s tail and reconcile the model snapshot and
   the applied-migrations assertion. PLAT-068 and AUTO-018 both hit this.
3. `origin/main` `32f8679d` is two commits ahead of `dev` through direct
   pushes; reconciling that is an administrator action.

## Operating notes

Lanes run restore, Release build and the Core + Architecture projects only;
GitHub CI is the pre-merge full-suite gate; reviews are diff-scoped; one full
local run per wave at the merge commit for proof. Gate merges on the run
conclusion (`gh run list --branch <b> --limit 1`), never `gh pr checks`.
PowerShell gates must be invoked as `pwsh -NoProfile -File ./scripts/<x>.ps1`.
Subagents repeatedly end their turn while their own background command runs —
for short fix rounds have the agent do the work directly rather than shelling
out to a long Codex run, and resume a stalled agent with a message.
