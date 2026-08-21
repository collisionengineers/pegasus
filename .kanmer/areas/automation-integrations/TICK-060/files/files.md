# Files — TICK-060

## Where the change lands

| Path | Why |
|---|---|
| `src/Pegasus.Core/Intake/` | Add the single principal-authorized terminal-result query and its minimal nonterminal/success/failure result type. |
| `src/Pegasus.Infrastructure/Persistence/` | Resolve staged receipt ownership, terminal work outcome, processed receipt, and actual Case link in one read model without duplicating state. |
| `src/Pegasus.Web/` | Add the authenticated GET endpoint and stable HTTP mappings. |
| `tests/Pegasus.Core.Tests/`, `tests/Pegasus.IntegrationTests/`, `tests/Pegasus.ArchitectureTests/` | Prove ownership, fail-closed indistinguishability, actual Case-link authority, nonterminal retry, failures, and composition. |
| `docs/frd/frd-09-provider-and-intermediary-routes.md`, `docs/capabilities.md`, `docs/current-architecture.md` | Replace status/result wording with the terminal-result contract and record the implemented caller. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `src/Pegasus.Infrastructure/Persistence/EfQueuedIntakeStatusQueries.cs` | Existing join shape can be reused, but its staff state vocabulary must not leak externally. |
| `src/Pegasus.Core/Intake/DurableIntake.cs` | Work completion and failure authority and the staged-to-processed identity chain. |
| `src/Pegasus.Infrastructure/Persistence/EfIntakeWorkStore.cs` | Durable state codes and completed evaluation queries already exist. |
| `src/Pegasus.Infrastructure/Persistence/EfCaseAcceptanceStore.cs` | Actual Case creation/link persistence is the reference authority. |
| `docs/current-architecture.md` | A processing decision alone must never be reported as an allocated Case. |

## Ripple effects

Depends on API-01 receipt identity and API-04 authentication. Contract tests must pin 404 equivalence for unknown and foreign receipts, and docs must remove API-02 references.

## Out of scope

Transient state names, progress percentages, receipt contents/source download, general Case reads, workflow actions, list/search endpoints, webhooks, and performance changes.
