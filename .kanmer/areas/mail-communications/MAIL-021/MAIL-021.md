---
id: MAIL-021
type: ticket
title: >-
  Correct the StaleAfter rationale in RetainedMail for the five-minute recovery
  schedule
status: done
area: mail-communications
order: 2460
assignee: claude-code
profile: chore
stageEntered:
  preparing: '2026-08-27T11:24:25.861Z'
  review: '2026-08-27T16:38:07.892Z'
  verifying: '2026-08-27T17:07:48.352Z'
  done: '2026-08-27T17:11:33.113Z'
labels:
  - docs
  - mailbox
groups:
  - EPIC-010
links:
  - MAIL-022
commits:
  - 267b45a0
  - 86113ea15af69c27ae676b2c11b3a6bfb90e41e1
prs:
  - '#575'
archived: false
created: '2026-08-27T10:06:22.871Z'
updated: '2026-09-01T14:44:34.037Z'
---

## Problem

`src/Pegasus.Core/Intake/RetainedMail.cs` (StaleAfter comment) reasons from a one-minute inbound poll; recovery now runs every five minutes with notifications as the primary path.

## Required outcome

Comment states the current schedule and the meaning of the 15-minute threshold under it; no behaviour change.

## Outcome

Shipped as planned: comment-only change to the `StaleAfter` remarks in
`src/Pegasus.Core/Intake/RetainedMail.cs`, threshold unchanged. PR #575
merged into `dev` 2026-08-27 at `86113ea15af69c27ae676b2c11b3a6bfb90e41e1`.
Review finding RF-1 (stale `docs/open-decisions.md` sentence) deferred to
[[MAIL-022]].
