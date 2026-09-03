---
id: KANMER-008
type: ticket
title: Apply the 2026-08-25 full-board groom
status: done
area: kanmer-meta
order: 2140
assignee: ''
profile: chore
labels:
  - board-groom
  - audit
links:
  - DELIV-018
  - KANMER-006
  - KANMER-007
  - INTK-037
  - INTK-038
deployment: n/a
archived: false
created: '2026-08-25T06:58:08.309Z'
updated: '2026-09-03T09:06:54.576Z'
---

## What

Apply the approved 2026-08-25 full-board Kanmer groom as a board-only change.

## Starting evidence

The live starting snapshot held 363 active and 114 archived tickets: 82 Backlog, 23 Preparing, 2 Implementing, 0 Review, 11 Verifying, and 245 Done. It had 91 recorded takes, 0 off-board stages, and 0 parse warnings.

## Applied changes and plain justification

- Retired four completed or superseded group definitions and replaced HZN-002 with the thematic EPIC-009. A horizon should express current schedule; the 25 member tickets already carry their own schedule labels, while the epic now expresses the durable external-integration relationship.
- Renamed the `pr-review` area to **Review Findings**. The tickets are findings from review, not pull requests themselves.
- Archived 49 completed review findings plus PR-026 and six duplicate, superseded, or over-broad tickets. Archive keeps their evidence and links while removing finished noise from the working board.
- Removed `pr-review`, `blocking`, `blocked`, `redesign`, and `post-alpha` from 131 tickets. Area, typed dependency edges, EPIC-008 membership, and `next`/`later` already state those facts once.
- Consolidated CASE-002 into TICK-055; archived CASE-004 in favour of delivered CASE-017; split PLAT-015 into focused owners; narrowed PLAT-032 and CASE-011; corrected INTK-001, TICK-001, TICK-085, TICK-097, AUTO-003, PLAT-035, and TICK-102.
- Added INTK-037 and INTK-038 because Triage identity copy and Image Intake diagnostic copy are separate operator problems with separate evidence.
- Linked 38 capability tickets to their existing governing documents and resolved all 40 identified false governing-document debts without inventing new specifications.
- Cleared 17 obsolete `docs_todo` flags, including TICK-222 after its concurrent release completed.
- Released 80 stale completed takes after recording their branch/worktree evidence. No branch or worktree was removed.
- Backfilled the ten Verifying tickets' real PR/commit provenance and 43 proven deployment fields; replaced the false DOCS-001→PLAT-007 blocker with a normal relation.
- Closed PR-026, TICK-100, TICK-206, TICK-214, and TICK-216 only after their real gates passed. TICK-216 now says Andy Patterson is the sole complete accepted engineer tuple; Ed Mawdsley and Neil O'Reilly remain unavailable pending accepted qualifications.
- Added DELIV-018, KANMER-006, and KANMER-007 because arithmetic drift, setup drift, and inconsistent historical Done evidence require focused proof rather than speculative edits.

## Preserved concurrent work

Release 28 completed TICK-222, DELIV-017, ENG-016, and PR-055 through PR-061 while this groom ran. Their evidence and completion were preserved; the newly completed review findings were archived under the same approved rule.

## Known exception

TICK-222 is Done, released, and no longer claims outstanding documentation, but it remains area-less. Three fresh Kanmer `update_item` attempts failed with Windows `EPERM` while renaming its ticket folder. KANMER-006 owns the retry after setup/reconnect releases that process lock; no manual filesystem move was attempted.

## Concurrent GUI change after the groom

At 06:58:52Z the GUI archived PLAT-005 while it was still Implementing and taken. That action was outside this groom and was preserved. The board now reports ten visible active takes; PLAT-005 retains its claim inside the archive and needs the human owner's intended disposition.

## Outcome

Before this audit record and the later GUI action, the groom result was 312 active and 170 archived tickets with 11 legitimate active takes. The final live snapshot is 312 active and 171 archived tickets: 82 Backlog, 19 Preparing, 0 Implementing, 0 Review, 10 Verifying, and 201 Done. It has 0 warnings, 0 off-board stages, no redundant target labels, and 56/56 approved archive targets retired. No repository source, Git branch/worktree, PR, deployment, cloud, or external-system write was performed by this groom.

The final evidence is recorded in this ticket's `proof.md`.
