# Estimate Construction

How the line-by-line repair estimate is built, justified, totalled, checked, and projected into
every deliverable. This file is the sole owner of the estimate-table spec — other references
point here and do not restate it.

## One list, three renderings

The operations list, expressed as validated `assessment_payload.json`, is the single source of
truth. The pack table, the Audatex/EVA PDF, and the branded-PDF estimate datatable are
projections of it — never independently drafted. Line value = WU/10 × rate (the generator's
formula), so all renderings always agree. An assessment with an outcome or repair position is
not complete without a validated payload.

## Costing posture — the maximum defensible estimate

The estimate states the full cost of putting the vehicle right properly. Build it to the
highest figure at which every line survives scrutiny by an opposing engineer. Two failure
modes, equally wrong:

- **Invention** — a line with no evidence, condition, or labelled inference behind it. Never.
- **Omission** — defensible scope left out, or a justified charge narrated in prose instead of
  costed. This is the common failure. If a line can carry an honest justification and a status
  flag, it goes in the table: the `E`/`P` flags and the justification are what make inclusion
  defensible, so uncertainty is a labelling problem, not a reason to leave value out.

The challenge test for every line: can it answer an opposing engineer's "why is this here?"
with named evidence, a labelled inference, or an ABP condition? If yes, it goes in. If not, it
comes out — no padding survives the test, and no survivor is omitted.

Sweep for commonly omitted defensible scope before closing the list:

- **Strip-dependent lines** — reinforcement bar, energy absorber, brackets, mounts, carriers,
  and one-time clips/fasteners behind any deformed panel: include as **P** with "subject to
  strip", not as prose risk.
- **ADAS calibration** where fitment is standard or likely for the model/year/trim: include as
  **E**/**P** with the fitment basis stated. Gotcha 2 is the floor (no ADAS lines on cars that
  cannot have it); this rule is the ceiling — evidenced or standard-fit systems are costed
  subject to spec/scan confirmation, not deferred to commentary.
- **Refinish scope** — blend adjacent panels wherever colour match or method requires; check
  the paint code before assuming solid, since pearl/3-stage changes the refinish figure.
- **The full ABP extras** whose conditions are met (`extras-package.md`) — diagnostics,
  corrosion protection, refinish protection, QC, BS 10125 compliance. Conditions decide, not
  habit.
- **Geometry/alignment check** where the impact pattern (wheel, suspension, corner load path)
  justifies it.
- **Storage and recovery** where the vehicle is unroadworthy or held — cost them (per-day rate
  × days to date, or a stated per-day accrual line), never "justified but not added".
- **Parts basis** — estimated parts price at OEM list unless the instruction or vehicle age
  justifies otherwise; state the basis. A borderline repair-vs-renew call goes to the
  decision-matrix factors: where a safety or quality factor favours renewal, take renewal and
  name the factor.

Prices and WUs sit at the credible top of the defensible range for a proper repair — full
method scope, OEM basis where justified, no speculative discounting. A figure that cannot be
justified at its stated level comes down to the level that can be.

## The canonical table

Group the rows Labour / Parts / Paint / Extras, one row per payload operation:

| # | Op | Panel / item | WU | Labour £ | Parts £ | Paint £ | Justification | Status |
|---|---|---|---|---|---|---|---|---|

- Line values use the generator's exact formula: WU/10 × rate for WU rows; price for parts and
  fixed extras.
- `*` marks unpriced parts (the `unpriced: true` convention from `eva-routing.md`).
- Status flags: **C** confirmed (verified price / instructed fact), **E** estimated (desktop
  judgement, basis stated), **P** provisional (strip/diagnostic-dependent).

Totals block, in this order: Labour · Parts + sundries (3.5% unless transcription) · Paint
(materials + paint labour) · Extras · Subtotal ex VAT · VAT 20% · **Total inc VAT**. P lines
are costed inside these totals — the estimate states the full defensible scope, subject to
strip — never held out as a side figure (the payload totals them anyway, so holding them out
would also desync the pack from the Audatex/EVA PDF).

Sensitivity line, under the totals: "Strip-dependent lines (P): n lines, £x–£y of the total —
£z if none confirm."

## Justification standard

One line per estimate line, tied to at least one of: visible evidence (photo/damage-catalogue
row), the escalation matrix zone, an ABP condition (name it), or a repair-renew factor. Reuse
the `source-governance.md` labels. Justifications render in the pack and the branded PDF only —
never in Audatex row descriptions or continuations (`eva-routing.md` row-description discipline
governs there). The payload carries optional `justification`, `evidence_label`, and `status`
fields per operation (`scripts/assessment_payload.schema.json`); the frozen generator ignores
them, so they never leak into the Audatex/EVA PDF.

## Provisional/unpriced convention — extended to the pack

Estimated prices and WUs are stated, marked (`E`/`P`/`*`), and justified — never withheld.
Provisional labelled lines always beat no lines. A charge the pack argues for in prose —
storage, calibration, a geometry check — is costed as a line or a stated per-day accrual,
never left as commentary: money narrated but not costed is scope omitted.

## Magnitude-language ban

A repair figure may only exist as the sum of stated lines. Banned without lines: "comfortably
above", "of the order of", "well in excess of", "in the region of", any range guess.

## Ceiling caps authorisation, not costing

This is the canonical statement — `gotchas.md` item 9 and `total-loss-and-salvage-routing.md`
point here:

- Complete the estimate regardless of any instructed ceiling (e.g. an 80%-of-PAV limit).
- Crossing the ceiling IS the finding — state the repair total, the ceiling figure, and the
  ratio, and let the engineer decide.
- Never present a total-loss or near-ceiling position without the costed estimate that
  evidences it.

## Estimate sanity checks

Run these checks after drafting the estimate table, before presenting the pack — not only
before rendering. A pack-only run gets full estimate QA.

- **Thin output:** if the operation list only covers the obvious outer panel, re-open the photos and
  check adjacent trim, lamps, brackets, sensors, wheel/tyre, suspension, glass, and inferred strip/refit.
- **Zero or near-zero parts total:** a real impact assessment with renewals normally has parts. If
  the parts subtotal is very low, confirm whether the job is genuinely repair/refinish only or
  whether renewals were missed.
- **Under-replacement:** renew instead of repair where the part is torn, split, sharply creased,
  structurally distorted, mount-broken, or more expensive to repair than replace.
- **Wrong side:** verify nearside/offside against the photo orientation, registration plate view,
  steering-wheel side, fuel-filler side, and sequence of images before finalising row descriptions.
- **Missed inferred damage:** an airbag/SRS light, wheel displacement, smashed rear screen, fluid
  leak, broken lamp mount, or distorted shut line usually implies additional checks or renewals.
- **Paint routing gap:** each renewed or repaired painted panel needs the matching paint operation;
  adjacent panels need blend operations where colour match or repair method makes blending necessary.
