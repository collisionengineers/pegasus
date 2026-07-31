# Collision Engineers — Assessment Report Template (Design I, locked July 2026)

Approved by Andrew, 21/07/2026, after review of drafts A–I against the EVA original (PK12 TMZ)
and the current fee note (126448.001-1).

## Layout decisions (agreed)

- **Base style**: matches the current fee note — logo top-left, company block top-right,
  centred red title over a full-width 1.5pt red rule, red header-row tables, grey label cells.
- **Title**: *TOTAL LOSS REPORT* — red, uppercase, letterspaced, **italic**.
- **Page 1 key information**: status badges ("TOTAL LOSS — CATEGORY S" charcoal,
  "UNROADWORTHY" red) followed by four figure tiles — PAV, repair cost inc VAT, salvage,
  and settlement (red tile). A reader gets the outcome without reading a paragraph.
- **Page 1 vehicle details**: identity only (make, model, reg, VIN, odometer, engine/fuel,
  condition, impact) — grey-label grid. Conclusions and identity are kept separate.
- **No photo captions** — anywhere. Captions add work and risk being wrong.
- **Photo section heading**: "Vehicle Images" (not "Inspection Photographs").
- **Vehicle Data (page 3)**: includes **Year**; empty rows (e.g. "Extras: —") are omitted.
- **VAT number**: appears **only on the fee note page** (header and footer), not on report pages.
- **Footer, every page**: `<REG> · <our ref> | Collision Engineers Ltd | <site>` + "Page n of N"
  bottom-right — every page of a court bundle is traceable.
- **Fee note page**: mirrors the standalone fee note — description/amount table with
  Subtotal/VAT/TOTAL DUE rows, Payment Details grid (Lloyds, 30-12-80, 50858868),
  Terms, "Thank you for your business."
- **Colours**: doc red #C80A32, charcoal #2C2A27, label grey #F2F2F2, total-row grey #EFEFEF,
  grid grey #BEBEBE. Body face: Arial/Liberation Sans.

## Reliability rules (enforced by ce_report_generator.py)

1. **Computed-once figures.** The job JSON carries only raw components. The generator derives:
   labour = hours × rate; subtotal = labour + parts + paint + specialist; VAT = 20%;
   repair total = subtotal + VAT; settlement = engineer value − salvage; fee VAT/total.
   Decimal arithmetic, ROUND_HALF_UP. Every figure on every page renders from one variable —
   pages cannot disagree, and the settlement narrative sentence is generated, not typed.
2. **Validation before render**: required fields, 17-char VIN, outcome/legal/category enums,
   photos exist on disk, engineer signature key known. Invalid jobs fail loudly — nothing renders.
3. **Fixed slots**: photos in a uniform 48mm-crop grid, 6 per page, auto-continuation;
   work lists flow as tables with repeated headers; long strings wrap.
4. **States**: outcome = total_loss | repairable | cash_in_lieu; legal_status = roadworthy | unroadworthy;
   category = A | B | S | N (required for total loss only).

## The three assessment report outcomes (agreed 21/07/2026)

| | Title | Badge | Red tile | Settlement section |
|---|---|---|---|---|
| total_loss | Total Loss Report | TOTAL LOSS — CATEGORY x | Recommended Settlement (PAV − salvage) | "equitable settlement…" + value box, then Salvage section |
| repairable | Repairable Report | REPAIRABLE | Repair Cost inc VAT | "This vehicle is considered a repairable proposition and we have calculated a repair cost of £X." + value box (Recommended settlement) |
| cash_in_lieu | Cash in Lieu Report | CASH IN LIEU | Cash in Lieu Settlement (= repair total) | "We recommend settlement by way of a cash in lieu payment based upon the estimated repair cost of £X." + value box |
| contract_repair | Contract Repair Report | CONTRACT REPAIR | Repair Cost inc VAT | Heading "Contract Repair": "A contract repair has been agreed for the sum of £X including VAT. Costs cannot increase above this figure." + value box (Agreed contract repair) |

Cash in lieu and contract repair are otherwise identical to repairable (per Andrew).
All settlement figures/wording are generated from computed values — never typed.

## Files

- `ce_report_generator.py` — the locked generator. `python ce_report_generator.py job.json out.pdf`
- `sample_job_PK12TMZ.json` — worked example (reproduces the approved draft).
- `report_data_schema.json` — JSON Schema for the job file.
- `assets/` — logo + engineer signatures. Photos are supplied per job (paths in the JSON).

## Variables walkthrough — decisions with Andrew (22/07/2026)

The dashboard ("the portal") is the single data-entry point; the generator renders from its data.
Each variable below is recorded as: field → dashboard input type.

**Section 1 — header block**
- our_ref → text, from the portal per case (case-specific, no fixed format enforced)
- your_ref → text, the instructing principal's reference, from the portal
- report_for → the instructing principal's address block, from the portal's principal record;
  court-addressed cases are "FAO The Court, C/o [principal]"
- date → report generation date (auto)
- matter line → COMPOSED: "Road Traffic Accident: [claimant_name]: [incident_date]" — never typed
- Header shows: Date, Our Ref, Your Ref (in that order). No claim/policy number.

**Section 2 — badges & tiles**
- outcome → dropdown: total_loss | repairable | cash_in_lieu (complete set for assessment reports)
- legal_status → dropdown: roadworthy | unroadworthy (no third option)
- unroadworthy_reason → text, MANDATORY when unroadworthy (dashboard enforces on tick;
  generator also refuses to render without it). Renders as a composed sentence in
  Engineer's Comments: "Please note the vehicle is unroadworthy due to [reason]."
- category → dropdown: A | B | S | N | N/A (N/A for out-of-scope items e.g. bicycles, trailers);
  badge shows "TOTAL LOSS — CATEGORY x" including N/A
- salvage_value → currency, engineer's opinion, same treatment for all categories
- Tiles approved as rendered: TL = PAV / repair cost / salvage / settlement(red);
  repairable = PAV / labour hours / repair cost(red); CIL same but red tile = "Cash in Lieu Settlement"

**Section 3 — vehicle details**
- vehicle_type → dropdown: car | van | motorcycle | scooter | bicycle | trailer | caravan | other.
  All vehicle types in scope. VIN/engine/fuel/odometer OPTIONAL, no VIN format rule
  (bicycles, trailers etc. may have none) — missing values render as "—" / "TBC".
- mileage_source → dropdown: online_data | owner | repairer | principal | average | tbc.
  Each option composes its own sentence in Engineer's Comments (e.g. "The mileage has been
  calculated from online data."). odometer_miles required unless source is tbc.
- condition → dropdown, fixed scale: poor | below average | average | good | excellent
- impact_severity → dropdown: light | light to moderate | moderate | moderate to heavy | heavy
- impact_location → dropdown: front, left front, right front, left side, right side, rear,
  left rear, right rear, roof, underside, wheel(s), interior, mechanical, multiple
- Impact Magnitude line and the default Nature of Incident sentence are both COMPOSED from
  severity + location ("The vehicle has suffered moderate collision/impact damage to the
  right rear."). Engineer can override Nature of Incident with custom text.
- Open item: DVLA lookup on registration to auto-fill make/model/year/engine/fuel in the dashboard.

**Section 4 — narrative sections**
- assessment.method → dashboard control: select "Image Based Assessment" OR enter a vehicle
  location address. image_based → intro says "Vehicle located at: Image Based Assessment" and the
  Desktop Assessment section (fixed sentence) is included. physical + address → address shows as
  the vehicle location and Desktop Assessment is omitted entirely.
- Intro paragraph → fully COMPOSED from instructions_received date, assessed date, and method/location.
- Vehicle History Check → pass-through text from the dashboard's data-provider API (e.g. Experian
  AutoCheck, triggered by button on the dashboard). Whatever the API returns is printed —
  clear or adverse (e.g. previous total loss). Mandatory.
- Pre-Incident Condition → COMPOSED from the condition dropdown: "The vehicle is considered to be
  in [poor/below average/average/good/excellent] condition for its age and type."
- Salvage paragraph → COMPOSED per category with the computed salvage figure. Category S wording
  confirmed; **Categories N, A, B and N/A are placeholders — Andrew to supply wording.**
- Remaining free-text inputs in the whole report: history_check (API pass-through),
  optional engineers_comments, optional nature_of_incident override, unroadworthy_reason.

**Section 5 — values & repair cost calculation**
- Dashboard valuation section: guide boxes for CAP retail/trade, Glass's retail/trade,
  Cazana retail/trade. Engineer reviews guides and selects the retail and trade to use;
  engineer's value is opinion (usually = retail, may deviate up or down). Only the three
  chosen figures reach the report.
- Glass's code REMOVED from all reports.
- Labour rate and full repair specification drag through from the dashboard.
- VAT: always 20%. Dashboard tick "repairer VAT registered":
  - registered → conventional layout, VAT on the full subtotal at the bottom line
  - NOT registered → VAT added to parts and paint costs only; no VAT on labour or
    additional operations; calc row reads "VAT (20% — parts & paint only)"
- recovery_charge / storage_charge → optional dashboard boxes; if filled, a
  "Recovery & Storage" paragraph is generated with the figures
  (**wording is a placeholder — Andrew to supply**); if empty, no paragraph.

**Section 6 — repair specification lists & photos**
- Three list sections on the report: Main New Parts Required / Repairs Required / Additional
  Operations. Paint Operations is NOT a separate section — paint items merge into
  Additional Operations.
- The repair specification is produced in an estimating system (e.g. Audatex, Glass's),
  imported into the dashboard in a standardised format, and drags through to the report.
  Names only — no part numbers or per-line prices on the report.
- Photos: no layout rules. The report renders exactly what the dashboard sends, in the
  engineer's order. Include/exclude toggle, resize, crop and rotate are DASHBOARD features —
  the generator never manipulates images.

**Section 7 — statement of truth, signature, fee note**
- Statement of truth: keep current wording for now; **to be revised at finalisation**
  (note: it references Glass's Evaluator/Thatcham — review once dashboard guide values are live).
- Engineers: the dashboard holds the engineer list (name, qualifications, signature image);
  the generator's engineer map mirrors it. Currently: A Patterson (M.Inst.IAEA),
  E Mawdsley, N O'Reilly — **qualifications for the latter two to be confirmed**.
- Fee note: agreed fee and billing details come from the dashboard per case/principal;
  layout as approved (description/amount table, payment details grid, terms).

## Open wording placeholders (Andrew to supply)

1. Salvage paragraphs for Categories N, A, B and N/A
2. Recovery & Storage paragraph
3. Final statement of truth wording
4. Qualifications for E Mawdsley and N O'Reilly

## Future improvements (agreed direction, not yet built)

- Impact diagram generated from a location code (e.g. "RH rear") selecting pre-drawn overlays,
  instead of a per-case image.
- Stress-test suite: max parts list, 14+ photos, minimal repairable job, long model names —
  re-render on every template change.
- API pipeline for volume: Claude structures incoming case data into this JSON (validated),
  generator renders, engineer approves. See chat discussion 21/07/2026.
