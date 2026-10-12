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

## P3 · Supplementary from an import onto the spec

- D7. An estimate imported onto the selected repair spec replaces its lines, freezing the grid it replaces and the import as numbered versions, as a Glass's return does today.
- D8. The Supplementary panel then opens at the top of the section, above the grid: the version it changes against (the one the last sent report used, else the grid replaced) with its Sent on report mark, the cost movement and a full comparison link; one line per change (Added, Changed with the fields that changed, Removed) with the line's value; Explain the change on the report; the reason; Dismiss.
- D9. The statement prints under the panel once Explain is ticked, and on the report as the Supplementary damage block; Dismiss folds the panel while editing and the versions keep the history.

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
