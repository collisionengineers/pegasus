# Files — TICK-058

## Where the change lands

| Path | Why |
|---|---|
| `src/Pegasus.Web/` | Add the first provider authentication handler and the real submission endpoint together in the existing Azure Container App. |
| `src/Pegasus.Core/Intake/` | Reuse `IGroupedIntakeSubmission`/`ReceiveIntake`; add no second intake policy owner. |
| `src/Pegasus.Infrastructure/Persistence/` | Consume TICK-061's credential verification port and existing SQL/outbox; add no intake store. |
| `tests/Pegasus.Core.Tests/`, `tests/Pegasus.IntegrationTests/`, `tests/Pegasus.ArchitectureTests/` | Prove transport mapping, authentication, replay/conflict, isolation, durability, composition, and dependency direction. |
| `docs/frd/frd-09-provider-and-intermediary-routes.md`, `docs/capabilities.md`, `docs/current-architecture.md` | Settle the exact public contract before code and record the eventual caller. |

## Existing code/resources reused

`GroupedIntake.cs`, `DurableIntake.cs`, existing upload envelope limits, Azure SQL outbox, transport Storage Queue, Function Worker, custody Storage, Web managed identity, Container App HTTPS ingress, and Application Insights.

## Out of scope

API-02/status vocabulary, synchronous processing, general Case lookup, files/reports returned to providers, outbound delivery, APIM/Front Door/Service Bus/new Function/new store, live activation, and latency optimization.
