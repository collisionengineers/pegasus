# Files — UIIMP-002

## Where the change lands

| Path | Why |
|---|---|
| `docs/design/test-ui/index.html` | Canonical catalogue and single route-classification list; groups visual pages and states and records reasons for non-visual routes. |
| `docs/design/test-ui/pages/*.html` | Flat, double-clickable rendered replicas and state variants using real semantic structure and class names. The main risk is drift from current Razor. |
| `scripts/Test-UiCatalogue.ps1` | Enforce routed-file coverage, classification uniqueness, linked-page existence, relative asset resolution, and isolation from Web publish inputs. |
| `docs/design/README.md` | Add the Test UI boundary and state that it is disposable design evidence, not implemented, deployed, or accepted behavior. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `src/Pegasus.Web/Pages/**/*.cshtml` | Current routed markup, shells, partial composition, and route directives; code and rendered behavior outrank old mockups. |
| `src/Pegasus.Web/Pages/**/*.cshtml.cs` | Whether a route renders HTML, redirects, returns a file, and which realistic states exist. |
| `src/Pegasus.Web/wwwroot/css/site.css` | The only runtime styling source; prototypes reference it rather than authoring a parallel stylesheet. |
| `src/Pegasus.Web/wwwroot/js/site.js` | Existing progressive interactions to reuse where they function safely under `file:`; business rules must not move into prototype JavaScript. |
| `src/Pegasus.Web/Pages/Shared/_Layout*.cshtml` | The three shell families and their accessibility/navigation landmarks. |
| `docs/design/system/` | Existing component names and exact-class design-tool bindings; reuse where useful without making React a runtime requirement. |
| `docs/design/references/mockups/inbox-message-page/README.md` | Accepted local-preview precedent and the distinction between a proposal and approved runtime behavior. |
| `docs/frd/frd-12-operator-experience.md` | Required visual states, responsive behavior, and accessibility coverage. |

## Ripple effects

[[UIIMP-001]] consumes the catalogue index. Any future Razor route addition must update the catalogue classification or fail the validator. Design review gains local visual artifacts, but application tests and deployment remain unchanged.

## Out of scope

Running Razor, SQL, authentication, services, form submissions, business behavior, production data, generated screenshots as authority, and changing the Live UI.
