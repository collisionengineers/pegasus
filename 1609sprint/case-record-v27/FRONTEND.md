# Case record v27 — front-end changes

Snapshot: 16 September 2026, `dev` at `45a011165`. One section per feature,
in the work-package order of [INTEGRATION-PLAN.md](INTEGRATION-PLAN.md).
The front end is `src/Pegasus.Web`: the Case record page
(`Pages/Cases/Details.cshtml`, its page model split across
`Details.cshtml.cs` / `Details.Frame.cs` / `Details.Report.cs` /
`Details.Valuation.cs` / `Details.Files.cs`), the section partials
`Pages/Cases/Shared/_Case*.cshtml`, the labels
(`Presentation/CaseWorkspaceLabels.cs`, `OperatorLabels.cs`), the script
`wwwroot/js/case-workspace.js` (2,673 lines; IIFE modules bound by
`data-*` hooks) and the styles `wwwroot/css/case-workspace.css` (508
lines) over `site.css`. The mockup's markup in
`design/planning-and-old-designs/v27_planning/current/v27-build/case-record.src.html`
and its scripts are the drawing to convert, not code to copy: the mockup's
`data-when` engine, strip and fixtures are not product.

Rules every change keeps (`docs/design/README.md`, FRD-12): read mode shows
values, never empty inputs; both `.fv` (value) and `.fi` (input) render in
every cell and `.is-editing` / `.is-locked` switch them so entering edit
never moves the page; one primary plus one menu per section head; no
explanatory copy; every string is an `OperatorLabels` /
`CaseWorkspaceLabels` constant; script is progressive — every action has a
no-script form.

Conformance evidence for every package: screenshots of the routed page at
1580×1000, 1440×900 and 760×1000 in the same states as the mockup shots
cited, compared side by side (the `razor-html-mockup-conversion` skill's
Stage 2 lens), plus the integration tests named.

## 1. Damage marks (WP1; G–G7)

Files: `_CaseDamage.cshtml`, `Presentation/DamagePlanGeometry.cs`,
`case-workspace.js` (the `[data-damage-editor]` module at ~line 955),
`case-workspace.css` (`.damage-*`, `.dv-*` at ~line 99), `Details.cshtml.cs`
`OnPostSaveAsync` (`damageImpacts` parameter, lines 1279, 1308, 1445),
`CaseWorkspaceLabels.Damage`.

- `DamagePlanGeometry` (Web) adds the eight area guide polygons from Core's
  `DamageAreaGeometry` and drops the per-panel hit paths for the chosen
  variant (the panels remain drawn as seams, `.dv-seam`, not as targets).
- `_CaseDamage.cshtml`: the SVG keeps `.dv-body`, glass, seams, wheels,
  mirrors, legend and the three chips; adds a `<g data-damage-guides>`
  with the dashed area outlines (visible only in `.is-editing`), a
  `<g data-damage-marks>` rendered server-side from the recorded marks so
  a no-script read shows them, and the readout `[data-damage-readout]` now
  naming the area under the pointer. The hidden field `damageImpacts`
  carries the marks JSON in the new shape (BACKEND § 1). The list
  `[data-damage-impact-list]` becomes rows of: number, area names
  (`DamageAreas` display words, no kind word), severity select, note
  input, ×. Heading per G7. Derived cells `[data-damage-location]`,
  `[data-damage-severity]`, `[data-damage-count]` and the narrative cell
  read from Core (§ 3 below) on load and are recomputed by script from
  the same words table passed in `data-damage-*` attributes.
- Script, per variant (only the chosen one ships):
  - A Pins: `pointerdown` on the silhouette → hit-test against the body
    path (`SVGGeometryElement.isPointInFill`) and wheels → append a mark
    at the ViewBox point → render pin (circle + number, halo colour by
    severity) → append row; drag on a pin moves it and re-derives its
    area; the row's severity select recolours the halo.
  - B Brush: `pointerdown`/`move`/`up` collects points (throttled, ≤ 24
    after simplification), draws a `<path>` with round caps; one stroke =
    one mark; areas = union of `areaAt` over the points.
  - C Areas: press sets the centre, drag sets the radius; `<circle>`;
    areas = centre + eight ring points.
  - D Arrows: press sets the impact point, drag sets the vector;
    `<line>` + head marker; severity band from length, written into the
    row's select (still editable); direction stored if G3.
  - Common: `areaAt(x, y)` is the same partition as Core's
    `DamageAreaGeometry` (ported once, asserted equal by a test that
    samples the grid against Core's answers rendered into a data
    attribute); numbering follows list order; remove re-numbers; the
    module recomputes the hidden JSON on every change; read mode (no
    `data-damage-editable`) binds nothing.
- Labels: `Damage.RecordedAreas` (G7), `Damage.AreaUnderPointer`
  readout text, the eight area words (G5) — all in
  `CaseWorkspaceLabels.Damage`; the legend words are unchanged.
- Tests: `CaseDamageAndViewerWebTests` — the page renders recorded marks
  in read mode without script; a POST with marks JSON saves and the
  derived cells show Core's derivation; legacy impacts render as pins;
  an off-vehicle point is refused with the page's validation notice.
- Conformance: shots 66–71 states (edit with marks, read with marks,
  empty) at three widths.

## 2. Brand mark (WP2; H)

Files: `wwwroot/images/marks/pegasus-lockup.png` and its `README.md`,
`Pages/Shared/_Layout.cshtml:84`.

- Replace the PNG with the refined mark cropped to bounds at 256 px (the
  128 px rendition is not needed once the browser scales one asset; the
  slot is 52 px and 36 px). Keep the file name so `asp-append-version`
  busts the cache and no markup changes; update the marks README row.
- If the operator also wants the favicon and the sign-in page to follow,
  that is a separate ask (H covers the rail slot only).
- Tests: none (asset). Conformance: shots 72–73.

## 3. Composed sentences (WP3; `composed`, J9)

Files: `_CaseOverview.cshtml`, `_CaseInspectionAddress.cshtml` /
`_CaseOverview.cshtml` (inspection band), `_CaseVehicle.cshtml`,
`_CaseValuation.cshtml`, `_CaseEstimate.cshtml` (VAT bar), `_CaseDamage.cshtml`
(narrative cell), a new view helper `Presentation/NarrativeCells.cs`
wrapping Core's `AssessmentNarrative`, `case-workspace.js`.

- Each cell is `<div class="fc ro"><span class="lbl">{label}</span><div
  class="fv derived" data-narrative="{key}">{sentence}</div></div>` in
  the live derived style (`.fv.derived`, `.empty` when nothing is
  recorded) — exactly as the Damage narrative cell is drawn today
  (`_CaseDamage.cshtml:290`).
- Placement: Matter on Overview under the claimant band (shot 75);
  Assessment method and Recovery and storage charges on Inspection (76);
  Engineer's comments under Mileage source and Pre-incident condition
  under Condition on Vehicle (77); What the report carries under the
  applied calculation on Valuation (78); Drives the calculation above the
  VAT categories on Estimate (79).
- Live tracking while editing: a small script module
  `[data-narrative]` recomputes each sentence from its source inputs on
  `input`/`change`, using the same templates Core uses, passed as
  `data-narrative-template` strings with `{placeholders}` so no sentence
  is written twice (the vocabulary words for mileage source and condition
  come from the existing select options). On save the server re-renders
  from Core, which is the authority.
- Labels: the six cell labels in `CaseWorkspaceLabels`; the sentences
  themselves are Core's (BACKEND § 3).
- Tests: `CaseDetailsWebTests` (the split Details class): each cell shows
  Core's sentence for a seeded Case; `empty` state when the source field
  is absent.
- Conformance: shots 75–79 at three widths.

## 4. CAP guide card (WP4; `cap`)

Files: `_CaseValuation.cshtml`, `_CaseValuationLines.cshtml`,
`Details.Valuation.cs` (`OnPostSaveValuationAsync`,
`OnPostGetValuationAsync` — source switch), `CaseWorkspaceLabels.Valuation`.

- A fourth card in the guide-card row, identical in shape to Glass's /
  Brego / Super CAP (month, mileage, retail, trade, Get valuation, Save),
  wired to `ValuationSource.Cap`; the recorded-cards list and the basis
  select accept it. The no-provider notice is the existing one.
- Labels: `Valuation.SourceCap = "CAP"`; CONTEXT.md entry per J11.
- Tests: `CaseValuationV26WebTests` — card renders, Save records a Cap
  row, Get valuation shows the notice.
- Conformance: shot 78.

## 5. Settlement, Files, Notes and frame presentation (WP5)

### 5.1 Salvage slider (`salvage`)

Files: `_CaseSettlement.cshtml`, `case-workspace.js`, `case-workspace.css`.

- Under the Salvage value `.fi` while editing (and only when the Engineer's
  Value is recorded): `<input type="range" min=0 max=100 step=1>`, five
  snap buttons 5/10/15/20/25 %, a readout "= {pct} % of {EV}". Script
  binds the range and the £ input both ways (last touch wins, whole
  pounds); the £ input is the posted field. Read mode: nothing new.
- Labels: `Settlement.SalvagePercentOf` readout template.
- Tests: `CaseDetailsWebTests` (Settlement) — the slider renders only
  with an Engineer's Value; the posted salvage value is what is saved.
- Conformance: shot 82.

### 5.2 Reason bank (`bank`)

Files: `_CaseSettlement.cshtml`, `Details.cshtml.cs` (new handler
`OnPostSaveReasonPhraseAsync`), `case-workspace.js`, a new Administration
page `Pages/Administration/ReasonPhrases/Index.cshtml(.cs)` beside
`ValuationPresets`, `CaseWorkspaceLabels.Settlement`, `OperatorLabels`.

- Under the Unroadworthy reason textarea while editing and Unroadworthy:
  the enabled phrases as chip buttons; a click inserts at the caret,
  joining with " and " when the textarea already has text (script; the
  no-script fallback is the phrase list as plain text). **Save this
  wording to the bank** posts the textarea text to the new handler (a
  form inside the section, lease-bound), which returns to the section
  with a notice "Saved to the bank".
- Administration page: list, add, disable, reorder — the Valuation presets
  page's shape and labels pattern.
- Tests: `CaseDetailsWebTests` (Settlement) and a new
  `ReasonPhraseAdministrationWebTests` (per the Valuation presets tests).
- Conformance: shot 82.

### 5.3 Decision tick rows (`ticks`, J21)

Files: `_CaseSettlement.cshtml`, `case-workspace.js`, `case-workspace.css`
(`.dec` grid).

- In the Decisions strip while editing, Outcome, Salvage category and
  Roadworthiness render their `<select>` as now plus a `[data-tick-row]`
  of `<button type="button" data-tick="{code}">` per option; script marks
  the pressed one and writes the select; without script the select alone
  is used (the buttons are `hidden` until bound). The Proposed column and
  Accept / Accept all are untouched (J21 decides layout with a proposal).
- Tests: the buttons drive the select (a script-driven test in the
  integration suite is not possible; test that the select is still the
  posted control and the row renders per option).
- Conformance: shot 92.

### 5.4 Click to include (`include`, J19)

Files: `_CaseImages.cshtml`, `Details.Files.cs` (existing role-change
handler), `case-workspace.js` (`.image-tile` module), `case-workspace.css`.

- While editing on the Images tab each `.image-tile` shows an in-report
  tick chip; for Supporting / Not used tiles a click posts the role toggle
  (a form per tile; script intercepts to a fetch and swaps the chip and
  the Report section's count `[data-report-image-count]`), and the viewer
  opens from a separate "Open" control on the tile. Close-up and Overview
  tiles keep their role chip and open the viewer on click (J19).
- Tests: `CaseDetailsWebTests` (Custody/AssetPreparation): a POST toggles
  Supporting ↔ Not used; the count follows.
- Conformance: shot 85.

### 5.5 Queries panel (`queries`, J20)

Files: `_CaseHistory.cshtml`, `CaseWorkspaceLabels`.

- A `.subpanel` "Queries" under the timeline with the live empty-state
  style (`.empty-state`) and, if J20 lists correspondence, rows in the
  Correspondence table's shape. The empty-state sentence is approved copy
  or none.
- Conformance: shot 86.

### 5.6 Reference placements (`place`)

Files: `_CaseOverview.cshtml`, `_CaseReport.cshtml`, `_CaseVehicle.cshtml`,
`_CaseDamage.cshtml`, `_CaseValuation.cshtml`; `Details.cshtml.cs`
`OnPostSaveAsync` (the `signOffEngineerId` and `assessmentFields` bindings
already accept the fields from any section's form).

- Overview: an "Assigned engineer" read-only cell and the Sign-off
  Engineer select (moved from Report) side by side in the Case column;
  the select posts through the page save as it does today.
- Vehicle: unrelated damage + deduction cells (moved from Damage) with
  "Include unrelated damage" beside them (moved from Report).
- Valuation: "Disclose guide source" and "Include valuation commentary" +
  the 4,000-character commentary textarea (moved from Report).
- Report: loses those cells; the readiness list still names them by
  section.
- Tests: `CaseDetailsWebTests` — each moved control posts and saves from
  its new section; the readiness list links to the new section keys.
- Conformance: shots 75, 77, 78.

### 5.7 Ribbon badges and nine sections (`badges`, `nine`; I1, J24)

Only if I1 changes the rules.

- `badges`: `Details.cshtml` ribbon gains three chips (`.chip` with the
  aside's colour rules: `--red` total loss, amber unroadworthy, and the
  ratio chip green < 66 %, amber < 80 %, red ≥ 80 %) computed in
  `Details.Frame.cs` from the same figures the aside shows; the aside
  drops its chips and keeps the three figures. The ribbon's facts must
  yield (drop the principal fact or shorten the claimant) — the truncation
  seen at 1580 px is the reason I1 recommends rejection.
- `nine`: the section list in `Details.Frame.cs` (keys, labels, order),
  `Details.cshtml` (section row links, `?section=` handling,
  `OnGetSectionAsync`), Damage and Valuation rendered as `.subpanel`s
  inside Vehicle (losing `_CaseSectionHeadTools` and the availability
  sentence — or keeping them as sub-heads, a design choice the mockup did
  not make), Files renamed Images with the Images tab first, Settlement
  labelled Decisions, and a Claim section carved from Overview. The
  `/Cases/{id}/Assessment` redirect (D30) must target the new key.
- Conformance: shots 75, 87–89.

## 6. Estimate (WP6)

Files: `_CaseEstimate.cshtml` (head at ~line 227, grid at ~573, dialogs at
~728), `Details.cshtml.cs` (`OnPostSaveEstimateAsync`, `OnPostEditLineAsync`),
`case-workspace.js` (estimate modules), `case-workspace.css`,
`CaseWorkspaceLabels.Estimate / EstimateTotals / EstimateVat`.

### 6.1 Delete all + Undo (`estdel`)

- A **Delete all lines** entry in the Draft's More menu (not a new primary)
  opening a `_ReasonDialog`-style confirm (no reason); confirm posts the
  Draft save with no lines. After a single-line remove (`removeLine`
  submit), the returned page carries a `[data-undo]` toast with an
  **Undo** form that posts the line back (hidden fields with the removed
  line's values and origin; BACKEND § 7); script hides the toast after six
  seconds, the no-script page keeps it until the next action.
- Tests: remove then undo restores the line at its position with its
  Source chip unchanged.

### 6.2 Off-pattern cells (`offpattern`)

- The grid marks a cell `<td class="is-off-pattern" title="{reason}">`
  when `EstimateTotals.OffPattern` names its position and field; amber
  background from a new rule in `case-workspace.css`; the value is never
  hidden. Read and edit both.
- Tests: a seeded anomaly renders the class and title.

### 6.3 Regional uplift (`uplift`)

- In the estimate header beside the rate card select: a switch **Regional
  uplift +{pct} %** (posted with the Draft save as
  `RegionalUplift`), its chip reading "Suggested · {source} ({outward})"
  from Core's `UpliftSuggestion` or the plain region name; the rate cell
  shows the uplifted hourly rate. Read mode shows "Regional uplift · +15 %"
  as a value when on.
- Tests: `CaseEstimateHeaderWebTests` — the switch posts; the totals strip
  shows the uplifted rate; the suggestion appears for a London repairer
  postcode and not for a Liverpool one.

### 6.4 Provenance chips (`prov`)

- `LineSource` in `_CaseEstimate.cshtml:59` returns "imported · AX" /
  "imported · GL" from the version's `RepairSpecificationSource.Route`
  (`AudatexPdf` → AX, `Glasses` → GL, `Json` → JSON) when the line is
  imported and unamended; labels in `EstimateLabels`.
- Tests: an Audatex-imported estimate shows the AX chip.

### 6.5 Compare with diff and print (`compare`)

- The `compare-estimates-dialog` keeps its totals table and gains two
  selects **From** / **To** (GET form; `?dialog=compare-estimates&from=
  {id}&to={id}` so the state survives a reload and the no-script path
  works); when both are chosen and differ, `Details.cshtml.cs` computes
  `EstimateComparison` and the dialog renders the summary line, the
  side-by-side table (`.cmp-added`, `.cmp-changed` with `<strong>` on
  changed cells, `.cmp-removed`) and a **Print comparison sheet** button
  (`window.print()`); a `@media print` block in `case-workspace.css`
  prints the dialog alone (the mockup's rule, ported).
- Tests: the dialog with two versions chosen renders the added / changed /
  removed rows Core computed; unchosen renders only the totals table.
- Conformance: shots 81, 81b.

### 6.6 Supplementary (`supp`)

- A `.subpanel` under the estimate tabs: the header line "Supplementary —
  changes vs" with a select of the Case's other versions (the Sent one
  first, labelled "· Sent on report"); nothing else renders until a
  version is chosen (server-side, via `?supp={id}` or the saved baseline
  field). Then: the diff list (§ 6.5's table in compact form), the £ delta
  line, the **Explain the change on the report** switch and the reason
  select (posted through the Draft/section save as
  `report.supplementary_*` assessment fields), and the composed
  paragraph as a derived cell.
- Tests: unchosen shows the header only; chosen shows the diff; explain
  without a reason is refused with the page's validation notice.
- Conformance: shots 80, 93, 94.

## 7. Report and delivery (WP7)

Files: `_CaseReport.cshtml`, `Details.Report.cs`
(`OnPostPrepareReportDeliveryAsync`, `OnPostSendPreparedReportAsync`,
`OnPostGenerateReportAsync`), `case-workspace.js`, `case-workspace.css`,
`CaseWorkspaceLabels.ReportDelivery / Report`.

### 7.1 Address book (`abook`)

- **To**: the existing input gains a `<datalist>` (no-script) and a
  script dropdown listing the suggestions Core returns, grouped Principal
  / This case / CE, each row name + address. **Cc**: chips
  (`.chip.is-removable`) over a hidden comma-joined input the handler
  already reads; a typed entry adds on Enter; one-click buttons for the
  Principal handler and the claim source add a chip. The prepared block
  lists the frozen To / Cc as now.
- Tests: `CaseReportApprovalWebTests` — suggestions render; a Cc chosen
  from the book is frozen into the preparation; the fingerprint stale
  path still works.
- Conformance: shot 83.

### 7.2 Attachments (`attach`)

- An **Attach** row of switches on the delivery form: Report (checked,
  disabled), Fee note, Breakdown, Images — each enabled only when that
  artifact is confirmed on the current generation; posted as
  `attachmentKinds`. The prepared block lists what was frozen.
- Tests: the row reflects artifact availability; the preparation freezes
  the chosen kinds.

### 7.3 Re-send naming and message (`resend`)

- Read-only cells **File name** and **Message** on the delivery form,
  values from Core (BACKEND § 15); after a Sent generation exists the
  message shows the supersedes variant. No input.
- Tests: the cells show Core's values before and after a send.

### 7.4 Report / Fee tabs (`feetab`)

- The Report section gets a `.tabs` row (the Files section's tab pattern,
  both panes rendered for no-script) with Report and Fee; Fee holds the
  fee note preview and, per J16, the source line. The head tools stay on
  the section head.
- Conformance: shot 84.

### 7.5 Sign-off follows Engineer, report date on generate (`signoff`, `reportdate`)

- No page change: the Sign-off cell and the Report date cell show what
  Core recorded after the hand-off or generation. Tests belong to the
  lifecycle and generation tests (BACKEND § 17–18), plus one page test
  each that the cell reads the new value after the action.

### 7.6 Report wording (`wording`; I2)

Files: `_CaseReport.cshtml` (new `.subpanel` "Report wording"),
`Details.Report.cs` (new handlers `OnPostSaveReportWordingAsync` for the
block set; `OnPostRecomposeWordingBlockAsync`), `case-workspace.js` (a
`[data-wording]` module), `case-workspace.css` (`.wb` block styles from
the mockup's `.wb` ported), labels.

- Read mode: one derived cell per block in order, titled.
- Edit mode: a list of blocks, each: drag grip (`draggable`, HTML5 DnD;
  the no-script fallback is Up / Down buttons), the title as a text input
  (renamed on change), the chip (composed · tracks fields / edited · no
  longer tracking / pass-through / manual), the text as a textarea whose
  first edit flips the chip and shows **Recompose from fields** (a
  confirm, then a post that returns the composed text), and × remove;
  removed blocks reappear as **+ {title}** buttons at the end; **New
  paragraph** adds a manual block. The whole set posts as an ordered
  array with the section save; the mandatory blocks (`nature`,
  `settlement`) render no ×.
- Removing PAV commentary or Unrelated damage unticks its content switch
  (the switch is the same fact).
- Tests: integration tests for save order, rename, remove and reappear,
  recompose, new paragraph, mandatory blocks; a rendering test that the
  PDF prints blocks in the saved order.
- Conformance: shots 90, 91.

## 8. Shared script and style work

- `case-workspace.js`: new modules are IIFEs bound on
  `pegasus:section-replaced` (the existing section-refresh event) as the
  damage module is; no global state beyond the module; no framework.
- `case-workspace.css`: new rules use the existing tokens (`--control`,
  `--line`, `--surface-2`, `--navy-bg`, the severity colours); the
  mockup's `.dm-*`, `.salslide`, `.bank`, `.viol`, `.supp`, `.cmp-diff`,
  `.ab`/`.ccchip`, `.wb`, `.tickrow` blocks are the drawing to port,
  renamed to the live naming (`.damage-*`, `.dec-*`, `.cmp-*`, `.wording-*`).
- Labels: every new string is added to `CaseWorkspaceLabels` or
  `OperatorLabels` once and the coverage audit
  (`v27_planning/current/v27-build/audit.py`) is re-run against the
  routed page's HTML at the end of each package so nothing the mockup
  showed is missing from the product.
