# Labour Rates — ABP 2026

> **Source-workspace boundary:** Synchronized package copy for standalone rendering only; dated evidence, not policy/default authority. This is evidence, an example, a package-local format, or an experiment only; `Pegasus.Core`, current operator authority, and an authorised human own every accepted fact, cost, category, outcome, legal position, send, report issue, and approval.


Source basis: dated ABP retail and non-contract charge schedule effective
1 January 2026. The figures below are historical, package-local candidate
values only; they are not current runtime defaults. `abp-reference-data.2026.json`
records the same dated candidates for evaluation and must stay aligned with
this copy. Any activation requires current rate evidence accepted through
`Pegasus.Core`, operator authority, and authorised human review.

## Rate matrix

| Vehicle type | Rate per hour |
|---|---|
| Standard cars | £83.28 |
| Prestige / aluminium cars | £103.06 |
| Vans (light goods up to 3.5t GVW) | £83.28 |
| VM-approval uplift | +£5.00 on top of standard or prestige |

The ABP schedule marks the hourly rates (and the storage charge) as subject to a 15% uplift for
regional cost variations such as London and the home counties. All schedule values exclude VAT.

When applying or declining the uplift, state the location evidence used (instruction letter,
plate/licensing evidence, repairer address) and label the call `inference` — there is no
packaged postcode list to check against; flag marginal cases for the engineer.

## Standard rate marques

Mainstream volume manufacturers: Kia, Hyundai, Honda, Toyota, Lexus, Ford, Vauxhall, Nissan, Renault, Dacia, Peugeot, Citroën, DS, Fiat, Alfa Romeo, SEAT, Skoda, Mazda, Mitsubishi, Suzuki, Subaru, MG. **Volkswagen is generally standard.**

## Prestige rate marques

Premium / aluminium-bodied / high-spec: BMW (all models including older E39), Mercedes-Benz, Audi, Land Rover / Range Rover (all, especially L405/L460 aluminium), Jaguar, Porsche, Tesla. Bentley, Rolls-Royce, Aston Martin, Maserati go above prestige if the engineer specifies.

## Worked combinations

| Situation | Rate |
|---|---|
| Standard car, no approval | £83.28 |
| Standard car, manufacturer approved | £88.28 |
| Prestige car, no approval | £103.06 |
| Prestige car, manufacturer approved | £108.06 |

Manufacturer approval is an hourly uplift. Do not add it as a flat extra and do not use it merely
because a bodyshop is generally approved; use it where the assessment or instruction requires a
manufacturer-approved repairer basis. The ABP schedule presents this uplift and the BS 10125
compliance charge (£41.64 per job) as alternatives ("or") — do not stack both on one job unless
the engineer directs it.

Where the prestige rate is applied for aluminium construction, state it in the payload `notes`
field with the standard line: *"Please note this vehicle has aluminium body panels, uplift applied
to the labour rate as per ABP guide."* — the rate is then pre-answered if challenged.

## Material rates

| Item | Default |
|---|---|
| `sundry_parts_pct` | 3.5% of parts subtotal (set to 0.0 for transcription jobs) |
| `sundry_paint` | £120.16 fixed |
| `pre_sundry` | £46.43 fixed |
| `paint_material_base` | Variable — scale per job size |

Only `sundry_parts_pct` comes from the ABP schedule ("Sundries / clips / fixings — 3.5% of parts
total"). The `sundry_paint`, `pre_sundry`, and paint-material-base figures are house/Audatex
defaults with no ABP schedule equivalent — do not expect them to match an ABP guide on audit.

## Paint material base — rough scale

| Job size | Suggested base |
|---|---|
| Single panel repair | £120–£250 |
| 2–3 panels basic | £250–£400 |
| 3–5 panels metallic with blends | £400–£700 |
| Multi-panel side damage | £700–£1,200 |
| Major work, 5+ renewed panels, aluminium | £1,200–£2,000+ |

## When to ask

The dated source warns that an ambiguous rate can materially change a draft.
Its example distinguishes ordinary marques from a prestige rate and records a
separate manufacturer-approval uplift. These are historical evidence points
for authorised review, not selection instructions or accepted defaults.
