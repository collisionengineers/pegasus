# Files — ENG-004

## Change

- `src/Pegasus.Core/Intake/InstructionFieldExtraction.cs` — `InstructionFieldEngine`: label-position rule, segment-boundary value truncation, per-field candidate validator hook, `IsPlausibleVehicleMakeModel`.
- `src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosInstructionExtractionPolicy.cs` — wire the make/model validator onto the two `FieldDefinition` rows.
- `tests/Pegasus.Core.Tests/Intake/Qdos/QdosInstructionExtractionPolicyTests.cs` — red-first fixtures reproducing the flattened brake-table line shape from QDOS26002.

## Read (context, unchanged)

- `src/Pegasus.Core/Intake/IntakeContracts.cs` — `IntakeContentFragment`, `InstructionFieldCandidate`, `InstructionReviewField`, `InstructionDraft`.
- `src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosCaseMatchPolicy.cs` — only consumes `InstructionFieldEngine.ParseDate`; unaffected.
- `tests/Pegasus.Core.Tests/Intake/ProcessIntakeTests.cs` — pins behaviours that must not change: overlong values remain full candidates with null typed values; `"Claim Number | X"` leading-separator trim; next-line value fallback; two distinct dates conflict.
- `tests/Pegasus.IntegrationTests/InstructionDraftWebTests.cs` — uses one-field-per-line fixtures at line start; unaffected by the label-position rule.
