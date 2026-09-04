# Current auto run — EPIC-012

Run record: `automation/runs/20260904T103000Z-claude-fable.md` (continues
`20260902T203000Z-claude-fable`). Status: **running** since 2026-09-04 10:30Z.
Approved plan: `C:\Users\PC\.claude\plans\objective-plan-completion-lazy-haven.md`.
Binding build policy: this group's `context.md` §Build policy (2026-09-04).

## Done

Wave 1 (PLAT-070 #649, DOCS-017 #651, ENG-035 #648, PLAT-068 #655, AUTO-018
#654) verified together at `80f0ca262b0fe2ca354a5dfb18933dc3f105b917`.

## Phase A — in progress

- CASE-038 (#656, `.worktrees/case-038` @ `1ed9da3a9`): regenerate the Case
  snapshots with the full capture, eye-check `case-details--default.html`,
  correct `catalogue.json`, push, CI, **fresh independent review**, merge.
- UIIMP-015 (new): scoped snapshot capture; plan then build.
- PLAT-069: Operations notice.
- CASE-032 (EPIC-011, pulled in): research → plan → build; unblocks CASE-042.

## Merge queue (one at a time, in this order)

UIIMP-015, PLAT-069, CASE-032, CASE-038 · CASE-009, CASE-039, CASE-041,
CASE-040, CASE-029, CASE-042, ENG-034, CASE-043 · ENG-029, ENG-036, ENG-031,
DOCS-018 · UIIMP-014, DELIV-044.

## Checkpoints

V1 after ENG-034 merges; V-final after DELIV-044 merges (plus adversarial
claims). Critic after Phase B and after Phase C.

## Carried forward

1. Report generation is blocked on `dev` until CASE-040 wires the sign-off
   Engineer through `EfAssessmentReportProjectionSource` (its plan was
   corrected on 2026-09-04 to own that file). No promotion to `main` before
   then; release needs explicit `MERGE AUTH GRANTED`.
2. Migration ordering: regenerate after `dev`'s tail at merge prep.
3. `origin/main` `32f8679d` is two commits ahead of `dev`; administrator
   action; recorded on the release PR.
4. Verify the artifact, not the gate.
