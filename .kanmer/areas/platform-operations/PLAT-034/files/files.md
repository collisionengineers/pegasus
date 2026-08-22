# Files

Committed in `ca564ac5`.

| File | Change |
| --- | --- |
| `src/Pegasus.Web/Pegasus.Web.csproj` | Adds `Microsoft.ApplicationInsights.AspNetCore` — the Web host had no telemetry package at all |
| `src/Pegasus.Web/Program.cs` | Registers telemetry in the Production block when the connection string is present, and supplies the Entra credential ingestion requires |
| `src/Pegasus.Worker/Program.cs` | Gives the worker's telemetry client the same credential; it registered the SDK but never authenticated |

## Ruled out first, so the fix is not a guess

| Checked | Result |
| --- | --- |
| Connection string present and naming the right component | yes, `ApplicationId=b2c7c738…`, ingestion endpoint `uksouth-1` |
| Component healthy | workspace-based, ingestion Enabled, retention 90 days |
| `disableLocalAuth` | not set — key auth would have been allowed |
| `Monitoring Metrics Publisher` on both runtime identities | **held by both** — not the usual missing-role cause |
| Worker identity assigned to the app | yes, `UserAssigned` |
| Any telemetry in 30 days | **none** — so never a regression, a standing gap |

## Not claimed

Ingestion is not proved until a deployed run produces rows. Correlation, sampling,
retention and alert delivery remain unverified, and `docs/current-architecture.md` says so.
