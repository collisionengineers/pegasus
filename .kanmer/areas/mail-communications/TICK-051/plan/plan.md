# Plan — MAIL-09

## Chosen approach

Add one focused Core `AssociateRetainedMailWithCase` use case. Its evidence port reads the current mailbox receipt, normalized VRM candidates across current non-archived Cases and exact mailbox-thread current Case candidates. Core accepts one target only when each qualifying evidence source is unique and all qualifying sources agree; zero evidence, multiplicity or contradiction abstains. It then calls the existing `IAutomaticCaseAssociationStore`.

Extend that existing request with an optional evidence fingerprint. `EfIntakeMutationStore` reloads the same evidence inside its existing serializable transaction and refuses a changed fingerprint before writing the existing manual-association row/history. Provider/QDOS callers omit the seam and preserve their established request hash and behavior.

## Governing docs

- `docs/frd/frd-08-email-mailbox-and-background-processing.md`: add the already accepted MAIL-09 system-wide normalized-VRM / exact mailbox-thread rule, agreement and fail-closed behavior; preserve one current association and permanent history.
- `docs/design/README.md`: no new control; the existing message Case link remains the read surface and TICK-052 owns manual confirmation.
- No ADR: the accepted Core/Infrastructure transaction boundary already carries the change.

## Steps

1. Extract the existing Case registration normalizer for reuse, then add the focused Core evidence/result/use case with no Case/PO input or generic matcher registry.
2. Implement the evidence query in the existing EF automatic-association store, using current Case data, exact retained mailbox/conversation identity and one shared current-association precedence helper.
3. Add the expected fingerprint to the existing automatic-association request; on non-replay MAIL-09 writes, re-read/compare inside the current serializable transaction before its existing association/history logic.
4. Invoke MAIL-09 from `ProcessQueuedIntake` after the provider-specific attempt on both live and completed-replay paths, only while no current Case exists. Treat abstention/failure as advisory; preserve cancellation.
5. Use the shared precedence helper in retained-mail list/detail so the landed Web/MCP readers show the system-worker association. Update FRD/capabilities/current architecture without deployment/live claims.
6. Prove Core policy, exact query scope, stale/contradictory abstention, transaction replay/history/reversal and retained/queued caller behavior with local SQL/fakes. Run locked restore, Release build, focused/proportional suites, four lenses, PIR and one PR to `dev`.

## Acceptance

- Unique normalized VRM associates system-wide even before the thread has an association.
- With no VRM, one Case already current in the exact mailbox/thread associates; cross-mailbox identity never qualifies.
- Multiple candidates or disagreeing unique VRM/thread evidence abstain; Case/PO text is unused.
- Evidence changed between read/write refuses before mutation; stable replay remains idempotent and history append-only.
- Existing staff reversal/current-association precedence wins and retained detail shows the same current Case.
- No external system or production data is written.

## Risks

- **Stale read:** fingerprint revalidated under the existing serializable transaction.
- **Policy duplication:** Core chooses the candidate; EF only gathers facts and verifies unchanged evidence.
- **Normalizer drift:** share the existing Case-search convention.
- **Scope growth:** no migration/table/index/framework/manual UI/MCP/external operation.

## Simplification pass — 2026-08-20

- **Reuse:** pass. Reused `IAutomaticCaseAssociationStore` / `EfIntakeMutationStore`, the Case registration grammar, queued-intake advisory convention, and one shared current-association precedence query. The provider-scoped `CaseMatchIndex` cannot own system-wide MAIL-09 candidates, so current `CaseDataFields` remain the authoritative read.
- **Simplification:** one finding applied. The expected evidence fingerprint is a transaction freshness precondition, not stable command identity; excluding it from the established request fingerprint preserves both legacy callers and same-key replay after the association increments receipt version.
- **Efficiency:** pass. Retained projection stays page-batched; thread reads exact mailbox/conversation tokens; no N+1 query, duplicate projection, table, index, or migration was added. The system-wide registration scan is the smallest correct query because stored values require the shared normalization grammar and the existing match index is provider-limited.
- **Altitude:** pass. One focused Core policy selects or abstains; Infrastructure gathers current facts and verifies freshness inside the existing transaction; the existing queued caller invokes it. No generic mail matcher, registry, action matrix, provider adapter, or external operation was introduced.
- **Unapplied findings:** none.
