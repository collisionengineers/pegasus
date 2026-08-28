---
id: KANMER-006
type: ticket
title: Reconcile the current Kanmer setup drift
status: verifying
area: kanmer-meta
assignee: claude-code
profile: chore
stageEntered:
  preparing: '2026-08-28T08:08:09.750Z'
  review: '2026-08-28T08:13:41.548Z'
  verifying: '2026-08-28T08:19:26.546Z'
taken_at: '2026-08-28T08:11:49.416Z'
branch: task/kanmer-006-setup-drift
worktree: ../pegasus-worktrees/kanmer-006-setup-drift
labels:
  - kanmer
  - setup
  - board-groom-follow-up
groups:
  - EPIC-011
links:
  - KANMER-003
  - TICK-222
commits:
  - cc8863d3
  - 0248da08
prs:
  - '#582'
archived: false
created: '2026-08-25T06:35:41.710Z'
updated: '2026-08-28T08:19:26.546Z'
---

## What

Reconcile the repository's current Kanmer-managed setup after [[TICK-222]] and release 28, then finish the one board-folder move that the current Windows process lock refused.

## Why

Live `get_status` reports that `.claude/skills/kanmer-setup` differs from packaged Kanmer 0.3.3 and `.claude/skills` has no ownership/version stamp. The missing `questions-resolved` text in `board.yml` is runtime-compensated and is not a defect.

The full-board groom cleared TICK-222's obsolete `docs_todo` flag, but three fresh `update_item` attempts to assign its evidenced `delivery-repository` area failed with Windows `EPERM` while renaming `.kanmer/areas/_none/TICK-222`. No manual filesystem move was attempted.

## Approach

- Use the `kanmer-setup` skill after re-reading live status.
- Reconcile only the reported behind/unstamped artefacts.
- Preserve the board worktree and the MCP path corrections delivered by [[TICK-222]].
- After the process lock is gone, use `update_item` to assign TICK-222 to `delivery-repository`; do not move its folder by hand.

## Verification

- [ ] `get_status.repo.upToDate` is true, or every remaining entry is explicitly informational/compensated.
- [ ] TICK-222 is in `delivery-repository`, has `docs_todo: false`, and remains Done with its release evidence unchanged.

## Outcome
