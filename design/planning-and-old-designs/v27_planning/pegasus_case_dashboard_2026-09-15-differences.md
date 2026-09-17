# Differences — `pegasus_case_dashboard_2026-09-15.html` against Pegasus today

Compared 16 Sep 2026 against the live Case record on `dev`
(`src/Pegasus.Web/Pages/Cases/Shared/_Case*.cshtml`, `Details.*.cs`) and the
governing FRDs (FRD-01, FRD-06, FRD-08, FRD-11, FRD-12). The feature
inventory is in
[pegasus_case_dashboard_2026-09-15-features.md](pegasus_case_dashboard_2026-09-15-features.md).

Verdicts: **Same** (already built this way), **Different** (built, but another
shape), **Absent** (not in Pegasus), **Conflicts** (contradicts a settled
decision). This is a comparison, not a decision list; anything to adopt
needs its own lettered sign-off item in the v27 notes.

## Frame and edit model

| Mockup | Pegasus today | Verdict |
| --- | --- | --- |
| Whole-case lock, heartbeat, 15-min stale, **Ask to release**, **Take over** another person's lock | One server-owned expiring lease per Case; a non-holder sees "{name} is editing" with a lock and no take-over control and is never given a time; Take over exists only for the operator's own lease in another window (FRD-01 § Case edit authority; FRD-12 ribbon) | **Conflicts** |
| Padlocked fields (Principal, Our ref, Case status, Instructed) unlockable inline, one at a time | One page-wide edit session (Edit Case → Save/Cancel); identity cells read with a lock; Our ref is immutable; Case status changes only through lifecycle actions; Principal changes only via **Correct principal** | **Conflicts** |
| Sticky header: plate, vehicle, refs, outcome/legal badges, repairs-%-of-PAV badge, assigned engineer, lock chip; 9 numbered jump tabs | Ribbon (heading, claimant, principal, Engineer, state/type/editing chips, Edit/Save/Cancel, one **Actions** menu) + section row with Scroll/Tabs switch, Refresh, fold state; outcome and legal chips plus three figures live in the **Figures aside**, not the header; repair-cost-of-value % is on Settlement | **Different** |
| "Demo · viewing as" strip | v26 mockup strip convention (bottom-left "Mockup controls", query-string presets) | Mockup furniture, n/a |
| Nine sections: Case details, Claim, Inspection, Vehicle, Images, Repair spec, Decisions, Report, Notes & queries | Ten sections: Overview, Inspection, Vehicle, Damage, Valuation, Estimate, Settlement, Report, Files, Notes (D29/D30) | **Different** — Damage and Valuation are their own sections; Decisions folded into Settlement; Images are a Files tab |

## 1–3 Case, claim, inspection

| Mockup | Pegasus today | Verdict |
| --- | --- | --- |
| Product type select (Standard / C. / A. / AP. / D.) | Case type is set at creation and shown as a ribbon chip; Audit Cases are created by **Create audit** (ADR-0051), never by switching a select | **Conflicts** |
| Assigned engineer select on the record | **Hand to Engineer** / **Assign to me** action in Review with an eligible-Engineer dialog | **Different** |
| Sign off engineer select on Case details, auto-follows assigned | Sign-off Engineer select on the **Report** section; default is the assigned Engineer when flagged, otherwise A Patterson (D31) | **Different** (placement; default rule matches) |
| Principal generic notes + case-specific; Claim source generic + case-specific | Same Notes band on Overview (FRD-04) | **Same** |
| Your ref, claimant name/address, accident date, accident circumstances, notes from client | Claim reference, claimant fields, incident date, accident circumstances, client notes on Overview | **Same** |
| Claimant VAT status on Claim details | `settlement.claimant_vat_registered` on Settlement, also read by Valuation's VAT increase | **Different** (placement) |
| Report date lockable, auto on generate | Report date override switch + date on Report section | **Different** |
| Composed matter line | Not composed on the record; the report template owns it | **Absent** |
| Inspection type + location select with "Other…"; composed "Vehicle located at:" | Inspect-at fast-update choice (IBA / Claimant / Repairer / Storage / previous addresses / Manual) (D33); no composed sentence on the record | **Same** shape, composed line **Absent** |
| Repairer name/address/VAT status; composed VAT explanation | Repairer name, address, directory pick, `vatStatus`; VAT categories default from status on the Estimate | **Same** |
| Storage name/address/rate/recovery; composed charges sentence | Storage location, storage per day, recovery charge on Inspection; lump storage charge on Settlement; no composed sentence | **Same** fields, composed line **Absent** |

## 4 Vehicle, damage, valuation

| Mockup | Pegasus today | Verdict |
| --- | --- | --- |
| DVLA-chipped read-only facts, VIN, vehicle type | Vehicle section with lookup provenance rows, **Look up DVLA & MOT** filling empties only, VIN, type, body, engine, fuel, colour, transmission, tax/MOT expiry | **Same** |
| Odometer with mi/km toggle | `vehicleMileageUnit` on the mileage field | **Same** |
| "Average mileage 7,100/yr" tick that overwrites the odometer | No such tick; conservative MOT-history estimation is an abstaining seam that never defaults a value into the Case (FRD-06) | **Conflicts** |
| Mileage source select, pre-incident condition select, composed sentences | `vehicle.mileage_source`, `vehicle.condition`; the report composes, the record does not | **Same** fields |
| "Run check" Experian button + verbatim pass-through | Experian is a disabled seam; History check is a read-only narrative editable in edit mode (D34) | **Different** |
| Impact zone SVG: 14 outside + 5 on-car + 4 wheels, multi-select, location auto-follows; severity is one global select | **Plan** clicker: 19 panels + 4 wheels + Underside/Interior/Mechanical chips, severity and note per zone, five graded fills, numbered markers; impact location and severity derived by Core (D39, D45) | **Different** — per-zone severity, derived severity, richer zone set; tyres/belts and material transfer not in the mockup |
| Unrelated damage on Vehicle with a "Print on report" tick | Unrelated damage + deduction on **Damage**; "Include unrelated damage" is a Report content switch | **Different** (placement) |
| Six guide cards incl. CAP and Cazana with fixed figures; click to select | Glass's, Brego, Super CAP entry cards with month/mileage/retail/trade, **Get valuation**, Save; Cazana disabled seam; AI market research job card; no "CAP" source; no click-to-select | **Different**; CAP **Absent** |
| Value increases: Tow bar, PCO, Decals, VAT +20%, Camper, Tuition, two Other | Admin-managed `ValuationPresets` with suggested amounts, plus Other; commercial VAT blocked when the claimant is VAT-registered | **Same** shape (presets are data, not hard-coded) |
| Adjustment order VAT → prev-TL % → extras → condition; Apply to engineer's value | Identical order in Core `ValuationCalculations`; whole-pound rounding; Apply is the only route to Engineer's Value | **Same** |
| PAV editable in two places (Valuation and Decisions) | Engineer's Value is read-only on Settlement with an "Apply in Valuation" link; adopted only by Apply | **Conflicts** |
| Disclose guide source / valuation commentary ticks on Valuation | Both are Report content switches, with a 4,000-character commentary text | **Different** (placement) |

## 5 Images

| Mockup | Pegasus today | Verdict |
| --- | --- | --- |
| Click-to-include thumbnails, "6 per page fixed grid", drag reorder (mock), Crop/rotate button | Files → Images tab (tags, viewer, Crop on the viewer stage, stored crop rectangle); **Report position** on Report: one Close-up, one Overview, ordered Supporting, rotation (D19) | **Different** — inclusion is role-based, not a tick; layout not fixed at 6 per page |

## 6 Repair specification

| Mockup | Pegasus today | Verdict |
| --- | --- | --- |
| Drop zone with confirm ("Overwrite grid") | Whole-page drop, imported immediately, no confirmation (D16); creates a new immutable Draft, never overwrites | **Conflicts** |
| One live grid overwritten by imports; frozen snapshot versions | Named estimate versions side by side; one is **Current** (Use estimate); Duplicate, Discard (reason), Compare when two or more | **Different** — same intent, version-first model |
| Columns Type/Description/Qty/Unit/Hours/Material/Provenance; per-type column availability; amber off-pattern cells | Type/Description/Part no./Qty/Unit £/Hours/Paint hours/Source; materials and other costs are estimate-level fields, not per line; no off-pattern rule | **Different** |
| Delete-all with modal; per-row undo toast | Remove line per row; Discard whole estimate with reason; no undo toast | **Absent** |
| Labour rate: ABP Standard/Prestige/Custom + 15% regional uplift with postcode suggestion | Admin labour-rate cards (enabled list, versioned) + "keep entered rate"; `rates.regional_uplift` exists in the vocabulary but no uplift control or postcode rule on the page | **Different**; postcode suggestion **Absent** |
| Target % of PAV slider that rescales prices and rate with floors; Apply / Remove scaling | **Send to AI** dialog with a target-% slider creates an `Estimate` job; Pegasus itself never rescales lines; results return as a reviewed proposal | **Conflicts** (deterministic self-scaling vs AI proposal) |
| Contract repair tick + agreed sum that rescales the spec; mismatch chip | `contract_repair` is an outcome; the Core-computed VAT-inclusive total is the cap — there is no separate agreed-sum input or rescale (FRD-11 outcomes table) | **Conflicts** |
| Discounts Parts/Materials/Specialist/Overall | Same four discounts | **Same** |
| VAT applies to Labour/Parts/Materials/Specialist, defaulted from repairer status, manual override + reset | Same, plus editable VAT % and Unknown status blocking Use as Current (D9/D17) | **Same** |
| Roll-up, three worklists | Totals strip and the same three names-only lists | **Same** |
| Versions / Compare modals with diff colouring, print | Compare dialog lists estimate, state, net/VAT/gross; no per-line diff colouring, no print sheet | **Different** |
| Supplementary panel composing a "Following receipt of…" paragraph | No supplementary diff or composed paragraph; a later estimate is another version | **Absent** |

## 7 Decisions and wording

| Mockup | Pegasus today | Verdict |
| --- | --- | --- |
| Outcome / PAV / Salvage category / Salvage value / Roadworthy / Unroadworthy reason tick rows; salvage collapses when not TL | Settlement **Decisions** strip with the same six fields; salvage rows hidden when not total loss; unroadworthy reason shown only when unroadworthy | **Same** |
| Brian "parked" | AI proposals per decision field with Awaiting/Accepted/Corrected status, Accept / Accept all while editing | **Different** — proposal review is built |
| Salvage slider with 5–25% snaps | Plain value field | **Absent** |
| Unroadworthy reason firm-level phrase bank, save-to-bank | Free text only | **Absent** |
| Report wording well: every paragraph editable in place, drag order, rename headings, custom paragraphs, "recompose from fields" | Engineer's comments, valuation commentary text and three content switches; templates are operator-supplied and the renderer may not substitute wording (FRD-11) | **Conflicts** with the template rule; as a feature, **Absent** |
| Settlement strip and "mandatory decisions" gate; "send is the approval act" | Metric strip on Settlement; readiness list on Report gates Generate; no separate approval act either — but sent is proved only by Sent-item evidence, not the click | **Same** intent, **Different** proof |
| Repairable reserve rounded up to the next £50 | `settlement.reserve` is a typed field | **Different** |
| Excess, betterment, hire, delays, diminution, salvage logistics | All present on Settlement (D41) | Mockup lacks these |

## 8 Report and send

| Mockup | Pegasus today | Verdict |
| --- | --- | --- |
| Generate preview → stale on any edit → regenerate | Generate report / Preview draft; generation goes Stale on a later change with a notice | **Same** |
| Channel Email / WhatsApp | Email only via an approved mailbox; WhatsApp is manual evidence intake, never a send channel (FRD-05, FRD-08) | **Conflicts** |
| To/CC address book with suggestions, chips | Reviewed recipients: To/Cc email inputs seeded from `CaseReportDeliveryPolicy` suggestions, **Prepare delivery** then **Send prepared report** | **Different** (two-step; no address-book UI) |
| Attach ticks Report / Fee note / Breakdown / Images | Include fee note switch; no breakdown PDF or image contact sheet | **Absent** (breakdown, images) |
| Filename dot-per-resend, composed body switching to "supersedes" | Not built; corrections are new reasoned versions with retained artifacts (FRD-11) | **Absent** |
| Edit after send implicitly reopens (Issued → In progress) | **Report sent** enters post-report work; Completed ⇄ Query; further engineering needs a reasoned **Return to Engineer** — no implicit reopen | **Conflicts** |
| Fee tab with a static principal fee | Agreed fee + description lines, fee-note preview, Generate fee note | **Same** |

## 9 Notes

| Mockup | Pegasus today | Verdict |
| --- | --- | --- |
| System timeline logging every act; inert Add note; Queries empty state | Notes: notes, business events, chase outcomes, AI events merged newest first; **Add Case note** works without a lease; Record chase dialog; queries are the Query lifecycle state and linked correspondence, not a panel | **Same** timeline idea; queries **Different** |

## Summary

- **Same / already built:** notes bands, inspection choices, repairer and
  storage fields, vehicle facts and lookup, mileage unit, valuation
  adjustment order, presets, discounts, VAT categories, roll-up and
  worklists, the six decision fields, stale preview, fee.
- **Built differently:** section map, header versus ribbon and aside,
  engineer assignment, sign-off placement, damage per-zone severity with
  derived location and severity, guide cards with providers, estimate
  versions and Current, rate cards, recipients and two-step send, image
  inclusion by role.
- **Absent in Pegasus:** composed sentences on the record, average-mileage
  shortcut, CAP source, delete-all and undo, off-pattern cells, postcode
  uplift suggestion, supplementary diff paragraph, salvage slider, reason
  bank, wording well, breakdown and image attachments, filename-dot and
  supersedes body, address book.
- **Conflicts with settled decisions:** take-over of another's lock; inline
  padlock editing of identity and status; product-type switch; two-place PAV
  entry; average-mileage default; import overwrite with confirmation;
  self-rescaling to a % of PAV and contract-sum rescale; editable report
  wording; WhatsApp channel; implicit reopen after send.
