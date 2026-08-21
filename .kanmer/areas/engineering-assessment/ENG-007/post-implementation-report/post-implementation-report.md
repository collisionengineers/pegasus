# Changes

- Removed Assessment's direct IVehicleEvidenceQueries dependency and duplicate GET.
- Reused CaseDetails.Data and CaseDetails.VehicleEvidence.
- Added explicit saved/Confirmed/Fact/lookup precedence, excluded Suggestion, and kept mileage provenance coherent.
- Updated the web integration fake and added extracted-fact disagreement coverage.

# Governing docs

FRD-06 is met by protecting accepted instruction facts from unaccepted lookup observations. FRD-12 is met without route or UI expansion.

# Verification

Release build passed; focused Assessment tests passed 2/2; serial Core tests passed 867/867. The concurrent three-worktree full run was invalidated by unrelated regex and LocalDB resource timeouts.

# Review focus

Check mileage tier/unit/source pairing and confirm no Assessment route or save behavior changed.

# Verify on merged main

Run Release build, AssessmentVehiclePrefillWebTests, and the full non-corpus profile serially.
