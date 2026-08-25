---
id: MAIL-005
type: ticket
title: 'Inbox: resolve allocated cases on mail tiles and tidy the outcome cell'
status: done
area: mail-communications
order: 1650
assignee: claude-code
profile: fix
stageEntered:
  preparing: '2026-08-20T19:05:11.282Z'
  review: '2026-08-20T19:15:59.362Z'
  verifying: '2026-08-20T20:26:15.620Z'
  done: '2026-08-20T20:52:47.232Z'
labels:
  - ui
  - mail
  - operator-reported
links: []
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
  - docs/frd/frd-12-operator-experience.md
archived: false
created: '2026-08-20T19:04:50.916Z'
updated: '2026-08-25T06:38:31.857Z'
---

## Why

Operator, 2026-08-20 (feedback round 2): mail tiles are untidy and a message can read "Ready for case allocation" when its case exists.

Live read-only check (2026-08-20, prod `pegasus` DB): the retained-mail projection resolves a case **only** through `CaseIntakeLinks`, which the manual acceptance route writes — the automatic allocation route records the created case on its succeeded `IntakeAllocationAttempts` row and writes no link, so an automatically allocated mailbox message falls through to the decision label. Live rows: 4 linked correctly; 3 with failed/blocked attempts already read "Case not created"; 3 pre-release-14 rows (Aug 13–14) have decision `case_created` with zero attempts — genuinely never allocated, and removed by the T9 test-data wipe.

## What

- The retained-mail projection resolves CaseId/Reference from `CaseIntakeLinks` first, else from the latest allocation attempt that carries a case — so "Ready for case allocation" is unreachable for an allocated receipt and the tile reads "Case created → reference".
- Outcome cell layout: chip and case link aligned on one row with consistent spacing.

## How to verify

Web test: a retained message whose receipt has a succeeded allocation attempt (no link row) renders "Case created" with the reference link. Live after deploy: the allocated messages show their case links.

## Outcome
