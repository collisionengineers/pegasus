---
id: DOCS-010
type: ticket
title: Retained case documents cannot be read back from Box
status: backlog
area: documents-reports
assignee: ''
profile: fix
labels:
  - qdos26011
  - production-defect
  - found-during-qa
links:
  - CASE-019
  - DOCS-009
docs_todo: true
deployment: not-deployed
archived: false
created: '2026-08-23T00:20:37.666Z'
updated: '2026-08-23T00:20:37.666Z'
---

## Symptom, on production QDOS26011 (2026-08-23)

Every route that reads a retained case document's **content** fails:

| Surface | Result |
| --- | --- |
| Evidence tab images | all eight `img` elements never complete |
| `GET /Cases/{caseId}/Documents/{occurrenceId}/Download?versionId=…` | **404**, with a correct occurrence id and version id taken from the database |
| Case export ([[CASE-019]]) | "The case could not be exported." |

The download page returns `NotFound()` from its `catch` over `ArgumentException / InvalidOperationException / InvalidDataException / IOException / UnauthorizedAccessException`. `BoxDocumentContentStore` throws `FileNotFoundException` — an `IOException` — when it cannot resolve the case folder or the file inside it. The export's own catch covers the same set. One fault, three symptoms.

## The records are not the problem

QDOS26011's `DocumentOccurrences` are exactly what a read needs:

| Ordinal | Role | File | Length | Custody | Current |
| ---: | --- | --- | ---: | --- | --- |
| 2 | Image | `1_CLVoffside-V1.jpg` | 462,270 | Confirmed | yes |
| 3–6, 8–10 | Image | seven more JPEGs | — | Confirmed | yes |
| 7 | Instruction | `53364_1_LtrtoEngineerIn.pdf` | 103,715 | Confirmed | yes |
| 1 | OriginalSource | the `.eml` | 4,841,595 | Confirmed | yes |

Ordinals are distinct and positive, versions are 1, nothing is logically removed, custody state is `confirmed` and `CustodyRootRemoteId` is `411135978094`.

## Where to look first

`BoxDocumentContentStore.ResolveCaseFolderAsync` finds the case folder **by name** — `CustodyNames.SafeName(address.CaseReference)` under the client root — and returns null (→ `FileNotFoundException`) when no such child exists. It does **not** use `Cases.CustodyRootRemoteId`, which is the durable folder identity the custody adapter itself relies on and which the database holds for this case.

If it resolves, the file is then found by `FlatFileName` = `"{OccurrenceOrdinal:000} {SafeName(FileName)}"`, e.g. `002 1_CLVoffside-V1.jpg`, and `VerifyFileMetadataAsync` compares the Box file's media type and length before the content hash is checked. Any of those four steps produces the same opaque outcome.

Confirming which needs either a Box read with the production credentials or a log captured at the moment of failure.

## Why this was never noticed

`EvaHandoffRevisions` is empty — the hand-off has never been generated in production, and it is the only other caller of `OpenReadVersionAsync`. DOCS-007 registered case documents and moved the Evidence gallery onto the case-document route, but nothing has read content back from Box in production since.

## A second, independent defect on the same surface

The Evidence gallery builds its image URLs from `CaseEvidenceImage.OccurrenceId`, but the value supplied is the **`CaseDocuments.Id`**, not the `DocumentOccurrences.Id`. Verified: `b4cae16e-80ed-4a28-9a47-0bd7dc8a9d8f` appears in `CaseDocuments` and matches no occurrence row, so those URLs 404 before any Box call happens. Both faults must be fixed for the gallery to render; only the Box one blocks the export.

## Diagnosis conditions to be aware of

Application Insights was `OverQuota` (0.1 GB/day, resets 03:00Z) throughout, so no telemetry was available — see PLAT-036. The container console stream is dominated by health-check `SELECT 1` output, so a 300-line tail covers only seconds and `--follow` is block-buffered through `az`. Neither route yielded the exception.
