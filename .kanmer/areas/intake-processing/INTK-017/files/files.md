# Files — INTK-017

## Change

- `src/Pegasus.Core/Intake/InstructionFieldExtraction.cs` — deterministic conflict resolution (typed-validity narrowing, then earliest-fragment preference), value truncation at a following known field label, sole current-format VRM pattern fallback for `Vehicle registration`, `FieldDefinition.IsValidTyped` hook.
- `src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosInstructionExtractionPolicy.cs` — registration label synonyms (longest-first); wire `IsValidTyped` narrowing predicates for registration, mileage, and the three date fields.
- `tests/Pegasus.Core.Tests/Intake/Qdos/QdosInstructionExtractionPolicyTests.cs` — fixtures for cross-fragment resolution, validated-beats-unvalidated, sole/ambiguous VRM fallback, label synonyms, multi-field flattened lines.

## Read (context, unchanged)

- `src/Pegasus.Core/Intake/IntakeContracts.cs` — contracts unchanged (`InstructionReviewField` shape carries resolved fields already).
- `src/Pegasus.Core/Intake/InstructionDraftCompleteness.cs` — required field set; unchanged.
- `src/Pegasus.Infrastructure/Persistence/CaseDataSnapshotFactory.cs` — `AddSuggestion` contract that resolved fields must satisfy (`HasConflict=false`, winner present in `Candidates`); unchanged.
- `src/Pegasus.Infrastructure/Intake/MimeKitPdfPigOpenXmlIntakeSourceReader.cs` — fragment ordering and `SourceLabel` shapes; unchanged.
- `src/Pegasus.Web/Pages/Intake/Details.cshtml` — renders suggested value + candidate list; unchanged.
- `tests/Pegasus.Core.Tests/Intake/ProcessIntakeTests.cs` — pinned behaviours; unchanged (must stay green).
