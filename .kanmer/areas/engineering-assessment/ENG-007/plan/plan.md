# Approach

Use the already-composed CaseDetails object as the sole shared case/vehicle input to the Assessment page. Keep IGetCaseAssessment for assessment-owned data. Implement small page-local selection helpers over existing CaseField tiers because this is one presentation caller and a new abstraction would violate the repository simplicity rule.

# Steps

1. Remove IVehicleEvidenceQueries from the Assessment page constructor, property state, and GET handler.
2. Resolve vehicle make/model from saved assessment, then Case.Data.Vehicle Confirmed, Fact, then Case.VehicleEvidence latest observation.
3. Resolve mileage as one value/unit/source selection: saved assessment first; accepted Confirmed/Fact case mileage next; lookup mileage last. Exclude Suggestion. Select online_data only for CaseDataSourceKind.VehicleLookup or lookup observation; otherwise retain the saved source or leave it unselected.
4. Keep lookup-only year, engine capacity, and fuel fallback because no canonical CaseData fields exist for them.
5. Rewrite the integration fake to attach Data and VehicleEvidence to CaseDetails, and add precedence/provenance cases.
6. Run focused tests, Release build, full non-corpus tests, and the required simplification pass.

# Governing docs

- FRD-06: accepted instruction evidence is not displaced by unaccepted external evidence.
- FRD-12: preserves the separate Assessment surface without introducing explanatory UI or duplication.

# Risks and mitigations

- Mixed mileage provenance: compute value/unit/source together.
- Suggestion accidentally treated as accepted: address Confirmed and Fact explicitly rather than using Current.
- Existing lookup-only fields regress: retain observation fallback for fields absent from CaseData.
- Over-abstraction: keep the resolver on this single presentation boundary.

## Simplification pass — 2026-08-21

Reuse: removed the second vehicle-evidence query and reused IGetCase/CaseDetails. Simplification: kept selection page-local because there is one caller. Efficiency: one fewer database query per Assessment GET. Altitude: preserved route and assessment-owned boundary. Finding applied: mileage units are compared case-insensitively because intake stores `miles` while lookup confirmation stores `Miles`.
