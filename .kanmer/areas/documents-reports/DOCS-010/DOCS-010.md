---
id: DOCS-010
type: ticket
title: Retained case documents cannot be read back from Box
status: done
area: documents-reports
order: 1320
assignee: claude-code
profile: fix
stageEntered:
  implementing: '2026-08-23T12:13:49.605Z'
  review: '2026-08-23T14:48:43.258Z'
  verifying: '2026-08-23T14:48:46.681Z'
  done: '2026-08-23T15:17:53.909Z'
taken_at: '2026-08-23T12:11:37.271Z'
branch: task/qdos26012-regressions
worktree: ../pegasus-worktrees/qdos26012-regressions
labels:
  - qdos26011
  - production-defect
  - found-during-qa
links:
  - CASE-019
  - DOCS-009
docs_todo: true
prs:
  - '521'
deployment: production
archived: false
created: '2026-08-23T00:20:37.666Z'
updated: '2026-08-25T01:27:00.447Z'
---

## Root cause — confirmed from the production exception

`BoxDocumentContentStore.VerifyFileMetadataAsync` compared the Box file's `content_type` against the recorded media type:

```csharp
|| !string.Equals(metadata.MediaType, expectedMediaType, StringComparison.OrdinalIgnoreCase)
```

**`content_type` is not a field of the Box v2 file object.** `GetFileAsync` asks for it in the `fields` list and Box returns nothing, so `ParseItem` sets `MediaType` to null and `string.Equals(null, "image/jpeg")` is false. The check **could never pass, for any file, ever**.

Captured from the production console stream, 2026-08-23 00:44:54Z:

```
Pegasus.Web.Pages.Cases.Documents.DownloadModel
Case document download was denied for case 266e5afa-5d66-4623-9136-abe21016df3b,
  occurrence 9a3c2843-5cdf-4dee-a7ae-deb8bec7efd9, version c921d50e-69b2-48e5-80d0-ead72d73357b.
System.IO.InvalidDataException: Managed Box custody type, ancestry, or length metadata is inconsistent.
   at BoxDocumentContentStore.VerifyFileMetadataAsync(...) BoxDocumentContentStore.cs:line 187
   at BoxDocumentContentStore.OpenReadVersionAsync(...) BoxDocumentContentStore.cs:line 94
   at EfDocumentCustodyStore.IDownloadCaseDocument.ExecuteAsync(...) line 252
   at DownloadModel.OnGetAsync(...) line 33
```

## Three symptoms, one fault

| Surface | Presentation |
| --- | --- |
| Evidence tab images | never load |
| `GET /Cases/{caseId}/Documents/{occurrenceId}/Download?versionId=…` | **404** with ids taken from the database |
| Case export ([[CASE-019]]) | "The case could not be exported." |

Each caller catches the same `InvalidDataException` and reports it differently, which is why it read as three separate faults.

## Why it survived this long

`EvaHandoffRevisions` and `EvaHandoffDownloadOperations` are both empty, and `CaseAssessmentFields` has no rows — so the hand-off, the hand-off download and the assessment projection have never run in production. DOCS-007 moved the Evidence gallery onto the case-document route and registered the records, but nothing ever read content back from Box. The case export was the first caller to try.

Box **writes** have always worked: custody is `confirmed` on QDOS26010 and QDOS26011 with remote ids and ETags recorded.

## Fixed

PR #521, branch `task/docs-010-box-read`. Ancestry and length are still always checked, and the caller verifies the content **hash** immediately afterwards — that is the real integrity guarantee. The media type is compared only when Box actually supplies one.

The predicate is extracted to `BoxDocumentContentStore.IsExpectedRevision` so the rule is testable at all: the store holds a concrete `BoxContentClient` and had no test coverage whatsoever. `BoxManagedRevisionTests` covers absent type, empty type, a supplied type compared case-insensitively, a wrong type refused, a wrong or missing length refused, and a file in another case's folder refused.

## Still open — the second, independent defect

The Evidence gallery builds its image URLs from `CaseEvidenceImage.OccurrenceId`, but the value supplied is the **`CaseDocuments.Id`**. Verified: `b4cae16e-80ed-4a28-9a47-0bd7dc8a9d8f` appears in `CaseDocuments` and matches no `DocumentOccurrences` row, so those URLs 404 before Box is reached.

Both faults must go for the gallery to render. Only the Box one blocked the export, so this ticket stays open for the gallery half after #521 ships.

## Diagnosis conditions worth remembering

Application Insights was `OverQuota` throughout (0.1 GB/day, resets 03:00Z) — PLAT-036 — so there was no telemetry at all. The container console stream is dominated by health-check `SELECT 1` at Information level, so `--tail 300` spans only seconds, and `az containerapp logs show --follow` block-buffers through a pipe so a streaming grep emits nothing. What worked was a **polling loop** re-running `--tail 300` every 8 seconds, deduping lines, filtered on `Pegasus.(Web|Infrastructure)` rather than on guessed keywords — the first filter missed the message because it says "denied", not "export" or "Box".
