# v36 notes: a full walk of the Case page

Temporary review artifact; see [README](README.md). The brief and the operator's remarks are in the [discussion log](discussion-log.md).

## 1. What the walk found and what the proposal changes

The Case page was walked on the running synthetic host on 9 October 2026 at `origin/dev` 37c4b96f5, in read mode and in the edit session, in seven states, in Scroll and Tabs, at 1580, 1440 and 760, with every dialog, the Actions menu, the viewer and crop, the Damage plan, a Valuation card click, a repair spec line, a save-as-you-go commit and Done. Evidence is in `live-shots/` (named by state, mode, width and section) and `captured/measure-*.json`; the mockup's numbered shots are listed in [verification.json](verification.json).

Tiers: **a** is alignment inside the existing contract (listed, not lettered); **b** moves or changes something the operator placed or an FRD rule (lettered in section 6); **c** is a behaviour a static page cannot show (section 2, with visual stand-ins in the mockup).

### Ribbon and section row

| # | Today | Proposal | Tier | Evidence |
| --- | --- | --- | --- | --- |
| f01 | With ten section links the row clips at 1440: Notes is cut and its icon sits beside Refresh; at 760 the overflow is 380px with the scrollbar hidden, so nothing says more links exist | Below 1500px the links drop their icons and tighten to 7px padding, so ten links fit at 1440; below 980px the row wraps | a | `live-shots/notready-read-1440.png`, `measure-engineer-read.json` (`navOverflow` 380 at 760) |
| f02 | The Saving… / Saved 10:04 word is 11px mono, and its width changes nudge Done | Body-size sans in a fixed 96px cell, right-aligned | a | `x-vehicle-saving-1580.png`, `engineer-edit-1580.png` |
| f03 | Claimant, Principal and Engineer share one basis (207px each at 1580) while Principal is always a short code, so the Engineer name is cut | Principal takes natural width like Registration; Claimant and Engineer get the room | a | `measure-engineer-read.json` (`ribbonItems`) |
| f04 | While a colleague holds the lease, "{Name} is editing" is on the ribbon chip and on all twelve section heads | Said once, on the ribbon beside Take over | b (A) | `colleague-read-1580.png`, `colleague-read-sec-claim-1580.png` |
| f05 | CONTEXT.md reserves "Estimate" for a repairer's or provider's source document and "AI Proposal" for the model's candidate repair specification, yet the aside says "Estimate draft ready" / "Review estimate" for an AI Proposal (`OperatorLabels.cs:622,1835`) and the Send to AI dialog says "Target Estimate" for the spec it works on (`:2411`); "Import estimate", "Estimate file" and "Drop estimate to import" are correct, the file is a source document | The AI Proposal rows say "AI Proposal ready" / "Review AI Proposal" (or the reserved term's own words); the dialog says "Target Repair Spec" | b (C) | `_CaseAside.cshtml:136-138`, `_CaseEstimate.cshtml` Send to AI dialog, CONTEXT.md "Repair spec", "AI Proposal" |
| f53 | At 760 the sticky chrome is 345px of a 1000px viewport (utility bar 96 + ribbon 208 + section row 40) | Below 980 only the section row stays sticky | b (B) | `measure-engineer-read.json` (`sticky.h` 249 at 760), `engineer-read-760.png` |

### Aside

| # | Today | Proposal | Tier | Evidence |
| --- | --- | --- | --- | --- |
| f06 | Figures draws an absent Repair cost inc VAT as a bold dash and the others as light dashes | One glyph, one weight, muted | a | `engineer-read-1580.png` |
| f07 | Below 1441 the folded strip stretches Figures to Next action's height and leaves Report not ready at half width | Cards keep their own height; Report not ready spans the strip | a | `engineer-read-1440.png`, `notready-read-1440.png` |
| f08 | Aside heads are 42px beside 51px section heads | 51px | a | `measure-engineer-read.json` (`asideCards`) |
| f09 | On a Held Case the step reads the word "Held" with a Case details button | The step names the review date and offers Release Hold | b (D) | `held-read-1580.png` |

### Case details and Claim

| # | Today | Proposal | Tier | Evidence |
| --- | --- | --- | --- | --- |
| f10 | Matter line wraps to two lines in a one-line row | Spans two columns at the foot of the Case sub-panel | a | `engineer-read-1580.png` |
| f12 | While editing, Sign-off Engineer, Claimant VAT registered, Vehicle type, Pre-incident condition and Transmission show a blank select where the box read "Unassigned" or "Not recorded" | The empty option carries the box's absent word | a | `engineer-edit-1580.png`, `engineer-edit-sec-vehicle-1580.png` |
| f13 | Case type, Our ref and Principal repeat the ribbon as greyed cells | The three go | b (E) | `engineer-read-1580.png` |
| f14 | Address wraps to two lines reading and clips editing ("LS1 1"), and Claimant VAT registered sits alone on a second row | Address spans two columns; the VAT pair shares the second row | a | `engineer-read-sec-claim-1580.png`, `engineer-edit-sec-claim-1580.png` |
| f15 | Two VAT facts side by side: free-text Claimant VAT status and Yes/No Claimant VAT registered, plus the Valuation warn tag | Listed; no change drawn | b (F, open) | `engineer-read-sec-claim-1580.png` |

### Inspection details

| # | Today | Proposal | Tier | Evidence |
| --- | --- | --- | --- | --- |
| f16 | The Storage sub-panel mixes Case-data and Engineer cells (`_CaseInspectionAddress.cshtml:24-25,175-198`), so a Case-data editor sees white and greyed boxes with no label | Storage per day and Recovery charge move to Decisions' Costs, hire & delays; Storage location keeps the row | b (G) | `engineer-edit-sec-claim-1580.png` |
| f17 | Inspect at offers disabled options suffixed " · not recorded" (`:109-111`) | Unavailable choices are absent | b (H) | source |

### Vehicle and Damage

| # | Today | Proposal | Tier | Evidence |
| --- | --- | --- | --- | --- |
| f18 | "Experian is not connected" is a pill on the section head and again on the Vehicle history sub-panel (`_CaseVehicle.cshtml:120,261`) | Once, on the sub-panel | b (K) | `engineer-edit-sec-vehicle-1580.png` |
| f19 | Thirteen cells in four columns leave MOT expiry alone, with editable and lookup-only cells interleaved so edit shows white and grey boxes mixed | Editable facts first, lookup-only last; the lone cell is a lookup cell | a | `engineer-edit-sec-vehicle-1580.png` |
| f20 | Mileage reads "42,000 mi" in mono and edits as "42000" beside a separate Odometer unit select | Listed; no change drawn | a | same |
| f21 | "UNRELATED-DAMAGE …" truncates | Unrelated damage and its deduction share one row at half width | a | `engineer-edit-sec-damage-1580.png` |
| f22 | "Recorded areas" is a count box ("0") beside "No damage recorded." | The count box goes | b (L) | same |
| f23 | Reset sits on a line of its own below the plan, above the area chips | Reset in the chip row | a | same |
| f24 | Material transfer spans two cells, leaving Airbags deployed alone on a fourth row | One cell each; Airbags joins the row | a | same |

### Valuation

| # | Today | Proposal | Tier | Evidence |
| --- | --- | --- | --- | --- |
| f25 | Get valuation at the right of the card head over a column of narrow right-aligned boxes (operator, 9 October) | head-link, foot or inline, the operator's choice | b (M) | `engineer-edit-sec-valuation-1580.png` (AI card), the operator's screenshot |
| f26 | Every unconnected card repeats a blue notice "{Source} valuation is unavailable. Contact an administrator or report a problem." while editing: five on one screen | One quiet line under the figures, the approved words | b (N) | `engineer-edit-sec-valuation-1580.png` |
| f27 | Card boxes are 28px beside 36px cells (sixteen on the ladder) | 32px, the small-control step | a | `measure-engineer-edit.json` (`heights`) |
| f28 | Reading omits Value increases and the Calculation line (`_CaseValuationCalculation.cshtml:54,81,202`): the section is 590px reading and 913px editing, so Edit grows it by 323px | Reading draws the edit geometry greyed | b (O) | `measure-engineer-read.json` / `-edit.json` (`sections.valuation.h`) |
| f29 | Cards read "—" for absent values where every cell says "Not recorded" | Listed; no change drawn | a | `engineer-read-sec-valuation-1580.png` |

### Repair Spec

| # | Today | Proposal | Tier | Evidence |
| --- | --- | --- | --- | --- |
| f32 | Figure columns and their headers do not share an alignment, Hours shows 0.700000 and clips, "Unit £" and "Material £" read as symbols (operator, 9 October) | Figures and headers right-aligned, two decimals, Part no. left; "Unit (£)" and "Material (£)", or "Unit" and "Material" with a £ prefix in the cell | b (X) | `x-estimate-line-edit-1580.png`, the operator's screenshot |
| f33 | Compare is a disabled button with fewer than two specs (`_CaseEstimate.cshtml:662`); Remove scaling is disabled when unscaled (`:401`) | Absent, not disabled | b (Y) | `x-estimate-line-edit-1580.png` |
| f46 | Five empty-state treatments: Notes icon under the global `.empty` 36px padding, the Repair Spec dashed box, Damage's plain line, the cell box, the hatched lazy placeholder | One: a muted sentence in a 36px row | a | `engineer-read-sec-valuation-1580.png`, `x-notes-read-1580.png` |
| f55 | The origin line prints an account id: "Populated 06 May 2031 11:30 by d47fbbae-…" | The person's name | a | `x-estimate-line-edit-1580.png` |

### Decisions

| # | Today | Proposal | Tier | Evidence |
| --- | --- | --- | --- | --- |
| f34 | "Not recorded" is a selected radio segment, reading and editing (`_CaseSettlement.cshtml:280-303`, v28 P29) (operator, 9 October) | box, segments-none or select, the operator's choice | b (J) | `engineer-read-sec-settlement-1580.png`, `x-decisions-repairable-edit-1580.png` |
| f35 | The metric strip repeats Figures, and Engineer's Value appears a third time in its own row with a Valuation tag; "Set in Valuation" and "From current repair spec" mix a pointer with a provenance | The strip goes; Labour hours becomes a cell beside Excess | b (I) | `engineer-read-sec-settlement-1580.png` |
| f36 | Repair delays and Report delay are 72px textareas beside 36px cells in one row | The two take a row of their own | a | `engineer-edit-sec-settlement-1580.png` |

### Report

| # | Today | Proposal | Tier | Evidence |
| --- | --- | --- | --- | --- |
| f38 | Engineer's comments (72px, span 3) sits beside Report date (36px) | Comments takes a full row like Valuation commentary | a | `engineer-read-sec-report-1580.png` |
| f39 | The Statement of truth is a 207px greyed box of five paragraphs on every Case | A sub-panel folded until opened | b (P) | same |
| f40 | Send report exists only while editing (`_CaseReport.cshtml:102-105`); Generate report in both modes; the attached "Report" checkbox is disabled (`:381`) | Listed | b (Z) | source |

### Files and Notes

| # | Today | Proposal | Tier | Evidence |
| --- | --- | --- | --- | --- |
| f42 | Two red primaries on one screen: Files' Add evidence and Notes' Add Case note, the latter without a lease in read mode | Add Case note is secondary | b (Q) | `engineer-read-sec-files-1580.png` |
| f43 | Tiles grow on Edit: the read status line gives way to a two-row tool panel | The read line keeps the panel's height | a | `x-files-images-read-1580.png`, `x-files-images-edit-1580.png` |
| f45 | A wrapped system note draws the actor centred on a line of its own, the text below starting with "—" | One line: when, who, what | a | `x-files-tag-picker-edit-1580.png` (the timeline under it) |

### Cross-cutting (listed)

- The control-height ladder on one page: 36 (cells, buttons), 32 (small buttons, impact rows), 30 (increase amounts), 28 (card boxes, report tabs), 24 (chips, layout switch), 22 (workbench icon buttons); textareas 64, 72 and 90; `measure-engineer-edit.json` (`heights`). The Ruler widget draws it.
- Paddings 10/12/14 across sibling cards and radii 2/3/4/5/8/12 one-offs; hard-coded colours in `case-workspace.css` (`#f4f7fa`, `#aeb8bd`, `#e2c0c2`, `#9aa3ad`, `#333`).
- The global focus-visible rule sets `position:relative` and displaces the absolutely positioned notice and toast dismiss ×; estimate grid cells double-ring (navy border, global outline, box shadow); focus colours differ (red global, navy segments and tabs, blue selects).
- `.toast-region` (z 1100) sits under `.case-viewer` (z 1200): the crop toast is hidden behind the viewer, and its words "The crop was staged. Save the Case to keep it." (`case-workspace.js:4976`) predate save-as-you-go.
- A refused save shows twice: a notice on the page and a toast.
- Empty value words differ: "Not recorded", "—", "none", "Not applicable", "Unassigned", "Not linked".

## 2. Behaviour findings (tier c), with stand-ins

| # | Finding | Stand-in | Scope |
| --- | --- | --- | --- |
| f50 | The arrow link buttons Assign Engineer, Open Triage, Review source (Work Centre rows), Open full Case (Cases list Quick detail), and the Case page's own navigating links (the Next action control, Add evidence, Send chaser, Open report) show no busy state; `site.js:128-365` busies form submits and downloads only (operator, 9 October) | Busy widget | R |
| f51 | Workflow and Closure actions land at the top: `RedirectToDetails` drops the section (`CaseMutationPageModel.cs:648-675`); Create audit too (`Details.Frame.cs:256`, no notice); Refresh drops the selected spec | Landing widget | S |
| f52 | A landed commit swaps ribbon, notices and aside at once, then the catch-up swaps them again (`case-workspace.js:949-1047`); the commit swap does not re-anchor, so a notice or chip row above the reading line nudges the page; focus restore covers `#case-main` only; the first commit of the session recorded six fields when one was typed (walk: "Vehicle make, Inspection address treatment, Impacts, Disclose guide source, Include unrelated damage, Valuation commentary") | Flicker widget | T |
| f54 | Tabs switching and section jumps use `behavior:'auto'` under the global `scroll-behavior:smooth`, so they glide; a fold above the reading line moves the page; the Scroll/Tabs cookie is a session cookie while fold and rail cookies last a year | none | U |
| f56 | An Actions item that opens a dialog leaves the menu open behind the backdrop (`site.js:2129-2168`); the Damage clicker rebuilds its list on every pointermove; the estimate full screen registers a body-wide MutationObserver and Escape in a dialog above it may collapse it | none | V |
| f57 | A section head offers Edit on Engineer sections the viewer cannot edit (`SectionOffersEdit` ignores `CanEditEngineering`); a locked section says nothing | none | W |
| f58 | The Held chip read "Held" alone on the walk although HoldReviewOn was set (`StateChipText`); to confirm on a real hold | none | D |

## 3. Live rules the mockup mirrors

| Rule | Source |
| --- | --- |
| Read and edit share one geometry: a greyed `.fv` reading, a white `.fi` editing, `ro` where it cannot be edited | `site.css:990-1030`, design README |
| Sticky block 97px (ribbon 56 + row 40 + border), `--sticky-h` measured live | `case-workspace.js:84-86`, `site.css:1032-1091` |
| Aside 285px, sticky and capped at 1441px and above; folded two-up strip below | `site.css:1060-1080`, `case-workspace.css:95-100` |
| Section links: Damage and Valuation ride under Vehicle; Original report only on an Audit Case | `Details.Frame.cs:158-164`, `Details.cshtml:121` |
| Next action is one step; Report not ready folds until opened | FRD-16, v35 design 8 |
| Actions menu items per state; Export case always; Create audit gated with its reason | `_CaseRibbon.cshtml:26-58, 308-329` |
| Section head Edit claims the page-wide lease with `section`; Done releases it | `_CaseSectionHeadTools.cshtml`, `case-workspace.js:1136-1141` |
| Busy state: `aria-busy`, the control's `data-busy-label`, a spinner | `site.js:128-365`, `site.css:719` |

## 4. Frame rules (numbers)

- Utility bar 48px (96 at 760); ribbon 56px; section row 40px; sticky block 97px.
- Content capped at 1580 with 18px padding; workspace grid `minmax(0,1fr) 285px`, gap 10, padding 12.
- Cells 36px, small controls 32px, rows 40px, body 13.5px, labels 10.5px uppercase.
- Section heads 51px; aside heads 42px today, 51px proposed (f08).
- Valuation cards three to a row (two at 1180, one at 760); card boxes 28px today, 32px proposed (f27).

## 5. Decisions taken and their authority

- Sharpening only: no section moves, no new section, no new control vocabulary, no new copy (operator's brief; the necessary-copy rule).
- Every proposed word is a live label (`CaseWorkspaceLabels`, `OperatorLabels`) or the fixture's; the self-check's "read shows no live control" and the diff legend guard the frame.
- "One fact, one home" (guardrails) drives f04, f13, f18, f22, f35.
- "Read and edit share one geometry" (operator, 23 September 2026) drives f14, f28, f43.
- "Absent, never disabled" (design README, guardrails) drives f17, f33.
- Amber stays the incomplete colour; nothing new is coloured.

## 6. Sign-off list

Each is "Confirm, or …". Letters are stable; settled items keep their letter with the date and outcome in italics.

- **A** (f04). "{Name} is editing" is said once, on the ribbon; section heads drop the label. Confirm, or keep it on every head.
- **B** (f53). Below 980px only the section row stays sticky. Confirm, or keep the whole block sticky.
- **C** (f05). "Estimate draft ready" / "Review estimate" name the AI Proposal by its reserved term, and the Send to AI dialog's "Target Estimate" becomes "Target Repair Spec"; the import words keep "estimate" (a source document). Confirm the words, or keep them.
- **D** (f09, f58). The Held step reads "Held · review on {date}" and offers Release Hold. Confirm, or keep Case details.
- **E** (f13). Case details drops Case type, Our ref and Principal (the ribbon's facts). Confirm, or keep the greyed copies.
- **F** (f15). Claimant VAT status and Claimant VAT registered stay two facts. Confirm, or ask for one fact (an FRD-16 question).
- **G** (f16). Storage per day and Recovery charge move to Decisions' Costs, hire & delays. Confirm, or keep them under Inspection's Storage with a label.
- **H** (f17). Inspect at omits unavailable choices rather than disabling them with " · not recorded". Confirm, or keep.
- **I** (f35). Decisions' metric strip goes; Labour hours becomes a cell beside Excess. Confirm, or keep the strip.
- **J** (f34). "Not recorded" is the box's absent word, never a chosen segment: **box**, **segments-none** or **select**. Choose one, or keep today's segment.
- **K** (f18). Experian is not connected is said once, on the Vehicle history sub-panel. Confirm, or keep the head pill.
- **L** (f22). The Recorded areas count box goes. Confirm, or keep.
- **M** (f25). Get valuation: **head-link**, **foot** or **inline**. Choose one, or keep the head as it is.
- **N** (f26). The unconnected-card sentence reads as one quiet line, the approved words unchanged. Confirm, or keep the notice box.
- **O** (f28). Reading draws Value increases and the deductions in the edit geometry, greyed. Confirm, or keep them edit-only.
- **P** (f39). The Statement of truth folds under a sub-panel head. Confirm, or keep it open; or say it leaves the page (a Report-content decision).
- **Q** (f42). Add Case note is a secondary button. Confirm, or keep it red.
- **R** (f50). Navigating link buttons get the established busy state (operator's item). Confirm the list of buttons.
- **S** (f51). Workflow and Closure actions return to the section they were taken from, with their notice. Confirm, or keep the top.
- **T** (f52). One redraw per save, re-anchored, focus kept; the first save records only what changed. Confirm as Stage 2 scope.
- **U** (f54). Instant Tabs switching and jumps; the Scroll/Tabs choice lasts a year. Confirm as Stage 2 scope.
- **V** (f56). Menu closes on dialog open; crop toast above the viewer with its words brought up to date; Damage list not rebuilt while dragging. Confirm as Stage 2 scope.
- **W** (f57). Section Edit only where the viewer can edit; a locked section says why once. Confirm as Stage 2 scope.
- **X** (f32). The spec grid aligns its figures; the money headers read **"Unit (£)" / "Material (£)"** or **"Unit" / "Material" with a £ prefix in the cell**. Choose one.
- **Y** (f33). Compare and Remove scaling are absent until they can act, not disabled. Confirm, or keep.
- **Z** (f40). Send report shows in read mode too, its controls greyed, and the attached "Report" tick is a value, not a disabled control. Confirm, or keep it edit-only.
- **AA**. This folder is removed in the Stage 2 pull request. Confirm, or keep it as the record.

Tier a findings (f01, f02, f03, f06, f07, f08, f10, f12, f14, f19, f20, f21, f23, f24, f27, f29, f36, f38, f43, f45, f46, f55 and the cross-cutting list) are listed for information; say if any should not go to Stage 2.

## 7. Self-check

`python check-case-walk-v36.py` on 9 October 2026: `RESULT {"fail": [], "okCount": 1005}`, no console error, 134 screenshots ([verification.json](verification.json)). It covers today and the proposal in seven states, read and edit where the state has an edit frame, Scroll and Tabs, at 1580, 1440 and 760; each finding alone and switched off; the operator's variants; the dialogs, the viewer and the widgets. It also records the evidence for f01 (today clips the row at 1440 on the Audit Case and at 760), f14 (today's Address input clips its text) and f28 (today Valuation grows by 148px on Edit; the proposal within 48px, the remainder being the AI research card's head control). This is evidence about the mockup, not the application.

## 8. Known limits

- The frames are captured pages with their scripts removed: a section link scrolls, a fold toggles, a dialog opens and closes, the viewer opens on the first image; nothing saves, no menu item acts, Get valuation does nothing.
- Two fixture departures: Glass's is drawn as a connected card for the Get valuation variants (the host has no provider), and the Held step's review date is the fixture's.
- The With Engineer frames were recaptured after a Repairable outcome and one spec line were added on the walk, so they hold figures the earlier `live-shots/engineer-*` do not.
- Tabs layout is emulated by the live classes (`data-layout="tabs"`, `is-active`), not by `case-workspace.js`.
- The Work Centre and Cases list frames exist only for the busy-state stand-in.
- The behaviour findings are from reading `case-workspace.js` and `site.js` and from the edit-session pass (`captured/extra.json`); the redraw timings were not instrumented.
