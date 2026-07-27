# Structured Intake, Builder, and Claimant-Report Analyser

Use this reference before drafting a diminution rebuttal. It does not replace expert judgement; it
forces every attack line to be consciously included, excluded, or held pending evidence.

## Intake file

Create `diminution_intake.json` using `references/diminution_intake.schema.json`.

Required blocks:

- `ce_role` — what Collision Engineers is doing in this case.
- `output_mode` — formal rebuttal, solicitor advice, insurer response, or Part 35 addendum.
- `vehicle` — registration, make/model, variant/body style if material, and market segment.
- `claimant_report` — assessor, claimed figure, PAV/repair figures where present, formula/method,
  comparable count, and procedural defects.
- `evidence` — documents/photos reviewed, inspection evidence, paint-depth evidence, estimate vs
  invoice reconciliation, prior-history evidence, and any market-value support.
- `attack_line_assessments` — one entry for each attack line ID `1` to `14`.

Validate before drafting:

```bash
python scripts/validate_diminution_intake.py diminution_intake.json
```

## Claimant-report analyser pass

Before filling the intake, read the claimant's diminution report and extract:

- Claimed diminution amount.
- PAV and repair cost used by the claimant.
- Repair-cost/PAV percentage and the stigma band or formula step applied.
- Market multiplier / comparable count.
- Whether the report is desktop-only.
- Whether the underlying repair and diminution reports come from the same firm.
- Whether the statement of truth is signed.
- Any floating-point or arithmetic artefact.
- Whether the report admits zero accident-repaired comparables or thin market evidence.
- Vehicle variant, market segment, mileage, prior-history assertions, and paint-depth reliance.

If any item is absent, write `unknown` or a short note. Do not fabricate inputs to make an attack
line fit.

## Structured builder pass

After validation, build the draft from `attack_line_assessments`:

- Include every entry with `status: "include"` unless later evidence disproves it.
- Exclude entries with `status: "exclude"` and keep the rationale internal.
- For `status: "needs_evidence"`, either ask for the missing evidence if it is material or omit the
  point and explain the limitation internally.
- Always check line 9, the ABI 20% benchmark. It is normally included in every third-party
  formula rebuttal.
- Use `references/structure.md` for the chosen output mode and `ce-house-style` for the final
  voice/lint pass.

## Rendering

Formal reports and addenda render through the `collisionrenderer` connector with
`templateId: diminution-rebuttal` — payload mapping and sign-off rules are in
`references/structure.md`. There is no other render path.
