# Files — ENG-036 (2026-09-02, gpt-5.6-terra xhigh, wrapper-checked; reconciled 2026-09-03 with the corrected plan)

## Prospective map

- **VERIFIED** — `git cat-file -e HEAD:<path>` confirms every existing path
  below exists; `damage-diagram.js`, `_CaseDamage.cshtml`, and the proposed
  shared SVG asset are absent.

- **VERIFIED** — `Pegasus.Infrastructure.csproj` carries
  `<InternalsVisibleTo Include="Pegasus.IntegrationTests" />`, so a new
  internal Infrastructure type is directly assertable from an integration
  test. `Pegasus.Web.csproj` has no linked external static asset today (only
  `<Content Remove="wwwroot\lib\**" />`), so the Web item is new, not an
  existing convention.

- **ASSUMED** — the SVG asset proposal is the smallest way to keep vehicle
  geometry in one place for both JavaScript and the Playwright renderer.

| Path | Action (create/change) | Why | Reuses |
| --- | --- | --- | --- |
| `docs/design/assets/report-renderer/templates/damage-diagram.svg` | create | Single source for zone geometry, wheel geometry, marker anchors, and report-safe SVG structure. | Existing report-template asset directory. |
| `src/Pegasus.Infrastructure/Pegasus.Infrastructure.csproj` | change | Embed the shared SVG for the renderer. | Existing embedded Scriban/CSS resource entries. |
| `src/Pegasus.Web/Pegasus.Web.csproj` | change | One explicit static-web-asset item publishing the same SVG source under `wwwroot`; no copy in the tree. | None — stated as new; no existing Web linked-asset example. |
| `src/Pegasus.Web/wwwroot/js/damage-diagram.js` | create | Clone the SVG, map supplied Core codes to markers, support click/Enter/Space on zones and the three non-geometric zone chips, and render read-only state. Exposes `window.pegasusDamageDiagram.init(root)`. | Existing `data-*` event hooks and `data-edit-save` lifecycle. |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseDamage.cshtml` | change | Replace [[ENG-034]]'s read-only shell with the D39 body and one leased assessment form contract. | Existing Case partials and [[ENG-034]]'s shell. |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` | change, conditional shared-lock hand-off | Add the sole operator-facing labels for D39 headings, zones, severities, tyres, and belts. **No type labels (D45).** | `OperatorLabels.CaseWorkspace`. |
| `src/Pegasus.Web/wwwroot/css/site.css` | change, conditional shared-lock hand-off | Add the component layout, markers, impact rows, tyre cards, and 1180/760px rules. | Existing layout, panel, form, and responsive rules. |
| `src/Pegasus.Infrastructure/Reports/DamageDiagramMarkup.cs` | create | Internal composer: read the embedded shared SVG and return marked report HTML for the projected zones. New file, so no [[ENG-035]] whole-file overlap. | `ResourceText`, `Encode`, existing HTML/PDF pipeline. |
| `docs/design/assets/report-renderer/templates/report.css` | change | Marker and diagram print rules. Unclaimed by any EPIC-012 lane; stop if a concurrent lane holds it. | The single report stylesheet — no second stylesheet. |
| `tests/Pegasus.IntegrationTests/Reports/AssessmentReportDamageDiagramTests.cs` | create | Structural marker assertion, printed-PDF assertion, and saved-record caller evidence. New file; [[ENG-035]] keeps `AssessmentReportRendererTests.cs`. | Existing renderer-composition harness and PdfPig extraction. |
| `tests/Pegasus.Core.Tests/Assessment/DamageZoneTests.cs` | create | Prove the consumed contract: canonical zones, unique zones, highest-severity derivation, individual wheels, no `type` member. New file; [[ENG-035]] keeps `AssessmentPolicyTests.cs`. | Existing Core assessment test conventions. |

Two named insertions are handed to [[ENG-035]] rather than made by ENG-036,
because they land inside ENG-035-owned whole files: the
`assessment["damage_diagram"] = DamageDiagramMarkup.Compose(...)` line in
`PlaywrightAssessmentReportRenderer.cs`, and the
`{{ assessment.damage_diagram }}` slot with the Zone/Severity/Note rows in
`assessment_report.scriban`.

## Files ENG-036 must not touch

- [[ENG-035]]: `src/Pegasus.Core/Assessment/AssessmentContracts.cs`,
  `AssessmentPolicy.cs`, `src/Pegasus.Core/Reports/AssessmentReportProjection.cs`,
  `AssessmentReportRendering.cs`, `EfCaseAssessmentStore.cs`,
  `Persistence/Migrations/**`,
  `src/Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs`,
  `assessment_report.scriban`,
  `tests/Pegasus.IntegrationTests/Reports/AssessmentReportRendererTests.cs`,
  and Core projection/rendering tests.

- [[ENG-034]]: `Pages/Cases/Assessment/Index.*`, the Assessment redirect
  tests, and `_CaseDamage.cshtml` until its extracted shell has merged.

- [[ENG-029]]: `_CaseSettlement.cshtml`, `_CaseReport.cshtml`, Case-page
  `ISaveAssessment` handlers, and assessment Web tests.

- [[ENG-031]]: report-image crop, role, ordering, and related rendering paths.

- [[CASE-038]]: `Pages/Cases/Details.*`, `_CaseWorkspaceNav.cshtml`,
  `wwwroot/js/site.js` (retained by CASE-038 — ENG-036 never takes this lock),
  `wwwroot/css/site.css`, and `Presentation/OperatorLabels.cs` until the
  shared-lock hand-off.

- [[PLAT-070]]: `_ReadinessHiddenFields.cshtml` and `_CaseWorkflow.cshtml` —
  the surviving D44 staff-review flags and checkboxes are PLAT-070's removal,
  not ENG-036's.

- [[CASE-029]]: `_CaseVehicle.cshtml`, `_CaseValuation.cshtml`, vehicle lookup
  handlers, and valuation tests.

- [[CASE-043]]: `src/Pegasus.Core/Cases/CaseDataContracts.cs`,
  `src/Pegasus.Infrastructure/Persistence/CaseDataEntities.cs`, its additive
  migration and grants, the vehicle extraction vocabulary, and the intake
  caller of the DVLA/MOT lookup port (D49).

- [[UIIMP-014]]: `docs/design/test-ui/**` and the final Case-record browser
  walk at 1580/1100/760.

- Governing documents, including `docs/design/README.md`. Its Damage bullet
  still carries the D45 "Type" residue; the correction is raised in
  `open-questions` for its owning lane, not made here.

## Wrapper note

The shared-lock rows (`site.css`, `OperatorLabels.cs`) are conditional on an
explicit hand-off recorded in the ENG-036 plan; until then they belong to
[[CASE-038]]. `_CaseDamage.cshtml` is an ENG-034 create and an ENG-036 change
of its body only. The renderer rows that previously appeared here
(`PlaywrightAssessmentReportRenderer.cs`,
`AssessmentReportRendererTests.cs`) were removed on 2026-09-03: ENG-035 keeps
those whole files and ENG-036 ships the damage partial plus its own new tests.
