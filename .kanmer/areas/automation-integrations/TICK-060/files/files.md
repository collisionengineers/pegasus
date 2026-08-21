# Files — TICK-060

## Where the change lands

| Path | Why |
|---|---|
| `src/Pegasus.Core/Intake/` | Add the single Principal-authorized submission-result query with nonterminal, linked Case/PO success, and terminal failure outcomes. |
| `src/Pegasus.Infrastructure/Persistence/` | Resolve submission ownership, terminal work outcome, processed receipt, and actual Case link in one read model without duplicating state. |
| `src/Pegasus.Web/` | Add the authenticated result endpoint after the shared provider wire contract is settled. |
| `tests/Pegasus.Core.Tests/`, `tests/Pegasus.IntegrationTests/`, `tests/Pegasus.ArchitectureTests/` | Prove ownership, random/cross-Principal denial, actual Case-link authority, no-Case terminal failure, and absence of general lookup/file delivery. |
| `docs/frd/frd-09-provider-and-intermediary-routes.md`, `docs/capabilities.md`, `docs/current-architecture.md` | Record Case/PO as the sole successful result and completed-without-Case as failure. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `src/Pegasus.Infrastructure/Persistence/EfQueuedIntakeStatusQueries.cs` | Existing join shape can be reused, but its staff state vocabulary must not leak externally. |
| `src/Pegasus.Core/Intake/DurableIntake.cs` | Work completion/failure authority and the staged-to-processed identity chain. |
| `src/Pegasus.Infrastructure/Persistence/EfIntakeWorkStore.cs` | Durable completion is distinguishable from work still in progress. |
| `src/Pegasus.Infrastructure/Persistence/EfCaseAcceptanceStore.cs` | The actual Case link is the Case/PO authority. |
| `docs/operator-notes.md` | Provider API is a future intake channel; report delivery is a separate contract. |
| `docs/current-architecture.md` | A processing decision alone must never be reported as an allocated Case. |

## Ripple effects

Depends on API-01 receipt identity and API-04 authentication. Contract tests must pin unknown/random/foreign equivalence, completed-without-Case failure, and identifier-only responses. Governing docs must remove API-02 wording without absorbing later report delivery.

## Out of scope

General Case/PO lookup or search, files, reports, source download, outbound delivery, Case detail, workflow actions, transient state names, progress percentages, webhooks, and performance changes.
