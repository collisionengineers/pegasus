# Files

Two commits: `df6d3b66` (the DVSA parse defect) and `e4ce8e3b` (kilometres to miles).

| File | Change | Reuses |
| --- | --- | --- |
| `src/Pegasus.Infrastructure/Vehicle/DvlaDvsaProductionAdapter.cs` | `ParseProviderDate` reads a date **or** an instant, applied to `completedDate` and `expiryDate`; reading none of the tests offered is now a `dvsa_unreadable_tests` failure rather than silence | the existing adapter failure taxonomy |
| `src/Pegasus.Core/Vehicle/VehicleMileagePolicy.cs` | `ToMiles` at 1.609344 km/mile, rounded to the nearest whole mile; observations compared after conversion; always reports `Miles`; method version 1 → 2 | the existing policy and its comparison |
| `tests/Pegasus.IntegrationTests/ProductionVehicleLookupTests.cs` | The live DVSA response verbatim — kilometres, full-instant `completedDate` and all | existing production lookup harness |
| `tests/Pegasus.Core.Tests/Vehicle/VehicleWorkflowTests.cs` | Conversion, rounding, and one reading recorded in both units agreeing with itself | existing policy tests |

## Not changed

Raw observations keep their own units, as FRD-06 requires. Only the derived value is
normalised.
