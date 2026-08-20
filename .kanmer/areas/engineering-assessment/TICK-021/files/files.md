# EXT-02 file map

## Modified

- `src/Pegasus.Core/Vehicle/VehicleMileagePolicy.cs` — add `VehicleMileageEvidenceClass` enum (Supplied, External, Estimated) and `VehicleMileageEvidenceClassification.Classify(CaseDataSourceKind)`; sits beside the calculation it classifies (one mileage concept owner).
- `src/Pegasus.Web/Pages/Cases/Shared/_CaseWorkflow.cshtml` — (a) MOT chronology data table under the latest observation; (b) derived-estimate mileage row labelled Estimated; (c) confirmed-mileage classification; (d) facts-table Mileage row classification suffix.
- `src/Pegasus.Web/Pages/Cases/Shared/_CaseSummary.cshtml` — Odometer row classification suffix via the existing `DataRow` format delegate.
- `tests/Pegasus.Core.Tests/Vehicle/VehicleWorkflowTests.cs` — new classification facts pinning the labelling rule (VehicleLookup → Estimated, never Supplied; all other kinds → Supplied) beside the existing mileage-policy tests.

## Read, unchanged (seams relied on)

- `src/Pegasus.Core/Vehicle/LookupContracts.cs` (`MotTestObservation`), `VehicleWorkflow.cs` (`VehicleLookupObservation.MotTests`, `ConfirmedVehicleEvidence`), `src/Pegasus.Core/Cases/CaseDataContracts.cs` (`CaseDataSourceKind`, `CaseField`), `src/Pegasus.Core/Cases/CaseQueries.cs` (`GetCase` attaches `Data` + `VehicleEvidence`), `src/Pegasus.Infrastructure/Persistence/EfVehicleWorkflowStore.cs` (accept path source kinds), `EfVehicleLookupWorkStore.cs` (`MotTestsJson` hydration), `src/Pegasus.Web/Presentation/OperatorLabels.cs`.
- No migrations, no Infrastructure changes, no Worker changes, no MCP changes.
