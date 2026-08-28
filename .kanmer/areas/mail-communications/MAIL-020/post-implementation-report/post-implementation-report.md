# MAIL-020 — post-implementation report

## Delivered

Branch `task/mail-020-app-insights-cap`, worktree
`C:/Users/Alex/Documents/GitHub/pegasus-worktrees/mail-020-app-insights-cap`,
commit `46a21f92`, PR #576 → `dev`.

| File | Change |
| --- | --- |
| `src/Pegasus.Worker/SqlDependencyTelemetryFilter.cs` | New `ITelemetryProcessor`; drops `DependencyTelemetry { Type: "SQL", Success: true }` |
| `src/Pegasus.Worker/Program.cs` | Production caller: `.AddApplicationInsightsTelemetryProcessor<SqlDependencyTelemetryFilter>()` |
| `infra/modules/platform.bicep` | `var telemetryDailyCapGb = json('0.5')`; `workspaceCapping.dailyQuotaGb` on the workspace; `Microsoft.Insights/components/pricingPlans@2017-10-01` child `current` (`planType: 'Basic'`, `cap`, `warningThreshold: 90`, `stopSendNotificationWhenHitCap: false`) |
| `docs/operations.md` | Release-19 paragraph corrected (component cap at 00:00Z was the limit hit); Monitoring/cost bullet records the declared 0.5 GB with live caps still 0.1 GB |

Plan steps 1–5 all done as planned; no deviations. No packages added.

## Verification

| Command | Exit |
| --- | --- |
| `dotnet restore ./Pegasus.slnx --locked-mode` | 0 |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | 0 (run before and after the simplification edits) |
| `az bicep build --file infra/main.bicep --stdout` | 0 — both caps bound to the one variable, `planType` present |
| `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"` | 0 on the controller's serial run (attempt 2): Core 1001/1001, Architecture 100/100, Integration 987/987, 30 m 28 s; log `artifacts/mail-020/test-full.log`, `artifacts/mail-020/test-exit.txt` = `test exit=0` |

Attempt 1 (concurrent with other lanes) exited 1 with 7 integration
failures showing SQL transport/connection timeouts (LocalDB contention);
it was superseded, not retried by this worker, per the controller.

Simplification pass: recorded in `plan` (11 findings, each applied,
rejected with reason, or deferred).

## Operator approval still required (Azure writes — not performed)

Subscription `e6076573-23a5-46a8-acef-7e22d264e5db`, resource group
`rg-pegasus-prod`, daily cap 0.1 → 0.5 GB on both:

```text
az monitor app-insights component billing update --app pegasus-prod-appi-252ow37gij -g rg-pegasus-prod --cap 0.5
az monitor log-analytics workspace update -n pegasus-prod-logs-252ow37gij -g rg-pegasus-prod --quota 0.5
```

Or let the next release's `azd provision` apply the bicep-declared value.
Until then the live caps remain 0.1 GB and the Worker filter alone will not
keep a full day under the cap. The release that provisions must refresh
`docs/current-architecture.md` and `docs/open-decisions.md`, which still
(correctly, as-built) say 0.1 GB.

## Deferred

- Web host emits unfiltered SQL dependencies (`Pegasus.Web/Program.cs`
  `AddApplicationInsightsTelemetry()`); outside this brief — follow-up
  ticket if Web volume becomes material.
