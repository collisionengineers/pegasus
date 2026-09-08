# Files — INTK-065

## Where the change lands

| Path | Why |
| --- | --- |
| `scripts/reference_data/build_principal_identification_corpus.py` | Correct the existing five current-source snapshot mappings and every corresponding source ID reference; clarify historical dossier purpose without changing criteria or rebuilding policy. |
| `reference/workproviders-and-repairers/principal-identification-corpus.v1.json` | Generated package: current source IDs/paths/hashes/byte counts and matching references only, plus explicit baseline purpose prose. Immutable historical objects remain unchanged. |
| `tests/Pegasus.Core.Tests/ReferenceData/PrincipalIdentificationCorpusTests.cs` | Update source-ID expectations, preserve all corpus/hash assertions and add byte-count/current-policy identity diagnostics to catch this class without exclusions. |
| `docs/principal-rules-and-mappings/README.md` | Clarify historical v1 review baseline versus current FRD-09/runtime activation and current-source links. No new dossiers or normative rules. |

## Context files

| Path | Constraint |
| --- | --- |
| `docs/frd/frd-09-provider-and-intermediary-routes.md` | Current accepted fifteen-provider behavior; corpus presence is not activation. |
| `docs/runbook.md` | Existing corpus authoring and normalized-LF/raw-byte rules; full -Verify needs original roots. No command changes. |
| `reference/README.md` | Supplied evidence is immutable, not policy or deployment proof. |
| `scripts/Build-PrincipalIdentificationCorpus.ps1` | Existing wrapper and explicit input roots; do not add another CLI. |
| `scripts/reference_data/tests/test_build_principal_identification_corpus.py` | Existing two tests prove LF/CRLF/CR normalization and raw-byte preservation; unchanged. |
| `src/Pegasus.Core/Intake/PrincipalMailRoutePolicy.cs` | Current principal_mail_route v1 identity and bytes; read only. |
| `src/Pegasus.Core/Intake/Classification/PrincipalMailClassificationPolicy.cs` | Current principal_mail_classification v1; historical v5 counts are not its results. |
| `src/Pegasus.Core/Intake/CaseMatching/PrincipalCaseMatchPolicy.cs` | Current principal_case_match v1; no obsolete QDOS alias. |
| `src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosInstructionExtractionPolicy.cs` | Current extraction v8 although prior source ID said v7. |
| `src/Pegasus.Core/Intake/Classification/MailClassificationContracts.cs` | Current shared contract changed by TICK-035; refresh its existing source hash. |
| `tests/Pegasus.IntegrationTests/PrincipalIdentificationCorpusEvidenceTests.cs` | Separate original-reader cohort; no behavior change or rerun justified by inventory-only update. |
| `pegasus_pack/astra_output/v1_implementation_plans/registers/file-ownership.csv` | Historical exact owners A/A/Closed/C; root narrow handoff required, not foreign-claim deletion. |

## Ripple effects

All references to the four renamed policy snapshot IDs must move atomically
in generator, generated JSON and the existing test. Shared taxonomy retains
its ID and changes only hash/bytes. The other seven data/reference snapshots
remain byte-equal; QDOS extraction changes its ID only. No application caller
loads this file. No generated Test UI, migration, lock file or runtime artifact
changes.

## Out of scope

No original corpus/reference evidence writes; no policy activation, criterion
promotion, evaluation rerun claim, new source-copy scheme or parallel generator.
Do not modify runtime source, protected operator notes, the original frozen
218 roster, another ticket's state/claim or historical Git worktree.
Preparation creates no ownership: all four paths await root plan review and
narrow historical/current path handoff.
