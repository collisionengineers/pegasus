# Proof

**Shipped:** PR #491, merge `ee88c70c` · **Deployed:** ancestor of `4111ad29` → true (Release 16, active revision `…--4111ad291779`).

## The finding

PR #491 rejected unknown queue keys and Deleted-Items-plus-queue context on GET and reload,
but the successful exact-message **POST** handlers executed classification, folder-move and
Case-association/lease work **before** parsing that context. The validation existed and ran
in the wrong place, which is worse than absent: it read as covered.

## Verified in the shipped code

One parser — `TryParseListContext` at `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs:903` —
called at the head of every POST handler on the page, before any business call:

| Handler | Declared | `TryParseListContext` | First lease/mutation |
| --- | ---: | ---: | ---: |
| `OnPostPrepareLinkCaseAsync` | 199 | **211** | 237 |
| `OnPostPrepareUnlinkCaseAsync` | 260 | **272** | 295 |
| `OnPostLinkCaseAsync` | 318 | **332** | — |
| `OnPostUnlinkCaseAsync` | 383 | **397** | — |
| `OnPostCorrectClassificationAsync` | 448 | **456** | — |
| `OnPostMoveToRecommendedFolderAsync` | 511 | **520** | — |

In both preparation handlers the parse precedes `acquireCaseEditLease.ExecuteAsync` by
around twenty lines, so invalid context returns `NotFound()` with no lease taken and no
mutation attempted — the two bullets about unknown queue context and Deleted Items plus
queue context causing no classification, move, association or lease call.

Only the actor check (`TryGetActor`) runs earlier, which is correct ordering: authenticate,
then validate context, then act.

The third bullet — valid context preserved through successful and recoverable paths — is
carried by the same values being threaded onto every redirect and form
(`RedirectToMessage`, `RedirectToAssociationTarget`), so a successful action returns the
operator to the list position they came from.

**One parser, not two.** `grep` finds a single `TryParseListContext` definition and no
second parsing or authorization layer, which was the finding's constraint.

## Not claimed

Proved by the call-site ordering and the shipped tests. No malformed queue context has been
posted against production, and this proof does not claim it has.
