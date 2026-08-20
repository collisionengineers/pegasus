# Plan

## Governing docs

FRD-08 remains the behavioral owner; existing Case lease conventions remain the technical mechanism. No doc or ADR change.

1. Keep using `IReleaseCaseEditLease` with `CancellationToken.None` after definitive refusal.
2. If release is recoverably unconfirmed, retain the exact protected authority and return a distinct same-confirmation retry message.
3. On retry, rerun the definitive no-write refusal and compensation; clear state only after release succeeds.
4. Add a fail-once decorator around the real release port in the authenticated SQL/Web test and prove the Case is immediately reacquirable after retry.
5. Preserve uncertain association outcome behavior and add no background worker/framework/schema.

No second recovery owner.
