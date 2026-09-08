# Files — INTK-065

## Where the change lands

| Path | Why |
| --- | --- |
| `scripts/reference_data/build_principal_identification_corpus.py` | Correct the existing five current-source snapshot mappings and corresponding source-ID references. The script is changed in phase 1 but not executed until root grants the verifier slot. |
| `tests/Pegasus.Core.Tests/ReferenceData/PrincipalIdentificationCorpusTests.cs` | Update current source-ID/hash/byte expectations without removing any coverage. Tests are not run outside the root-held verifier slot. |
| `docs/principal-rules-and-mappings/README.md` | Rename source: relocate byte-identically. |
| `docs/principal-rules-and-mappings/qdos.md` | Rename source: relocate byte-identically. |
| `docs/principal-profiles/README.md` | Rename destination: exact bytes from the former principal-rules-and-mappings README. |
| `docs/principal-profiles/qdos.md` | Rename destination: exact bytes from the former principal-rules-and-mappings QDOS document. |
| `docs/index.md` | Point its documentation entry at `docs/principal-profiles/README.md`; no other index changes. |
| `scripts/Test-MarkdownPlacement.ps1` | Extend the existing canonical Markdown-destination matcher to permit only `docs/principal-profiles/**`. |
| `scripts/Test-TestMarkdownPlacement.ps1` | Add the canonical principal-profiles path to the existing allowed-destination regression fixture. |
| `AGENTS.md` | Record that current documentation placements must use the index-routed canonical location and satisfy the base..head Markdown placement gate. |
| `reference/workproviders-and-repairers/principal-identification-corpus.v1.json` | Generated package, reserved solely for the later existing-helper materialization under the root-held verifier slot. |

## Context files

| Path | Constraint |
| --- | --- |
| `docs/frd/frd-09-provider-and-intermediary-routes.md` | Current accepted fifteen-provider behavior; corpus presence is not activation. |
| `docs/runbook.md` | Existing corpus authoring and normalized-LF/raw-byte rules; full `-Verify` needs original roots. No command changes. |
| `reference/README.md` | Supplied evidence is immutable, not policy or deployment proof. |
| `scripts/Build-PrincipalIdentificationCorpus.ps1` | Existing wrapper and explicit input roots; do not add another CLI. |
| `scripts/reference_data/tests/test_build_principal_identification_corpus.py` | Existing hash-mode tests; run only by root in the verifier slot. |
| `src/Pegasus.Core/Intake/**` | Current policy sources are read-only evidence; no runtime source changes. |
| `tests/Pegasus.IntegrationTests/PrincipalIdentificationCorpusEvidenceTests.cs` | Separate original-reader cohort; no change or run for this inventory-only correction. |

## Ripple effects

The first three renamed policy snapshot IDs, QDOS extraction v8 ID, and shared
taxonomy hash/bytes are updated in generator/test code. The tracked package
stays untouched until the verifier executes the existing helper. Documentation
is a two-file relocation, the approved README clarification, and the one
necessary index target repair. The existing Markdown placement matcher and its
regression are extended only for the new canonical location. No application caller loads the package and no runtime route changes.

## Out of scope

No original corpus/reference evidence write; policy activation; criterion
promotion; evaluation rerun; source-copy scheme; parallel generator; runtime
source; protected operator notes; frozen roster; ticket claim; historical
worktree; or cloud write. No script, Python, .NET, documentation-check, or
JSON regeneration command before the root-owned verifier slot. Do not modify
historical `docs/docs-review-temp/**` old-path audit references. The approved
handoff is limited to the paths above and preserves INTK-060 and the historical
A/Foundation, Closed and C/Domain ownership records.
