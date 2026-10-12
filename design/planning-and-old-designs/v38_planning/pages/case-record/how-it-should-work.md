# Case record, Repair Spec: how it should work

Proposed on 12 October 2026. Each rule is one sentence FRD-25 or FRD-16 can carry once the operator settles its lettered item in the [notes](../../current/v38-notes.md). The five proposals are the operator's request; the items are the choices inside them.

## P1 · Columns by line type

- D1. A line type prices only its own columns: Replace — Part no., Qty, Unit (£), Hours, Material (£); Repair — Hours, Material (£); R&I — Hours; Paint and Blend — Hours, Paint h, Material (£); Specialist — Qty, Unit (£), Hours; Other — Hours. (item H)
- D2. A cell the type does not price reads as one dash in read and edit mode; a value sitting in one keeps its amber off-pattern box and is never cleared; changing the Type opens and closes the cells. (item I)
- D3. Core's anomaly rules and totals are unchanged; the map is the editor's. (item H)

## P2 · Compare over versions

- D4. Compare reads any two of the Case's specifications or any of a spec's frozen versions line by line, From and To side by side, with each side's Qty, Unit (£), Hours, Material (£) and Line £ under a head naming the side and its rate; unchanged rows are listed; a changed To cell is bold and a changed type says what it was. (the request)
- D5. Opened from More, Compare starts at the version the last sent report used against the selected spec's Working draft. (item D)
- D6. Every version row offers Compare with current, which opens Compare on that version against the Working draft; the dialog offers Print comparison sheet, which prints the summary and the table alone. (item E)

## P3 · Supplementary after an import

- D7. The spec an import has just made opens with Supplementary filled: Changes vs the version the last sent report used, else the spec it replaced; Reason Supplementary estimate received; Explain the change on the report unticked; the diff and the statement drawn. (item F)
- D8. Changes vs lists the Case's sent versions beside its other specs. (the request)
- D9. Dismiss folds the filled panel for that spec while editing; the versions keep the history. (item G)

## P4 · The agreed sum drives the spec

- D10. The agreed total sum is a box in the Contract repair bar, the same Case field Decisions holds. (item C)
- D11. Typing the sum previews the scaling to that sum as the Target % slider does; Apply records it. Open: preview as it is typed, or only on Rescale to agreed sum. (item B)

## P5 · The labour rate under the grid

- D12. Labour rate (£/h), with its rate-card select and typed figure, and Regional uplift stand in a bar directly under the grid, first in the bar stack; the head keeps Repair spec name, Other costs (£), VAT % and Repairer VAT status. Open: whether the head keeps them as well. (item A)

## Documentation impact when the FRDs are written

- FRD-25 "Engineer acts on a spec": D4–D7, D10, D11.
- FRD-16 "Case workspace": D1, D2, D12 (the section's geometry and the one Save carrying the cells drawn).
- Design authority, Case record frame: the rate bar and the dash cells.
