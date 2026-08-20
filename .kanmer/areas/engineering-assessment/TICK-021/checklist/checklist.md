# EXT-02 checklist

- [x] Core: `VehicleMileageEvidenceClass` + `VehicleMileageEvidenceClassification.Classify` in `VehicleMileagePolicy.cs`
- [x] Core tests: VehicleLookup → Estimated (never Supplied); other kinds → Supplied; accept-resolution proposes the derived calculation
- [x] `_CaseWorkflow.cshtml`: MOT chronology table (date/status/expiry/mileage, newest first)
- [x] `_CaseWorkflow.cshtml`: estimated mileage row on latest observation, labelled Estimated
- [x] `_CaseWorkflow.cshtml`: confirmed mileage + facts-row mileage carry classification
- [x] `_CaseSummary.cshtml`: Odometer row carries classification
- [x] `dotnet build -c Release` zero warnings
- [x] Focused `dotnet test` Vehicle filter green (23/23; full Core suite 703/703; architecture 97/97)
- [x] Simplification pass recorded in plan
- [x] PR #448 opened against dev; ticket → review with post-implementation-report
