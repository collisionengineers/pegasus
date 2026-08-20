# Files — INTK-021

| File | Change |
| --- | --- |
| src/Pegasus.Infrastructure/Persistence/CaseDataSnapshotFactory.cs | Extracted values written as Fact (auto-added); method renamed `AddExtractedValue` |
| src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosInstructionExtractionPolicy.cs | Real-shape synonyms; subject-fact fragment (QDOS subject grammar, appended last so body wins); combined `Vehicle description` field with make/model/registration derivation (two-word-make list) |
| src/Pegasus.Core/Intake/InstructionFieldExtraction.cs | Label boundary guard `(?!['\w])` — a label never matches into a longer word/possessive |
| tests/Pegasus.IntegrationTests/QdosExtractionCoverageTests.cs | New corpus-conditional coverage test with per-field CSV artifact + floors (registration, claimant) |
| tests/Pegasus.Core.Tests/... (extraction fixtures) | New/updated fixtures for boundary, subject grammar, description split |
