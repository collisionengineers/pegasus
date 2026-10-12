# Case record, Repair Spec: how it works today

Read from the live source on 12 October 2026 (`origin/dev` 6d47cd3cb, Release 102). Temporary review artifact; see the [round README](../../README.md).

## What the page does not show

- That an import always makes a new spec and makes it Current, demoting the spec in use to a Draft (`EstimateImport`, FRD-25); the tabs show the result, not the act.
- Which acts freeze a version: import, Apply (before and after), Remove scaling, Restore (before and after) and a sent report (`RepairSpecificationSnapshotKind`); Delete all lines and a save freeze nothing.
- That Compare compares specs only (`Model.Estimates`): a frozen version can be restored but not compared, and the Versions rows offer Restore alone.
- That Supplementary needs a second spec to appear at all, sits at the foot of the section, and does nothing until Changes vs is chosen (`estimate.Supplementary`, `Model.SupplementaryDiff`); and that only a Glass's return updates an existing spec in place (lines replaced, the import frozen as a version), while a file import always creates a spec.
- That the agreed total sum in the Contract repair bar is a read of Decisions' contract sum cell (`data-contract-sum-read`, `f-settlement-contract-sum`), and the mismatch chip is a statement, not a control; the sum only moves where the Target % slider starts.
- That every figure column is a box on every line type; Core's anomalies (`EstimateTotals.Compute`) decide after the fact which values are off-pattern and carry them as specialist.
- That the rate-card select and the typed rate are one control: choosing a card fills the figure, typing keeps the entered rate (`estimate-rate-card`, `estimateLabourRate`, v28 P33); the uplift is a flag on the spec (`estimateRegionalUplift`) with a chip that reads "Suggested · …" when a repairer, claimant or storage postcode is in London or the Home Counties.

## Governing documentation

| Document | What it settles for this section |
| --- | --- |
| [FRD-25](../../../../../docs/frd/frd-25-repair-estimates-imports-and-glasss-sessions.md) | Canonical repair specifications: one Current spec, import keyed by source hash, the Engineer's acts (Target %, agreed sum, versions, Restore, Compare, Supplementary), corrections and projections |
| [FRD-16](../../../../../docs/frd/frd-16-case-record-workspace.md) | The one Save, the edit session, the section's place on the record |
| [Design authority](../../../../../docs/design/README.md) | Case record frame, read/edit one geometry, absent versus disabled, no explanatory copy |
| [Case-workspace guardrails](../../../../../.agents/skills/pegasus-ui-guardrails/references/case-workspace.md) | Repair Spec head: Import, the Glass's slot, Send to AI, one More menu |
| [CONTEXT.md](../../../../../CONTEXT.md) | "Estimate" is a source document; "Repair spec" is the Case's specification |

## Source by layer

| Layer | File | Owns |
| --- | --- | --- |
| Web | `Pages/Cases/Shared/_CaseEstimate.cshtml` | the section: head, tabs, origin line, Use/Duplicate/Discard, header cells, grid, bars, Supplementary, work-lists, roll-up, the dialogs |
| Web | `Pages/Cases/Shared/_RepairSpecDiff.cshtml` | the one diff table (Added, Changed, Removed) read by Compare and Supplementary |
| Web | `Pages/Cases/Details.cshtml.cs`, `Details.*.cs` | `EditorLines`, `EditorTotals`, `SelectedEstimateSnapshots`, `Comparison`, `SupplementaryDiff`, `RegionalUpliftSuggestions`, the handlers (EditLine, ScaleEstimate, RemoveEstimateScaling, RestoreEstimateSnapshot, SetCurrentEstimate, DuplicateEstimate, DiscardEstimate, PreviewEstimateScale) |
| Web | `wwwroot/js/case-workspace.js` | the phantom row, the one rate control, the VAT reset, the slider preview (`is-previewed`), the Undo toast, the contract tick mirroring Decisions |
| Web | `Presentation/CaseWorkspaceLabels.cs`, `OperatorLabels.cs` | every label |
| Core | `Assessment/Estimates.cs` | line types and operations, `EstimateTotals.Compute`, the anomalies, `EstimatePolicy` |
| Core | `Assessment/RepairSpecificationActs.cs` | `ScalingFloors`, `RepairSpecificationScaling`, `RepairSpecificationComparison` (Compare, the four reasons, the statement), the snapshot kinds |
| Core | `Reports/ReportWording.cs` | the contract repair sentence |
| Infrastructure | `Persistence/EfRepairSpecificationStore.cs`, `EfRepairSpecificationSnapshotStore.cs` | the specs, lines and frozen versions; the origins "Before scaling", "Before vN was restored", "Imported from …" |

## Behaviours

### Specs and versions

A Case holds several live specs, one Current. Import, a Glass's return and New repair spec create a spec; a staff import becomes Current. Use repair spec switches; Duplicate copies; Discard deletes a spec that is not Current with its lines and versions (11 October 2026). The Versions dialog lists the Working draft then every frozen version with When, How it came about, Lines, Rate, Total inc VAT, Sent on report, and Restore while editing. (FRD-25 "One Current spec", "Engineer acts on a spec".)

### The grid

Type, Description, Part no., Qty, Unit (£), Hours, Paint h, Material (£), Source and a delete; a trailing blank line the script keeps; Add line; Delete all lines behind a dialog with Undo. Every column is open on every type. Reading draws every value in a `.gv` box in the control's place (23 September 2026). A Glass's included operation reads indented with its note (9 October 2026).

### Totals

`EstimateTotals.Compute` is the one owner: Parts, Panel labour, Paint labour, Materials, Specialist, the discount lines, Off-pattern items (treated as specialist), Net, VAT on the ticked categories, Repair cost inc VAT; the three names-only work-lists.

### Target % of value and the agreed sum

The slider runs from 1 % to the spec's own share of the Engineer's Value and lowers prices only; the floors are £50/h and 65 %; moving it previews Core's result in amber; Apply freezes, scales and freezes again; Remove scaling returns to the version frozen before. A contract repair's agreed sum below the share is the slider's starting target; the bar's mismatch chip states the gap. (FRD-25, 8 October 2026.)

### Compare and Supplementary

Compare lists the specs' Net, VAT and Gross and, for a chosen From and To spec, one table of Added, Changed and Removed lines with the changed cells bold. A spec may name the spec it supplements (Changes vs), with a reason; Explain the change on the report prints the statement. (FRD-25.)

## Things the FRD does not settle

- Whether a frozen version can be compared, and against what by default.
- Whether an import should propose the spec it supplements.
- Whether the agreed sum, once a scaling target, acts without the slider.
- Which figure columns a line type prices in the editor (Core only flags after the fact).
- Where the labour-rate control stands relative to the grid.
