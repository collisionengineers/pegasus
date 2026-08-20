# Research — PR-044

## Question

How can request cancellation after provider work begins preserve a recoverable durable operation while still propagating cancellation to the caller?

## Findings

- At head `83293162`, the operation is durably Pending before provider access. Both cancellation during `mover.MoveAsync` and cancellation from `CompleteAsync(... succeeded ...)` are rethrown immediately, leaving the database row Pending.
- PR-043 intentionally makes Pending replay non-recovering and keeps the filtered active slot. Therefore cancellation after the external mutation boundary needs a durable Pending→Uncertain handoff before rethrow.
- Reusing the request token cannot perform that handoff because it is already cancelled. A fresh DbContext and a short internal token isolate the one-row transition from the abandoned request and avoid reusing a context whose SaveChanges may have been interrupted.
- A conditional SQL update from Pending to Uncertain is safe if the success write actually committed before cancellation was observed: it affects zero rows and does not downgrade Success.
- Existing `SaveChangesInterceptor` test conventions can cancel exactly the success save. Existing fake movers can cancel during the provider call and retain a simulated destination for later same-key probe recovery.
- No new durable state is needed: the existing Uncertain outcome, filtered index, same-key replay and exact parent probe already own recovery.

## Implication

On request cancellation inside the move/success-completion block, use a fresh bounded persistence path to conditionally mark the row Uncertain, then rethrow the original cancellation. Later same-key replay probes only; different keys remain excluded while Uncertain.
