# Post-Impact System Checks

Mechanical and safety-system escalation after impact, in cause-chain form:

`impact evidence → likely load path → system risk → required evidence → assessment consequence`

Use with `post-impact-escalation-matrix.v1.json` (structured zones) — this file gives the
reasoning; the matrix gives the deterministic zone→system map. Two general rules apply
everywhere:

- **Fluid loss after impact is system evidence.** Identify the fluid, source and affected system
  before movement or road test.
- **A warning lamp, damaged loom or sensor fault is impact evidence** until diagnostic results
  show otherwise — not a "pre-existing fault".

## Steering, suspension and alignment

The highest-value mechanical check. Steering and suspension are one directional-control system;
a shock load anywhere in it travels.

- A front-corner impact can load the wheel, steering link, suspension arm, strut, hub and
  subframe. Geometry and component checks are needed before the damage can be treated as
  cosmetic.
- For any wheel, kerb, corner or suspension-zone impact, confirm: tyre condition, wheel run-out,
  hub/bearing play, ball joints, bushes, track rods and steering arms, strut/wishbone condition,
  subframe location, ride height, and four-wheel alignment (toe, camber, castor, SAI/KPI, thrust
  angle).
- Four-wheel alignment, not front tracking alone — a rear thrust-angle anomaly reads as rear
  structural/suspension evidence.
- A steering wheel off-centre with the wheels straight is geometry evidence in itself.
- ABP support: wheel alignment check / check-and-adjust-toe and steering angle reset lines in
  `extras-package.md`.

## Tyres and wheels

Read tyre evidence as mechanical evidence:

- Sidewall damage, bulges, exposed cords, or bead damage → renewal territory and an impact-load
  marker for the corner.
- Feathering → toe evidence. One-shoulder wear → camber/geometry evidence. Patchy/bald spots →
  steering, brake, damper or balance evidence.
- Rim run-out, cracked or bent rims, vibration reports → run-out measurement and hub checks.
- After a heavy corner impact, wheel replacement alone is not an answer — geometry and
  suspension-component evidence is still required.
- ABP support: alloy refurb (standard / diamond-cut), spare-wheel swap lines.

## Brakes and ABS

Where impact is near a wheel, brake line, suspension arm, underbody route or ABS sensor, request
brake-system evidence before treating the vehicle as roadworthy:

- Pipe/hose damage or chafing along the impact path; caliper and wheel-cylinder leakage.
- Disc/drum damage on the impacted corner.
- Brake fluid level and any fluid loss.
- ABS warning lamp and wheel-speed sensor wiring at the impacted corner.
- Reported pull/imbalance on braking.
- Diagnostic and bleeding requirements follow the manufacturer procedure.

## SRS and restraints

Any deployed airbag, triggered pretensioner, loaded seatbelt, SRS warning lamp, or impact near
restraint components (sensors, steering wheel, seats, pillars, restraint wiring) triggers
restraint-system inspection and diagnostic scan under manufacturer procedure.

- Airbags and pretensioners are one linked system — a deployment usually condemns more than the
  visible bag (belts, retractors, buckles, sensors, module, clock spring; headliner removal
  labour on curtain deployments).
- Seatbelts that were loaded in the impact need inspection even without deployment; locked
  retractors and damaged buckles are renewal evidence.
- An SRS lamp on a side-impact job almost certainly means deployment, not a pre-existing fault
  (see `gotchas.md`).

## Driveline and underbody

- Split CV boot or grease thrown around the arch → joint inspection.
- Shaft vibration, clunks, or joint play reports → driveline check before road use.
- Gearbox/transfer-box/diff leaks after impact → fluid-loss rule applies.
- Underbody scrapes near mounts, exhaust hangers, or fuel/brake line runs → lift inspection.

## Cooling and exhaust

- Coolant loss, radiator/condenser/fan damage, or a pushed-back cooling pack behind front-end
  contact → cooling-system evidence before any running of the engine.
- A/C condenser damage brings refrigerant handling into scope (A/C recharge line in
  `extras-package.md`).
- Exhaust mounts, flexible sections, catalyst and sensors near the impact or grounding path.

## Electrical

- Battery movement, cracked case, or electrolyte leakage.
- Harness chafing, stretched or crushed looms, connector damage along the impact path.
- Blown fuses, warning lamps, inoperative circuits → diagnostic scan evidence.
- Headlamp aim after any front-end repair.
- Diagnostic scans: pre- and post-repair system checks are standard ABP lines; scan reports are
  the evidence that closes these items.
