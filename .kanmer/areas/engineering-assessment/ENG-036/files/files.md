# Files — ENG-036 (2026-09-02, gpt-5.6-terra xhigh, wrapper-checked)

## Prospective map

- **VERIFIED** — `git cat-file -e HEAD:<path>` confirms every existing path
  below exists; `damage-diagram.js`, `_CaseDamage.cshtml`, and the proposed
  shared SVG asset are absent.

- **ASSUMED** — the SVG asset proposal is the smallest way to keep vehicle
  geometry in one place for both JavaScript and the Playwright renderer. The
  conditional rows require the named shared-lock hand-off.

| Path | Action (create/change) | Why | Reuses |
| --- | --- | --- | --- |
| `docs/design/assets/report-renderer/templates/damage-diagram.svg` | create | Single source for zone geometry, wheel geometry, marker anchors, and report-safe SVG structure. | Existing report-template asset directory. |
| `src/Pegasus.Infrastructure/Pegasus.Infrastructure.csproj` | change | Embed the shared SVG for the renderer. | Existing embedded Scriban/CSS resource entries. |
| `src/Pegasus.Web/Pegasus.Web.csproj` | change | Publish/link the same SVG as a static browser asset without copying it. | SDK content-item convention. |
| `src/Pegasus.Web/wwwroot/js/damage-diagram.js` | create | Clone the SVG, map supplied Core codes to markers, support click/Enter/Space, and render read-only state. | Existing `data-*` event hooks and `data-edit-save` lifecycle. |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseDamage.cshtml` | change | Replace [[ENG-034]]'s read-only shell with the D39 body and one leased assessment form contract. | Existing Case partials and [[ENG-034]]'s shell. |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` | change, conditional shared-lock hand-off | Add the sole operator-facing labels for D39 headings, zones, severities, types, tyres, and belts. | `OperatorLabels.CaseWorkspace`. |
| `src/Pegasus.Web/wwwroot/css/site.css` | change, conditional shared-lock hand-off | Add the component layout, markers, impact rows, tyre cards, and 1180/760px rules. | Existing layout, panel, form, and responsive rules. |
| `src/Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs` | change, only after [[ENG-035]] transfers ownership | Read the shared SVG and add the marked-report HTML to Scriban context. | `ResourceText`, `Encode`, and existing HTML/PDF pipeline. |
| `tests/Pegasus.IntegrationTests/Reports/AssessmentReportRendererTests.cs` | change, only after [[ENG-035]] transfers ownership | Prove a rendered PDF includes the diagram's expected text/markers and remains readable. | Existing renderer-composition and PdfPig tests. |

## Files ENG-036 must not touch

- [[ENG-035]]: `src/Pegasus.Core/Assessment/AssessmentContracts.cs`,
  `AssessmentPolicy.cs`, `src/Pegasus.Core/Reports/AssessmentReportProjection.cs`,
  `AssessmentReportRendering.cs`, `EfCaseAssessmentStore.cs`,
  `Persistence/Migrations/**`, `assessment_report.scriban`, and Core
  projection/rendering tests.

- [[ENG-034]]: `Pages/Cases/Assessment/Index.*`, the Assessment redirect
  tests, and `_CaseDamage.cshtml` until its extracted shell has merged.

- [[ENG-029]]: `_CaseSettlement.cshtml`, `_CaseReport.cshtml`, Case-page
  `ISaveAssessment` handlers, and assessment Web tests.

- [[ENG-031]]: report-image crop, role, ordering, and related rendering paths.

- [[CASE-038]]: `Pages/Cases/Details.*`, `_CaseWorkspaceNav.cshtml`,
  `wwwroot/js/site.js`, `wwwroot/css/site.css`, and
  `Presentation/OperatorLabels.cs` until the shared-lock hand-off.

- [[CASE-029]]: `_CaseVehicle.cshtml`, `_CaseValuation.cshtml`, vehicle lookup
  handlers, and valuation tests.

- [[UIIMP-014]]: `docs/design/test-ui/**` and the final Case-record browser
  walk at 1580/1100/760.

- Governing documents, including `docs/design/README.md`; D39 component
  vocabulary is already present.

## Wrapper note

The renderer rows (`PlaywrightAssessmentReportRenderer.cs`,
`AssessmentReportRendererTests.cs`) and the shared-lock rows (`site.css`,
`site.js` loader, `OperatorLabels.cs`) are conditional on an explicit
hand-off recorded in the ENG-036 plan; until then they belong to [[ENG-035]]
and [[CASE-038]] respectively. `_CaseDamage.cshtml` is an ENG-034 create and
an ENG-036 change of its body only.
