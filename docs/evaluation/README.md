# Evaluation evidence

This area indexes dated local evidence. Evaluation reports describe what was
observed for a named input scope and caller; they are not product authority,
deployment proof, or operator acceptance. Current caller status belongs in the
[implementation handoff](../agent-notes/current-implementation-handoff.md).

| Evidence | Date and input scope | Caller or harness | What it proves | What it does not prove |
| --- | --- | --- | --- | --- |
| [Local evaluation corpus](corpus.md) | Snapshot recorded 2026-07-23; genuine local operational formats | Inventory guidance, not an application caller | The local evidence boundary, safety rules, and observed format/count snapshot | Current corpus contents, extraction accuracy, workflow behavior, deployment, or acceptance |
| [Multi-format intake evaluation](multiformat-intake.md) | 2026-07-23; controlled protocol fixtures plus pinned genuine local samples | Historical Development-only `POST /Intake/Qdos`; the current route is `/Intake/Upload` through `ProcessIntake` | The recorded QDOS-policy behavior and failure boundaries for the sampled formats at that checkpoint | The current caller by itself, complete QDOS workflow, field-level accuracy, Worker/Graph/Box/Azure behavior, or production acceptance |
| [QDOS embedded PDF benchmark](qdos-pdf-engine-benchmark.md) | 2026-07-23; 74 unique PDFs and 567 reported pages from the immutable local QDOS cohort | Disposable local benchmark harness, not the Web or Worker entry point | Comparative embedded-text decoding and measured marker coverage for the sampled cohort | Literal field accuracy, OCR behavior, future layouts, production runtime behavior, or operator acceptance |

## Evidence rules

- Treat `corpus/` as ignored, immutable, untrusted local data. Never rename,
  modify, annotate, upload, publish, or commit its contents.
- Use `$collisionspike-corpus-evaluation` for intake or extraction evaluation.
  Sample genuine inputs immutably and run them through the actual caller when
  the claim concerns product behavior.
- Put generated manifests, extracted content, predictions, and detailed results
  under ignored `artifacts/evaluation/`.
- Commit only content-safe summaries: counts, aggregate outcomes, redacted
  identifiers, and limitations. Do not commit message bodies, source names,
  personal data, secrets, or case documents.
- Record the input scope, caller, date, observed outcome, negative paths, and
  untested boundaries. A passing sample does not establish behavior for every
  provider or format.
- Keep repository-consistency, real-caller, corpus, deployment, and operator-
  acceptance evidence as separate conclusions.

The executable local entry point is
[`scripts/Invoke-RepoCheck.ps1`](../../scripts/Invoke-RepoCheck.ps1). Its default
run excludes corpus-category tests; `-RequireCorpusEvidence` is used only when
genuine local intake or extraction evidence is present and required.
