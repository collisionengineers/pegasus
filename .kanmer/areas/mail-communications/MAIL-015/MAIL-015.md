---
id: MAIL-015
type: ticket
title: Correct the invalid Inbox recovery NCRONTAB schedule
status: done
area: mail-communications
order: 2430
assignee: codex-mcp-client
profile: fix
stageEntered:
  implementing: '2026-08-26T18:48:58.264Z'
  review: '2026-08-26T18:51:33.775Z'
  verifying: '2026-08-27T09:22:20.162Z'
  done: '2026-08-27T09:36:58.501Z'
labels:
  - mailbox
  - timer
  - release-defect
links: []
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
commits:
  - f01fed3f
prs:
  - '566'
deployment: production
archived: false
created: '2026-08-26T18:48:47.847Z'
updated: '2026-09-03T09:06:56.549Z'
---

## Problem

Release 33 configured `ApprovedInboxPollSchedule` with seven NCRONTAB fields (`0 */5 * * * * *`). Azure Functions expects six, so the recovery timer may not index or fire.

## Required outcome

Use the valid five-minute schedule `0 */5 * * * *` in the single infrastructure owner and every test/example derived from it. Deploy and verify the function is discoverable and the exact live setting is corrected.

## Outcome
