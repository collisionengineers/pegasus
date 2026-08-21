# Plan — AUTO-008: Measure durable intake processing latency

## Approach

Measure the current durable path before changing it. Separate SQL receipt-to-dispatch wait, queue enqueue-to-Function claim, inner processing, Case allocation/post-processing, and retries. Use existing timestamps/Application Insights where sufficient; record any missing telemetry as a focused follow-up rather than changing architecture in this spike.

## Steps

1. Define healthy-path timestamps and a disclosure-safe correlation method for durable receipt, dispatch/enqueue, queue trigger, inner `ProcessIntake`, actual Case link, and terminal persistence.
2. Query existing local/integration evidence and read-only Application Insights aggregate telemetry where available; never collect provider content or identifiers.
3. Run representative repository fixtures repeatedly and report median, p95, and worst case for each segment; report retry paths separately.
4. Test the live verified 15-second dispatcher cadence as a latency hypothesis and measure Azure Queue trigger polling separately.
5. If dispatch dominates, compare in order: shorter existing schedule; an existing-dispatcher wake-up with SQL reconciliation retained; only then a messaging change.
6. Record the smallest justified recommendation, expected gain, failure/replay impact, and create separate implementation tickets for code/config changes.

## Azure resource decision

Reuse Azure SQL, Queue Storage, Function Worker, Application Insights, and Log Analytics. Do not add Service Bus, Event Grid, another Function, or bypass the SQL outbox without measurements proving the current components cannot meet the target.

## Verification

The research artifact contains sample size, fixtures, environment, percentile method, segment timings, healthy/retry separation, and a recommendation traceable to evidence. No cloud state changes occur.
