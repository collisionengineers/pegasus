# Case record dialogs (Repair Spec)

Temporary review artifact; see the [page README](../README.md).

The section owns five dialogs. Two change in this round; the others are drawn as today so the mockup's `dialog=` preset and the self-check cover them.

| Dialog | Opens from | Round |
| --- | --- | --- |
| Repair Spec versions (`versions-dialog`) | More → Versions (n) | **P2**: a Compare with current button beside Restore on every version row |
| Compare repair specs (`compare-estimates-dialog`) | More → Compare; a version row's Compare with current | **P2**: From and To list the spec's versions and the Case's other specs; the table shows both sides; Print comparison sheet |
| Delete all lines (`delete-lines-dialog`) | Delete all lines | as today |
| Discard repair spec (`delete-estimate-dialog`) | Discard, on a spec that is not Current | as today |
| Send to AI (`send-to-ai-dialog`) | Send to AI | not drawn (the captured frame has no Engineer's Value, so the live page withheld it) |

## Compare repair specs (P2)

- **The action.** Compare any two of the Case's repair specifications or any of a spec's frozen versions, line by line, side by side; print the comparison on its own sheet.
- **The dialog.** The spec table as today (Repair Spec, State, Net, VAT, Gross). From and To: the selected spec's Working draft and its versions newest first ("v2 · 28 Aug 2026 16:40 · £3,004.79 · Sent on report"), then each other spec and its versions, named. Opening from More defaults From to the version the last sent report used and To to the selected spec's Working draft. The result: one summary line (From £ → To £ · ±£ · n added · n changed · n removed · the rate when it differs) and one table — Change, Type ("(was Repair)" after a changed type), Description, then five figure columns per side under a head naming the side and its rate: Qty, Unit (£), Hours, Material (£), Line £. Added rows have figures on the To side only, Removed on the From side only, a Changed row's changed To cells are bold, unchanged rows follow plain. Print comparison sheet prints the summary and the table alone.

| Where this lands | Entry |
| --- | --- |
| Repair Spec head | More → Compare (offered with two specs, or one spec with a version) |
| Repair Spec versions | Compare with current on each version row |

Documentation impact when the FRD is written: FRD-25 "Engineer acts on a spec" — "Compare reads any two of a Case's specifications line by line" widens to the spec's frozen versions, and the printed sheet is named.

Decided 12 October 2026: the mechanics are the reference file's (`openCompare`, `renderCompare`, `openVersions`); nothing in this dialog is an open item.
