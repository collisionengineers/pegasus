# Current auto run — EPIC-012

Run record: `automation/runs/20260902T203000Z-claude-fable.md`. Status:
**running**.

Prepare is **complete for all 20 build tickets**. Prepare-3 (run
`wf_4e195065-fc8`) wrote ENG-036's plan and checklist and CASE-043's full
pipeline; gpt-5.6-sol xhigh reviewed both and returned REQUEST CHANGES
(ENG-036: 9 findings, 6 blockers; CASE-043: 11 findings), all dispositioned
by Opus in the plans. The five questions it escalated were resolved by the
controller on 2026-09-03 from rules already recorded, and both tickets now
pass `leave-preparing`:

- ENG-036 snapshots — every lane regenerates the Test UI snapshots its own
  page change affects and commits them with the change (CLAUDE.md); the
  capacity-one lock means one lane at a time, not one lane for the epic.
  UIIMP-014 owns new states, catalogue entries and the walk in wave 5.
- ENG-036 design authority — PLAT-070 already removes the damage `Type` from
  `docs/design/README.md` in wave 1.
- CASE-043 — the ten fields are optional, not completeness inputs; the lookup
  fills only what the approved adapter returns and never synthesises a value;
  CASE-043 ships the editable path itself (done means wired). ENG-035 was
  told not to strand an existing edit route.

In flight: **Build wave 1** (run `wf_82493dad-cf6`) — PLAT-070 → DOCS-017 →
PLAT-068 → ENG-035 → AUTO-018, serial because every lane touches a
capacity-one shared-lock path. gpt-5.6-sol medium implements under Sonnet
wrappers; gpt-5.6-terra xhigh reviews each PR, Opus dispositions and merges
to `dev`.

## Verification change, operator-agreed 2026-09-03 (wave 2 onward)

Waves 2–5 run `case-workspace-v2-build-2.js`, which verifies **once per wave**
instead of once per ticket. Wave 1 finishes on the original script.

- Lanes stay serial where they share a capacity-one path. That serialisation
  is about write conflicts — the operator label file, the linear EF migration
  chain, the case detail page — not about test cost, and it does not change.
- After a wave's merges, one detached checkout at the head of `origin/dev`
  runs the full delivery commands and the union of the wave's plan-named
  acceptance commands once. Every ticket's merge SHA is first proved an
  ancestor of that wave SHA; a ticket whose SHA is not reachable stops the
  run. Each ticket's `proof.md` cites the wave SHA, that ancestry check and
  the shared command log.
- A single non-zero exit fails the whole wave: every ticket gets a FAIL proof
  and stays in Verifying. No ticket reaches Done on a red run, and a later
  pass does not erase the failure.
- The pre-merge review run is scoped to the diff (restore, build, then the
  tests covering the changed types), with the snapshot verify and the
  migration-grants script still run unscoped whenever those files change. The
  reviewer records the scope and why it covers the change; a red result
  blocks the merge exactly as before. The implementer's own full run is
  unchanged.

Remaining: wave 2 CASE-038; wave 3 CASE-039, CASE-040, CASE-041, CASE-029,
CASE-042 (blocked by CASE-032), PLAT-069, CASE-009, then ENG-034 serial last;
wave 4 ENG-036, ENG-031, ENG-029, DOCS-018, CASE-043 serial; wave 5
UIIMP-014 → DELIV-030, then the adversarial claims.

Git: `origin/dev` 07ac7f1b; `origin/main` 32f8679d, two commits ahead of
`dev` through direct pushes. Lanes branch from `origin/dev` and must not
reconcile that divergence; it is an administrator action.
