---
name: manufacturer-methods-evidence
description: Provides Collision Engineers manufacturer repair-method evidence pointers for repair scope, wheel and tyre replacement, alloy refurbishment, blending, painted sensors, structural measurement, bench/setup, steering rack/tie-rod replacement, Tesla Model 3 methods, and OEM-method dispute responses. Use when the user asks whether a repair operation is manufacturer-supported, needs OEM evidence, challenges wheel/tyre/blend/sensor/structural/steering method lines, or wants safe wording based on manufacturer methods.
---

# Manufacturer Methods Evidence

## Overview

Use this skill to organise manufacturer-method evidence and produce safe, paraphrased decision
pointers. It does not replace current OEM repair data, Thatcham, or repairer method access, and it
must not reproduce copyrighted procedures, diagrams, dimensions, or step-by-step method text.

Use `total-loss-assessment` when the method affects the estimate. Use `ce-house-style` when drafting
an insurer/TPI response. Use `vehicle-valuation` only where method evidence affects
repairability, category, provenance, or value.

## Workflow

1. Identify make, model, year, variant, component, damage location, and the disputed operation.
2. Read `references/source-verification-and-licensing.md` before relying on any manufacturer-method
   point.
3. Check `references/method-index.json` for a maintained pointer keyed by make, model, component,
   method, and source family. Use `references/method-index-examples.md` for lookup examples. If no
   entry matches, say "no maintained pointer found" rather than inventing one.
4. Select the relevant reference:
   - `references/tesla-model-3.md`
   - `references/wheel-tyre-refurbishment.md`
   - `references/blending-and-paint-sensors.md`
   - `references/structural-measurement-and-bench.md`
5. State the paraphrased method point and its limits. Do not quote or recreate the source procedure.
6. Require current-source verification before an engineer relies on exact cut lines, measurements,
   joining methods, calibration steps, tolerances, tyre rules, or repair prohibitions.
7. If drafting externally, hand the distilled point to `ce-house-style` for response tone.

## Output

Provide:

- matching source pointer or "no maintained pointer found"
- component and model/year scope
- paraphrased method implication
- what must be verified in the current OEM/source material
- repair/valuation/external-response handoff

## Boundaries

- Do not generalise model-specific methods across all vehicles.
- Do not copy full OEM procedures, diagrams, dimensions, or cut locations.
- Do not treat a historical screenshot as current manufacturer instruction.
- Do not tell the user a method is definitively allowed/prohibited unless the current source is
  verified for the exact vehicle and repair section.

## References

- `references/source-verification-and-licensing.md` - copyright and verification rules
- `references/method-index.json` - structured sanitized pointer index
- `references/method-index-examples.md` - lookup examples and expected output behavior
- `references/tesla-model-3.md` - Tesla Model 3 source pointers
- `references/wheel-tyre-refurbishment.md` - wheel, tyre, alloy, and steering rack/tie-rod challenge pointers
- `references/blending-and-paint-sensors.md` - blend and painted sensor pointers
- `references/structural-measurement-and-bench.md` - structural setup and measurement pointers
