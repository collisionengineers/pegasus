---
id: MAIL-022
type: ticket
title: >-
  Correct the stale-threshold rationale in docs/open-decisions.md for the
  five-minute recovery schedule
status: done
area: mail-communications
order: 2500
assignee: claude-code
profile: chore
stageEntered:
  preparing: '2026-08-27T22:04:46.291Z'
  implementing: '2026-08-27T22:04:47.944Z'
  review: '2026-08-27T22:04:50.336Z'
  verifying: '2026-08-27T22:04:52.347Z'
  done: '2026-08-27T22:05:19.597Z'
labels:
  - docs
  - mailbox
groups:
  - EPIC-010
links:
  - MAIL-021
  - DELIV-029
commits:
  - cb2ab070
  - 68adedafb9159772515b1b4fb9758f0ab2261fe7
prs:
  - '#578'
archived: false
created: '2026-08-27T17:06:22.245Z'
updated: '2026-09-03T09:06:57.040Z'
---

## Problem

`docs/open-decisions.md` line 314 (Stale threshold row) still says "Ship the provisional 15 minutes (fifteen missed one-minute ticks)". [[MAIL-021]] corrected the `StaleAfter` remark in `src/Pegasus.Core/Intake/RetainedMail.cs` to the current model (Graph change notifications primary; `InboxRecoveryFunction` on `ApprovedInboxPollSchedule` = `0 */5 * * * *`, so 15 minutes = three missed recovery ticks), and that remark cites this open decision. Raised by the Codex review thread on PR #575.

## Required outcome

The open-decisions row states the current schedule and the meaning of the 15-minute threshold under it; no behaviour change, no other doc edits.
