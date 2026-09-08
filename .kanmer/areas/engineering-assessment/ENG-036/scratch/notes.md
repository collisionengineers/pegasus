2026-09-02 — kanmer-research started (Claude wrapper around gpt-5.6-terra xhigh). Board reads done from the board worktree; Codex researching origin/dev in `.worktrees/research`.

2026-09-03 — cross-model plan review: gpt-5.6-sol xhigh read the plan read-only at origin/dev 07ac7f1b in .worktrees/research (clean afterwards); verdict REQUEST CHANGES, nine findings. Claude Opus dispositioned all nine — seven fixed in plan/checklist/files, one fixed-in-part plus escalated, plus two further D45 doc residues found during disposition. Two unticked operator/coordination questions now block leaving Preparing. Ticket not moved.

## Original brief retained before current synchronization — 2026-09-08

Root authorized current title/body synchronization during preparation only.
The following is the unchanged recorded brief from the ticket last updated
2026-09-02T22:30:32.534Z; its original author is not asserted. D45 supersedes
Type and current Case-only asset loading supersedes the separate module
proposal. No old evidence, claim, stage or dependency is cleared.

Original title: Vehicle damage map: clickable diagram with per-zone severity, type and notes, tyres and seat belts, printed on the report

## What

One SVG vehicle diagram (top-down) whose zones toggle damage entries with severity, type and note; tyres and seat belts per corner; read-only markers outside edit; the marked diagram prints on the report.

## Why

D39. No diagram exists in the codebase today (the ENG-006 grid was removed by the ENG-025 port). Mockup source: `Pegasus_UI_v2_src/src/23-damage-diagram.js`, `40-engineer.css`.

## Approach

- One component drawn once (`wwwroot/js/damage-diagram.js`), keyboard operable; zones map to the Core codes.
- Renderer prints the same SVG.

## Verification

- [ ] Click and Enter toggle a zone and add an Impacts row.
- [ ] Report preview prints the marked diagram.
- [ ] Browser test at three widths.

## Outcome


## Current preparation handoff

Research@c56007a0e80d6697 and files@66f9e028c99fd81e; root approved the
bounded canonical-code/shared-geometry shape, not execution. Whole plan
and path handoff remain outstanding. No builds/tests/source changes occurred.

## Root bounded plan review — 2026-09-08

Root fully read plan@8e5c42c8c0ebf15b, files@66f9e028c99fd81e and
open-questions@1838b908abc04b95. Approved the proposed design/scope:
existing 34 canonical entries, one embedded geometry with actual Case/PDF
callers, unchanged three-member ReportImpact retaining canonical codes,
native typed global Save and the bounded existing-browser harness.

This is not take or source authority. Exact predecessor/shared-index
handoffs remain outstanding; the combined execution prerequisite stays
unchecked. No claim, stage, branch, source or evidence state is changed.
