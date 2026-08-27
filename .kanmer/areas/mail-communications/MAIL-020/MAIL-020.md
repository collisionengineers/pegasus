---
id: MAIL-020
type: ticket
title: 'Web App Insights request/dependency telemetry stopped at 2026-08-27 05:31Z'
status: backlog
area: mail-communications
assignee: ''
profile: fix
labels:
  - observability
links: []
archived: false
created: '2026-08-27T10:06:22.851Z'
updated: '2026-08-27T10:06:22.851Z'
---

## Problem

`AppRequests`/`AppDependencies` from the Web container app stop at 2026-08-27 05:31:56Z while console logs continue and the Worker keeps reporting; the workspace cap is `RespectQuota`. Found during the inbox-stale investigation; independent of it.

## Required outcome

Root cause found and Web telemetry flowing again, with `docs/operations.md` refreshed if the deployed shape changes.
