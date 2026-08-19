# Research — PR-009: long assessment pagination omission

## Question

Why does the integrated real-Chromium renderer omit the trailing Statement of Truth/signature when accepted repair lists and photos produce a long assessment, and what is the smallest safe correction?

## Findings

### The defect is reproducible on the merged renderer

Read-only execution of TICK-213's already-built verification test (no source edit) reproduced the failure: a Repairable assessment with 80 uniquely labelled items in each of the three work lists and 8 accepted hashed photos rendered in about 3 seconds, retained every terminal `080` entry, but PdfPig could not find `Statement of Truth`. The existing four-outcome representative test uses one item per list and one photo and therefore misses this boundary.

Source: `tests/Pegasus.IntegrationTests/Reports/AssessmentReportRendererTests.cs` on TICK-213's worktree and the focused `--no-build` test result.

Implication: this is a production rendering defect, not a density-policy question. PR-009 owns the fix; TICK-213 remains blocked and must not absorb production edits.

### The trailing content is present in the HTML template

The assessment template emits, in order: three work-list sections, an empty `.page-break` divider, the Vehicle Images section with `.photo-grid`, another empty `.page-break` divider, then Statement of Truth and signature. Core always supplies fixed Statement of Truth paragraphs and the accepted signature resource.

Source: `docs/design/assets/report-renderer/templates/assessment_report.scriban` and `src/Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs`.

Implication: omission occurs during Chromium print fragmentation, not because the renderer conditionally excludes or fails to compose the content.

### The risky construct is an unbounded print-fragmented CSS grid followed by a forced-break sentinel

The photo section is a CSS Grid with two columns and 48mm fixed-height images. Chromium must fragment that grid when content and preceding forced pages put it near a page boundary. The following page break is expressed as an empty element using `break-before: page`. CSS print fragmentation of grid containers and empty forced-break sentinels is less reliable than block-flow content whose break is attached to the real following section.

Source: template/CSS inspection: `.photo-grid { display: grid; ... }`, `.vehicle-photo { height: 48mm; break-inside: avoid; }`, and `.page-break { break-before: page; }`.

Implication: the first implementation experiment should remove grid fragmentation from assessment photos and attach the forced break to the following semantic section (or otherwise use block rows), preserving the approved two-column/no-caption/48mm presentation. It must not alter density, shrink content, cap list/photo counts, or multipass/retry.

### Governing behaviour forbids content loss and density invention

FRD-11 fixes the active rendererref1 assessment/fee-note surface, accepted resources and no caller-selectable density. ADR-0025 requires the renderer remain inside the existing Infrastructure adapter. The supplied design specifies ordered photos, normal styling and normal page flow.

Source: FRD-11 lines 46–69, ADR-0025, EPIC-004 context and `reference/rendererref1/DESIGN_SPEC.md`.

Implication: the fix belongs in the existing template/CSS/adapter and must preserve all content and page furniture. No new setting, project, renderer pass, or policy owner is justified.

## Verified premises versus assumptions

Verified: real Chromium failure; terminal work-list content survives; trailing Statement of Truth does not; HTML always contains the trailing section; current photos use CSS Grid; explicit break sentinels separate phases; no density selector exists.

To verify during execution: which minimal block/row and semantic-break adjustment makes Chromium retain the full tail; whether all eight images remain embedded and ordered; whether every page retains reference/footer text; whether the existing four-outcome suite remains green.

No operator-only question remains. This is a correctness repair under existing approved behaviour.
