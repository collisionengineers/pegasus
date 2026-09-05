# Current auto run — EPIC-012

Run record: `automation/runs/20260904T103000Z-claude-fable.md`. Status:
**running** (Phase B) as of 2026-09-05 03:00Z. Approved plan:
`C:\Users\PC\.claude\plans\objective-plan-completion-lazy-haven.md`. Binding:
`context.md` §Build policy; D51 in `decisions/d51-image-initiated-principal.md`.

## Merged to dev (Verifying, awaiting checkpoint V1)

PLAT-069 #657 `8f3d0960` · UIIMP-015 #658 `df31d21a` · CASE-032 #659
`e66e1069` · CASE-038 #656 `ddbbc5e8` · CASE-009 #665 `67ee1643` · CASE-039
#669 `6e79f33d` · CASE-041 #664 `4fdfa21d`. Wave 1's five are Done.

## Phase B — in the merge queue (run `wf_ba05d301-711`)

1. CASE-042 #663 @ `44a5871b` — every review finding closed; regenerating the
   queues snapshots with the FULL capture (scoped capture picked a different
   `queues--empty` candidate than CI), then re-review and merge.
2. ENG-034 #668 @ `c2a5d7e6` — merge-prep, review, merge (no migration).
3. CASE-040 #666 @ `64889c42` — round-3 findings applied (Core-owned
   once-only automatic refusal, Sign-off row on Current position, dead label
   removed, Send page labels); merge-prep over ENG-034, review, merge.
   Report generation on `dev` is closed by this ticket.
4. CASE-029 #670 @ `ffa1effe` — merge-prep (regenerate migration), review.
5. CASE-043 — starts and merges after CASE-029.
6. CASE-045 — starts after CASE-042, merges after CASE-043.

## Lessons applied today

- Foreground bounded waits; resume branches; `fixFirst` for known findings.
- UIIMP-015 exempt from the tooling no-touch rule; the bootstrap census file
  and the runtime-role migration test are allowed for migration lanes.
- Scoped capture limit: states with ambiguous matchers (queues) regenerate
  with the full capture.

## Other machine

EPIC-013 (Linux/WSL) runs concurrently: PLAT-073 #661 and DELIV-046 #660
merged to dev; UIIMP-016 #662 and DELIV-047 #667 open (docs-only overlap on
`docs/design/README.md`). Not this run's tickets.

## Next

Checkpoint V1 (`case-workspace-v2-verify-3.js`) after ENG-034 merges: critic,
one verification run, proofs, Done, closeout. Then Phase C (ENG-029, ENG-036,
ENG-031, DOCS-018), Phase D (UIIMP-014, DELIV-045 + release PR), V-final.

## Carried forward

1. No promotion to `main` before CASE-040's end-to-end draft proof at V1;
   release needs explicit `MERGE AUTH GRANTED`.
2. `main` ancestry repaired by DELIV-046; the release PR still records the
   MERGE AUTH condition.
3. Verify the artifact, not the gate.
