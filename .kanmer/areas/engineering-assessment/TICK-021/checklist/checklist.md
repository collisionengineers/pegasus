# EXT-02 checklist

- [ ] Core: `VehicleMileageEvidenceClass` + `VehicleMileageEvidenceClassification.Classify` in `VehicleMileagePolicy.cs`
- [ ] Core tests: VehicleLookup → Estimated (never Supplied); other kinds → Supplied; accept-resolution proposes the derived calculation
- [ ] `_CaseWorkflow.cshtml`: MOT chronology table (date/status/expiry/mileage, newest first)
- [ ] `_CaseWorkflow.cshtml`: estimated mileage row on latest observation, labelled Estimated
- [ ] `_CaseWorkflow.cshtml`: confirmed mileage + facts-row mileage carry classification
- [ ] `_CaseSummary.cshtml`: Odometer row carries classification
- [ ] `dotnet build -c Release` zero warnings
- [ ] Focused `dotnet test` Vehicle filter green
- [ ] Simplification pass recorded in plan
- [ ] PR opened against dev; ticket → review with post-implementation-report
