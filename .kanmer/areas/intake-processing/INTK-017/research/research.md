# Research — INTK-017 deterministic extraction coverage

## Production evidence (read-only SQL/App Insights checks, 2026-08-20 — verified)

- QDOS26002 (0b22b9d6): 28 IntakeAssets, policy `qdos_instruction` v2, reader `mimekit_pdfpig_openxml`. Only 9 CaseDataFields rows; 4 suggestions (vehicle_make, vehicle_model, incident_date, inspection_date) + defaulted instruction_date + confirmed inspection rows + work_provider_code fact.
- NO `vehicle_registration` row exists at all.
- The make/model suggestions were MOT/brake-table pollution (fixed by [[ENG-004]], PR #437 — this ticket builds on that branch).
- The "majority empty" mechanism (verified in code): `InstructionFieldEngine.ExtractFields` nulls a field as conflicting whenever a label matches more than one distinct value anywhere across ALL fragments — extraction runs over the email body plus every attachment page, so appended reports/MOT history produce multi-candidate labels and blank most fields.

## Code facts (verified by reading)

- Engine: `src/Pegasus.Core/Intake/InstructionFieldExtraction.cs`. Candidates discovered per fragment (`IntakeContentFragment(Source, SourceLabel, Text)` — provenance is fragment-level); `DistinctBy` value (OrdinalIgnoreCase) already dedupes identical values; >1 distinct → `HasConflict=true`, `SuggestedValue=null` (lines ~69–79 pre-ENG-004 numbering).
- Fragment order is document order: the reader (`MimeKitPdfPigOpenXmlIntakeSourceReader`) emits the email body first, then attachments in sequence, pages in order; labels are `"uploaded X.eml"` / `"uploaded X.eml, attachment N: name.pdf, page M"`. The instruction document is not deterministically identifiable by provenance alone, but it always precedes appended report/MOT attachments in fragment order.
- QDOS field set: 11 `FieldDefinition` rows in `QdosInstructionExtractionPolicy` — Claimant name, Claim number, Vehicle registration, Vehicle make, Vehicle model, Vehicle mileage, Accident circumstances, Date of incident, Instruction date, Inspection address, Inspection date (optional).
- Suggestions reach the case via `CaseDataSnapshotFactory.AddInstructionSuggestions` → `AddSuggestion`, which SKIPS null draft values, THROWS on `HasConflict=true` with a non-null value, and with multiple candidates picks the one equal to `SuggestedValue` — so a resolved field must set `HasConflict=false` and keep the winner in `Candidates`.
- CaseDataFields check constraint (`CaseDataFieldNames.All`, 19 names): the extraction-fed subset is work_provider_code (mail-route fact), claimant_name, claim_number, vehicle_registration, vehicle_make, vehicle_model, vehicle_mileage, vehicle_mileage_unit, accident_circumstances, incident_date, instruction_date, inspection_date, inspection_address, inspection_mode (+ inspection_deadline from case acceptance). `contact_name`, `contact_email_address`, `contact_phone_number`, `vat_status` have NO extraction pathway: they are not on `InstructionDraft` (persisted entity — extending it is a schema migration) and are operator-entered.
- Completeness policy (`InstructionDraftCompleteness.cs`) requires: Claimant name, Claim number, Vehicle registration, Vehicle make, Vehicle model, Vehicle mileage, Accident circumstances, Date of incident, Instruction date, Inspection address — the same set the definitions cover.
- UI: `Pages/Intake/Details.cshtml:479-490` renders `SuggestedValue` plus the full candidate list per field; a resolved field with extra candidates renders correctly.
- FRD-05 requires structured text/provenance with explicit outcomes and deterministic policy provenance; FRD-06 owns registration-linked vehicle facts. Nothing in either mandates a specific conflict rule — behaviour here is policy-owned.

## Premises verified vs assumed

- Verified: everything above (code reads + the shared prod-diagnostics read-only survey).
- Assumed (cannot read the real QDOS26002 PDF — `corpus/` is immutable/off-limits and production blobs are not accessible): the exact label spellings in the real instruction form. Mitigation: rules are shape-based (validators, order preference, sole-VRM pattern) rather than dependent on new unverified label spellings; the only synonyms added are registration-label variants, which are gated downstream by `NormalizeRegistration`.

## Pinned behaviours that must not change (existing tests)

- `ProcessIntakeTests`: overlong values remain full candidates with null typed values; `"Claim Number | X"` leading-separator trim; next-line value fallback; same-fragment distinct dates conflict; invalid mileage text remains a raw suggestion.
- ENG-004 fixtures: MOT/brake rows never become make/model.
