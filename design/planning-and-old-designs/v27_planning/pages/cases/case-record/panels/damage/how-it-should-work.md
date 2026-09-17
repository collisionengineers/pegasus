# Damage panel — how it should work

Proposed 16 September 2026; nothing decided. The operator's requirement:
*"we dont want to select specific panels/sections - we want to be able to
click anywhere on the vehicle to indicate the damage"*. Four variants are on
the mockup's **Damage selector** strip variable (`clicker=`); the full
description and the sign-off items G–G3 are in
[`../../../../../current/v27-notes.md`](../../../../../current/v27-notes.md) § 11.

Draft rules, one per variant, to be kept for the chosen one and deleted for
the rest:

- **D1 (A · Pins).** Damage is recorded as points on the plan silhouette: a
  click anywhere on the vehicle drops a numbered pin, a pin can be dragged,
  and each pin carries a severity and a note.
- **D1 (B · Brush).** Damage is recorded as strokes drawn over the plan
  silhouette; one stroke is one recorded damage with a severity and a note.
- **D1 (C · Areas).** Damage is recorded as circular areas on the plan
  silhouette, pressed at the centre and dragged to size; each area carries a
  severity and a note.
- **D1 (D · Impact arrows).** Damage is recorded as impact points with a
  direction of force: pressed where the vehicle was struck and dragged in the
  direction of the impact; the arrow's length sets the severity, which stays
  editable.
- **D2 (all).** No panel is chosen or recorded. A mark records the area or
  areas of the vehicle it sits in, from the list Front, LH Front, LH Rear,
  LH Side, Rear, RH Front, RH Rear, RH Side (Core's eight broad zones);
  `impact_location`, `impact_severity` and the incident narrative derive from
  those areas.
- **D3 (all).** A click outside the silhouette records nothing; reading the
  record never places a mark.

Open: which variant (G).
Open: whether the mark's point is stored with the derived panels, or only
the panels (G1).
Open: one mark across several panels — one damage with several zones, or one
per panel (G2).
Open: for D, whether direction of impact becomes a recorded fact (G3).
Open: the roof — nearer side, or its own area (G4).
Open: LH / RH as the operator words for the eight areas (G5).
Open: whether Underside, Interior and Mechanical stay beside the eight (G6).
Open: "Recorded areas" as the list heading (G7).

## Where this lands

| Page | Entry |
| --- | --- |
| Case record › Damage | the selector and the recorded list |
| Report (FRD-11 marked diagram) | the diagram prints the marks, or the derived panels, per G1 |

## Documentation impact when the FRD is written

- FRD-06 § Damage record: the record's unit becomes the eight broad areas
  (already `BroadDamageZones`) with the mark's point beside them or not (G1);
  the 23 detailed regions stop being entered.
- FRD-12 § Case workspace: the Plan clicker sentence names the chosen
  gesture.
- FRD-11 § Assessment-report outcomes: what the marked diagram prints.
