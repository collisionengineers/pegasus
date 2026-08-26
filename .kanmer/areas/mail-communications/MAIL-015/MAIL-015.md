---
id: MAIL-015
type: ticket
title: Correct the invalid Inbox recovery NCRONTAB schedule
status: implementing
area: mail-communications
assignee: codex-mcp-client
profile: fix
stageEntered:
  implementing: '2026-08-26T18:48:58.264Z'
taken_at: '2026-08-26T18:48:58.199Z'
branch: task/mail-015-recovery-cron
worktree: 'C:/Users/PC/Documents/GitHub/pegasus-worktrees/mail-015-recovery-cron'
labels:
  - mailbox
  - timer
  - release-defect
links: []
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
deployment: not-deployed
archived: false
created: '2026-08-26T18:48:47.847Z'
updated: '2026-08-26T18:48:58.264Z'
---

## Problem

Release 33 configured `ApprovedInboxPollSchedule` with seven NCRONTAB fields (`0 */5 * * * * *`). Azure Functions expects six, so the recovery timer may not index or fire.

## Required outcome

Use the valid five-minute schedule `0 */5 * * * *` in the single infrastructure owner and every test/example derived from it. Deploy and verify the function is discoverable and the exact live setting is corrected.

## Outcome
