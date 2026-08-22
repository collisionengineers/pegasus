---
id: PLAT-034
type: ticket
title: No telemetry is reaching Application Insights
status: verifying
area: platform-operations
assignee: ''
profile: fix
stageEntered:
  preparing: '2026-08-22T00:49:06.634Z'
  implementing: '2026-08-22T00:49:09.347Z'
  review: '2026-08-22T00:51:22.121Z'
  verifying: '2026-08-22T04:36:13.807Z'
labels:
  - observability
  - release-17
  - blocking-diagnosis
links: []
docs_todo: true
deployment: not-deployed
archived: false
created: '2026-08-21T23:30:28.100Z'
updated: '2026-08-22T04:36:13.807Z'
---

## Why

Found while diagnosing QDOS26009's custody failure on 2026-08-22: **the estate emits no telemetry at all.**

```
union traces, exceptions, requests | where timestamp > ago(12h) | summarize by itemType
  -> []   (empty)
AppTraces 0 | AppExceptions 0 | FunctionAppLogs 0   (Log Analytics, 6h)
```

A custody operation failed in production and the exception could not be read. That is the cost: diagnosis fell back to reading source and inferring, which is slower and less certain than reading the stack trace.

## What was already ruled out

- The connection string is present on the Worker and names the right component (`ApplicationId=b2c7c738-…`, ingestion endpoint `uksouth-1`).
- The component is workspace-based, ingestion **Enabled**, retention 90 days.
- AAD auth is configured (`Authorization=AAD;ClientId=d7d9a0ad-…`) and **both** runtime identities hold **Monitoring Metrics Publisher** on the component — so it is not the usual missing-role cause.

Still to check: adaptive sampling (`APPLICATIONINSIGHTS_ENABLEADAPTIVESAMPLING` is set), whether the Worker's AAD credential can actually acquire a token for the ingestion audience, and whether the Web container app is wired at all.

## Why this matters beyond one bug

The runbook is explicit that a releasable implementation requires correlated Web/Worker telemetry and alerts, and that only deployed live evidence can prove ingestion, sampling, KQL, retention and alert delivery. Right now none of that is provable, and the two alert rules in the estate (`pegasus-prod-web-http5xx`, `pegasus-prod-application-exceptions`) cannot fire on data that never arrives.

## How to verify

A deliberate request and a deliberate handled exception from each host appear in the workspace within minutes, with correlation intact — and the exception alert rule is shown to evaluate against real rows.
