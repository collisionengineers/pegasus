# CASE-027 files

Lane E2 owns `Vehicle.*`, `Custody.*`, `Tasks.*`, `_CaseDocuments`,
`Documents/**` under `Pages/Cases`, plus `CaseVehicleWebTests.cs`,
`CaseCustodyWebTests.cs`, `CaseTasksWebTests.cs`.

## In my lane

| File | Change |
| --- | --- |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseVehicle.cshtml` | **new** — the `?section=vehicle` body. Assigned to lane E2 by name in [[CASE-012]]'s post-implementation report §2 and by `_CaseWorkspaceNav.cshtml:3–5`. |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseInspectionAddress.cshtml` | **new** — the `?section=inspection-address` body. |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseFiles.cshtml` | **new** — the `?section=case-files` body; absorbs the two gallery blocks currently inline in `Details.cshtml`. Assigned to lane E2 by name in the same report. |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseDataHiddenFields.cshtml` | **new** — the twenty `CaseEditableData` values as hidden inputs, so a second edit form does not carry a second copy of that list. |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseDocuments.cshtml` | restyled onto the design system; explanatory empty-state copy removed. |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` | **append only**, inside one new nested `static class CaseWorkspace`. No existing member reordered or edited. |
| `docs/design/test-ui/catalogue.json` | `Vehicle`/`Custody`/`Tasks` reclassified `redirect` → `protocol` with an accurate reason (handed to this lane by [[CASE-012]] round 2 by name). Text edit only — **no snapshot regeneration**. |
| `tests/Pegasus.IntegrationTests/CaseVehicleWebTests.cs` | render pins for the Vehicle view + fixture properties. |
| `tests/Pegasus.IntegrationTests/CaseCustodyWebTests.cs` | render pins for Case Files. |
| `tests/Pegasus.IntegrationTests/CaseTasksWebTests.cs` | render pin for Notes and the Inspection address view. |

## Outside my lane — declared, minimal, reported loudly

| File | Owner | Change and why it is unavoidable |
| --- | --- | --- |
| `src/Pegasus.Web/Pages/Cases/Details.cshtml` | lane E1 ([[CASE-012]]) | The `?section=` dispatch lives here and nowhere else, so no section body can be reached without it. The edit is confined to the four branches at :303–427: each becomes a one-line `<partial>` reference. Net **removal** of markup from E1's file. |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseWorkflow.cshtml` | lane E1 ([[CASE-012]]) | Two hidden inputs added. The Overview edit form omits `claimantContactNumber` and `claimantAddress`, which `SaveCase` then clears (research premises 8–9): silent data loss on the product's main edit form. D19 rule 2 — small, in a file whose lane is at `verifying` and not in flight. |
| `tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs` | shared fixture | Two lines in the `CaseDetails` object initialiser so the shared recording store can carry `VehicleEvidence` and `Documents`. PR #615 is merged, so no open PR targets this file (research premise 15). |

## Confirmed clear of every lane in flight

None of the files above lies under `Pages/Administration/**` (PLAT-025/026/027),
`Pages/Operations/**` (PLAT-049), `Core/Assessment` (ENG-027), `Upload*` or
`Uploads/**` (INTK-047), or `Core/Intake` extraction (DELIV-036). DELIV-034
owns the credential-tamper flake in `PrincipalCredentials` tests, not the
Case web tests.

## Explicitly not touched

- `src/Pegasus.Web/Pages/Cases/Shared/_CaseWorkspaceNav.cshtml` — lane E1's,
  and its known inline-section-list breach is recorded on [[CASE-012]].
- `src/Pegasus.Web/Pages/Cases/Shared/_CaseHistory.cshtml` — lane E1's, and
  it already delivers the Notes view this ticket describes.
- `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs` — no new model data is
  needed; everything rendered is already on `Model.Case`.
- `src/Pegasus.Web/wwwroot/css/site.css`, `wwwroot/js/site.js` — PLAT-029's.
- `docs/design/test-ui/pages/**` — regenerated once per merge by the
  orchestrator.
