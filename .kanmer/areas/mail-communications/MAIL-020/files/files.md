# MAIL-020 — files

Verified 2026-08-27 by read-only checks on `origin/dev` (a9184315).

| Path | Role | Change |
| --- | --- | --- |
| `src/Pegasus.Worker/Program.cs` | Worker composition root; `AddApplicationInsightsTelemetryWorkerService()` with default modules (dependency tracking on) | Register the SQL dependency telemetry filter |
| `src/Pegasus.Worker/SqlDependencyTelemetryFilter.cs` | New: `ITelemetryProcessor` dropping successful `SQL` dependency items | Create |
| `src/Pegasus.Worker/host.json` | Host-side sampling already excludes `Request;Dependency;Exception` | Unchanged (read for context) |
| `infra/modules/platform.bicep` | `applicationInsights` (line 58) and `logAnalytics` (line 48) declare no cap today; caps were set live | Add `telemetryDailyCapGb` param, `pricingPlans/current` on the component, `workspaceCapping` on the workspace |
| `infra/main.bicep` | Passes params to the platform module | Unchanged unless the param needs surfacing (default suffices) |
| `docs/operations.md` | Release 19 note (lines 712–719) and the Monitoring/cost bullet (1177–1179) document the 0.1 GB cap | Record the new telemetry shape and the pending live cap change |
| `src/Pegasus.Web/Program.cs` | `AddApplicationInsightsTelemetry()` (line 216) | Unchanged — the brief attributes `AppDependencies` volume to the Worker |

Out of scope: `APPLICATIONINSIGHTS_ENABLEADAPTIVESAMPLING` app settings (unchanged), perf counters/metrics (15 MB of the window; separate ticket if still needed after this lands).
