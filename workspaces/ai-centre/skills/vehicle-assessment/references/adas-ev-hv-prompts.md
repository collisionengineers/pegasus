# ADAS, EV/HV, And MET Prompts

Modern-vehicle scope that desktop assessments most often miss. These three areas share one rule:
current manufacturer procedure governs, and the assessment states what evidence is missing
rather than assuming the operation away.

## ADAS and calibration

Consider ADAS whenever the repair involves bumpers, grilles, windscreens, mirrors, lamps,
suspension, steering, wheel alignment or sensor mounting points. State whether calibration
evidence is missing.

Triggers — calibration may be required after:

- Sensor/camera/radar replacement, or removal and refit of the part carrying it.
- Bumper or grille repair/renewal over radar or parking sensors (paint build matters).
- Windscreen replacement (camera behind glass) or mirror replacement.
- Any wheel-alignment change, suspension or steering work, or ride-height change.
- Structural repair affecting sensor mounting geometry.

Evidence set: diagnostic scan (pre/post), whether calibration is required for the affected
system, calibration completion certificate/report, and the current method source.

First check the vehicle actually has the system: do not add ADAS calibration to cars without
ADAS (pre-2017 mainstream cars usually lack it — see `gotchas.md`), and do not add surround
view / blind spot / lane keep / ACC / parking calibrations unless the vehicle has that system
and the damage or repair could disturb it.

ABP support (`extras-package.md`): first and subsequent calibrations, steering angle reset,
dynamic-calibration second technician, and the extensive road test that applies to any
calibration on the job.

## EV / HV risk

Where EV/HV components may be within the impact path — battery, orange cables, charging
components, underbody, battery cooling, or HV warning lamps — **the assessment stops at risk
identification** until a competent technician and current manufacturer procedure confirm
isolation, make-safe, battery condition and repair permissions.

Prompts:

- Underbody or sill impact on a BEV/PHEV → battery-case inspection under manufacturer
  procedure before the vehicle is moved, charged, or repaired.
- Damaged HV system, thermal event risk, or water ingress → quarantine question (dedicated
  quarantine distance/procedure per manufacturer guidance).
- Do not double-count power-down time where the manufacturer repair method already includes it.
- Storage, movement, and charging decisions on a damaged EV are safety decisions, not logistics.

ABP support: EV/hybrid risk management, power-down, quarantine procedure, and recharge lines in
`extras-package.md` — each tied to its condition, not added by default.

## MET strip/refit scope

Mechanical, electrical and trim work is not incidental — it is part of the repair scope where
the damage path affects trim, lamps, sensors, wiring, glass, cooling, restraints or
calibration-related components.

- Strip/refit may expose hidden damage: state that supplementary findings at strip are a
  separate evidence stage.
- Check estimates for missing MET: lamp removal for wing work, bumper strip for sensor work,
  glass out for aperture repairs, interior trim for restraint or quarter work, cooling pack
  displacement for front-end work.
- An estimate with panel and paint lines but no MET on a sensor-carrying, lamp-adjacent repair
  is under-scoped — flag it.
