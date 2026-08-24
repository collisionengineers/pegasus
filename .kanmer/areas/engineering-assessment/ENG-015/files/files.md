# Files

| File | Why |
| --- | --- |
| `src/Pegasus.Infrastructure/Persistence/EvaHandoffStore.cs` | (a) `Reference` reads `Claim.Number` instead of the hard-wired case reference; (d) `Mileage Unit` emits `Miles`/`Km`; (e) the case-field `Vehicle Model` fallback prepends the make. |
| `src/Pegasus.Core/Eva/CaseEvaMapping.cs` | (b) the 6-line `Inspection Address` shape, the EVA-facing `Image-based Assessment` export literal, and the named exemption from `NormalizeValue`'s `Trim()`. |
| `src/Pegasus.Core/Intake/InstructionFieldExtraction.cs` | (g) one new per-field policy member on `FieldDefinition`, honoured in `ResolveConflictingCandidates`, plus the docstring that currently states earliest-wins unconditionally. |
| `src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosInstructionExtractionPolicy.cs` | (f) the bare `Date` label with its `AcceptsValue` discovery filter; (g) sets the new member on `Inspection date`; (c) appends the labelled damage area to the extracted circumstances. |
| `src/Pegasus.Core/Cases/CaseDataOperations.cs` | (c) `AccidentCircumstances` keeps its blank line — `CaseDataPolicy.Text` collapses every whitespace run, so the stored value is single-line today. Named exemption, that field only. |
| `tests/Pegasus.Core.Tests/Intake/Qdos/QdosInstructionExtractionPolicyTests.cs` | (c)(f)(g) extraction tests, including the (f) regression guard and the (g) two-fragment precedence test. |
| `tests/Pegasus.Core.Tests/Qdos/QdosBoundaryContractTests.cs` | (b) the 6-line address, and the pinning tests for blank `VAT Status` and lookup-derived `Mileage`. |
| `tests/Pegasus.Core.Tests/Cases/CaseDataOperationsTests.cs` | (c) the circumstances multi-line exemption, and that other text fields still collapse. |

## Not touched, deliberately

- `src/Pegasus.Core/Eva/EvaBundleSchema.cs` — packaging and serializer options are [[ENG-014]]'s.
- `src/Pegasus.Core/Address/Ext18InspectionAddressPolicy.cs` — the intake-side literal stays `Image Based Assessment`; only the EVA-facing value changes.
- `docs/frd/frd-07-eva-and-external-engineering-handoff.md` — see the plan's "Governing docs" note.
