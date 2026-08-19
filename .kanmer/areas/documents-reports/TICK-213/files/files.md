# Files — density scope

| Path | Expected change | Risk |
| --- | --- | --- |
| Migrated renderer contracts/descriptor | Keep internal per-template fit profile; assessment templates use no target | Accidental global compaction |
| Migrated report CSS/templates | Preserve accepted normal design and page flow | Visual regression |
| `src/Pegasus.Core/Reports/**` | No density option in business contract | Transport leakage |
| Migrated renderer tests | Prove assessment normal density and clean overflow | Chromium cost |
| Visual regression fixtures | Compare four approved variants/fee note and stress cases | Platform font variance |

## Context files

| Path | Why |
| --- | --- |
| `workspaces/report-renderer/docs/adr/0007-density-auto-fit.md` | Existing per-template decision |
| `workspaces/report-renderer/src/CollisionRenderer.Core/DocumentRenderer.cs` | Actual density algorithm |
| `reference/rendererref1/DESIGN_SPEC.md` | Approved initial design and stress expectations |
| `TICK-206 research` | Approved active template subset |

## Out of scope

- Valuation one-page auto-fit, because that family is inactive.
- Caller-selectable density.
- Universal page-count targets.


## Re-plan file delta — 2026-08-19

| Path | Change | Reason |
| --- | --- | --- |
| `tests/Pegasus.IntegrationTests/Reports/AssessmentReportRendererTests.cs` | Add one real-Chromium long-list/multi-photo continuation test, reusing the existing composed renderer and supplied image fixture | Closes the missing stress-evidence slice without changing production styling or contracts |

No production, CSS, template, lock, solution, CI, or documentation file should change.
