# The estate's telemetry is capped, not missing

Read live from the production estate on 2026-08-22 at 04:35Z, after Release 19
(`42125b34`) was deployed. It corrects this ticket's original diagnosis.

## The original premise was half wrong

The ticket said "the estate emits no telemetry at all" and "thirty days of
production produced no traces". Every check that produced that conclusion was
run in a window where ingestion was stopped. Queried inside the ingestion
window, the workspace holds:

```
AppRoleName                      Type              N        First                 Last
pegasus-prod-worker-252ow37gij   AppTraces         71,078   2026-08-15T04:33Z     2026-08-22T04:32Z
pegasus-prod-worker-252ow37gij   AppRequests       15,445   2026-08-15T04:33Z     2026-08-22T04:32Z
pegasus-prod-worker-252ow37gij   AppDependencies  158,542   2026-08-15T04:33Z     2026-08-22T04:32Z
(blank — the Web container)      AppRequests          617   2026-08-22T03:54Z     2026-08-22T04:32Z
(blank — the Web container)      AppDependencies    1,577   2026-08-22T03:55Z     2026-08-22T04:32Z
```

**The Worker was never uninstrumented.** It has been reporting continuously for
the whole retained window. The Web genuinely was not instrumented — its rows
begin at 03:54Z today, which is this ticket's own fix arriving with Release 19.
So half the ticket is fixed and provable; the other half was never broken.

`Monitoring Metrics Publisher` on both identities and `disableLocalAuth: null`
(local auth **enabled**) were already recorded here. With local auth enabled the
`SetAzureTokenCredential` call this ticket added is not load-bearing on either
host — ingestion would authenticate by key regardless. It is not harmful, but it
is not what made the Web start reporting; `AddApplicationInsightsTelemetry()` is.

## What is actually wrong

```
components/pegasus-prod-appi-252ow37gij  DataVolumeCap.Cap = 0.1   ResetTime = 0
workspaces/pegasus-prod-logs-252ow37gij  dailyQuotaGb = 0.1
                                         quotaNextResetTime = 2026-08-23T03:00:00Z
                                         dataIngestionStatus = RespectQuota
```

**A 0.1 GB daily quota, resetting at 03:00Z.** The estate exhausts it within
hours, and the rest of the day is ingested nowhere. The 3-hour histogram shows
the shape exactly:

```
2026-08-20 03:00Z  354     2026-08-21 03:00Z  895     2026-08-22 03:00Z  1,450
2026-08-20 06:00Z  756     2026-08-21 06:00Z  1,328
2026-08-20 09:00Z  110     2026-08-21 09:00Z  19
2026-08-20 12:00Z    2     (nothing until 03:00Z)
(nothing until 03:00Z)
```

Ingestion dies each morning around 09:00–12:00Z and does not resume until the
03:00Z reset. Everything Pegasus does during a UK working day is invisible.

## What this cost, concretely

Both production custody failures fell inside a capped window and left no trace:

| Case | Custody due | Ingested? |
| --- | --- | --- |
| QDOS26009 | 2026-08-21T23:00:58Z | no — capped since ~09:00Z |
| QDOS26010 | 2026-08-22T02:02:19Z | no — capped, 58 minutes before reset |

That is why [[DOCS-008]] could not be diagnosed from logs, and why several hours
went into reproducing it locally instead. The exception was thrown, caught,
recorded as `custody_unexpected_failure` in `ExternalWorkItems`, and its stack
trace was discarded at the ingestion boundary.

The two alert rules (`pegasus-prod-web-http5xx`,
`pegasus-prod-application-exceptions`) are blind for the same window, which is
the more serious version of the same fault.

## What is left to do

Raising `dailyQuotaGb` on `pegasus-prod-logs-252ow37gij` and `DataVolumeCap.Cap`
on `pegasus-prod-appi-252ow37gij` is a **cloud write with a billing
consequence**, so it is not taken without the operator naming the number. It is
the only outstanding item on this ticket; the instrumentation half is done and
provable.

A useful second step once the cap is raised: `APPLICATIONINSIGHTS_ENABLEADAPTIVESAMPLING`
is `true` on both hosts, and 158,542 dependency records in a week from an estate
processing two cases a day says the Worker's own polling is most of the volume.
Sampling the pollers rather than buying more quota may be the cheaper fix, and
should be measured before the cap is raised far.
