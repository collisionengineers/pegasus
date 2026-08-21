# Research — TICK-060: provider Case/PO result lookup

## Question

How should a provider retrieve the Case/PO created or linked by its own API submission without creating a general lookup surface, exposing internal processing states, or returning another Principal's work?

## Findings

- FRD-09 permits retrieval only of the authenticated Principal's own submission result and requires cross-Principal disclosure to fail closed.
- The operator requires Case/PO as the success condition. A completed submission that did not create or link a Case must fail; it is not a successful result and must not remain indefinitely nonterminal (operator clarification, 2026-08-21).
- The provider supplies its own opaque submission receipt from API-01. That receipt is an authorization-scoped correlation key, not permission to search arbitrary Cases.
- `EfQueuedIntakeStatusQueries` already joins a staged receipt to its processed receipt and active Case link; its staff Received/Processing/Complete/Failed vocabulary must not become the external contract.
- `IIntakeWorkStore.GetCompletedEvaluationAsync`, the staged work item, persisted receipt, and Case intake link provide terminal authority; no result table is needed (`src/Pegasus.Core/Intake/DurableIntake.cs`, `src/Pegasus.Infrastructure/Persistence/EfIntakeWorkStore.cs`).
- A `case_created` decision is not Case-existence authority. Only the actual active Case link supplies the immutable Case/PO (`docs/current-architecture.md`).
- This capability returns identifiers only. Files, reports, source downloads, delivery, general Case detail, search, and workflow mutation are separate or excluded capabilities.

## Implications

Add one Core query that accepts the authenticated Principal and its opaque submission receipt. It has three outcomes: still nonterminal; success with the actual linked Case/PO; or terminal failure. When processing has completed without an actual Case link, return terminal failure with a bounded reason. Unknown, random, and cross-Principal identifiers remain indistinguishable absence. There is no general Case/PO search and no two-way file API.

## Open questions

The Case/PO success requirement and no-Case terminal failure are settled. Exact public wire details remain part of the separately unresolved provider contract.

## Azure architecture refresh — 2026-08-21

Read-only inspection confirms the result endpoint can run in the existing public HTTPS Web Container App and query the existing Azure SQL state. It needs no new Azure resource, result store, queue, blob container, webhook service, or report-delivery path. The response is an identifier projection only: actual linked Case/PO or failure, scoped to the authenticated Principal's own receipt. Existing Application Insights can measure request latency and disclosure-safe outcome counts. Any rate limit belongs initially at the real Web endpoint; API Management is deferred until actual multi-provider traffic or gateway policy warrants it (Microsoft per-key policy: https://learn.microsoft.com/azure/api-management/rate-limit-by-key-policy).
