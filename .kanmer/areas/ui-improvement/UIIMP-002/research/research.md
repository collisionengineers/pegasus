# Research — UIIMP-002: complete static Test UI catalogue

## Question

How can Pegasus expose disposable, locally viewable replicas of all real visual pages while staying faithful to Razor markup and design rules and remaining outside the application?

## Findings

- `src/Pegasus.Web/Pages` currently contains 76 `.cshtml` files: 52 declare `@page` and 24 are layouts or partials. The `@page` count is discoverable and must be checked at implementation time rather than frozen as a permanent contract.
- Not every routed file is a visual page. Source/image/document endpoints return bytes, Export redirects on GET, SignOut redirects, and Search/Unidentified include compatibility redirects. Coverage must classify every route as visual, redirect, download/inline content, or protocol endpoint before requiring an HTML replica.
- The authenticated, auth, and external shells are owned by `_Layout.cshtml`, `_LayoutAuth.cshtml`, and `_LayoutExternal.cshtml`; shared partials and `site.js` carry reusable behavior. A prototype must copy rendered semantic markup, not Razor handlers or business policy.
- `docs/design/system` already provides React design-tool bindings with the real class names and a build that copies `site.css`. It is design-only and not referenced by the application. It is useful source context but is not required at prototype runtime.
- `docs/design/references/mockups/inbox-message-page/preview/*.html` proves the accepted precedent: plain local HTML, real class names, explicit states, and no application caller. Older `design/planning-and-old-designs/PegasusClaudeDesign/screens` proves broad page coverage but is historical and may be stale.
- The current stylesheet is a single `src/Pegasus.Web/wwwroot/css/site.css`; pages also use `site.js`, inline Lucide sprite markup, and approved marks. A flat catalogue can reference those tracked assets by repository-relative paths and remain double-clickable without copying another styling system.
- FRD-12 and `docs/design/README.md` require loading, empty, current, stale, unavailable, partial, failed, validation, conflict, and access-denied states where applicable, plus keyboard, screen-reader, 200% zoom, forced-colour, reduced-motion, and responsive behavior.
- Repository rules prohibit fabricating domain emails, images, documents, data, or work instructions. Static examples must use already approved test fixtures or established evidence-safe values and must not copy local corpus or production material.

## Implications

Create one isolated catalogue at `docs/design/test-ui/` with a machine-readable route inventory embedded in `index.html`, flat state pages, and relative references to the real tracked CSS/JS/assets. A focused validator must enumerate current `@page` files, require exactly one classification for each, require linked prototypes for every visual route, reject orphaned entries, and verify local assets. State variants use `<route-key>--<state>.html`. Non-visual routes are recorded with a reason rather than represented by fake screens.

## Open questions

None.
