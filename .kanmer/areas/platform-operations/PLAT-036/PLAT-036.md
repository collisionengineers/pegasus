---
id: PLAT-036
type: ticket
title: Raise or earn back the Application Insights daily ingestion quota
status: done
area: platform-operations
order: 2040
assignee: codex-mcp-client
profile: chore
stageEntered:
  preparing: '2026-08-25T15:37:36.501Z'
  review: '2026-08-25T15:46:19.108Z'
  verifying: '2026-08-25T16:20:53.580Z'
  done: '2026-08-25T16:22:26.887Z'
labels:
  - observability
  - needs-operator-decision
  - cost
links: []
blocks:
  - DELIV-021
commits:
  - 702737f2
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/550'
archived: false
created: '2026-08-22T06:02:29.302Z'
updated: '2026-08-26T14:34:46.550Z'
---

## Why

The estate is instrumented and both hosts report — [[PLAT-034]] closed that. What
is not solved is keeping the signal for a whole day.

```
workspaces/pegasus-prod-logs-252ow37gij   dailyQuotaGb           = 0.1
                                          quotaNextResetTime     = 03:00Z
                                          dataIngestionStatus    = RespectQuota
components/pegasus-prod-appi-252ow37gij   DataVolumeCap.Cap      = 0.1
```

The estate exhausts 100 MB within roughly six hours of the reset, so ingestion
stops for the rest of the day. The 3-hour histogram is unambiguous:

```
2026-08-20 03:00Z  354     2026-08-21 03:00Z  895     2026-08-22 03:00Z  1,450
2026-08-20 06:00Z  756     2026-08-21 06:00Z  1,328
2026-08-20 09:00Z  110     2026-08-21 09:00Z  19
2026-08-20 12:00Z    2     (nothing until 03:00Z)
```

Everything Pegasus does during a UK working day is invisible.

## What it has already cost

Both production custody failures fell in capped windows and left no trace:
QDOS26009 at 23:00:58Z, QDOS26010 at 02:02:19Z. [[DOCS-008]] had to be diagnosed
by reading `sys.database_permissions` instead of a stack trace, and hours went
into reproducing it locally first. It also produced a confidently wrong
diagnosis — that the estate emitted nothing at all for thirty days — which was
written into two governing documents before being corrected.

The two alert rules (`pegasus-prod-web-http5xx`,
`pegasus-prod-application-exceptions`) are blind for the same window, which is
the more serious version of the same fault: an incident during business hours
raises nothing.

## Two routes, and the order matters

**Measure before buying.** `APPLICATIONINSIGHTS_ENABLEADAPTIVESAMPLING` is `true`
on both hosts, and the Worker produced **158,542 dependency records in a week**
for an estate processing about two cases a day. That is polling noise, not
business signal. Establish where the volume actually goes before raising spend:

1. Attribute the volume by table, role and operation over one uncapped day.
2. Suppress or sample what turns out to be the Worker's own timer polling —
   `PendingWorkDispatchFunction`, `InboxPollFunction`, `DueWorkSweepFunction`
   fire on tight schedules and each emits a request plus dependencies.
3. Only then set a quota that covers a working day with headroom.

## Needs the operator

Raising `dailyQuotaGb` is a **cloud write with a recurring billing
consequence**, so the number is not mine to choose. This ticket cannot leave
Backlog until the operator names it, or accepts route 1 as the first step.

## How to verify

A full UK working day with `dataIngestionStatus` never entering
`RespectQuota`, and a deliberate handled exception raised at 15:00Z appearing in
the workspace within minutes with correlation intact.
