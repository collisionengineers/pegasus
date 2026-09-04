# Current auto run — EPIC-012

Run record: `automation/runs/20260902T203000Z-claude-fable.md`. Status:
**paused for handoff on 2026-09-04** at the operator's request.

Handoff (rewritten 2026-09-04, authoritative):
`C:\Users\PC\Downloads\Pegasus_EPIC-012_handoff.md`. Scripts:
`C:\Users\PC\Downloads\Pegasus_EPIC-012_workflows\case-workspace-v2-build-2.js`.

## Wave 1 — COMPLETE

All five Done and closed out, verified together at wave SHA
`80f0ca262b0fe2ca354a5dfb18933dc3f105b917` with each ticket's merge SHA proved
an ancestor first: PLAT-070 `60fc84dc` (#649), DOCS-017 `86ce276d` (#651),
ENG-035 `ce027748` (#648), PLAT-068 `3f0cb45e` (#655), AUTO-018 `80f0ca26`
(#654). Worktrees and branches removed, tickets released, Outcomes filled.

## Wave 2 — CASE-038 in Review, NOT merged

PR #656, branch head `1ed9da3a9`, worktree `.worktrees/case-038`.

An independent cross-model review returned REQUEST CHANGES with five
blockers. Two of them were changes the controller had authorised on a
mistaken premise — a snapshot placeholder rewrite that existed only to green
a wrong artifact, and a CI timeout raise whose stated evidence was
contradicted by the run it cited. Both are now reverted.

The fix round closed four of five blockers plus every minor finding: the
section fragment moved to `/Cases/{id}/Section`, the placeholder rewrite and
the CI caps reverted to `origin/dev`, the record reduced to exactly one
editor (proved by a test asserting one form, one save action and one
occurrence of each of the twenty editable names), and the report, checklist
and scratch notes corrected to match the diff.

**Outstanding: finding 1's artifact.** The ~30-minute snapshot capture did not
complete, so `docs/design/test-ui/pages/case-details--default.html` still
holds the 3,437-byte Files fragment and `catalogue.json` still describes a
frame it does not contain. CI on this head will be red on the `test-ui` lane.

Next action, in `.worktrees/case-038`: run
`pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1`, then
`-Verify -SkipCapture`, then `Test-UiCatalogue.ps1`; **open the regenerated
default snapshot and confirm by eye** (doctype, `case-sticky`, eleven
`id="section-*"` hosts, tens of kilobytes not 3.4 KB); correct
`catalogue.json`; commit, push, wait for CI, then obtain a **fresh
independent review** before merging. If a genuine full-record page still
yields an unloadable offline image, that is a separate finding to fix at its
cause — do not reinstate the placeholder rewrite.

## Waves 3–5 — Prepared, not started

Wave 3: CASE-039, CASE-040, CASE-041, CASE-029, CASE-042 (blocked by
CASE-032), PLAT-069, CASE-009, then ENG-034 serial last. Wave 4: ENG-036,
ENG-031, ENG-029, DOCS-018, CASE-043 serial. Wave 5: UIIMP-014 → DELIV-030,
then the adversarial claims.

## Carried forward

1. **Report generation is blocked on `dev`** until CASE-040 wires the sign-off
   Engineer into `EfAssessmentReportProjectionSource`; its proof must show a
   draft generating end to end from the production path. No promotion to
   `main` before then, and release needs explicit `MERGE AUTH GRANTED`.
2. **Migration ordering**: a lane whose migration predates a since-merged one
   must regenerate it after `dev`'s tail and reconcile the model snapshot and
   the applied-migrations assertion.
3. `origin/main` `32f8679d` is two commits ahead of `dev` through direct
   pushes; reconciling that is an administrator action.
4. **Verify the artifact, not the gate.** A gate turning green is not evidence
   that the thing it guards is correct — that is how the wrong snapshot passed.
