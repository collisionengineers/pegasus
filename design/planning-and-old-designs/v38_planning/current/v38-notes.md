# v38 notes: the Repair Spec section

Temporary review artifact; see [README](README.md). The brief and the operator's words are in the [discussion log](discussion-log.md).

## 1. What changes and why

The operator's 15 September Case dashboard reference (section 6, Repair specification) was compared with the live Repair Spec section at `origin/dev` 6d47cd3cb on 12 October 2026. Most of section 6 had been rebuilt by v28; five ideas had not, and the operator asked for them back. Each is one proposal with one variable; `design=proposal` turns them all on.

| # | Today | Proposal | Variable | Shots |
| --- | --- | --- | --- | --- |
| P1 | Every figure column is a box on every line type: a Repair line offers Part no., Qty and Unit (£); an R&I line offers Material (£); a Replace line offers Paint h. Only Core's anomalies say which figures are off-pattern (amber) | A line type prices only its own columns (section 2, the map). A cell the type does not price reads as one dash in both modes; a value that sits in one (an import's off-pattern amount) keeps its amber box, so nothing is cleared; changing the Type opens and closes the cells | `cols: today, dash` | 18, 21, 14, 30 |
| P2 | Compare reads two of the Case's specs: one table of the To values for Added, Changed and Removed rows; a version has Restore only; nothing prints | From and To list the selected spec's Working draft and its frozen versions, then the other specs and theirs; opening from More starts at the version the last sent report used against Working; one summary line; one table with the five figure columns per side (Qty, Unit (£), Hours, Material (£), Line £) under a head naming the side and its rate, unchanged rows included, a changed To cell bold, "(was Repair)" after a changed type; Compare with current on every version row; Print comparison sheet prints the dialog alone | `cmp: today, side` | 23, 24, 35, 10, 11 |
| P3 | An import makes a second spec; with two specs the Supplementary sub-panel sits at the foot and waits: Changes vs is Choose… until the Engineer picks the other spec; the diff and the statement follow the choice | As the reference: the supplementary estimate is imported onto the existing repair spec (the grid it replaces frozen as a version, the import frozen too), and the Supplementary panel opens at the top of the section, above the grid: "Supplementary · changes vs v2 · Sent on report · £3,004.79 → £3,821.62 (+£816.83) · full comparison", one line per change (Added, Changed with what changed, Removed, with the line's value), then Explain the change on the report, the reason and Dismiss; the statement appears under the panel once Explain is ticked; full comparison opens Compare on that version against the working grid | `supp: today, auto` | 18, 21, 22, 31 |
| P4 | The agreed total sum is read from Decisions; the Target % slider starts at the sum's share; the mismatch chip states "Spec £X above agreed" and does nothing | As the reference: the sum is a box in the Contract repair bar (the same Case field Decisions holds); typing it previews the scaling to that sum exactly as the slider does (amber cells, readout, Preview, Apply); when the spec drifts from the sum again the mismatch chip is the control that rescales ("Spec £X above agreed · rescale") | `agreed: today, drives` | 25, 27, 12 |
| P5 | Labour rate (£/h) and Regional uplift are two of six head cells above the grid | As asked ("labour rate also below repair spec"): the rate and the uplift are also a bar directly under the grid, first in the bar stack, where the reference places them; the head keeps its six cells. `bar` is drawn for comparison: the bar alone, four head cells | `rate: head, both, bar` | 18, 14, 28, 29 |

## 2. Live rules the mockup mirrors

- Totals: `EstimateTotals.Compute` (`src/Pegasus.Core/Assessment/Estimates.cs`): parts on Replace, panel labour on every line's Hours but Specialist, paint labour on Paint and Blend Paint h, materials on every line, Specialist priced by amount, a unit amount on a line that prices no part carried as off-pattern into specialist, the four discounts, VAT on the ticked categories; pence rounding per printed component.
- Scaling: `RepairSpecificationScaling` (`RepairSpecificationActs.cs`): one factor lowers prices and materials to 65 % and the rate to £50/h; hours never move; bisection on the gross; the slider's top is the spec's own share of the Engineer's Value (`PercentOfValue`); the readout ends "labour at floor" when the rate stops there.
- Compare and the statement: `RepairSpecificationComparison.Compare` and `SupplementaryStatement` (lines matched by description; the four reason leads; "The repair time for … (3.5 h → 4.5 h) has been revised."; "… is no longer required."; "The estimated repair cost has increased from £A to £B.").
- The contract sentence: `ReportWording.ContractRepairSentence`.
- Labels: `OperatorLabels.CaseWorkspace.EngineerSections`, `CaseWorkspaceLabels.Estimate`, `.EstimateTotals`, `.EstimateVat`, `.EstimateDiscount`; the Source chip words (`imported · AX`, `amended`, `manual`); the version origins (`Imported from Audatex`, `Report sent`); the ribbon chip `Repairs N% of value` at the 66 and 80 percent tones.
- P1's map (which columns a type prices), the reference's `COLS` map carried to today's seven types: Replace — Part no., Qty, Unit (£), Hours, Material (£); Repair — Hours, Material (£); R&I — Hours; Paint and Blend — Hours, Paint h, Material (£); Specialist — Qty, Unit (£), Hours, Material (£); Other — Hours. Core's anomaly rules are unchanged: the map decides what is drawn, Core still says what is off-pattern.
- The Undo toast after a removed line is drawn as `case-workspace.js` draws it (`toast--undo`, v28 P16).

## 3. Frame rules (numbers)

- Content capped at 1580 with 18px padding; workspace grid `minmax(0,1fr) 285px`, gap 10; the aside folds into a strip below 1441.
- Ribbon 56px, section row 40px; 36px controls, 32px small controls; 13.5px body text; the grid's boxes 30px.
- One primary (Import) plus one More menu per head; Compare is a More item offered with two specs, never a disabled item.
- Read and edit share one geometry: the bar, the sum box and the dash cells stand where their edit-mode controls stand.
- No explanatory copy: the proposals add four control labels and no sentence.

## 4. Decisions taken and their authority

- Today an import makes a new spec that becomes Current (FRD-25 "One Current spec"), so Today's fixture holds two specs: the just-imported supplementary estimate (Current) and the earlier spec with the sent version. Under P3 the import lands on the existing spec, as a Glass's return already does today (FRD-25 "A Glass's estimate belongs to its repair spec": lines replaced, the import frozen as a version), so the proposal's fixture is one spec with versions v1 imported, v2 sent on report and v3 the import, and the panel reads against v2.
- Every import, scale, removal, restore and sent report freezes a version (FRD-25 "Engineer acts on a spec"); the fixture's versions follow that.
- The Case's one Save records the spec (FRD-16); the mockup commits on change and shows "Saved 09:41".
- The agreed sum is Case data beside the specification and a different sum is a scaling target (FRD-25); P4 makes the target act.
- The design authority's absent-versus-disabled rule: Remove scaling appears only when scaled; Compare only with two specs.
- The operator's request settles the shape of each proposal, and the reference file settles its mechanics (operator, 12 October 2026: "things I tell you to do are not issues to be settled"). Taken from the reference without asking: the rate bar is drawn as well as the head cells ("also"); typing the agreed sum rescales and the mismatch chip rescales on click (`crAmount`, `crMis`); Compare defaults to the last sent version against the working grid, with "Compare with current" on each version row and "Print comparison sheet" (`openCompare`, `openVersions`); the Supplementary panel is born of an import onto the existing grid, sits above the grid, and reads against the last sent version, else the grid replaced, with the change list, the Explain tick, the reason, Dismiss and the full comparison link (`doImport`, `showSupp`, `renderSupp`; operator, 12 October: "this supplementary bit is at the top clearly, and i think its from uploading/importing something onto an existing repair spec"); a cell the type does not price reads "—" in the editor and an imported value in one stays amber, never cleared (`COLS`, `cellIn`).
- Carried to today's columns by judgment, since the reference had neither Part no. nor Paint h nor an Other type: Part no. on Replace only; Paint h on Paint and Blend; Other (a Glass's included operation) prices Hours only.

## 5. Deliberate departures from live

- The rest of the page is the v36 frame of 9 October: it still shows the pre-v36 Decisions radios, the blocker "how" line and has no Queries section. The Repair Spec section is the tip's.
- The ribbon chip "Repairs N% of value" and the Figures card are patched by the runtime from the fixture's Engineer's Value (£4,200), which the captured Case lacked.
- Today's Supplementary fills the diff as soon as Changes vs is chosen; live records the choice with the next commit and redraws then.
- The Target % and agreed-sum previews are computed in the page; live asks Core through `PreviewEstimateScale`.
- Restore, Use repair spec, Duplicate and Discard are drawn but do nothing offline.

## 6. Sign-off list

The five proposals are the request and their mechanics are the reference file's (section 4); neither is an item. One item remains.

- **A**. Keep or remove this folder in the Stage 2 PR.

*12 October 2026: items A–I of the first draft restated the request and were withdrawn the same day (operator: "things I tell you to do are not issues to be settled"); the mockup now follows the reference on each, and section 4 records what was taken from it.*

Stage 2 consequences: FRD-25 "Engineer acts on a spec" sentences for Compare over versions, the agreed-sum preview, and the import onto a selected spec (today a file import always creates a spec; the Glass's-return path that replaces a spec's lines and freezes the import is the precedent) with the Supplementary panel that follows it; `RepairSpecificationComparison.Compare` over snapshots and a Compare-with-current route; the rate bar in `_CaseEstimate.cshtml`; the editor's column map in the partial and `case-workspace.js`, with hidden inputs keeping the posted arrays aligned as the included-operation rows do; no Core totals change.

## 7. Self-check

12 October 2026 (third run, after the Supplementary rework): `RESULT {"fail":[],"okCount":307}` over today (two specs) and the proposal (one spec), read and edit, at 1580, 1440 and 760; each variable alone on today and off on the proposal; the P1 dashes and the amber off-pattern cell; the side-by-side Compare with 18 rows, the sent-version default, Compare with current from v1, the print sheet; P3 at the top with six changes, Explain showing the statement, full comparison opening Compare on v2, Dismiss, read mode; P4 previewing as typed and from the chip; the Target % preview and Apply; P5 both and the bar alone; the four dialogs; Diff, Split; a type change, a removed line and its Undo. 37 shots, listed in [verification.json](verification.json). Evidence about the mockup, not the application.

## 8. Known limits

- Font fallback and synthetic tile images; the frames' fixture Case (QDOS31001) with the v36 host's fixed clock.
- The frame deltas in section 5: the page around the section is 9 October's.
- Send to AI is not drawn: the captured Case had no Engineer's Value, so the live page withheld it.
- The print sheet is checked by print-media emulation; the browser's own print dialog is not exercised.
- Nothing persists; a reload resets the grid, the previews and the dismissed panel.
