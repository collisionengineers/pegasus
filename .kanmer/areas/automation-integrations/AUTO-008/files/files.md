# Files — AUTO-008

## Where the change lands

| Path | Why |
|---|---|
| Ticket `research/` and ignored `artifacts/` outputs | Record measurement method, raw timing output, percentile summary, and recommendation without changing application code during the spike. |
| `src/Pegasus.Core/Intake/DurableIntake.cs` | Read/instrument in a later follow-up at durable receipt, processing claim, evaluation completion, and terminal tail boundaries if existing telemetry is insufficient. |
| `src/Pegasus.Worker/IntakeFunctions.cs` and Worker configuration | Identify dispatcher cadence and enqueue boundary; any schedule or dispatch change is a separate follow-up. |
| `src/Pegasus.Core/Intake/ProcessIntake.cs` | Reuse existing `intake.duration_ms` as the inner-processing measurement. |
| `tests/Pegasus.IntegrationTests/` | Host representative fixture runs and timing probes without corpus mutation. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `src/Pegasus.Infrastructure/Persistence/EfIntakeWorkStore.cs` | Durable state transitions and timestamps available for queue/processing timing. |
| `src/Pegasus.Core/Custody/ExternalWorkProcessing.cs` | The shared timer dispatches two durable outboxes sequentially; optimizing intake must not starve external work. |
| `src/Pegasus.Worker/AzureQueueIntakeWorkQueue.cs` | Actual enqueue boundary and external queue effect. |
| `src/Pegasus.Core/Intake/ProcessIntake.cs` | Existing Activity duration covers only the inner evaluation. |
| `docs/engineering.md` | Efficiency findings need concrete repeated/blocking work and a concrete alternative. |
| `docs/runbook.md` | Canonical verification and permitted local evidence boundaries. |

## Ripple effects

A measured bottleneck may produce separate tickets for dispatch cadence, inline outbox notification, query reduction, extraction cost, or telemetry. API-01 remains durable and asynchronous regardless of healthy-path speed.

## Out of scope

Changing code or schedules in this spike, production load tests, cloud writes, corpus modification, a provider-facing progress feature, retry-policy changes, and fabricated TypeScript comparisons.
