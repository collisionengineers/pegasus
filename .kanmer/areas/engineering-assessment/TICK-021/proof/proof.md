# Proof — TICK-021 (EXT-02)

Type: command-log. Released in **release 14** (`d91fd7d7…`, PR #448), production smoke passed 2026-08-20; promoted to `main` (`39bb118a`).

- Verification lane at the cut: `VehicleMileageEvidenceClass { Supplied, External, Estimated }` with the Core classifier (lookup-derived → Estimated, never Supplied — pinned both ways by tests); MOT chronology table newest-first with External-classified mileage; per-source classification words through `OperatorLabels.MileageEvidence`; the policy abstains on conflicting readings (never invents mileage).
- Copy follow-through: the one found defect (raw `VehicleMileageUnit` enum render) was fixed in this release's copy pass (PR #472, `OperatorLabels.MileageUnit`).
- Live: renders on the deployed case workspace once a vehicle observation exists.
- Full transcript: DELIV-013 scratch.
