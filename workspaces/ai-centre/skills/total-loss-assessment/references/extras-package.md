# ABP 2026 Extras Package

Source basis: ABP retail and non-contract charge schedule effective 1 January 2026. Include
charges because the operation and condition apply, not simply to inflate a total.

Structured source: `abp-reference-data.2026.json` is the versioned machine-readable record for the
always-include extras, conditional extras, and exclusions. Keep this prose guide aligned with that
file.

## Always include on every job

### Fixed-price items (`specialist_fixed`)

| Description | Price |
|---|---|
| Assessment Fee | £176.96 |
| BS 10125 Compliance Charge | £41.64 |
| Environmental (EPA) Charge | £31.23 |
| Environmental Sustainability | £26.02 |
| Vehicle Care Kit | £10.41 |

The ABP schedule presents the BS 10125 compliance charge and the VM approval hourly uplift as
alternatives ("or"), and attaches holder conditions (BS 10125 accreditation; ARIES/SBTi/ISO 14068-1
commitment for the sustainability charge). Do not stack the £41.64 charge and the +£5.00/hr uplift
on one job unless the engineer directs it.

### Labour-time items (`specialist_wu`) — MUST be specialist_wu, not rnr

| Description | WU |
|---|---|
| Pre Repair Clean | 5 |
| Wash And Vacuum | 10 |
| Pre Repair System Diagnostic Check | 10 |
| Post Repair System Diagnostic Check | 10 |
| Standard Vehicle Shutdown | 10 |
| QC And Road Test | 10 |
| Personal Belongings Removal | 3 |
| Specialist Valet | 10 |
| Yard Charge | 10 |

---

## Conditional — include when applicable

| Item | Type | Value | Condition |
|---|---|---|---|
| Older Vehicle Allowance | specialist_wu | 10 WU | Vehicle over 10 years old |
| Air Conditioning Recharge | specialist_fixed | £271.70 | A/C system disturbed (front/wing/door work) |
| Wheel Alignment 4-Wheel Check Only | specialist_fixed | £112.42 | Geometry verification without adjustment; includes tyre pressure check/adjust |
| Wheel Alignment Check And Adjust Toe | specialist_fixed | £174.88 | Suspension or wheels disturbed; 4-wheel check with toe adjustment |
| Steering Angle Reset | specialist_wu | 5 WU | Prior to ADAS calibration |
| ADAS 1st Calibration | specialist_fixed | £312.30 | First ADAS system calibrated; ADAS-equipped car |
| ADAS Subsequent Calibration | specialist_fixed | £156.15 each | Per additional system (surround view, blind spot, lane keep, ACC, parking sensors) |
| Dynamic ADAS Second Technician | specialist_wu | 5 WU per calibration process | Dynamic (on-road) ADAS calibration — second technician operates the equipment while the other drives, to comply with UK law |
| Extensive Road Test Post ADAS Calibrations | specialist_wu | 5 WU | Any ADAS calibration on the job — required for IIR verification even when the calibration was performed by a dealer or sub-contractor |
| Paint Protection 1st Panel | specialist_fixed | £135.33 | First painted panel |
| Paint Protection Additional | specialist_fixed | £33.31 each | Each subsequent painted panel |
| Corrosion Protection Labour First Panel | specialist_wu | 3 WU | Any panel work — once per job |
| Corrosion Protection Labour Additional Panel | specialist_wu | 1 WU each | Per additional panel treated |
| Corrosion Protection Materials First Panel | specialist_fixed | £15.61 | Any panel work — once per job |
| Corrosion Protection Materials Additional Panel | specialist_fixed | £7.29 each | Per additional panel treated |
| Machine Polish | specialist_wu | 3 WU per panel | Per panel machine polished |
| Sill Dressing | specialist_wu | 7 WU per side | Sill work, per side |
| Trial Panel Fit | specialist_wu | 5 WU per fit | 2nd and subsequent trial fits only — first fit is included in standard hours |
| Storage Charge | specialist_fixed | £41.64/day | Vehicle non-driveable or held pending decision |
| Salvage / Total Loss Administration Charge | specialist_fixed | £98.89 | Vehicle deemed total loss; excludes the assessment compilation fee, so both may appear on one job |
| Alloy Wheel Refurb Standard | specialist_fixed | £110.35 each | Per kerbed wheel |
| Alloy Wheel Refurb Diamond Cut | specialist_fixed | £196.74 each | Per damaged diamond-cut wheel; includes remove/refit tyre, replace valve, and balance |
| Swap Wheel With Spare | specialist_wu | 5 WU | Wheel swapped with the spare wheel |
| EV / Hybrid Risk Management | specialist_wu | 5 WU | Any PHEV or BEV |
| Power Down PHEV Vehicle | specialist_wu | 30 WU | PHEVs/BEVs after impact (HV battery isolation) |
| Quarantine Vehicle PHEV Procedure | specialist_wu | 40 WU | Damaged HV system on PHEV/BEV |
| EV Full Recharge | specialist_fixed | £52.05 | BEV discharged during repair or storage |
| PHEV Full Recharge | specialist_fixed | £20.82 | PHEV discharged during repair or storage |

---

## Conditional-use notes

- **Repair plan / assessment fees:** use where the assessment reasonably requires methods review,
  repair planning, ADAS identification, and report administration. Do not duplicate a fee already
  present on a transcription source estimate.
- **EV / hybrid power-down:** do not double-count power-down time if the manufacturer repair method
  already includes it in the operation time. Use quarantine only where HV battery damage, thermal
  risk, water ingress, or manufacturer guidance makes quarantine relevant.
- **ADAS calibration:** include only for ADAS-equipped vehicles and affected systems. Do not add
  surround view, blind spot, lane keep, ACC, or parking calibration unless the vehicle has the
  system and the damage/repair operation could disturb it. Where any calibration is performed, add
  the extensive road test (5 WU) — it is required for IIR verification even when a dealer or
  sub-contractor did the calibrating. Add the second technician (5 WU per calibration process) only
  for dynamic (on-road) calibrations.
- **Salvage / total loss administration:** include on total-loss outcomes only. It excludes the
  assessment compilation fee, so it sits alongside the Assessment Fee rather than replacing it.
- **Trial panel fit:** the first trial fit is inside standard repair hours — charge only the 2nd
  and subsequent fits.
- **Wheel alignment:** the ABP schedule prices 4-wheel alignment as check only (£112.42) or check
  and adjust toe (£174.88); camber and castor adjustment is a further charge with no fixed price in
  the schedule — agree it per job where geometry damage requires it. Use check-and-adjust-toe on
  meaningful side impacts, suspension involvement, or steering geometry concerns. Add the steering
  angle reset (5 WU) prior to ADAS calibration where applicable.
- **Paint protection and corrosion protection:** include the first-panel line once per job and the
  additional-panel line per extra panel treated. Do not duplicate first-panel lines unless the
  engineer specifically directs it.
- **Repair-method inclusion:** standard vehicle shutdown, the system diagnostic checks, and EV
  power-down are chargeable per ABP only where the time is not already included in the repair
  method; drop the line if the method time covers it.
- **Storage:** use where the vehicle is non-driveable, insecure, airbag-deployed, has glazing out,
  has exposed HV/fire/water risk, or is held pending a category/repair decision.
- **Recovery:** excluded by default. Add only when the engineer or source evidence shows recovery
  occurred or is required. 2026 figures for when it does apply: standard £182.17 including the
  first 20 miles then £4.68 per mile thereafter (per return journey); specialist and inherited
  charges at contractor rate + 20% admin; vehicle movement (driven, to/from dealer or owner)
  £78.07 per journey.

## Never include by default

- **Recovery charge** — Andy's stated default: skip this. Engineer adds manually if specifically required for the job (2026 figures recorded on the exclusion entry and in the recovery note above).

---

## Parts-section fixed prices

ABP parts are supplied at manufacturer's list price; the items below carry fixed prices in the
schedule. They are parts lines, not specialist extras — add them to the parts total. Panel Repair
Sundries is a separate ABP line from the 3.5% sundries/clips/fixings percentage, not a duplicate.

| Item | Price |
|---|---|
| Panel Repair Sundries (inc body filler) | £26.02 per panel repaired |
| New Welded / Bonded Panels (materials) | £12.49 per panel joined |
| Boron Drill (8mm UHSS drill bit) | £72.87 each |
| Number Plate (exc fitting) | £26.02 each |
| Glass Moulding Lifting Tape | £15.61 |
| Sound Deadening Pad (large / small, exc fitting) | £16.65 / £13.53 each |
| Screenwash | £7.28 per litre |

Everything else in the parts section (bead sealer, brake fluid, coolant, engine oil, bonding kits,
windscreen bonding kit) is OE price; tyres are as agreed on a retail basis.

---

## Transcription jobs

When the engineer says "match this estimate to the penny," the source already has its own extras list. **Do not add the standard ABP package** — it will double-count. On transcription jobs:
- Set `sundry_parts_pct: 0.0`
- Back-calculate `paint_material_base` to hit the source's paint material total exactly
