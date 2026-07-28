# Method Index Examples

Use these examples to check how `method-index.json` should be searched and cited. They are
sanitized fixtures, not evidence that a method is currently approved for a live repair.

## Example 1 - Tesla rear quarter query

Input context:

```text
Make: Tesla
Model: Model 3
Component: rear quarter
Dispute: insurer says sectioning should be removed as unnecessary
```

Expected lookup:

- Match `tesla-model-3-rear-quarter-structural` by make, model, component, and method terms.
- Read `source-verification-and-licensing.md` and `tesla-model-3.md` before drafting.
- Output a pointer-only statement: rear quarter and structural body work is method-dependent for
  the exact affected section, and current Tesla repair data must be checked.
- Do not quote procedure steps, dimensions, diagrams, or joining instructions.

## Example 2 - Tesla door, lamp, parts, or tyre query

Input context:

```text
Make: Tesla
Model: Model 3
Component: front door / tyre pair
Dispute: repairer says the item was specified because Tesla source material requires verification
```

Expected lookup:

- Match `tesla-model-3-doors-wing-lamp-parts` for door, wing, tail-lamp seal, or parts queries.
- Match `tesla-model-3-tyre-pair-replacement-parts` for tyre-pair or replacement-parts queries.
- Read `source-verification-and-licensing.md` and `tesla-model-3.md` before drafting.
- Output a pointer-only statement that current Tesla repair data must be checked for the exact
  component and issue; do not quote a procedure or state the live source outcome as fact.

## Example 3 - Toyota Corolla blend query

Input context:

```text
Make: Toyota
Model: Corolla
Year: 2021
Component: rear panel / adjacent panel paint
Dispute: blend line challenged
```

Expected lookup:

- Match `toyota-corolla-2019-2022-rear-blend`.
- Treat the year range as pointer scope only; verify exact vehicle and paint/method source before
  external reliance.
- Output a guarded blend pointer for the specific panel relationship, not a general rule that
  blending is always required.

## Example 4 - Brand-level wheel query

Input context:

```text
Make: BMW
Model: not supplied
Component: diamond-cut alloy
Dispute: replacement challenged, refurbishment proposed
```

Expected lookup:

- Match `bmw-alloy-wheel-refurbishment`, but mark the model and wheel specification as missing.
- Ask for or verify exact model, wheel type, finish, damage, and current source before relying on
  replacement/refurbishment wording.
- Do not state that BMW always requires replacement or always allows refurbishment.
