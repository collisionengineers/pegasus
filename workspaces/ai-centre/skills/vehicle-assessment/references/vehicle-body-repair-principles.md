# Vehicle Body Repair Principles

The CE damage model: how to reason from photographs to a defensible repair position without
overclaiming. This is assessment logic, not a repair manual — current manufacturer methods govern
any actual repair operation.

## The damage model

Work in this order:

1. **Contact damage first.** Identify where the impact object met the vehicle: the scuffed,
   dented, torn, or crushed surfaces directly evidenced in the photos.
2. **Then transferred damage along the impact path.** Impact energy travels. From the contact
   point, reason where the load went: lamp carriers, crash reinforcements, chassis leg ends,
   subframes, suspension pickups, sills, pillars, floor. Flag each as `reasonable inference` or
   `requires further evidence` — not as fact.
3. **Keep visible damage separate from concealed damage.** The photographs support outer-panel
   damage; they do not prove the condition of the underlying reinforcement, mounting points, or
   alignment. Say which is which.

Standard wording: *"The visible contact damage is concentrated around the [zone]. The impact
path also raises a risk of transferred load into [components]. That cannot be confirmed or
excluded without strip and measurement evidence."*

## Evidence signals

Read these as system evidence, not cosmetic detail:

- Panel gaps and shut lines — side-to-side comparison; a gap that differs from its mirror side
  needs a cause.
- Closure fit — doors, bonnet, boot, tailgate that catch, stand proud, or misalign.
- Glass — cracked glazing away from the contact point suggests body flex or aperture distortion.
- Lamps and trim — displaced or stressed fittings show where load travelled.
- Wheel position and stance — a wheel set back, toed out, or at odd camber is geometry evidence.
- Tyres — sidewall damage, exposed cords, abnormal wear (see `post-impact-system-checks.md`).
- Underbody marks — scrapes, fresh deformation, disturbed corrosion protection.
- Interior movement — seats, trim, or carpets displaced; airbag/pretensioner state.
- Fluid loss — identify the fluid, its source, and the affected system before movement or road
  test.

## Body construction awareness

Decide what kind of structure took the load before judging severity:

- **Unitary (monocoque)** — panels share structural duty; energy-management zones are designed
  to deform. Decide whether damage is limited to replaceable outer panels, whether a crumple
  zone has operated, or whether load reached structural members, suspension mounts, subframes,
  pillars, sills, or the occupant cell. Do not classify deformation by panel appearance alone.
- **Separate chassis / body-on-frame** — body damage and chassis alignment are separate
  questions; check both.
- **Subframes** — a displaced subframe moves everything mounted to it; geometry evidence
  required.
- **Space-frame / aluminium-intensive** — material and joining constraints govern; see
  `material-and-joining-cautions.md`.

## Repair sequence logic

- Structure first, then bolt-on panels and finish. Cosmetic panel replacement does not answer
  whether the underlying load path is within tolerance.
- Confirm suspension pickup points, datum points, wheel geometry, and underbody condition before
  cosmetic repair is priced as the whole job.
- Supplementary damage found at strip is a separate review item — recorded as new evidence, not
  assumed into the desktop scope.
- Refit and check closures, glass, trim, seals, lamps, and sensors before final quality checks;
  the assessment should account for MET operations where the damage path affects them (see
  `adas-ev-hv-prompts.md`).

## Panel gap and aperture checks

Where panel fit is off, identify the likely cause before writing the operation:

- **Adjustment** — bolt-on panel out of adjustment; realign and confirm.
- **Panel deformation** — the panel itself is distorted; repair/renew judgement.
- **Hinge/latch movement** — hinges, strikers, or latches shifted or bent.
- **Structural displacement** — the aperture itself has moved; geometry evidence required and
  the job changes character (see `structural-and-alignment-evidence.md`).

Check door, bonnet, boot, tailgate, and wing fit; glass apertures and windscreen fit; latch and
hinge movement; seal compression and water/air leak risk.

## Measurement comparison prompts

Where measurements exist or are requested:

- Record the reference (manufacturer or recognised data), the observed value, the variance, and
  its significance.
- State whether the variance can be adjusted out or indicates distorted structure or running
  gear.
- Do not infer structural alignment from cosmetic panel fit alone; confirm with datum, diagonal,
  aperture, wheelbase, or geometry evidence where relevant.
