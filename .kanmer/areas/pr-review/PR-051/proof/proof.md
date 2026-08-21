# Proof

**Shipped:** PR #490, merge `4baae5f0`, fix commit `563bb2ec` · **Deployed:** ancestor of `4111ad29` → true (Release 16, active revision `…--4111ad291779`).

## The finding

`AssociationLeaseState` recorded only Case id, versions, lease token and operation key. It
did **not** bind the prepared authority to the exact retained message/receipt or to Link
versus Unlink — so a confirmation prepared for one message could be carried to another
targeting the same Case, or repurposed across action kinds, and the final handler would
resolve that other message server-side and submit a new valid Core fingerprint.

An authorisation hole, not a usability one: the Core fingerprint would have been genuinely
valid for the wrong message.

## Verified in the shipped code

`src/Pegasus.Web/Pages/Mail/Message.cshtml.cs:744-760` — the protected state is now an
eight-part token, and `RestoreAssociationLease` refuses anything that does not parse as all
eight:

| Part | Binds |
| ---: | --- |
| 0 | **message id** |
| 1 | **receipt id** |
| 2 | **`Link` or `Unlink`** — `parts[2] is LinkAssociationAction or UnlinkAssociationAction` |
| 3 | case id |
| 4–5 | intake and case versions |
| 6 | lease token, length-checked against `CaseEditAuthority.LeaseTokenLength` |
| 7 | operation key, `Guid.TryParseExact(…, "D")` |

Parts 0, 1 and 2 are the fix. The view then gates the confirm control on all three
matching the message in front of the operator (`Message.cshtml:390-397`:
`AssociationLeaseMessageId == summary.Id && AssociationLeaseReceiptId == associationReceipt.Id
&& AssociationLeaseAction == UnlinkAssociationAction`), and `RequirePreparedAssociation`
(`:822`) re-checks server-side before the mutation. Authority prepared for message A cannot
act on message B, and Unlink authority cannot drive Link.

The constraint held: this is the existing protected TempData and the existing Core commands.
No new store, no generic authorization framework.

## Not claimed

Proved by the token shape and both check sites, with the ticket's four verification bullets
ticked at the time of the fix. No live cross-message attempt has been made against
production, and this proof does not claim one has.
