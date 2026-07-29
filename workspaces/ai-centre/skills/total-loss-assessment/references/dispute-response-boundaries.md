# Dispute Response Boundaries

> **Source-workspace boundary:** This file is package-local evidence or an experiment only; it is not a Pegasus caller, settlement/legal policy, current instruction, or acceptance authority. `Pegasus.Core`, current operator authority, and an authorised human own every accepted fact, outcome, response, and approval.


Use this when the user asks for an external response about a repair estimate, total-loss position,
ABP charge, storage/yard charge, PPE/consumable item, or repair-specification challenge.

## Evidence gate

Check the underlying CE report, estimate lines challenged, damage photos, PAV/repair-cost ratio,
repairability status, storage dates, vehicle mobility/security, and any contrary engineering
evidence supplied by the opponent.

Do not defend a line item merely because it appears in a standard package. Remove or qualify any
charge that is unsupported, duplicated, stale, or not applicable to the vehicle condition.

## Repairable vehicle challenged as total loss

- Separate repair economics from salvage category.
- State the repair/PAV percentage from the report and why the repair remains economically viable.
- If repair costs are agreed on a fixed or contract basis, state that only where the report/evidence
  confirms it.
- Do not present a client settlement threshold or policy cap as an engineering defect.

## Vehicle alleged to be already total loss

- Ask for documentary evidence of any prior total-loss marker or category.
- Do not accept a prior total-loss proposition from assertion alone.
- If a marker is proven, use `vehicle-valuation` for value effect and `salvage-categorisation` for
  category implications.

## Recognise the standard TPI challenge patterns

Insurer cost-challenge letters (Verisk-style desktop reviews are typical) recur on four angles.
Check each against the actual assessment before responding:

- **Duplicated operations** — the same panel appearing as both renewed and repaired. If genuinely
  duplicated, correct it rather than defend it; if the lines are for different operations (e.g.
  renew skin + repair aperture), explain the distinct scopes.
- **Parts-reuse pushback** — assertions that undamaged sensors, brackets, or trims can be reused.
  Answer from the damage evidence: single-use clips, deformed mounts, calibration consequences, or
  manufacturer method. Concede reuse where it is genuinely supported.
- **Labour-rate challenge** — answer with the current ABP retail/non-contract basis and the vehicle
  class (standard vs prestige/aluminium), per `labour-rates.md`.
- **"The vehicle is roadworthy / would pass an MOT"** — roadworthiness is not the repair standard.
  The claimant is entitled to restoration to pre-accident condition; an MOT pass does not answer
  panel distortion, finish, or corrosion-protection scope.

## ABP, retail repair, PPE, environmental, storage, or yard charge challenge

- Explain the operation or condition supporting each challenged item.
- Use current ABP retail/non-contract charge guidance only for current-rate support.
- Treat ABP as a pricing benchmark, not as an exhaustive list of every process consumable that may
  be required.
- For PPE, masking products, mixing cups, abrasives, and refinishing consumables, check that the
  item is required for safe/compliant refinishing and is not duplicated in paint/materials.
- For yard charge, explain internal vehicle movement or handling separately from manufacturer repair
  time, and defend it only where the assessment context supports it.
- For storage, defend only where the vehicle was non-driveable, insecure, airbag-deployed, had
  glazing out, had exposed HV/fire/water risk, or was held pending a category/repair decision.
- Do not use date-sensitive energy or supplier-cost notes as a standing justification unless current
  ABP/supplier evidence supports the charge.

## External wording

Draft through `ce-house-style` after the technical position is set. Keep the response factual:
what was reviewed, which charge or operation is supported, why it is reasonable, and what specific
contrary evidence would be needed to revisit the position.
