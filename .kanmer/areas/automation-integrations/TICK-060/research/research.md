# Research — TICK-060: provider terminal result lookup

## Question

How should a provider retrieve only the terminal Case/PO or bounded failure for a receipt created by API-01 without exposing internal processing states or another principal's work?

## Findings

- FRD-09 permits retrieval only of the authenticated Principal's own receipt and resulting Case/PO and requires cross-principal disclosure to fail closed.
- The operator retired API-02 and chose no provider-facing Processing state. A nonterminal receipt therefore needs one generic retry response, not the existing staff `QueuedIntakeStatusKind` vocabulary.
- `EfQueuedIntakeStatusQueries` already joins a staged receipt to its processed receipt and active Case link; it is a useful persistence precedent but its staff projection exposes Received/Processing/Complete/Failed and must not become the external wire contract.
- `IIntakeWorkStore.GetCompletedEvaluationAsync`, the staged receipt/work item, persisted receipt, and Case intake link already provide terminal authority; no result table is needed (`src/Pegasus.Core/Intake/DurableIntake.cs`, `src/Pegasus.Infrastructure/Persistence/EfIntakeWorkStore.cs`).
- A `case_created` processing decision is not Case-existence authority; current architecture requires the actual Case link before a Case/PO is claimed (`docs/current-architecture.md`).
- Principal and references are immutable after allocation, and no reference may be inferred before accepted processing (AGENTS.md product invariants; capabilities API-03 row).

## Implications

Add a Core query that accepts the authenticated Principal and opaque staged receipt identifier, resolves ownership, and returns one of: terminal Case/PO, terminal bounded failure, or nonterminal. The HTTP adapter maps nonterminal to 202 with `Retry-After`, terminal success to 200, terminal failure to a stable problem response, and unknown/cross-principal to the same 404. It never exposes internal queue state, attempt count, failure exception, receipt contents, or general Case detail.

## Open questions

The result vocabulary and isolation behaviour are resolved for planning; live throttling remains parked with API activation.
