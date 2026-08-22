## 2026-08-22 — what is left, exactly

Deployed in release 17 (`71911734`), carried to release 20 (`05fe7a7f`).

Both halves shipped: the projection fix (`IntakeAssociations.AllocationMayStandIn`,
so a reversed association stops reporting the old case) and the cancel-on-unlink
half, with `SourceEmailUnlinked` added to both the lifecycle state and the
closure outcome, refused by the generic close, and written in the same
transaction as the reversal. The terminal taxonomy was consolidated to one owner
first, so the new state is terminal everywhere rather than in one of three
copies.

**Single gate:** verifying it means unlinking the spawning email of a real case,
which mutates an Outlook association and cancels a live case. That is an operator
action on operator data, not something to try on QDOS26009 or QDOS26010 — both
are real audits.

The cleanest verification is on the **next** case, after the operator has
finished with it: unlink its origin, confirm the dialog names the reference,
the case closes as `Cancelled — email unlinked`, the inbox stops showing the
link, and the search-and-link surface returns.
