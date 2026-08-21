# Proof

**Shipped:** PR #490, merge `4baae5f0`, fix commit `6b7c62a4` · **Deployed:** ancestor of `4111ad29` → true (Release 16, active revision `…--4111ad291779`).

## The finding

A definitive association failure **after** acquiring a five-minute Case edit lease could
strand edit authority until the lease expired — the operator locked out of their own case
by a failure, for five minutes, with no action available.

## Verified in the shipped code

The lease is cleared by the same envelope that takes it, on the success path, through the
canonical convention rather than a Mail-specific one: `CaseMutationGuard.Complete(workflow)`
(`CaseMutationGuard.cs:81-86`) bumps the version and calls `ClearLease`, and
`EfIntakeMutationStore.ExecuteAsync` invokes it after the mutation (`:682-685`). "Successful
link/unlink consumes and clears the canonical lease" therefore holds by construction — there
is no separate release call that could be skipped.

On the failure path the transaction is `IsolationLevel.Serializable` and is never committed
(`:613-615`), so a definitive failure rolls back the lease acquisition with everything else
and the case is immediately editable again. No compensating release is needed, which is why
no Mail-specific store or framework was added — the finding's own constraint.

The uncertain-outcome case is deliberately *not* treated as a failure: that distinction is
[[PR-052]]'s subject, and the two tickets together are what keep "roll back on definitive
failure" from becoming "roll back whenever anything goes wrong".

## Not claimed

Proved by the transaction boundary and the shipped tests (checklist 5/5 at the time of the
fix). No live association has been failed mid-lease in production, and this proof does not
claim one has.
