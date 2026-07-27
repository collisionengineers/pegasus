# Damage Cataloguing

## What to look for when walking through photos

Walk through **every photo**. For each visible damage point, note:
- Which panel
- Severity: scuff / dent / bent / torn / destroyed
- Decision: repairable or renewal

## Side determination checklist

Side errors are high-impact because they make the assessment look careless. Before writing
operations:

- Establish whether the image is front-facing or rear-facing; left/right in the photo may reverse.
- Use vehicle convention first: nearside = UK kerb/passenger side, offside = driver/traffic side.
- Cross-check with visible steering wheel, fuel-filler flap, badges, number plate orientation, and
  image sequence.
- If the same damage appears from multiple angles, reconcile them before naming the side.
- If side remains genuinely uncertain, state the assumption in the chat summary and use neutral
  wording only where the engineer can resolve it.

## Non-obvious damage — watch for these

| Signal | Implication |
|---|---|
| Airbag warning light on dash | SRS deployed → mandatory full diagnostic + likely belt/airbag renewals + headliner removal labour |
| Yellow seatbelt tags | Pre-tensioners fired → renew belts |
| Wheel kerb damage | Alloy refurb extra per kerbed wheel |
| Premium car mirrors with surround-view cameras | Expensive (£1,200+) full assemblies — do not assume cap-only |
| Fuel filler door damage near a quarter panel | Quarter panel work needed |
| Rear screen smashed | Vehicle not roadworthy → storage charge justified |
| Fluid leaks visible | Radiator / coolant / power steering — flag and include where relevant |
| Damaged underbody visible | Subframe / crossmember check at minimum |

## Renewal vs repair decision

- **Renew:** panel destroyed; creased structural panel; panel with multiple deformations; panel where repair cost exceeds part cost
- **Repair:** dented but structurally sound; minor deformation on bolt-on panel; no crease through metal

On borderline cases, state the assumption and invite engineer to override.

## Repair-vs-replace triggers

Prefer renewal, or at least explicitly flag the judgement, where any of these appear:

- Sharp crease through a body line or swage line.
- Torn metal, split plastic, cracked bumper corner, or failed mounting lug.
- Distorted wheel arch lip, shut line, aperture edge, or bonded/welded structural edge.
- Broken lamp, radar, parking sensor, camera, mirror assembly, bracket, grille tab, or hidden mount.
- Door/bonnet/tailgate skin kinked enough that panel gaps or latch alignment are affected.
- Co-located fragile items beside the impact point, such as lamps, parking sensors, trims, arch
  liners, splash shields, undertrays, wheel-arch mouldings, and clips.
- Airbag, pretensioner, SRS, suspension, steering, cooling, or HV warning evidence near the impact.

Do not describe a panel as repairable merely because damage is cosmetic from a distance. Close-up
photo evidence, material type, adjacent mounting points, and paint/blend consequences matter.

## Unroadworthy statement — the `notes` field phrase bank

Where the damage makes the vehicle unroadworthy, say so in the payload `notes` field using CE's
standard SOP pattern: **"Please note the vehicle is unroadworthy due to / as …"**, completed by the
dominant evidence:

| Evidence | Completion |
|---|---|
| Front-end panel distortion | `the extent of panel distortion sustained to the front end` |
| Side panel distortion | `the extent of panel distortion sustained to the offside/nearside [front/rear]` |
| Rear body compromised | `as the rear body has been compromised` |
| Broken / insecure bumper | `the broken / insecure front|rear bumper` (`punctured` where visible) |
| Impacted wheel/tyre | `the impacted wheel(s) and tyre(s)`; `misaligned wheel and tyre` where misalignment is the evidence |
| Suspension | `the damaged [rear] suspension`; owner-reported: `as we understand the suspension has dropped since the accident` / `the vehicle doesn't drive straight` |
| Bonnet | `the distorted bonnet` |
| Door / hinge | `the extent of panel distortion sustained to the door` / `the damaged door / hinges`; owner-reported: `as we understand the nearside/offside door does not operate correctly` |
| Broken lamp / mirror / glass | `the broken [headlamp / nearside rear lamp / offside door mirror / rear window / window regulator]` |
| Radiator leak | `as we understand the radiator is leaking` |
| Engine management light | `as the engine management light is illuminated` |
| ADAS fault | `as we understand the ADAS is not working correctly` |
| Motorcycle | `the broken / damaged frame` / `front forks` |
| Taxi / private hire | `…unroadworthy / unfit for purpose due to the damage sustained. We do not expect the vehicle to comply with the local licensing authority regulations.` |

Convention: damage visible in the photos is stated directly; owner-reported or unverified
mechanical symptoms are prefixed **"we understand"** — that keeps a desktop assessment honest. An
unroadworthy vehicle also normally justifies the storage charge (see `extras-package.md`).

## Panel identification

Use directional labels throughout: LHF (left hand front), RHR (right hand rear), nearside, offside. Don't use front/rear alone when side matters.

## Describing the vehicle

Always state in your reply:
1. Vehicle identified from: [plate / badge / VIN plate / instrument cluster / description]
2. VIN decoded as: [full make/model/derivative]
3. Any disagreement between badge and VIN (e.g. badge says X5M but VIN is X5 xDrive30d M Sport)
