# Post-implementation report

**Branch:** `task/qdos26008-regressions` · **PR:** #505 · **Commit:** `1a86f5db`

## What was built

| Setting | Was | Now |
| --- | --- | --- |
| `extensions.queues.maxPollingInterval` (`host.json`) | **unset** → 60s default per hop | `00:00:02` |
| `ApprovedInboxPollSchedule` | `45 * * * * *` | `*/15 * * * * *` |
| `IntakeStagedArtifactReconciliationSchedule` | `30 * * * * *` | `*/10 * * * * *` |
| `PendingWorkDispatchSchedule` | `*/15 * * * * *` | `*/5 * * * * *` |

Mirrored in `local.settings.example.json` and `Invoke-LocalDevelopment.ps1` so local and
deployed stay identical.

## The unset `maxPollingInterval` was the largest single cost

Each queue hop idled back off to the Azure Functions 60s default, and there are two
(`intake-work`, `external-work`) — up to 120 seconds of pure waiting before any timer
granularity is counted. That alone accounts for most of the reported 30–60s+ and is why
Box folder creation, which sits behind the reconciliation tick *and* a second queue hop,
lagged furthest.

## Cost arithmetic, as the ticket required

Flex Consumption bills per execution; these are no-op ticks. Inbox poll 1→4/min,
reconciliation 1→6/min, dispatch 4→12/min — about **+20 executions per minute** of no-op
work, ~29,000/day. Against the £75 budget alert this is immaterial, and the alert is the
backstop if that estimate is wrong.

## Stated honestly rather than hidden

**Cold start remains.** The operator chose queue-polling and timer tightening only — no
always-ready instance, no Graph change notifications. This ticket removes the *idle
back-off*, which is a different and larger cost than cold start. The first request after an
idle period still pays it.

## Deployment consequence — this is not a code deploy

`infra/modules/platform.bicep` changed, so this needs **`azd provision`**. The `host.json`
change ships inside the Worker package, which must go via
`az functionapp deployment source config-zip` — `azd deploy worker --from-package`
triggers an Oryx rebuild that rejects the pre-published package and crash-loops the host.

Verified live before the release, so the "before" is on the record:

```
ApprovedInboxPollSchedule                     45 * * * * *
PendingWorkDispatchSchedule                   */15 * * * * *
IntakeStagedArtifactReconciliationSchedule    30 * * * * *
```

## Evidence

- Bicep, `host.json`, local settings and the dev script all carry the same values
- Deployed settings matching after provision — Phase 5
- **Measured** received-to-case-visible and received-to-Box-folder timings, before and
  after, in numbers — Phase 6. No timing claim is made here; nothing about this ticket can
  be proved without a real arrival.
