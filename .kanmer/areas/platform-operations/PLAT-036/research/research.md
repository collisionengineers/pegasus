# Research — PLAT-036: retain a full working day of telemetry

## Question

What is consuming the 0.1 GB/day production workspace quota, and what is the smallest repository change that can restore working-day coverage before buying a higher cap?

## Findings

- A read-only Azure resource query on 2026-08-25 confirmed `pegasus-prod-logs-252ow37gij` still has `dailyQuotaGb = 0.1`, was already `OverQuota`, and would next reset at `2026-08-26T03:00:00Z`. The workspace is therefore still blind for the capped part of the day.
- The current Application Insights component is workspace-based (`IngestionMode = LogAnalytics`) and its 2020-02-02 resource payload has no separate `DataVolumeCap`. The effective cap observed now is the Log Analytics workspace cap, correcting the older linked [[PLAT-034]] snapshot that reported two cap surfaces.
- A live seven-day `Usage` query attributed 492.84 MB to `ContainerAppConsoleLogs`, 115.89 MB to `AppDependencies`, 47.82 MB to `AppMetrics`, 46.38 MB to `AppTraces`, 30.94 MB to `AppPerformanceCounters`, 14.31 MB to `AppRequests`, and 6.30 MB to `AppExceptions`.
- The largest source is not the Worker timer telemetry assumed in the ticket body. `ContainerAppConsoleLogs` contained 1,076,364 Web stdout rows and 470.01 billed MB over seven days. Live aggregation of the log prefix shows successful `Microsoft.EntityFrameworkCore.Database.Command[20101]` output dominates it, including repeated readiness SQL such as `SELECT 1` and migration-history queries.
- The repository explains that duplication: `infra/modules/platform.bicep:252-260` routes Container Apps console and system logs to the same workspace, while `src/Pegasus.Web/appsettings.json` leaves the default at Information and has no EF command override. `DatabaseReadinessHealthCheck` runs both `CanConnectAsync` and `GetPendingMigrationsAsync`, and the deployed readiness probe invokes it repeatedly.
- Worker polling is real but secondary. Live seven-day Application Insights rows show 113,550 Worker dependencies / 103.05 billed MB; its main SQL dependency name accounts for 85,992 rows / 76.50 MB. Worker request counts are led by `PendingWorkDispatchFunction` 4,179, `StagedArtifactReconciliationFunction` 2,233, and `InboxPollFunction` 1,872. Those timers are being slowed or replaced by the linked near-real-time intake work, so PLAT-036 should not create a second polling-specific mechanism.
- `src/Pegasus.Worker/host.json` currently excludes Request telemetry from sampling, so timer requests are retained, while both deployed hosts set `APPLICATIONINSIGHTS_ENABLEADAPTIVESAMPLING=true`. Changing this sampling contract is broader and less targeted than removing known successful SQL command noise.
- `infra/modules/platform.bicep` does not declare `dailyQuotaGb`; raising the live cap would therefore be a separately approved cloud write with recurring cost. No quota increase is required to try the measured earn-back route first.
- The repository runbook says Bicep or local tests cannot prove live ingestion, sampling, alerts, or coverage. [[DELIV-021]] already owns release plus the normalized production observation.

## Implications

The first implementation should add one Production-default logging filter: `Microsoft.EntityFrameworkCore.Database.Command = Warning` in `src/Pegasus.Web/appsettings.json`. This preserves EF warnings and failures while stopping successful command text from being written to Web stdout and then ingested through the Container Apps diagnostic setting. A small configuration contract test should lock that filter.

This is the smallest measured change and removes the source responsible for roughly 470 of 755 MB observed over seven days. It avoids an Azure write and should leave substantial headroom beneath 0.1 GB/day. Do not disable Container Apps console diagnostics: those logs were the independent diagnostic path during the earlier capped Application Insights incident. Do not raise the quota or build custom telemetry processors until post-deployment measurement shows the targeted filter is insufficient.

## Open questions

No question blocks planning. The operator has accepted the earn-back-first route; the exact paid quota remains conditional on production measurement.
