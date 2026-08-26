# Files — Razor-generated Test UI

## Change map

| Area | Expected change | Risk |
| --- | --- | --- |
| `tests/Pegasus.IntegrationTests` | Add scenario registry, real-browser capture, normalization/rewrite and parity tests using existing factory/support | Scenario setup must represent the named branch without inventing domain evidence |
| `scripts/Update-TestUiSnapshots.ps1` | Explicit update entrypoint that invokes the focused generator | Must never rewrite snapshots during ordinary tests |
| `docs/design/test-ui` | Replace manual inventory/pages with generated manifest, index and 60 rendered snapshots | Generated output must remain locally viewable and reviewable |
| `scripts/Test-UiCatalogue.ps1` | Validate generated provenance, coverage, references and clean regeneration | Must not retain a second route/state inventory |
| `docs/design/README.md`, `README.md`, `docs/runbook.md` | Correct the authority and regeneration workflow | Must not claim behaviour beyond captured states |

## Ripple effects

- `scripts/Invoke-LocalDevelopment.ps1 -UiMode Test` continues opening the same catalogue path.
- Web/Worker project and release inputs must continue excluding `docs/design/test-ui`.
- Existing Web and browser test helpers are reused rather than duplicated.
- `UIIMP-002` proof/outcome must be superseded through the new linked fix ticket, not rewritten as if the first proof were valid.

## Context files

| File | Why |
| --- | --- |
| `src/Pegasus.Web/Pages/Shared/_Layout.cshtml` | Defines shell, sprite, navigation, authentication controls and site.js |
| `tests/Pegasus.IntegrationTests/IntakeWebTestSupport.cs` | Existing deterministic app host and authentication |
| `tests/Pegasus.IntegrationTests/Browser/BrowserTestSupport.cs` | Existing Playwright/loopback mechanism |
| `docs/frd/frd-12-operator-experience.md` | Governs UI states and accessibility evidence |
| `docs/design/test-ui/index.html` | Current 52-route/60-state roster to preserve |

## Out of scope

- No Live Razor redesign.
- No new business data, product states, frontend framework, runtime mode, project or deployment unit.
- No generic Razor/HTML conversion skill.
- No attempt to make offline form submissions execute server behavior.
