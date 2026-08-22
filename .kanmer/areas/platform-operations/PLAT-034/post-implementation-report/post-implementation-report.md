# Post-implementation report

**Branch:** `task/qdos26009-operator-fixes` · **PR:** #506 · **Commit:** `ca564ac5`

## What was built

`Pegasus.Web` gains the Application Insights package and registers telemetry in the
Production composition block, gated on the connection string being present so
DevelopmentOffline is unaffected. Both hosts now supply the Entra credential ingestion
requires, reusing each host's existing managed-identity credential.

## The diagnosis, and what it cost

The ticket was filed as "no telemetry in 12 hours". Widening the window changed its meaning
entirely: **zero rows in 30 days.** This was never a Release 17 regression — it is a
standing gap that has been there since the estate was built.

Two independent faults produced one symptom:

- the Web host had **no telemetry package and no registration at all**, while its container
  has carried `APPLICATIONINSIGHTS_CONNECTION_STRING` throughout;
- the Worker registered the SDK correctly but never gave it a credential, so Entra
  ingestion rejected everything it sent.

Both fail silently by design — a telemetry client that cannot authenticate drops rather
than throws. That is why an estate with configured alerts, a healthy component and correct
RBAC produced nothing, and why the absence of alerts meant nothing either.

## What was ruled out before changing anything

Connection string present and naming the right component; component workspace-based with
ingestion enabled and 90-day retention; `disableLocalAuth` not set; **both** runtime
identities holding `Monitoring Metrics Publisher`; the worker identity assigned to the app.
The usual causes were all absent, which is what pointed at the applications rather than the
estate.

## The real cost, recorded

A custody operation failed in production and its exception could not be read. Diagnosis of
[[DOCS-008]] fell back to reading source and writing reproductions — three of them — and
still has not identified the fault. That is the price of an instrument that fails quietly.

## Evidence

- Both hosts build clean; 916 Core and 99 architecture tests pass
- **Not proved:** ingestion itself. Until a deployed run puts rows in the workspace,
  correlation, sampling, retention and alert delivery remain unverified, and
  `docs/current-architecture.md` now says exactly that rather than implying telemetry works.

## Next

Phase 6: a deliberate request and a handled exception from each host, read back from the
workspace, then re-run the failed `create_case_custody` work item and finally read the
exception behind [[DOCS-008]].
