# Case record — Place on Hold — how it should work

Decided with the operator on 13 September 2026 while planning the Work Centre ([`../../../work-centre/how-it-should-work.md`](../../../work-centre/how-it-should-work.md) D5).

## The dialog

Place on Hold gains one optional field:

| Field | Type | Required | Rule |
| --- | --- | --- | --- |
| Reason | text | yes | as now |
| Review on | date | no | today or later; Europe/London calendar date |

Nothing else changes: same lease, same reason requirement, same Release Hold.

## The record

- The hold stores the Review on date when given (`PutCaseOnHoldRequest` gains an optional date; the workflow record keeps it with held-at and the reason).
- Releasing the hold clears it. Placing a new hold sets a new one.
- The Case's Held read-out shows "Held · review on 24 Sep" when a date is set, "Held" otherwise. The Cases › Held tab shows the same in its State column.

## Where it is used

- The Work Centre's Held decision row is due at the Review on date when set; otherwise held-at plus the Held decision target from [`../../../configuration/how-it-should-work.md`](../../../configuration/how-it-should-work.md).
- Nothing sends anything on that date. It is a due instant for the person, not a chaser.
