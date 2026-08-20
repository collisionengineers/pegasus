# Plan

Estimated diff: about 35–55 test lines plus one catch-filter line.

1. Add the existing Graph response-validation exception types to `GraphDeletedMailSearchSource`'s unavailable mapping; retain the existing caller-cancellation guard.
2. Add focused fake-Graph cases for malformed JSON, missing required identity/time, foreign parent folder, and escaped next link.
3. Run the focused `ProductionGraphSourceTests`, Release build, and diff check.
4. Record four-lens dispositions and the shared-PR result.

Reuse: the current Graph client validators, Deleted source state model, and `DelegateHandler` test fake. No new abstraction.
