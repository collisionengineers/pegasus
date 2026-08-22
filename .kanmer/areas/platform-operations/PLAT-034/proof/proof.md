# Proof

**Shipped:** PR #506, commit `ca564ac5` (Web) · **Deployed:** Release 19,
`42125b34`, and carried forward in Release 20, `05fe7a7f`.

## The Web host now reports, and did not before

Read from `pegasus-prod-logs-252ow37gij` on 2026-08-22. Application Insights
splits by `AppRoleName`; the Container App reports with the role name unset,
which is how its rows are identified here:

```
AppRoleName                      Type              N        First                 Last
pegasus-prod-worker-252ow37gij   AppTraces         71,078   2026-08-15T04:33Z     2026-08-22T04:32Z
pegasus-prod-worker-252ow37gij   AppRequests       15,445   2026-08-15T04:33Z     2026-08-22T04:32Z
pegasus-prod-worker-252ow37gij   AppDependencies  158,542   2026-08-15T04:33Z     2026-08-22T04:32Z
(blank — the Web container)      AppRequests          617   2026-08-22T03:54Z     2026-08-22T04:32Z
(blank — the Web container)      AppDependencies    1,577   2026-08-22T03:55Z     2026-08-22T04:32Z
```

The Web rows begin at **03:54Z on 2026-08-22** — this fix arriving. Before that
the container had carried `APPLICATIONINSIGHTS_CONNECTION_STRING` since the
estate was built while never calling `AddApplicationInsightsTelemetry`, so it
emitted nothing at all. That half of this ticket is fixed and observed live.

## This ticket's original premise was half wrong, and the correction is the finding

It claimed *"the estate emits no telemetry at all"* and *"thirty days of
production produced no traces"*. The Worker had been reporting **continuously
throughout the retained window**. Every check that produced the zero-telemetry
conclusion ran inside a window where ingestion had already stopped, because the
workspace runs a **0.1 GB daily quota resetting at 03:00Z** that the estate
exhausts within hours.

That is a real and serious fault, but it is a capacity and cost decision, not an
instrumentation defect, and it is not mine to price. Split to [[PLAT-036]] with
the measurements, rather than left attached to a ticket whose stated fix is
done.

Two things this ticket recorded as "still to check" are also settled:

- `disableLocalAuth` is **null** on the component — local auth is enabled, so the
  `SetAzureTokenCredential` call added here is not load-bearing on either host.
  It is harmless and left in place; `AddApplicationInsightsTelemetry()` is what
  made the Web start reporting.
- Both runtime identities hold **Monitoring Metrics Publisher** on the component,
  confirmed by role-assignment listing, so RBAC was never the cause.

## Evidence tier

**Observed live.** Row counts and first-seen timestamps read directly from the
workspace, not inferred from deployed code.

## What is still not proved

Correlation across Web and Worker, retention, and alert delivery — none can be
demonstrated while ingestion stops mid-morning. They belong to [[PLAT-036]] and
are stated there as its acceptance conditions rather than claimed here.
