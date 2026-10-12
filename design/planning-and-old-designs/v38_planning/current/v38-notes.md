# v38 notes: the Repair Spec section

Temporary review artifact; see [README](README.md). The brief and the operator's words are in the [discussion log](discussion-log.md).

## 1. What changes and why

The operator's 15 September Case dashboard reference (section 6, Repair specification) was compared with the live Repair Spec section at `origin/dev` 6d47cd3cb on 12 October 2026. Most of section 6 had been rebuilt by v28; five ideas had not, and the operator asked for them back. Each is one proposal with one variable; `design=proposal` turns them all on.

| # | Today | Proposal | Variable | Shots |
| --- | --- | --- | --- | --- |
| P1 | Every figure column is a box on every line type: a Repair line offers Part no., Qty and Unit (£); an R&I line offers Material (£); a Replace line offers Paint h. Only Core's anomalies say which figures are off-pattern (amber) | A line type prices only its own columns (section 2, the map). A cell the type does not price reads as one dash in both modes; a value that sits in one (an import's off-pattern amount) keeps its amber box, so nothing is cleared; changing the Type opens and closes the cells | `cols: today, dash` | 18, 21, 14, 30 |
| P2 | Compare reads two of the Case's specs: one table of the To values for Added, Changed and Removed rows; a version has Restore only; nothing prints | From and To list the selected spec's Working draft and its frozen versions, then the other specs and theirs; opening from More starts at the version the last sent report used against Working; one summary line; one table with the five figure columns per side (Qty, Unit (£), Hours, Material (£), Line £) under a head naming the side and its rate, unchanged rows included, a changed To cell bold, "(was Repair)" after a changed type; Compare with current on every version row; Print comparison sheet prints the dialog alone | `cmp: today, side` | 23, 24, 35, 10, 11 |
| P3 | With two specs, Supplementary waits: Changes vs is Choose… until the Engineer picks the other spec; the diff and the statement follow the choice | The spec an import has just made (Current) opens with Supplementary filled: Changes vs the version the last sent report used (else the spec it replaced), Reason Supplementary estimate received, Explain unticked, the diff and the statement drawn; Changes vs also lists the sent versions; Dismiss folds the panel while editing | `supp: today, auto` | 18, 31, 05 |
| P4 | The agreed total sum is read from Decisions; the Target % slider starts at the sum's share; the mismatch chip states "Spec £X above agreed" and does nothing | The sum is a box in the Contract repair bar (the same Case field Decisions holds). `preview`: typing it previews the scaling to that sum exactly as the slider does (amber cells, readout, Preview, Apply). `button`: the chip stays and a Rescale to agreed sum button runs the preview | `agreed: today, preview, button` | 25, 27, 12 |
| P5 | Labour rate (£/h) and Regional uplift are two of six head cells above the grid | They are a bar directly under the grid, first in the bar stack, as the reference places them; the head keeps Repair spec name, Other costs (£), VAT % and Repairer VAT status. `both` keeps them in the head as well | `rate: head, bar, both` | 18, 14, 28, 29 |

## 2. Live rules the mockup mirrors

- Totals: `EstimateTotals.Compute` (`src/Pegasus.Core/Assessment/Estimates.cs`): parts on Replace, panel labour on every line's Hours but Specialist, paint labour on Paint and Blend Paint h, materials on every line, Specialist priced by amount, a unit amount on a line that prices no part carried as off-pattern into specialist, the four discounts, VAT on the ticked categories; pence rounding per printed component.
- Scaling: `RepairSpecificationScaling` (`RepairSpecificationActs.cs`): one factor lowers prices and materials to 65 % and the rate to £50/h; hours never move; bisection on the gross; the slider's top is the spec's own share of the Engineer's Value (`PercentOfValue`); the readout ends "labour at floor" when the rate stops there.
- Compare and the statement: `RepairSpecificationComparison.Compare` and `SupplementaryStatement` (lines matched by description; the four reason leads; "The repair time for … (3.5 h → 4.5 h) has been revised."; "… is no longer required."; "The estimated repair cost has increased from £A to £B.").
- The contract sentence: `ReportWording.ContractRepairSentence`.
- Labels: `OperatorLabels.CaseWorkspace.EngineerSections`, `CaseWorkspaceLabels.Estimate`, `.EstimateTotals`, `.EstimateVat`, `.EstimateDiscount`; the Source chip words (`imported · AX`, `amended`, `manual`); the version origins (`Imported from Audatex`, `Report sent`); the ribbon chip `Repairs N% of value` at the 66 and 80 percent tones.
- P1's map (which columns a type prices): Replace — Part no., Qty, Unit (£), Hours, Material (£); Repair — Hours, Material (£); R&I — Hours; Paint and Blend — Hours, Paint h, Material (£); Specialist — Qty, Unit (£), Hours; Other — Hours. Core's anomaly rules are unchanged: the map decides what is drawn, Core still says what is off-pattern.
- The Undo toast after a removed line is drawn as `case-workspace.js` draws it (`toast--undo`, v28 P16).

## 3. Frame rules (numbers)

- Content capped at 1580 with 18px padding; workspace grid `minmax(0,1fr) 285px`, gap 10; the aside folds into a strip below 1441.
- Ribbon 56px, section row 40px; 36px controls, 32px small controls; 13.5px body text; the grid's boxes 30px.
- One primary (Import) plus one More menu per head; Compare is a More item offered with two specs, never a disabled item.
- Read and edit share one geometry: the bar, the sum box and the dash cells stand where their edit-mode controls stand.
- No explanatory copy: the proposals add four control labels and no sentence.

## 4. Decisions taken and their authority

- Import makes a new spec that becomes Current (FRD-25 "One Current spec"), so the fixture's just-imported supplementary estimate is Current and the earlier spec, with the sent version, is a Draft.
- Every import, scale, removal, restore and sent report freezes a version (FRD-25 "Engineer acts on a spec"); the fixture's versions follow that.
- The Case's one Save records the spec (FRD-16); the mockup commits on change and shows "Saved 09:41".
- The agreed sum is Case data beside the specification and a different sum is a scaling target (FRD-25); P4 makes the target act.
- The design authority's absent-versus-disabled rule: Remove scaling appears only when scaled; Compare only with two specs.

## 5. Deliberate departures from live

- The rest of the page is the v36 frame of 9 October: it still shows the pre-v36 Decisions radios, the blocker "how" line and has no Queries section. The Repair Spec section is the tip's.
- The ribbon chip "Repairs N% of value" and the Figures card are patched by the runtime from the fixture's Engineer's Value (£4,200), which the captured Case lacked.
- Today's Supplementary fills the diff as soon as Changes vs is chosen; live records the choice with the next commit and redraws then.
- The Target % and agreed-sum previews are computed in the page; live asks Core through `PreviewEstimateScale`.
- Restore, Use repair spec, Duplicate and Discard are drawn but do nothing offline.

## 6. Sign-off list

The five proposals are the request. These are the choices inside them, phrased "Confirm, or …".

- **A** (P5). The labour rate and the uplift sit in a bar under the grid and leave the head (`rate:bar`). Confirm, or keep them in the head as well (`rate:both`).
- **B** (P4). Typing the agreed sum previews the scaling at once (`agreed:preview`). Confirm, or preview only on the button (`agreed:button`), and confirm the button's words "Rescale to agreed sum".
- **C** (P4). The agreed sum is a box in the Contract repair bar as well as the Decisions cell, one Case field in two places. Confirm, or keep it in Decisions only and have the bar's chip rescale.
- **D** (P2). Compare opened from More starts at the version the last sent report used against the selected spec's Working draft. Confirm, or start at Choose… as today.
- **E** (P2). "Compare with current" on every version row, and "Print comparison sheet" on the dialog. Confirm the two labels.
- **F** (P3). The just-imported spec supplements the version the last sent report used, else the spec it replaced. Confirm, or always the replaced spec.
- **G** (P3). Dismiss folds the filled panel for that spec while editing (the versions keep the history). Confirm, or no Dismiss.
- **H** (P1). The type map in section 2 (Other prices Hours only; Specialist Qty, Unit (£), Hours; Part no. on Replace only). Confirm, or amend a row.
- **I** (P1). An unavailable cell reads as a dash in edit mode too, and a value sitting in one keeps its amber box. Confirm, or blank in edit mode.
- **J**. Keep or remove this folder in the Stage 2 PR.

Stage 2 consequences recorded per item: FRD-25 "Engineer acts on a spec" sentences for Compare over versions (D, E), the automatic supplementary (F, G) and the agreed-sum preview (B, C); `RepairSpecificationComparison.Compare` over snapshots and a Compare-with-current route; the rate control's placement in `_CaseEstimate.cshtml` (A); the editor's column map in the partial and `case-workspace.js`, with hidden inputs keeping the posted arrays aligned as the included-operation rows do (H, I); no Core totals change.

## 7. Self-check

12 October 2026: `RESULT {"fail":[],"okCount":370}` over today and the proposal, read and edit, both specs, at 1580, 1440 and 760; each variable alone on today and off on the proposal; the P1 dashes and the amber off-pattern cell; the side-by-side Compare with 18 rows, the sent-version default, Compare with current from v1, the print sheet; P3 filled, dismissed, read; P4 preview and button; the Target % preview and Apply; P5 both and read; the four dialogs; Diff, Split; a type change, a removed line and its Undo. 37 shots, listed in [verification.json](verification.json). Evidence about the mockup, not the application.

## 8. Known limits

- Font fallback and synthetic tile images; the frames' fixture Case (QDOS31001) with the v36 host's fixed clock.
- The frame deltas in section 5: the page around the section is 9 October's.
- Send to AI is not drawn: the captured Case had no Engineer's Value, so the live page withheld it.
- The print sheet is checked by print-media emulation; the browser's own print dialog is not exercised.
- Nothing persists; a reload resets the grid, the previews and the dismissed panel.
