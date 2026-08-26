# Files — UIIMP-003

## Where the change lands

| Path | Why |
|---|---|
| `src/Pegasus.Web/Pages/<selected-page>.cshtml` | Port the approved semantic markup delta while retaining Razor bindings, Tag Helpers, conditions, and forms. Exact path is selected after [[UIIMP-002]]. |
| `src/Pegasus.Web/Pages/<selected-page>.cshtml.cs` | Change only if the approved experience requires a real server-presented state; presentation work must not invent business behavior. |
| `src/Pegasus.Web/wwwroot/css/site.css` | Reuse existing classes first; add only the smallest approved shared or page-specific rules. |
| `src/Pegasus.Web/wwwroot/js/site.js` | Change only for progressive UI behavior that cannot use native HTML; never move policy or validation authority here. |
| `tests/Pegasus.IntegrationTests/<focused-web-tests>.cs` | Preserve route, authorization, forms, antiforgery, validation, state, and generated-markup contracts for the selected page. |
| `tests/Pegasus.IntegrationTests/Browser/<focused-browser-tests>.cs` | Prove the selected interaction, keyboard/focus behavior, responsive/zoom behavior, and visual-state DOM. |
| `docs/design/README.md` | Record only the approved durable behavior or component rule; do not describe a prototype as deployed or accepted. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `docs/design/test-ui/pages/<selected-state>.html` | The user-approved presentation target; unchanged regions are not authorization for redesign. |
| `docs/frd/frd-12-operator-experience.md` | Governing state, accessibility, responsive, vocabulary, and truthful-evidence requirements. |
| `src/Pegasus.Web/Pages/Shared/_Layout*.cshtml` | Existing shell ownership; do not copy a shell into a page. |
| `src/Pegasus.Web/Pages/Shared/*.cshtml` | Existing prepared-markup reuse candidates; extract only when a second real caller or current clarity requires it. |
| `tests/Pegasus.IntegrationTests/Browser/AccessibilityTests.cs` | Existing axe/semantic/style expectations and the correct accessibility evidence level. |
| `AGENTS.md` | One Core owner, no speculative abstraction, no compatibility fallback, and independent simplification/review requirements. |

## Ripple effects

Each selected page can affect shared layouts, CSS, JavaScript, browser tests, design documentation, and other pages using the same classes. The implementation plan must name those callers after selection. A broad selection is split rather than hidden inside this ticket.

## Out of scope

Selecting a prototype on the user's behalf, changing business requirements, adding a generic HTML↔Razor converter, creating a new skill without evidence, retaining old and new UI side by side, or deploying Test UI.
