# Change files

| Area | Purpose and risk |
| --- | --- |
| `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml.cs` | Remove duplicate dependency/query and centralize page prefill selection over the composed Case projection. |
| `tests/Pegasus.IntegrationTests/AssessmentVehiclePrefillWebTests.cs` | Cover all precedence and provenance cases through `CaseDetails`. |

# Context files

| File | Why read it |
| --- | --- |
| `src/Pegasus.Core/Cases/CaseQueries.cs` | `IGetCase` already composes Data and VehicleEvidence. |
| `src/Pegasus.Core/Cases/CaseDataContracts.cs` | Canonical value tiers and provenance. |
| `src/Pegasus.Core/Assessment/AssessmentContracts.cs` | Assessment-owned fields remain separate. |
| `docs/frd/frd-06-vehicle-and-engineering-evidence.md` | Accepted evidence hierarchy. |

# Out of scope

No route merge, new public query interface, persistence mutation, report contract change, or UI redesign.
