# Field Mapping — HS Roadworthy Report

Status: **Inactive — renderer source change required.**

This is a prepared-template mapping, not case evidence, roadworthiness policy,
or an activation contract. Every fallback and fixed value below is template
behavior only. In particular, `Legal Status=Roadworthy`, `Status=Repaired`,
`Passed MOT (taxi)=TBC`, dates, locations, and Cat S values must never be
inferred as accepted facts. The package remains non-invocable until a separately
accepted renderer change stops unless a cited source artifact contains the
named Engineer's approved roadworthy/legal-status fact.

## The 14 fields — fill these and only these

| # | HS field | Source | Fallback |
|---|---|---|---|
| 1 | Our Ref (header) | Vehicle registration number | — (always present) |
| 2 | Your Ref (header) | Engineer's "Your Ref" value | Leave existing template value |
| 3 | Date (header) | Today's date, `DD/MM/YYYY` | — |
| 4 | RE: line — accident date | Date of road traffic accident | Today's date |
| 5 | RE: line — registration | Vehicle registration | — |
| 6 | "instructions received on" date (body) | Today's date, `DD/MM/YYYY` | — |
| 7 | Make (table) | Make, capitalised normally (e.g. "Toyota" not "TOYOTA") | — |
| 8 | Registration (table) | Vehicle registration | — |
| 9 | Model (table) | Short form only (e.g. "Corolla", not "Corolla Icon VVT-I"; "Astra" not "Astra SRi 1.4T") | — |
| 10 | VIN (table) | VIN | "TBC" |
| 11 | Status (table) | **Always "Repaired"** | — |
| 12 | Cat S (table) | "Yes" only if the engineer's report records the vehicle as a total loss Category S. Cat N, Cat A, Cat B, "Repair", blank, Salvage, anything else → "No" | "No" |
| 13 | Passed MOT (taxi) (table) | **Always "TBC"** | — |
| 14 | Legal Status (table) | **Always "Roadworthy"** | — |

Plus one inline body edit in the paragraph beginning *"We understand the vehicle has been previously sustained damage to the ___"*:

| Field | Source | Fallback |
|---|---|---|
| Damage location word(s) | Directional words from engineer's "Nature of incident" — e.g. "left hand rear", "nearside front", "offside", "rear" | "rear" |

That paragraph is part of the body, not the table. Replace only the directional placeholder — leave all other body text unchanged.

There is no separate unroadworthy-reason field in the current 14-field HS mapping. Do not add a
reason phrase elsewhere in the template. Use the controlled damage-location wording below only for
the approved inline body edit.

## Damage location source priority

1. Exact directional phrase from the engineer's report, if concise and compatible with the template.
2. Damage location plus roadworthy conclusion from the report.
3. Controlled fallback phrase from the table below.
4. Default `rear` only where the report provides no usable location.

## Controlled damage-location phrases

| Source evidence | Phrase for inline body edit |
|---|---|
| Front-end impact, frontal distortion, bonnet/bumper/front panel damage | `front` |
| Rear impact, tailgate/boot/rear bumper/rear panel damage | `rear` |
| Nearside front damage | `left hand front` |
| Offside front damage | `right hand front` |
| Nearside rear damage | `left hand rear` |
| Offside rear damage | `right hand rear` |
| Nearside side damage without clear front/rear split | `left hand side` |
| Offside side damage without clear front/rear split | `right hand side` |
| Bumper-only front damage | `front bumper` |
| Bumper-only rear damage | `rear bumper` |
| Suspension/steering damage with stated side | use the matching side phrase, e.g. `right hand front` |
| Lighting/visibility damage with stated side | use the matching side/front/rear phrase |

If the report uses nearside/offside and the HS wording would read more naturally with left/right,
convert nearside to `left hand` and offside to `right hand` for UK vehicles. Do not convert where the
report appears to use a non-UK convention or the side is genuinely ambiguous; use the clearest
directional wording from the report and do not ask clarifying questions.

---

## Worked example — Toyota YC19JDY

Engineer's report contained:
- Your Ref: 225763.TA
- Accident date: 02/04/2026
- Registration: YC19JDY
- Make: TOYOTA
- Model: COROLLA ICON VVT-I
- VIN: SB1Z93BE40E055307
- Status: Repair
- Nature of incident: "moderate collision/impact damage to the left hand rear"

Resulting HS report fields:
- Our Ref: `YC19JDY` · Your Ref: `225763.TA` · Date: today's date
- RE: `02/04/2026 YC19JDY`
- "instructions received on": today's date
- Make: `Toyota` · Registration: `YC19JDY` · Model: `Corolla` · VIN: `SB1Z93BE40E055307`
- Status: `Repaired` · Cat S: `No` · Passed MOT (taxi): `TBC` · Legal Status: `Roadworthy`
- Damage location: `left hand rear`
