# Proof

**Shipped:** PR #490 (`task/tick-052-mail-10-manual-case-association`), merge `4baae5f0`
**Deployed:** `git merge-base --is-ancestor 4baae5f0 4111ad29` → **true** (Release 16, active revision `…--4111ad291779`).

## The finding

The Mail page performed freshness checks and lease acquisition **before** the canonical Core
replay lookup, so an exact successful link/unlink POST could not replay — the page rejected
it before Core could recognise it as the same operation.

## Verified in the shipped code

The replay lookup is Core's and runs inside the store's own transaction.
`EfIntakeMutationStore.ExecuteAsync` reads `IntakeMutationHistory` by `OperationKey`
**first** (`:617-633`), before loading the receipt, before any version check and before any
case lease guard. A matching key with a matching event type and fingerprint returns the
prior result; a changed one raises `IntakeOperationConflictException`. That is both bullets
in one place: exact replay succeeds, changed inputs under the same operation identity fail
closed.

The ticket's fourth bullet — *"existing `ILinkIntake` / `IReverseIntakeLink` and EF
transaction remain the only business owner"* — is checkable structurally. `ReverseLinkAsync`
and `LinkAsync` both delegate to that one `ExecuteAsync` envelope; there is no second
association policy and no generic action framework. Work done on
[[INTK-029]] in Release 17 confirmed this from the inside: adding cancel-on-unlink required
one conditional inside the existing envelope and no new path, which is only possible
because that ownership held.

`CaseAcceptanceReplayTests` exercises exact replay, replay idempotency and changed-replay
conflict against the real store; it passes on the Release 17 branch.

## Not claimed

Proved by the code path and by the tests. No live Outlook message has been linked and
replayed in production, and this proof does not claim one has.
