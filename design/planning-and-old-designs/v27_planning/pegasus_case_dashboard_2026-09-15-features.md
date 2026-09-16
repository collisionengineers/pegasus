# Feature inventory — `pegasus_case_dashboard_2026-09-15.html`

Read 16 Sep 2026. The file is a single self-contained HTML page (2,705 lines,
168 KB) with inline CSS and vanilla JavaScript, no external dependencies,
titled "Pegasus — Case MA59BDY · QDOS26214 (v25 · 9 Sep 2026)". It is a
one-page Case record mockup with nine numbered sections, a sticky header and
a demo strip. This inventory describes what the file does as built; it is a
reference for the v27 round, not a requirement.

## Page frame

**Sticky header** (`.top`)

- Identity line: registration plate `MA59BDY`, vehicle `MINI First 1.4 · 2009`,
  refs `QDOS26214 · yr Q-88214-LC`.
- Three live badges: outcome (`TOTAL LOSS — CAT N` or the outcome name; dark
  for TL, green otherwise), legal status (`UNROADWORTHY` red / `ROADWORTHY`
  green), and a repairs-to-PAV ratio badge (`REPAIRS 62% OF PAV · £1,802 /
  £2,900`) hidden until there is a grid total, grey when no PAV, green <66%,
  amber 66–79%, solid red ≥80%.
- "Assigned **A Patterson**" follows the assigned-engineer select.
- Lock chip: `EDITING · YOU · SINCE 14:02` (green) or `VIEW ONLY · <NAME>
  EDITING` (blue, amber when stale).
- Jump-tab nav with nine anchors (`1 Case details` … `9 Notes & queries`); an
  `IntersectionObserver` highlights the tab for the section in view.
- Global stale banner: "Report preview is out of date… Regenerate before
  sending."
- Lock banner (blue, amber when stale) with **Ask to release** and **Take
  over**.
- Demo strip: "Demo · viewing as" select (A Patterson / E Mawdsley / N
  O'Reilly) and a "simulate holder idle 20 min" link.

**Whole-case lock** (one editor at a time)

- `S.lock = {holder, since, lastActive}`; stale after 15 min idle. Any `input`
  event refreshes the holder's heartbeat.
- If the viewer is not the holder, `body.viewonly` disables pointer events on
  the frame, greys inputs and dims sections.
- **Ask to release** logs a note and toasts "Request sent to…".
- **Take over** opens a modal whose text differs for stale vs live holder
  ("switch their screen to view-only immediately — they'll see that it was
  you"), notes that "unsaved typing is already saved field-by-field", and on
  confirm swaps the holder, logs to the timeline and toasts.

**Modals and toasts**: take-over, delete-all-lines confirmation, Versions
(wide), Compare (wide, with a print stylesheet that prints only the
comparison sheet), and a bottom toast with an optional inline **Undo**.

## 1 Case details

- Four-column field grid. **Lockable fields** (Principal, Our ref, Case
  status, Instructed) are disabled with a padlock button; click toggles the
  lock/unlock glyph and focuses the input; on re-lock it logs
  `"<Field> amended by <engineer> — "old" → "new"."` if changed. Enter
  re-locks.
- Plain inputs: Your ref; Product type select (Standard / Commercial (C.) /
  Audit (A.) / Audit — TL (AP.) / Diminution (D.)); Assigned engineer select
  (nine names) — changing it updates the header and, if the name is in the
  three-person `SIGNOFF` list, auto-sets Sign off engineer; Sign off engineer
  select.
- **Principal** subcard: read-only "Generic notes" (chip `PRINCIPAL RECORD —
  EVERY CASE`) and editable case-specific notes.
- **Claim source** subcard: name / contact number / email, read-only generic
  notes (`SOURCE RECORD — EVERY CASE`) and case-specific notes.

## 2 Claim details

- Claimant name, address, VAT status select (drives the VAT value-increase
  block in §4), accident date, Report date (lockable, chip `AUTO ON GENERATE`
  — set by Generate preview if empty), Accident circumstances textarea (chip
  `NON-BLOCKING · MAY ARRIVE LATE`), Notes from client.
- Composed matter line: "Road Traffic Accident: Ms L Carter: 14 July 2026"
  (static text in this build).

## 3 Inspection details

- Inspection type (Image based / Physical), date, **Inspection location**
  select (Image based assessment / Claimant address / Repairer address /
  Storage address / Other…); "Other" reveals a free-text field.
- Composed assessment method: `Vehicle located at: <resolved address>.`
  recomputed from whichever address source is chosen.
- **Repairer** subcard: name, address, VAT status. A composed "Drives the
  calculation" line explains the VAT default ("VAT registered — VAT defaults
  to all categories." / "NOT VAT registered — VAT defaults to parts &
  materials only." plus a note when overridden).
- **Storage** subcard: name, address, daily rate, recovery charge. Composed
  sentence appears only when a charge is entered, with three variants
  (recovery + storage / recovery only / storage only) and a faint "No charges
  entered — no line appears on the report." otherwise.

## 4 Vehicle details

- Read-only DVLA/LOOKUP-chipped facts (make/model, first registered,
  engine/fuel, colour, transmission, body style, CO₂/Euro, MOT/tax), editable
  VIN and Vehicle type select (Car … Other).
- **Mileage & condition**: odometer with `mi`/`km` unit toggle that converts
  the figure in place; "Average mileage — 7,100/yr from first registration"
  tick that computes years since Sep 2009 × 7,100, disables the odometer,
  forces Mileage source to "Average" and shows a warning ("not appropriate for
  imported vehicles or taxis"); Mileage source select (six values);
  Pre-incident condition select (five). Two composed sentences (engineer's
  comments on mileage source; condition sentence).
- **Impact**: an SVG **damage zone picker** built at runtime — 14 outside
  zones (front/rear corners and centre, N/S and O/S wings, doors, quarters),
  5 on-car zones (bonnet, windscreen, roof, rear screen, boot/tailgate), 4
  wheels. Multi-select, yellow when on; chips list the selected zone names.
  `zonesToLoc()` derives the region set: one region sets the Impact location
  select; more than one disables the select and the composed "nature of
  incident" sentence becomes a bulleted list of regions. Impact severity
  select (five). Composed: "The vehicle has suffered moderate collision/impact
  damage to the rear."
- **History / provenance check**: "Run check" button (alerts that it would
  call Experian AutoCheck) and a verbatim pass-through composed line.
- **Valuation**: six guide cards — CAP, Glass's, Cazana, Brego (fixed
  retail/trade), Super CAP and Market research (manual inputs; Market research
  has no trade). Click a card to select it (typing in an input does not
  select). Selection feeds `S.guide`.
  - **Value increases** panel: tick list — Tow bar £300, PCO plated £1,500,
    Decals £500, VAT (on commercial) +20%, Camper conversion, Driving tuition
    £500, two "Other…" free-label rows with amounts. The VAT row is blocked and
    force-unticked when the claimant is VAT registered (chip `CLAIMANT VAT
    REG`).
  - **Adjustments bar**: Condition deduction £, Previous total loss tick with a
    −10%/−20% segment (inert until ticked), a live working line
    (`£3,100 + 20% VAT (£620) − 10% (prev. TL, £372) + Tow bar £300 − £200
    (condition) = £3,448`), and **Apply to engineer's value**. `adjCalc()`
    order: VAT on guide value → previous-TL % on the VAT-inclusive figure →
    fixed extras → condition deduction. Apply sets PAV and logs the arithmetic.
  - **Engineer's value (PAV)** input, chip `ONE FIELD, TWO PLACES — MIRRORS §7
    DECISIONS`.
  - Ticks: "Disclose guide source on report" (composed line switches between
    the guide name and "Source not disclosed"); "Include valuation commentary
    on report" (shows a composed multi-line commentary block and adds the PAV
    commentary paragraph to §7 wording).
  - Composed "what the report carries": `CAP — Retail £3,100 · Trade £2,320 ·
    Engineer's value £2,900.`
- **Unrelated damage**: description input and "Print on report" tick (`OFF BY
  DEFAULT`), which adds or removes a wording block in §7.

## 5 Images

- Twelve coloured placeholder thumbnails; click toggles include/exclude (tick
  badge, dimmed when off); counter "9 of 12 in report". Upload images and
  Crop / rotate buttons are inert. Note: "6 per page, fixed grid".
  Drag-to-reorder is labelled "(mock)" and not implemented.

## 6 Repair specification

- **Drop zone** for estimates (Audatex PDF/XML, Glass's PDF) with two demo
  links: "supplementary 88214-02" and "different Glass's estimate".
  `mockImport()` shows a pending-import panel ("Parsed as … — N lines… This
  will overwrite the current N-line grid (frozen as vN)", warning if manual
  edits exist) with **Overwrite grid** / Cancel.
- Toolbar: **Add row**, **Versions (n)**, **Compare…**, and an import log line
  ("Populated 28 Aug 14:22 from Audatex est. 88214-01 (PDF) — 15 lines ·
  labour rate £83.28/hr").
- **Grid**: columns Type (select: New / Repair / R&R / Paint / Blend /
  Specialist), Description, Qty, Unit £, Hours, Material £, Provenance chip
  (`IMPORTED · AX`, `IMPORTED · GL`, `AMENDED`, `MANUAL`, `SCALED`), and a
  per-row × delete. A `COLS` map declares which numeric columns apply per
  type; unavailable cells render `—`, but an imported off-pattern value
  renders as an amber `viol` input with a tooltip rather than being discarded.
  Editing an imported row flips it to `amended`; changing type to a
  qty-bearing type seeds qty 1. Row delete logs a note and offers a 6-second
  **Undo** toast. Empty grid shows "No repair lines. Drop an estimate above or
  add a row."
- **Delete all** header button → confirmation modal → clears the grid, freezes
  the outgoing grid as a version, logs.
- **Labour rate bar**: segmented Standard (ABP 2026 £83.28) /
  Prestige-aluminium (£103.06) / Custom; "+15% regional uplift" tick (disabled
  on Custom) with a working `£83.28 × 1.15 =`; typed rate box (typing switches
  to Custom, `MANUAL` chip). Rate changes log.
  - **Regional uplift suggestion**: a postcode outward-code parser scans
    repairer, claimant, storage and "other" inspection addresses against a
    London & Home Counties set (full areas plus district lists for
    SG/OX/RG/CM); when any is in scope the chip becomes `SUGGESTED` (clickable
    to tick the uplift) with a tooltip listing the hits.
- **Target % of PAV scaling bar**: range slider and % box bounded between the
  floor-scaled minimum and the as-estimated maximum; floors for labour £/hr
  (50) and prices % (65). `scaleToAmount()` bisects a multiplier k so that
  gross(scaled rows, scaled rate) hits the target — unit prices and materials
  scale (floored), the labour rate scales to the nearest 50p (floored), hours
  never change. While previewing, the grid body goes amber and read-only
  (`body.scaling`), a `PREVIEW` chip and **Apply** appear; Apply freezes a
  version, logs the full arithmetic and sets origin "Scaled to N% of PAV".
  `SCALED` chip and **✕ Remove scaling** restore the pre-scale estimate (also
  freezing a version).
- **Contract repair bar**: "Contract repair agreed" tick (mirrors the §7
  Contract repair outcome both ways, remembering the previous outcome), agreed
  total sum box (seeded from the gross; typing it rescales the spec to that
  sum), a clickable mismatch chip (`SPEC £X ABOVE/BELOW AGREED — RESCALE`),
  and a composed Contract Repair sentence.
- **Discounts %**: Parts, Materials, Specialist, Overall.
- **VAT applies to**: Labour / Parts / Materials / Specialist ticks, defaulted
  from the repairer's VAT status; any manual change shows `MANUAL OVERRIDE`
  and a **Reset to repairer status** button.
- **Roll-up**: Parts, Panel labour (h × rate), Paint labour (flagged "single
  rate — open item"), Materials, Specialist operations (hours listed, never
  costed), Off-pattern items (treated as specialist), each discount line when
  non-zero, VAT with the category list, **Repair cost inc VAT**.
- Three derived **worklists**: Main new parts required / Repairs required /
  Additional operations.
- **Versions store**: frozen snapshots `{n, when, who, origin, rows, rate,
  sent}`. Every import, scale, clear, restore and send freezes the outgoing
  grid (deduplicated if unchanged). The Versions modal lists the working grid
  plus each version with lines, rate, total inc VAT, `SENT ON REPORT` tag,
  **Compare with current** and **Restore** (restore itself freezes a version
  and lands the rate as Custom).
- **Compare modal**: From/To selects (defaulting to last-sent → working), a
  summary line with £ delta and added/changed/removed counts, and a
  side-by-side table colour-coded red added / orange changed / green removed
  with per-cell diff bolding, printable via **Print comparison sheet**.
- **Supplementary panel**: after an import, diffs the new grid against the
  last-sent version (or the grid just replaced), lists added/changed/removed
  lines with £ delta, offers "Explain the change on the report", a reason
  select (supplementary estimate / dismantling / further inspection / further
  images), and composes a paragraph ("Following receipt of a supplementary
  estimate the following additional items are now required: …; The repair
  time for … has been revised; … is no longer required. The estimated repair
  cost has increased from £A to £B."). **Dismiss** hides it.

## 7 Engineer's decisions

- A "parked" notice: Brian AI pre-assessment deferred; decisions entered
  directly.
- Tick rows: **Outcome** (Repairable / Total loss / Cash in lieu / Contract
  repair — mirrors §6 contract repair), **Engineer's PAV** (mirrors §4),
  **Salvage category** (A/B/S/N) and **Salvage value** (£ box, a 0–100% slider
  of PAV, snap buttons 5/10/15/20/25%, readout "= 25% of £2,900"); when the
  outcome is not Total loss the salvage rows collapse to an "N/A" line and the
  change is logged. **Roadworthy status** (Roadworthy / Unroadworthy).
- **Unroadworthy reason** card (only when Unroadworthy): a firm-level phrase
  **bank** (seven starters) that inserts with " and " joining, a textarea,
  "save this wording to the bank" (adds, logs, shows "saved — firm-level,
  everyone sees it"), composed "Please note the vehicle is unroadworthy due to
  …".
- **Report wording well** — every narrative block in print order: Nature of
  incident, Engineer's comments (mileage + unroadworthy), Supplementary damage
  (auto, from §6), PAV commentary (optional, tied to the §4 tick), Unrelated
  damage (optional, tied to its tick), Vehicle history check (pass-through),
  Pre-incident condition, Settlement, Salvage (only for Total loss). Each
  block has a drag grip (HTML5 drag-and-drop reorders; drop on the add row
  sends it to the end; the order is the print order), an editable heading
  (contenteditable, rename logged), a chip (`COMPOSED — TRACKS FIELDS LIVE` /
  `EDITED — NO LONGER TRACKING FIELDS` / `PASS-THROUGH` / `MANUAL — FREE
  TEXT`), a "recompose from fields" link when edited (confirm dialog), and ×
  remove (heading will not print; removed blocks reappear as "+ <title>" add
  buttons). **+ New paragraph** adds a custom free-text block. Every action
  logs a note.
  - Settlement text is outcome-driven: TL (PAV − salvage), Cash in lieu,
    Contract repair (agreed sum), Repairable with a reserve rounded up to the
    next £50. Salvage paragraphs per category A/B/S/N.
- **Settlement strip** (dark): TL shows PAV − Salvage = Recommended settlement
  plus repair cost (with "exceeds PAV"); otherwise PAV, labour hours and the
  outcome-specific figure.
- **Mandatory decisions gate**: counts missing outcome / PAV / salvage
  category and value (TL only) / roadworthy status / unroadworthy reason;
  states that Generate preview stays locked until complete and that there is
  no separate approval act.

## 8 Produce & send report

- **Report / Fee tabs**.
- Preview card with a thumbnail, a title that follows the outcome ("Total
  Loss Report — MA59BDY"), a status line, and **Generate preview** (refuses
  with the missing-decision list; otherwise stamps the report date if blank,
  unlocks send and clears the stale banner).
- **Delivery**: Channel ticks Email / WhatsApp (WhatsApp shows a `CLOUD API —
  NOT YET LIVE` chip); **To** input with a searchable address-book dropdown
  (six entries tagged Principal / This case / CE), defaulting from the
  principal record; **CC** chips with the same dropdown, Enter-to-add typed
  addresses, and one-click suggestion buttons for entries flagged `sugg`;
  **Attach** ticks Report / Fee note / Breakdown / Images with tooltips;
  filename readout (`QDOS26214 MA59BDY Total Loss Report.pdf`, gaining an
  extra `.` per re-send as a version tell); composed email body that switches
  to "updated … supersedes our report dated …" on re-send.
- **Attach and Send** (primary, disabled until previewed and at least one
  document ticked; delivery changes never stale the preview). Send logs,
  alerts, marks the case Issued, increments the send counter and freezes the
  grid as a sent version.
- **Stale logic** (`touch()`): any change after preview locks send and shows
  the stale banner; any change after send implicitly reopens the case (Issued
  → In progress), logs it, and the next send takes the next filename dot and
  the supersedes body.
- **Fee** pane: a static fee table (£132.00 + VAT = £158.40) from the QDOS
  principal fee table, with an open question on additional charges.

## 9 Notes & queries

- Timeline: `logNote()` prepends a timestamped **System** entry for every
  logged act (locks, takeovers, grid changes, versions, scaling, decisions,
  wording edits, sends, reopen); three seeded entries. "Add note" input is
  inert. The Queries subcard is an empty state describing post-report items
  (PAV disputes, TP cost challenges, Part 35, supplementaries).

## Cross-cutting mechanics

- `compose()` recomputes every composed sentence; `recalc()` recomputes the
  roll-up, worklists, VAT line, settlement, header ratio, contract-repair
  state, supplementary panel and wording well (several functions are
  monkey-patched after definition to chain these). `touch()` is called on
  every mutation and drives the stale/reopen states. A frame-level `change`
  listener calls `touch()` for any input not otherwise wired.
- All fixtures are synthetic (case, engineers, address book, estimates,
  versions). Nothing persists; a reload resets. A responsive breakpoint at
  900px collapses grids to one column and thumbnails to three across.

## Notes for the v27 round

- The file's title still says v25 · 9 Sep 2026.
- Several ideas in it differ from settled Pegasus rules and the v26 decisions
  (whole-case lock with Take over versus the current per-edit lease; guide
  providers and Cazana; implicit reopen on edit after send; WhatsApp send;
  Brian pre-assessment). They are described here as built, not adopted.
