# Proof

**Shipped:** PR #491 (`task/tick-057-ui-14-mail-queues`), merge `ee88c70c`
**Deployed:** `git merge-base --is-ancestor ee88c70c 4111ad29` → **true** (Release 16, active revision `…--4111ad291779`).

## The finding

PR #491 added `Current view: …` as a `field-hint` and a queue-specific empty-state paragraph
to the read-only Inbox. `docs/design/README.md` permits labels and values but forbids field
hints, explanatory copy and empty-state panels on read-only views — and the native selected
option already exposes the active value, so the hint restated what the control said.

## Verified in the shipped markup

On the deployed revision, `src/Pegasus.Web/Pages/Mail/Index.cshtml` contains
**zero** occurrences of `field-hint`. The `Current view:` hint is gone.

The selected value is still exposed by the native control: the queue filter is a labelled
`<select>` inside the `filterbar` form, so the active value is both visible and
programmatically determinable from the selected `<option>` — the first bullet, satisfied by
the platform rather than by replacement copy.

No replacement guidance was added, which is the part that mattered: removing a hint and
substituting a differently-worded one would have failed the same rule.

## An honest limit on this proof, found by the Release 17 design check

This ticket cleaned the categorised mail **selector**. It did not cover the rest of the same
page, and three further copy defects survived on `Mail/Index.cshtml` into production — a
banned word (`bounded`) in operator-visible text, a hint sentence for the Deleted Items
search field, and a sentence narrating how search works. They are fixed in Release 17 under
[[MAIL-010]].

That does not invalidate this ticket: what it was scoped to remove is gone and has stayed
gone. It does mean the design rule needed a page-wide scan, not a control-level one, and
that scan is now on the record.

## Not claimed

Verified by reading the deployed markup. The recorded screen-reader, forced-colours and
200%-zoom inspection that `docs/design/README.md` requires at acceptance has not been
performed, and is not claimed here.
