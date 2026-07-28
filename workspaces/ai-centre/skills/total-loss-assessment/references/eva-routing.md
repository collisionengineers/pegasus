# EVA Routing Rules — Audatex/EVA Output

This reference governs the Audatex/EVA-compatible PDF. The generated document mimics Audatex
formatting for EVA import — do not call it native Audatex, and do not apply CE branding to it.

EVA classifies items by which table they land in, plus keywords in descriptions. Get this wrong and items vanish from the Engineer's Report or appear in the wrong category. The generator handles keyword prefixes automatically based on the `type` field.

---

## Operation types

| Type | PDF section | EVA classification | Engineer's Report |
|---|---|---|---|
| `repair` | Labour (with "REPAIR" prefix) | Repair | Repairs box ✓ |
| `rnr` | Labour | R&R | **Hidden** |
| `check_labour` | Labour (with "CHECK" prefix) | Check | Additional Operations ✓ |
| `paint_new` | Paint (with "NEW PART PAINT K1R") | Paint | Additional Operations ✓ |
| `paint_repair` | Paint (with "REPAIR PAINTING <50%") | Paint | Additional Operations ✓ |
| `paint_blend` | Paint (with "SURFACE PAINT") | Blend | Additional Operations ✓ |
| `paint_prep` | Paint (with "PREPARATION FOR PRE-PAINTING") | Paint | Additional Operations ✓ |
| `new_part` | Parts | New | Main New Parts ✓ |
| `specialist_fixed` | Extras | Specialist | Additional Operations ✓ |
| `specialist_wu` | Extras (price = WU/10 × rate) | Specialist | Additional Operations ✓ |

---

## The big trap — `specialist_wu` vs `rnr`

The original AudaPad workflow has engineers entering items like "QC AND ROAD TEST 10 WU" as Specialist labour in the Labour table. EVA classifies that as R&R and **hides** it. Fix: put these in the Extras table as `specialist_wu`.

Items that **MUST** be `specialist_wu`, never `rnr`:

- Pre Repair Clean
- Wash And Vacuum
- Pre Repair System Diagnostic Check
- Post Repair System Diagnostic Check
- Standard Vehicle Shutdown
- QC And Road Test
- Personal Belongings Removal
- Specialist Valet
- Yard Charge
- Older Vehicle Allowance
- Corrosion Protection Labour
- Power Down PHEV Vehicle
- EV/Hybrid Risk Management

---

## Row-description discipline

EVA/Audatex-style rows should read like bodyshop estimate lines, not explanatory prose.

- Use concise component descriptions: `LHF DOOR`, `FRONT BUMPER`, `RHR QUARTER PANEL`.
- Do not use parentheses, causal clauses, "presumed", "likely", "possible", or "allow for" in row
  descriptions.
- Do not put evidence reasoning in `desc`; keep assumptions and caveats in the chat summary.
- Use consistent side prefixes: LHF/RHF/LHR/RHR, nearside/offside, front/rear.
- Let the `type` supply the operation prefix. Do not write `REPAIR REPAIR DOOR` or duplicate
  `CHECK`/`SURFACE PAINT` in the description.
- Continuation lines are for short fitment scope only, not narrative justification.

## Paint and blend routing

- Add a paint operation for every renewed or repaired painted panel.
- Use `paint_blend` for adjacent panels where colour transition, metallic/pearl finish, panel
  position, or manufacturer repair method makes blending defensible.
- Use `paint_prep` once where the job needs pre-paint preparation time across the repair.
- Do not hide blend reasoning in the row text. If challenged, use manufacturer/paint-method
  evidence and `ce-house-style` for any external response.

## Field reference per operation type

```python
# repair / rnr / check_labour
{'type': 'repair', 'guide': '1481', 'wu': 15.0, 'desc': 'LEFT FRONT DOOR'}
# desc is auto-uppercased; 'repair' gets "REPAIR " prefix, 'check_labour' gets "CHECK " prefix

# paint types
{'type': 'paint_new',    'guide': '221',  'wu': 4.0,  'desc': 'FRONT BUMPER'}
{'type': 'paint_repair', 'guide': '222',  'wu': 6.0,  'desc': 'LEFT DOOR'}
{'type': 'paint_blend',  'guide': '223',  'wu': 3.0,  'desc': 'REAR QUARTER'}
{'type': 'paint_prep',   'guide': '',     'wu': 12.0}  # no desc or guide needed

# new_part — set unpriced: True on any part without verified catalogue data
{'type': 'new_part', 'guide': '410', 'desc': 'FRONT BUMPER', 'part_num': '1234567',
 'price': 350.00, 'unpriced': True}

# specialist_fixed
{'type': 'specialist_fixed', 'desc': 'ASSESSMENT FEE', 'price': 176.96}

# specialist_wu — price computed automatically: WU/10 × labour_rate
{'type': 'specialist_wu', 'desc': 'QC AND ROAD TEST', 'wu': 10}

# continuations — extra description lines for the same item
{'type': 'rnr', 'guide': '111', 'wu': 18.0, 'desc': 'R + R WINDSCREEN',
 'continuations': ['INCLUDES: R + R COWL TRIM, WIPER ARMS', 'AND A-PILLAR TRIMS']}
```

---

## Unpriced parts

Set `'unpriced': True` on any part where you don't have a verified catalogue part number and price. This adds a `*` to the price in the PDF — the convention real Audatex uses for manually-added items. **Almost all your part prices will be estimates, so almost all parts should be `unpriced: True`.**
