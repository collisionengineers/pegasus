# Post-implementation report — CASE-018

Commit `94b6a9dd` on `task/qdos26011-regressions`.

## What changed

| File | Change |
| --- | --- |
| `_CaseWorkflow.cshtml` | Removed the read-only restatement inside "Case detail" (−131 lines) and the value listing inside "Vehicle evidence". Both sections now render only under an edit lease, because both are now only editors. Three now-unused local functions deleted. |
| `_CaseSummary.cshtml` | Removed "Engineer queries" and "Where this case stands". Added "Inspection" and "Contact" blocks. VAT status joined "Case identity". |
| `site.css` | `.datarow` became a three-track grid; `.datarow__sug` deleted. |

## The one departure from the plan

The plan said to delete the read-only list from "Case detail" outright. Reading it properly showed that is not safe: seven of its seventeen rows — contact name, e-mail and phone, VAT status, inspection date, deadline, address and mode — appear **only** there. Deleting the list as written would have removed the only place an operator can read them.

So the facts moved before the list was deleted. "Inspection" and "Contact" are new blocks in the grid, and VAT status sits with the claim number it belongs beside. Every fact that was readable before is still readable; each is now readable in exactly one place, which is what the ticket actually asked for.

Two smaller consequences of removing "Where this case stands":

- Its `Corrects` / `Corrected by` links moved to "Case identity" — a correction link is an identity fact.
- Its `Due by` and `Next chase` rows moved into the existing "Chase history" panel. `State` was simply dropped: the header already renders it as a status chip, so the block was the second place it appeared.

## Why the alignment broke

`.datarow` was a flex line. `.datarow__field` and `.datarow__value` were sized from whatever space remained after the optional `.datarow__end`, so a row carrying a provenance icon gave its field column less width than a row without one, and the value column started at a different x. That is exactly why Claimant and Claim number — the two extracted rows in Case identity — sat out of line with the plain rows around them. Fixed tracks make the alignment independent of what a row ends with.

## Evidence

`dotnet build` clean. `dotnet test tests/Pegasus.Core.Tests` — 923 passed. Full integration suite run on the branch; visual confirmation against the live case follows in `proof`.

## Not done

The plan's step 4 (manufacture year and fuel type rows) was dropped during file mapping: `CK_CaseDataFields_FieldName` has no entry for either, so showing them needs a Core contract change and a migration for two facts the operator did not ask for. Recorded in `files`.
