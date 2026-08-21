# Proof

**Shipped:** PR #477, merge `e4d56d9e` · **Deployed:** ancestor of `4111ad29` → true (Release 16, active revision `…--4111ad291779`).

## The finding

> `OperationCanceledException` with the request token cancelled is rethrown both around the
> provider move and around provider-success completion. The durable row remains `pending`
> … so a client disconnect can strand the message permanently even when Graph may already
> have moved it.

A browser tab closing at the wrong moment could permanently freeze a message, because
`pending` is correctly refused by every replay ([[PR-043]]) and every different key
([[PR-038]]) — the two guards that make the stranding total.

## Verified in the shipped code

`src/Pegasus.Infrastructure/Persistence/EfRetainedMailFolderMoveStore.cs`:

| Line | Guard |
| --- | --- |
| `172-174` | cancellation **during** the provider move → `MarkUncertainAfterCancellationAsync` |
| `193-195` | cancellation **after** provider success, while persisting the result → same |
| `208` | the transition itself |

Both cancellation windows the finding named are covered, and both move the row from
`pending` to `uncertain` — the state that is recoverable by probing ([[PR-039]]) rather
than by a blind retry. The request token is not permitted to decide that an external
operation's outcome may be lost.

**No worker, timer, lease or blind retry was added.** The finding asked for the smallest
durable transition, and a state change inside the existing persistence path is what
shipped. The recovery it enables is operator-initiated and read-only until the probe
resolves.

Different keys remain blocked throughout, because `uncertain` is inside the filtered unique
index alongside `pending`.

## Not claimed

Proved by the exception paths and the shipped tests. No live request has been cancelled
mid-move against production Graph, and this proof does not claim one has.
