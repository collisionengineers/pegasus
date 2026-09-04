# Current auto run — EPIC-012

Run record: `automation/runs/20260904T103000Z-claude-fable.md` (continues
`20260902T203000Z-claude-fable`). Status: **running** since 2026-09-04 10:30Z.
Approved plan: `C:\Users\PC\.claude\plans\objective-plan-completion-lazy-haven.md`.
Binding build policy: this group's `context.md` §Build policy (2026-09-04).
Decision D51 (image-initiated principal, operator 2026-09-04):
`decisions/d51-image-initiated-principal.md`.

## Done

Wave 1 (PLAT-070 #649, DOCS-017 #651, ENG-035 #648, PLAT-068 #655, AUTO-018
#654) verified together at `80f0ca262b0fe2ca354a5dfb18933dc3f105b917`.

## Phase A — in progress (as of 13:40Z)

- **PLAT-069 merged** — PR #657, merge `8f3d0960`; Verifying, awaits V1.
- **UIIMP-015 merged** — PR #658, merge `df31d21a` (scoped snapshot capture:
  `Update-TestUiSnapshots.ps1 -Scope <prefixes> -CaptureFilter "<filter>"`);
  Verifying, awaits V1. Every later lane uses the scoped form.
- CASE-032 — PR #659; merge-prepped over UIIMP-015 (`ed0dc6ad`), review
  returned two should-fix findings (blank labelled rows for absent values;
  vacuous assignee assertion); fix round in progress.
- CASE-038 — PR #656; the full capture completed and the real Case page is
  committed (`b5f5ccda`, CI green); the fresh independent review found one
  new blocker — the inspection address rendered with `form=` outside the
  form's DOM subtree escapes the CASE-007 dirty guard, so Finish editing can
  discard a typed address — plus a record-only finding; fix round in progress.
- **CASE-045 pulled in** by the operator (created by another session at
  10:21Z): prepared under D51; Phase B, after CASE-042, merging after CASE-043.

## Outside this run, affecting it

- PR #660 (DELIV-046, other session) merges `origin/main`'s history into
  `dev` through a repair branch, which would clear condition (1) on the
  release PR. Not this run's to merge; lanes merge-prep over it if it lands.

## Merge queue (one at a time, in this order)

CASE-032, CASE-038 (as each is ready) · CASE-009, CASE-039, CASE-041,
CASE-040, CASE-029, CASE-042, ENG-034, CASE-043, CASE-045 · ENG-029, ENG-036,
ENG-031, DOCS-018 · UIIMP-014, DELIV-045.

## Checkpoints

V1 after ENG-034 merges; V-final after DELIV-045 merges (plus adversarial
claims). Critic after Phase B and after Phase C.

## Carried forward

1. Report generation is blocked on `dev` until CASE-040 wires the sign-off
   Engineer through `EfAssessmentReportProjectionSource` (plan corrected
   2026-09-04 to own that file). No promotion to `main` before then; release
   needs explicit `MERGE AUTH GRANTED`.
2. Migration ordering: regenerate after `dev`'s tail at merge prep.
3. `origin/main` `32f8679d` is two commits ahead of `dev` — DELIV-046 (#660)
   is repairing it; until it merges the release PR records the condition.
4. Verify the artifact, not the gate.
5. A wrapper that idles on a background command is forced to its final answer;
   all long waits are bounded foreground loops (build-3 script, 11:30Z).
