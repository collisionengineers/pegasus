# Plan

## Governing docs

FRD-08 remains the behavioral owner; existing Case lease conventions remain the technical mechanism. No doc or ADR change.

1. Keep using IReleaseCaseEditLease with CancellationToken.None after definitive refusal.
2. If release is recoverably unconfirmed, retain the exact protected authority and return a distinct same-confirmation retry message.
3. On retry, rerun the definitive no-write refusal and compensation; clear state only after release succeeds.
4. Add a fail-once decorator around the real release port in the authenticated SQL/Web test and prove the Case is immediately reacquirable after retry.
5. Preserve uncertain association outcome behavior and add no background worker/framework/schema.

No second recovery owner.

## Simplification pass — 2026-08-20

- **Reuse:** retained IReleaseCaseEditLease, its real implementation, non-request cancellation and the existing exact confirmation.
- **Simplification:** a boolean result only selects the recoverable message; the protected payload itself is the bounded retry authority. No queue, worker, lease framework or persistence was added.
- **Efficiency:** recovery retries only the original definitive refusal and existing release command; successful or definitively absent releases clear immediately.
- **Altitude:** Web owns request compensation; the established Case lease service remains the sole release owner.
- **Applied findings:** used TempData Peek and explicit clear rather than a second recovery copy, preserving authority across roleless and transient-failure requests.
- **Unapplied findings:** none.
