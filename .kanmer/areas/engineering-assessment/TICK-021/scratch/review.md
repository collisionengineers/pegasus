## Independent review — PR #448 (orchestrator, 2026-08-20)

Verdict: **pass**, merge on green CI.

- The classification the capability requires now exists as one Core-owned vocabulary (`VehicleMileageEvidenceClass {Supplied, External, Estimated}`) with the classification rule beside `VehicleMileagePolicy` — staff-attributed → Supplied, MOT odometer reading → External, policy-derived → Estimated — and the operator-truth rule (docs/operator-notes.md:244, derived estimate never relabelled supplied) pinned by dedicated tests in both directions.
- MOT chronology renders as a dated data table (test date / result / expiry / mileage labelled External) in the case partials; every mileage figure carries its class via `OperatorLabels.MileageEvidence` — one label owner, no narration.
- Proportional diff: 5 files, reusing the stored MotTestsJson observations; no adapter or workflow change needed.
