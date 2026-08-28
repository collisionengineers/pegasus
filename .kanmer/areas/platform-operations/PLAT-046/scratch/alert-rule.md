# The alert rule as it actually stands

Read 2026-08-28 from `az monitor scheduled-query show -g rg-pegasus-prod -n
pegasus-prod-application-exceptions`.

| Property | Value |
| --- | --- |
| Severity | 1 |
| Window | 15 minutes |
| Evaluation frequency | 5 minutes |
| Threshold | `Total > 0`, 1 of 1 failing period |
| Auto-mitigate | true |

The threshold looks naive but is not: the KQL does the filtering, and it is
already built to ignore one-off noise. Three arms, unioned:

1. **`FailedRecent`** — an exception correlated by `OperationId` to an
   `AppRequests` row with `Success == false` in the last 5 minutes. Fires on a
   single occurrence.
2. **`PersistentCorrelated`** — one exception signature across
   **`DistinctOperations >= 3`** in 15 minutes.
3. **`PersistentUncorrelated`** — one signature (no `OperationId`) in
   **`DistinctMinuteBuckets >= 3`** in 15 minutes.

Signatures are normalised: exception type plus the first 512 characters of the
message with GUIDs replaced by `<id>`.

## What this means for the ticket

Our incident cleared every arm comfortably. 13 failed
`StagedArtifactReconciliationFunction` runs is well past `DistinctOperations >=
3`, and 02:56/02:57/02:58 is exactly three minute buckets. The alert did its
job.

**So "retune the alert" is the wrong lever.** Any threshold loose enough to
hide 52 exceptions and 13 failed timer runs over two minutes would also hide a
genuine two-minute outage — and "the Worker is throwing on every tick" is
precisely what this alert exists to report. Raising `DistinctOperations` or
`DistinctMinuteBuckets` buys silence at the cost of the thing it is for.

The honest options are:

- **Fix the cause** so no storm occurs. Then the rule needs no change and keeps
  its current sensitivity. This is the primary fix.
- **Suppress by deployment window**, not by threshold — an Azure Monitor alert
  processing rule scoped to the release window, so a *known* deployment is
  quiet while an unknown fault at the same rate still pages. This is a cloud
  write and needs explicit per-target approval.

Recommend both, in that order, with the second explicitly optional: if the
first works there is no storm to suppress.

## Not to be confused with

`pegasus-prod-web-http5xx` (metric alert, Sev1, PT5M/PT5M) is a separate rule
and did not fire here.
