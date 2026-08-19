# Files — PR-009

## Change surface

| File/module | Expected change | Risk |
| --- | --- | --- |
| `docs/design/assets/report-renderer/templates/assessment_report.scriban` | Replace the fragile photo-grid/empty-break boundary with semantic print-flow markup if required by the reproduction. | This is governed report presentation; preserve section order, headings, wording, normal style and two-column photos. |
| `docs/design/assets/report-renderer/templates/report.css` | Add the smallest print-safe photo row/section fragmentation rules and attach forced breaks to real sections. | Broad global break/density changes could alter unrelated fee/report layout; scope selectors to assessment photo/tail elements. |
| `src/Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs` | Only if needed, render ordered photos as explicit two-image block rows rather than one unbounded grid string. | Must preserve custody-validated bytes and caller order; avoid a second renderer/policy implementation. |
| `tests/Pegasus.IntegrationTests/Reports/AssessmentReportRendererTests.cs` | Add the TICK-213 real-Chromium stress reproduction to the owning regression suite and assert terminal lists, 8 embedded images, Statement of Truth/signature and page furniture. | PDF text/image extraction can be structural rather than pixel-exact; assertions must remain strong and deterministic. |

## Ripple effects

| Consumer/artifact | Check |
| --- | --- |
| Existing four assessment outcomes | Run the full focused real-Chromium renderer suite. |
| Fee note | It shares CSS but not assessment photo markup; ensure scoped rules do not affect it. |
| Embedded resources | Templates/CSS are embedded by Infrastructure; Release rebuild is required before Browser tests. |
| TICK-213 | Inspect/copy the reproduction intent without editing its worktree; PR-009 remains its structured blocker until independently reviewed and merged. |
| Documentation | No governing behaviour changes. FRD/ADR edits are neither needed nor authorized. |
| CI/browser lane | The regression carries `Category=Browser` and must run in the existing browser lane. |

## Context files

| File | Why read |
| --- | --- |
| `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md` | Active family, fixed resources, fail-closed and no caller density authority. |
| `docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md` | Existing monolith/Infrastructure boundary. |
| `reference/rendererref1/DESIGN_SPEC.md` | Normal styling, ordered photos, no captions and fixed report structure. |
| `src/Pegasus.Core/Reports/AssessmentReportRendering.cs` | Core owns accepted content/custody; the pagination repair must not move policy. |
| TICK-213 checklist and local test diff | Exact stress fixture and assertions that exposed the omission. |
| SIMPLI-014 proof/PIR | Existing Chromium, page/hash/resource and four-outcome evidence baseline. |

## Deliberately out of scope

Caller-selectable density; compact/ultra-compact assessment modes; content truncation; item/photo caps; global auto-fit or multipass rendering; wording, arithmetic, identity, workflow, storage, deployment or Azure changes.
