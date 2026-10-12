# Case record, Repair Spec: how it should work

Decided with the operator on 12 October 2026: the five proposals are the request and their mechanics are the reference file's (`current/pegasus_case_dashboard_2026-09-15.html`, section 6). Each rule is one sentence FRD-25 or FRD-16 carries at Stage 2.

## P1 · Columns by line type

- D1. A line type prices only its own columns: Replace — Part no., Qty, Unit (£), Hours, Material (£); Repair — Hours, Material (£); R&I — Hours; Paint and Blend — Hours, Paint h, Material (£); Specialist — Qty, Unit (£), Hours, Material (£); Other — Hours.
- D2. A cell the type does not price reads as one dash in read and edit mode; a value sitting in one keeps its amber off-pattern box and is never cleared; changing the Type opens and closes the cells.
- D3. Core's anomaly rules and totals are unchanged; the map is the editor's.

## P2 · Compare over versions

- D4. Compare reads any two of the Case's specifications or any of a spec's frozen versions line by line, From and To side by side, with each side's Qty, Unit (£), Hours, Material (£) and Line £ under a head naming the side and its rate; unchanged rows are listed; a changed To cell is bold and a changed type says what it was.
- D5. Opened from More, Compare starts at the version the last sent report used against the selected spec's Working draft.
- D6. Every version row offers Compare with current, which opens Compare on that version against the Working draft; the dialog offers Print comparison sheet, which prints the summary and the table alone.

## P3 · Supplementary after an import

- D7. The spec an import has just made opens with Supplementary filled: Changes vs the version the last sent report used, else the spec it replaced; Reason Supplementary estimate received; Explain the change on the report unticked; the diff and the statement drawn.
- D8. Changes vs lists the Case's sent versions beside its other specs.
- D9. Dismiss folds the filled panel for that spec while editing; the versions keep the history.

## P4 · The agreed sum drives the spec

- D10. The agreed total sum is a box in the Contract repair bar, the same Case field Decisions holds.
- D11. Typing the sum previews the scaling to that sum as the Target % slider does; Apply records it; when the spec drifts from the sum again, the mismatch chip rescales it on press.

## P5 · The labour rate also under the grid

- D12. Labour rate (£/h), with its rate-card select and typed figure, and Regional uplift stand in a bar directly under the grid, first in the bar stack, as well as in the head cells.

## Open

- Open: whether the Stage 2 PR keeps this folder (item A).

## Documentation impact when the FRDs are written

- FRD-25 "Engineer acts on a spec": D4–D7, D10, D11.
- FRD-16 "Case workspace": D1, D2, D12 (the section's geometry and the one Save carrying the cells drawn).
- Design authority, Case record frame: the rate bar and the dash cells.
