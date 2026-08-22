# Plan — CASE-019

Same lane and branch as [[CASE-018]]: `task/qdos26011-regressions`. Verification depends on [[DOCS-009]] landing first, or the archive contains no photographs.

## Steps

1. **`EvaEvidenceStatus` gains `Unrecorded`.** `IsAccepted` stays `Accepted or Corrected`, so nothing about the hand-off moves. One enum, one place — the taxonomy is not copied.

2. **`CaseEvaMapping.MapForOperatorExport(evidence, acceptance, today)`.** Sits beside `MapForProduction` and shares `RequiredMappedFields`, `NormalizeValue` and `NormalizeRegistration` — the ordered field set is written once and neither method owns a copy. Behaviour:
   - refuses only when `IsSwitchedOn(acceptance)` is false;
   - a blank Inspection Date becomes `today` formatted `dd/MM/yyyy`, with provenance source `SystemDefault:Export date`, mirroring the existing `SystemDefault:Receipt date` on `instruction_date`;
   - any other blank field is emitted as an empty string with status `Unrecorded`, and its name is returned to the caller;
   - a value present but only suggested keeps status `Suggested` in provenance, so the archive never claims the lookup mileage was accepted.

3. **`EvaBundleSchema.ValidateSource` — separate format from evidence bar.** Delete the "every field non-empty" throw and the "status must be Accepted or Corrected" throw. Keep: mapping acceptance, exact ordered field set, provenance count and name match, value-matches-provenance, image integrity. `WriteProvenance` maps all four statuses.

4. **`EvaHandoffStore` — one evidence builder, two tiers.** Extract the `new EvaAcceptedCaseEvidence(...)` construction out of `MapAcceptedCase` into `BuildEvidence(caseData, vehicle, includeSuggestions)`. With `includeSuggestions: false` it reads `Accepted()` (`Confirmed ?? Fact`) exactly as today; with `true` it falls through to `Suggestion` and to `caseData.Vehicle.*` where the confirmed vehicle record is absent — which is how [[ENG-013]]'s lookup mileage reaches the export.

   *Reuses:* every existing helper — `ResolveInspection`, `FromCaseValue`, `VehicleModel`, `MissingEvidence`. The hand-off path keeps calling it with `false` and is behaviour-identical.

5. **`EvaHandoffStore.ExportAsync`.** Loads case data, vehicle evidence and the eligible images with the query `GetPreparationAsync` already uses, maps with `MapForOperatorExport`, calls `CreateOfflineReplay`, returns the archive and the blank-field names. It opens no transaction, bumps no version, writes no revision and no proxy. It is a read.

6. **Web — make the control a link.** `Details.cshtml`: `asp-route-caseId`. `Export.cshtml.cs`: `OnGetAsync(Guid caseId, …)` returning `File(bundle.Content, "application/zip", bundle.FileName)` with the same `nosniff` / `no-store` headers the sibling download pages set. `OnPostAsync` is untouched, so selective export still works.

7. **Web — stop drawing dead controls.** `_CaseDocuments.cshtml`: render the `Export` column header and its checkboxes only when `mayEdit`.

8. **Tests.**
   - Core: a case missing VAT status exports with twelve populated fields and one `Unrecorded`; a case missing an inspection date exports with today's; an unaccepted mapping still refuses.
   - Core: `MapForProduction` is unchanged by the `ValidateSource` relaxation — a case with a suggested-only mileage still yields a blocking reason.
   - Web: the Export control renders an `href` (the regression that started this), and the GET returns `application/zip`.

## Acceptance

- Pressing Export on QDOS26011 downloads `EVA-QDOS26011.zip`.
- It contains eight photographs, `EVA-QDOS26011.json` with all thirteen keys in order, `provenance.json` and `manifest.sha256`.
- No `EvaHandoffRevision` row is created by the export.
- The EVA hand-off panel behaves exactly as before.

## Simplification pass

Recorded after implementation, before the PR.
