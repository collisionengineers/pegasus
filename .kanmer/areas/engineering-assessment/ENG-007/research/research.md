# Question

Do Case Details and Assessment use the same shared-data retrieval boundary?

# Findings

- Case Details uses `IGetCase`, whose `CaseDetails` result contains canonical `Case.Data` and `Case.VehicleEvidence`.
- Assessment also calls `IGetCase`, but then directly calls `IVehicleEvidenceQueries.GetAsync` again and stores a second copy.
- Assessment's Web-layer helpers choose saved assessment, then confirmed lookup evidence, then latest observation. They omit the extracted Fact tier already present in `Case.Data.Vehicle`.
- `CaseField<T>` owns the canonical hierarchy `Confirmed ?? Fact ?? Suggestion`; automatic lookup observations do not mutate case fields.
- CASE-008 introduced both the duplicate query and the incomplete helper tests.
- The separate Assessment route is intentional and is the accepted report-draft entry point.

# Implication

Keep both routes. Remove only the duplicate query and resolve prefills from `Case.Data.Vehicle` plus `Case.VehicleEvidence`, excluding suggestions from accepted prefill precedence.
