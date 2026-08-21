# Proof

**Shipped:** PR #477, merge `e4d56d9e` · **Deployed:** ancestor of `4111ad29` → true (Release 16, active revision `…--4111ad291779`).

## The finding

> Staff are told to retry the same uncertain confirmation, but the redirected message page
> generates a **new** operation key. The store rejects that new key while an uncertain
> operation exists, so the only safe recovery path is unreachable from the Web caller.

## Verified in the shipped code

`src/Pegasus.Web/Pages/Mail/Message.cshtml.cs` binds `MoveOperationKey` as page state
(`:95`) and passes it into the move request (`:534`), rather than minting a fresh
`Guid.NewGuid()` on redirect. The recovery action therefore re-presents the **original**
key, which is the one the store will accept while an operation is uncertain — the path the
finding said was unreachable.

The store side pairs with it: `EfRetainedMailFolderMoveStore` routes a same-key arrival on
an uncertain operation into recovery (`:236`, `:295`), where the outcome is resolved by
**probing** the provider for the message's parent folder rather than issuing a second move.
`IRetainedMailFolderMover.GetParentFolderIdAsync` exists for exactly that and is read-only,
so "never issue a blind second move while location remains uncertain" holds by the shape of
the interface, not by convention.

Transport identities stay server-side: the page posts the operation key and expectation
versions; mailbox, source folder, immutable message id and destination folder are resolved
in `EfRetainedMailFolderMoveStore` into `RetainedMailFolderMoveCoordinates` and never
appear in the form.

## Not claimed

Destination / source / unresolved probe outcomes are covered by the shipped tests. No live
uncertain move has been recovered in production, and this proof does not claim one has.
