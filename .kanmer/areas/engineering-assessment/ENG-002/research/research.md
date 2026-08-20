# ENG-002 research — estimate import routes

Date: 2026-08-20. All premises below were verified by read-only checks (file reads, corpus listing, PdfPig coordinate probe) unless marked *assumed*.

## The three operator routes and their current state

1. **External estimating systems (Audatex, Glass's)** — no parser, no import surface. This ticket builds the first one (Audatex PDF; evidence below).
2. **AI estimate via MCP connector** — ALREADY WORKS. `pegasus_assessment_update` replaces the whole ordered estimate-line collection (`src/Pegasus.Web/Mcp/AssessmentMcpTools.cs:196-258`); lines land recorded-by Automation, unconfirmed. Covered by `tests/Pegasus.IntegrationTests/AutomationAssessmentIngressTests.cs` (posts `estimateLines`, asserts `CaseEstimateLines WHERE RecordedByKind = N'Automation'` and the `pegasus_assessment_update` action-history event). Nothing to rebuild.
3. **Drag-and-drop by staff** — no surface. Built here on the assessment page.

## Corpus evidence (read-only survey; contents never copied into the repo)

- The corpus contains **real Audatex full-report estimate PDFs** (email attachments; e.g. one forwarded "estimate" mail carrying a 5-page "Full Report" footed "Audatex System Using Manufacturer Times", with Assessment Number, Version, LABOUR / PAINT WORK / MATERIAL COST - PAINT / PARTS / Extras / Cost Summary sections).
- It also contains **Tractable "LINE_LEVEL_ESTIMATE.pdf"** AI-estimate attachments (4 copies) — that is the *AI/MCP* route's artefact class, not a staff-parsed format.
- **No Glass's export was found** in the corpus (searched attachment names and body text for glass/glassmatix). The Glasses parser is therefore parked pending a real sample (see open-questions).

### Audatex PDF structure (verified with a PdfPig word-coordinate probe against the corpus sample, run in the session scratchpad only)

- Words on one visual table row share an exact baseline per column, but the numeric column (work units / price) sits on a baseline **exactly ~1.0pt below** its description row (different font). Naive line-flattening (pdftotext -layout) mis-associates values with rows — which is exactly the wrong-money hazard. Grouping words by baseline and pairing each value row to the description row within a tolerance well under the ~11-12pt row pitch is deterministic and unambiguous.
- Column x-positions are stable per section (guide number ≈ x20, description ≈ x159, work units right-aligned ≈ x480+, parts: guide x20/desc x103ish/part-number/betterment x325/price x510+).
- The document carries its own checksums, verified against the sample: sum of labour work-units == printed "Total Work Units"; sum of paint work-units == printed paint total; sum of part prices == printed parts "Sub Total"; sum of Extras prices == printed "Total Extras"; bucket totals == printed Grand Total Excl VAT. These are the parser's fail-closed verification: any mismatch rejects the whole import.
- Labour *money* totals are NOT exactly recomputable from work units (WU/12 × rate rounds; the sample's printed labour total differs from the naive product by pennies) — so the parser checks work-unit sums, not labour money.
- Header carries `Assessment Number:` and `Version:` — the import's SourceVersion.

## The landing model (TICK-093, all exists)

- `RepairSpecificationSourceRoute` already enumerates `AudatexPdf`, `Glasses`, `ApprovedAiProposal`, `Manual`, `LegacyUnresolved` (`src/Pegasus.Core/Assessment/RepairSpecifications.cs`).
- `IRepairSpecificationStore.StartDraftAsync` (impl `src/Pegasus.Infrastructure/Persistence/EfRepairSpecificationStore.cs`): requires **staff Engineer**, validated `RepairSpecificationSource` (route + ArtifactReference + SourceVersion + 64-hex Sha256), case edit lease + expected version + operation key; refuses when a current draft exists; requires `SupersedesSpecificationId`+reason when an accepted specification exists. Lines land via `AssessmentPolicy.NormalizeRepairSpecificationLines`. The specification stays **Draft** — that is the "unconfirmed" state for this surface; nothing feeds a report from a draft.
- `AcceptAsync`: Engineer-only, requires a `RepairCalculationBasis` (Labour, Parts, PaintMaterials, SpecialistOther, VAT, Total; internal-consistency check Total = sum + VAT) — acceptance is the staff-Engineer confirmation step (MCP-06 precedent honoured).
- Recovery from a wrong imported draft already exists: `EfCaseAssessmentStore.SaveAsync` with `EstimateLines` replaces the current draft's whole line collection (lines 218-252).
- Estimate line vocabulary (`EstimateLineCodes`): types `rnr, repair, new_part, check_labour, paint_new, paint_repair, paint_blend, paint_prep, specialist_fixed, specialist_wu`; statuses `confirmed, estimated, provisional`.

## Custody home for the dropped file (no second intake route)

The existing convention for case-scoped staff files is **`IAddCaseDocument` with `DocumentSource.StaffUpload`** — `src/Pegasus.Web/Pages/Cases/Custody.cshtml.cs` `OnPostUploadDocumentAsync` (10 MiB cap, lease + expected version + operation key, semantic role). The intake receipt pipeline is for *inbound source material creating/attaching to cases*, not for a staff action inside an existing case workspace — the case-document path is the right custody boundary and is what MCP's `pegasus_document_add` also uses. The retained `DocumentVersion` already computes the Sha256 the specification source needs; the occurrence's `SourceOccurrenceIdentity` is the durable ArtifactReference join.

## Web conventions

- The assessment page (`src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml(.cs)`, post-ENG-003 with the single CombinedReadiness list) has **no edit-lease flow**; its estimate section is disabled design markup awaiting activation. Programmatic in-handler lease acquisition is an existing convention: `src/Pegasus.Web/Pages/Operations/Index.cshtml.cs` acquires via `IAcquireCaseEditLease` with a fresh operation key inside the handler.
- Every case mutation clears the lease (`CaseMutationGuard.ClearLease` in each store's guard), so a two-mutation import (retain document, then start draft) needs two sequential lease claims at consecutive expected versions.

## Report-cost boundary (EXT-09 — deliberately NOT settled here)

`AssessmentReportProjection` requires `Costs: ReportRepairCosts` (LabourHours × HourlyRate, its own 20% VAT rule) and every production caller passes null, firing the "Repair cost figures" readiness item. The accepted `RepairCalculationBasis` stores bucket *totals* + recorded VAT, which cannot honestly be mapped onto hours×rate (verified: the sample's printed labour money differs from hours×rate by rounding). Bridging basis → `ReportRepairCosts` is EXT-09's derivation authority. This ticket lands the lines and the Engineer-accepted basis; it does not wire report costs and the readiness item continues to fire — named as the residual gap.

## Assumed (not verified)

- PdfPig 0.1.15's `PdfDocumentBuilder` can place text at exact positions for synthetic test fixtures (checked at implementation; corpus bytes are never committed).
