# Proof — TICK-020 (EXT-01)

Type: command-log. Released in **release 14** (`d91fd7d7…`, PR #450), production smoke passed 2026-08-20; promoted to `main` (`39bb118a`).

- Verification lane at the cut: `VehicleLookupAvailability.ProductionLive` composed on the Web's production branch (only two runtime profiles exist, so the else-branch is genuinely production; no feature flag); availability gates `VehicleWorkflow` (fail-closed exception removed from the live path); make/model/year/engine/fuel render with MOT/mileage accept-correct forms; worker chain `ExternalWorkFunction` → `ProcessQueuedVehicleLookup` → `DvlaDvsaProductionAdapter` with keys resolved from the production vault (release-12 evidence).
- Live: "Check vehicle history" surfaces on the deployed assessment/case pages; a real DVLA/DVSA lookup fires on first operator use (no production lookup was spent by the release).
- Full transcript: DELIV-013 scratch.
