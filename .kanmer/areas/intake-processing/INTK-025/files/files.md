# Files — INTK-025

| File | Change |
| --- | --- |
| `src/Pegasus.Core/Intake/InstructionFieldExtraction.cs` | `FieldDefinition` gains `GuardedPrefixes` (neutral mechanism); the label regexes build their lookbehind from it; the hardcoded `TP ` literal goes |
| `src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosInstructionExtractionPolicy.cs` | QDOS supplies `["TP"]` on its definitions; `WithReportFacts` synthesizes labelled lines from report-named fragments (`Vehicle:` line cut at the report's column labels; digit-bearing `Speedo:`); `WithCircumstances` synthesizes `Accident Circumstances:` from the prompt-line paragraph; Version 3 → 4 |
| `tests/Pegasus.Core.Tests/Intake/Qdos/QdosInstructionExtractionPolicyTests.cs` | Facts: report vehicle line fills make/model only when the letter has none; run-together columns cut; Speedo without digits emits nothing; circumstances paragraph lands and stops at Damage Area; TP guard still holds via policy config; engine source carries no QDOS literal |
| `tests/Pegasus.IntegrationTests/QdosMappingExtractionTests.cs` | Expectations extended (circumstances per mapped file where the letter carries the prompt) |

Reuse: the `WithSubjectFacts` synthesized-labelled-lines pattern (rank-aware —
appended fragments lose to the letter); no engine grammar, no migration.
