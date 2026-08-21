# Research — AUTO-008: durable intake latency

## Question

Where can latency enter the current durable intake path, what is already observable, and what must be measured before changing the architecture?

## Findings

- Submission first retains staging bytes and writes a Pending work item through `ReceiveIntake`; successful Web upload returns after durability, before processing (`src/Pegasus.Core/Intake/DurableIntake.cs`, `src/Pegasus.Web/Pages/Upload.cshtml.cs`).
- Pending work is not enqueued inline. `PendingWorkDispatchFunction` runs on `PendingWorkDispatchSchedule`; the checked-in local example is `*/15 * * * * *`, so schedule alignment can contribute 0–15 seconds before queue delivery (`src/Pegasus.Worker/IntakeFunctions.cs`, `src/Pegasus.Worker/local.settings.example.json`).
- Queue delivery then calls `ProcessQueuedIntake`, which reads and hashes staging bytes, stores durable bytes, parses/extracts/classifies, persists receipt/evaluation, deletes staging, associates or allocates a Case, performs image automation, and synchronizes Unidentified work. These sequential boundaries need separate timing before any is blamed (`src/Pegasus.Core/Intake/DurableIntake.cs`).
- `ProcessIntake` already emits an Activity tag `intake.duration_ms`, but it measures only the inner retained-intake evaluation, not receipt-to-dispatch wait or the post-evaluation allocation/automation tail (`src/Pegasus.Core/Intake/ProcessIntake.cs`).
- Retry delays are deliberately 30 seconds, 2 minutes, 10 minutes, 30 minutes, and 2 hours for transient failure; retry latency must not be mixed with healthy-path performance.
- Existing integration tests can invoke `ProcessQueuedIntake` immediately and therefore do not represent timer/queue latency. No checked-in benchmark or percentile evidence was found.
- No older TypeScript intake runtime exists in the tracked repository. The only TypeScript files are design-system assets, so a performance comparison needs an approved predecessor source rather than recollection.
- The operator estimates ordinary processing under five seconds. That is a hypothesis until representative median/p95/worst-case observations separate queue wait and compute time.

## Implications

Do not redesign the durable boundary or expose processing states from static inspection. Instrument timestamps for durable acceptance, dispatch claim/enqueue, processing claim, evaluation completion, and terminal post-processing; then run representative local/integration fixtures repeatedly. Report healthy path separately from retries. If dispatch wait dominates, prefer the smallest safe dispatch improvement that preserves the SQL outbox and recovery semantics; any code change becomes a new implementation ticket.

## Open questions

The measurement method is resolved. Production or predecessor comparison needs separately available evidence.
