# Plan

Committed in `ca564ac5`.

## Two independent faults, one symptom

**The Web host was never instrumented.** No Application Insights package, no registration,
while the deployed container app has carried `APPLICATIONINSIGHTS_CONNECTION_STRING` since
the estate was built. Nothing read it.

**The Worker authenticated with nothing.** It registers
`AddApplicationInsightsTelemetryWorkerService` and `ConfigureFunctionsApplicationInsights`,
and the deployed app sets `APPLICATIONINSIGHTS_AUTHENTICATION_STRING` naming its
user-assigned identity — but the worker process's own telemetry client was never given a
credential, so Entra ingestion rejected what it sent.

Both fail **silently by design**: a telemetry client that cannot authenticate drops rather
than throws. That is why nobody noticed for thirty days, and why it took a custody failure
that had to be diagnosed without logs to surface it.

## The change

Register telemetry on the Web host in the Production block, gated on the connection string
being present so nothing changes for DevelopmentOffline; and set the Azure token credential
on `TelemetryConfiguration` in both hosts, reusing each host's existing managed-identity
credential rather than constructing a second identity story.

## Why it matters beyond one bug

The runbook requires correlated Web/Worker telemetry and alerts for a releasable
implementation, and states that only deployed live evidence can prove ingestion, sampling,
KQL, retention and recipient delivery. None of that was provable. The estate's two alert
rules — `pegasus-prod-web-http5xx` and `pegasus-prod-application-exceptions` — could not
fire on data that never arrived, so the absence of alerts meant nothing.

## Acceptance

- Both hosts build and register telemetry. ✅
- DevelopmentOffline is unaffected — registration is gated on the connection string. ✅
- Live: a deliberate request and a handled exception from each host appear in the workspace
  within minutes, with correlation intact, and the exception alert rule is shown to
  evaluate against real rows — Phase 6. **Until then this ticket is not proved.**

## Simplification pass

2026-08-22. Reuses each host's existing credential; adds no second identity path, no
wrapper, no configuration surface. No findings deferred.
