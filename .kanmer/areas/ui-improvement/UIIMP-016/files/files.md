# Files — UIIMP-016

## Where the change lands

| Path | Why |
| --- | --- |
| `docs/prd/pegasus-product.md` | Replace the Windows Edge/Narrator quality gate with package-pinned Chromium automation and its explicit screen-reader limitation. |
| `docs/engineering.md` | Align evidence tier 7 with the exact automated keyboard, focus, reflow, forced-colour, reduced-motion, semantic and axe scope. |
| `docs/design/README.md` | Preserve desired accessibility behavior while defining which acceptance items are automated and which are not claimed. |
| `docs/runbook.md` | Remove Edge/Narrator as a workstation/release prerequisite and make the Browser command the complete selected accessibility evidence procedure. |
| `docs/operations.md` | Update the current Browser profile snapshot to reflect the selected evidence and limitations. |
| `docs/frd/frd-12-operator-experience.md` | Clarify that screen-reader-compatible semantics remain required behavior but screen-reader interoperability is outside current acceptance evidence. |

## Context files

| Path | What it tells the implementer |
| --- | --- |
| `tests/Pegasus.IntegrationTests/Browser/AccessibilityTests.cs` | Existing route, axe, landmark, reflow, forced-colour, reduced-motion and colour-independent assertions. |
| `tests/Pegasus.IntegrationTests/Browser/OperatorJourneyTests.cs` | Existing keyboard activation, skip-link and visible-focus proof. |
| `tests/Pegasus.IntegrationTests/Browser/BrowserTestSupport.cs` | One package-pinned Chromium caller and axe owner; do not duplicate it. |
| `tests/Pegasus.IntegrationTests/Browser/LayoutIntegrityTests.cs` | Existing authenticated-route overflow/reflow coverage. |
| `EPIC-013/context.md` | Automation-only decision and prohibition on claiming screen-reader coverage. |
| `docs/prd/pegasus-product.md` | Governing product quality currently carrying the Windows-only gate. |

## Ripple effects

Documentation-link and Markdown placement checks must pass. The full Browser lane remains the executable release evidence and must pass on Linux. No UI snapshots are required because no routed page changes. DELIV-047 can remove Windows release-workstation dependence only after this evidence contract lands.

## Out of scope

Razor markup or CSS changes, a new accessibility library, WCAG conformance certification, manual accessibility review, Edge automation, Narrator or other screen-reader interoperability, mobile support, operator acceptance, CI redesign and production release.
