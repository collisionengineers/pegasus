# Plan

Estimated diff: about 35–55 test lines plus one catch-filter line.

1. Add the existing Graph response-validation exception types to `GraphDeletedMailSearchSource`'s unavailable mapping; retain the existing caller-cancellation guard.
2. Add focused fake-Graph cases for malformed JSON, missing required identity/time, foreign parent folder, and escaped next link.
3. Run the focused `ProductionGraphSourceTests`, Release build, and diff check.
4. Record four-lens dispositions and the shared-PR result.

Reuse: the current Graph client validators, Deleted source state model, and `DelegateHandler` test fake. No new abstraction.

## Simplification pass — 2026-08-20

- **Reuse:** Extended the existing Deleted-source catch policy and reused every existing Graph validator and fake HTTP handler.
- **Simplification:** One catch filter owns the complete provider-response failure taxonomy already emitted by this client; no wrapper, retry, or new exception hierarchy was added.
- **Efficiency:** Invalid responses stop immediately and return no partial matches; normal request counts and the fixed 100-message bound are unchanged.
- **Altitude:** Graph response validation and failure mapping remain in Infrastructure; Core and Web contracts are unchanged. No unapplied findings.
