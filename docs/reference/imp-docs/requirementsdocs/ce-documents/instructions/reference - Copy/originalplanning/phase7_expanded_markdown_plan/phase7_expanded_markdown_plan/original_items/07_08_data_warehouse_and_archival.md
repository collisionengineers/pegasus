# 7.8 Data Warehouse and Archival


## Purpose

Create a structured long-term data layer for analysis, audit, retention, search and controlled archival of historical case material.

## Step-by-step plan

### Step 1 — Define retention classes

Classify data into:

1. original email metadata;
2. source PDFs/documents;
3. vehicle images;
4. extracted text/OCR;
5. extracted structured JSON;
6. AI prompt/output logs;
7. review decisions;
8. EVA payloads/responses;
9. operational logs;
10. analytics aggregates.

Define retention period and deletion/archive rules for each.

### Step 2 — Define warehouse schema

Start with tables/facts:

- work items;
- documents;
- extracted fields;
- review events;
- EVA submissions;
- status events;
- provider/principal dimensions;
- engineer dimensions;
- time/date dimensions;
- exception reasons.

### Step 3 — Build export pipeline

1. Export from operational database to warehouse on schedule.
2. Export Box/file metadata and file links.
3. Export only necessary derived fields, not raw full documents, unless required.
4. Apply schema versioning.
5. Store data-quality checks.

### Step 4 — Cold storage and archive

1. Define when a case becomes closed/archived.
2. Move older source files to lower-cost storage or Box archive policy if available.
3. Preserve search metadata and audit trail.
4. Ensure archive retrieval process is documented.

### Step 5 — Searchability

1. Index work item IDs, VRMs, references, provider, dates and Box IDs.
2. Keep full-text search limited to controlled repositories.
3. Apply access restrictions.

### Step 6 — Audit/replay support

1. Store schema and payload versions.
2. Keep immutable event logs.
3. Preserve EVA payload and response per attempt.
4. Allow reconstruction of how a field was derived.

## Deliverables

- Retention/data-classification matrix.
- Warehouse schema.
- Scheduled export process.
- Archive policy.
- Search/index design.
- Data-quality report.

## Acceptance criteria

- Closed cases remain searchable by approved identifiers.
- Source evidence is not overwritten.
- Retention rules are documented and actionable.
- Analytics can run without querying production workflow tables heavily.
- Audit reconstruction is possible for a sampled case.

## Risks and controls

| Risk | Control |
|---|---|
| Retaining too much personal data | Data minimisation and retention review. |
| Losing source evidence | Checksums and immutable original storage. |
| Warehouse drift | Version schemas and exports. |
| Access sprawl | Separate operational, analytics and archive permissions. |

## Suggested priority

P2, but retention classification should be P0/P1 before production scale.
