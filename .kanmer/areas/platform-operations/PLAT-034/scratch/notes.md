## Release 18 deployed — the fix did NOT work. Ticket stays in review.

Deployed `1f3be493` on 2026-08-22, revision `pegasus-prod-web-252ow37gij--1f3be493c8c6`,
smoke passed. Then generated traffic (5 × `/health/live` → 200, `/Cases` → 302) and waited
seven minutes.

**Still zero telemetry.** Both the component and the workspace:

```
union traces,requests,exceptions,dependencies | ago(30m)   -> (no rows)
AppRequests 0 | AppTraces 0 | AppExceptions 0 | AppDependencies 0   (workspace, 1h)
```

### What the deploy did prove

- The SDK **is** shipped. `web.zip` carries `Microsoft.ApplicationInsights.AspNetCore.dll`,
  `Microsoft.ApplicationInsights.dll`, `Microsoft.Extensions.Logging.ApplicationInsights.dll`;
  `worker.zip` carries the WorkerService and Functions equivalents. So the package change
  landed and the registration code is in the running image.
- The Web app is healthy and serving — its own console logs show EF commands flowing
  normally, so the process is fine and this is not a startup failure.

### Two things found that were not in the original diagnosis

1. **`APPLICATIONINSIGHTS_AUTHENTICATION_STRING` is an App Service / Functions *host*
   convention.** The Web app is a **Container App** running plain ASP.NET Core — nothing in
   that host reads the setting. So the assumption that it "forces Entra ingestion" for the
   Web host is wrong, and `SetAzureTokenCredential` may be solving a problem the Web host
   does not have while leaving the real one untouched.
2. **The Container Apps environment sends console logs to `azure-monitor`, not
   `log-analytics`**, and its `logAnalyticsConfiguration.customerId` is null. That is why
   `search *` over the workspace returns only `Operation` rows — container stdout never
   reaches it. A separate gap from the SDK one, and worth its own line in the fix.

### Next experiment, deliberately not run yet

The cheapest decisive test is to drop `SetAzureTokenCredential` (or remove the AAD setting)
so the SDK uses key-based ingestion — `disableLocalAuth` is not set on the component, so
that path is permitted. That is a code or config change plus another deploy, and it is the
right next step rather than more inference.

**This ticket is not done and must not be moved to done.** The fix shipped; the outcome it
claims is not true.
