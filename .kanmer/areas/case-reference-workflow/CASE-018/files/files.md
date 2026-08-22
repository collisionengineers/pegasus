# Files — CASE-018

| File | Change |
| --- | --- |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseWorkflow.cshtml` | Delete the read-only `rows`/`populated` restatement inside "Case detail" (`:91-222`), keeping only the two edit forms. Strip the value listing from "Vehicle evidence" (`:410-466`), keeping the accept / correct / request-lookup controls. |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseSummary.cshtml` | Remove the "Engineer queries" block and the "Where this case stands" block. Move the two `OriginalCaseId` / `ReplacementCaseId` rows out of "Where this case stands" into "Case identity", which is where a corrected-case link belongs. |
| `src/Pegasus.Web/wwwroot/css/site.css` | `.datarow` — give the value column one shared left edge whether or not the row ends in a provenance icon. |
| `tests/Pegasus.Web.Tests/…` | Page assertions for the removed blocks and the single-occurrence rule. |

## Not touched

- `_CaseSummary`'s Chase history / Approved report / Report-Sent evidence panels — none of them duplicate the block-grid.
- The edit forms themselves. Removing the read-only rows above them changes nothing about what can be edited.
- `_CaseDocuments.cshtml` — [[CASE-019]] owns it.

## Rejected during file mapping

Adding manufacture-year and fuel-type rows to the Vehicle block, so the lookup's other values had somewhere to go. `CaseDataFields` carries a `CK_CaseDataFields_FieldName` check constraint pinning `FieldName` to `CaseDataFieldNames.All`, which has no entry for either. Showing them would mean a Core contract change and a migration for two facts the operator did not ask for. [[ENG-013]] is therefore scoped to the four vehicle fields that already exist.

## Read-only checks run

- Prod `CaseDataFields` for QDOS26011 (2026-08-22): eleven rows, no `vehicle_mileage` of any kind. Confirms the "Not recorded" the operator saw is real data, not a rendering fault.
- `VehicleLookupObservations` for the same case carries the mileage the "Vehicle evidence" panel was showing. The two containers were reading different tables, which is the whole of issue 1's mileage complaint.
- `CaseDataFields` primary key is `(CaseId, FieldName, ValueKind)`, so Fact / Suggestion / Confirmed genuinely coexist per field — which is what makes [[ENG-013]]'s approach possible without schema change.
