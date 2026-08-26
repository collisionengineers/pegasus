# Research — UIIMP-003: approved prototype reintegration

## Question

What is the smallest safe workflow for carrying an approved static Test UI experiment into the deployable Razor UI without losing application behavior or creating a permanent second implementation?

## Findings

- Live UI is ordinary ASP.NET Core Razor Pages: `Program.cs` registers/maps Razor Pages; page-specific composition lives in `.cshtml` and `.cshtml.cs`; shared document structure uses three layouts and prepared shared markup uses partials.
- The static catalogue cannot encode PageModel queries, authorization, model binding, antiforgery, validation, redirects, concurrency/version checks, file responses, or server-known state. Only agreed presentation markup and CSS may cross back.
- `src/Pegasus.Web/wwwroot/css/site.css` and `site.js` are shared runtime owners. A prototype must reuse an existing class/pattern or justify a narrowly scoped addition; no second stylesheet pipeline or browser business policy is needed.
- Existing `razor-pages-ui-design` and `razor-pages-ui-implementation` skills already cover page/state design, Razor mechanism selection, semantic controls, forms, validation, antiforgery, accessibility, and proportional verification. A new conversion skill has no second concrete need and is not justified now.
- FRD-12 requires truthful state labels and all applicable loading/empty/stale/unavailable/partial/failed/validation/conflict/access-denied states, plus keyboard, screen-reader, zoom, forced-colour, reduced-motion, and responsive support.
- The exact page, states, and accepted visual changes do not exist until [[UIIMP-002]] produces the catalogue and the user selects a prototype. Those choices cannot be inferred during this preparation pass.
- One approved page or coherent shared pattern is one implementation unit. If the eventual selection spans unrelated pages or behaviors, it must be split into separate tickets before execution.

## Implications

Use a manual, reviewable comparison: selected static state versus current Razor page, PageModel, shared partials, CSS/JS, and focused tests. Port only the approved delta into existing owners, preserve all server behavior, and delete superseded markup. Do not create a generic converter or new skill unless a later evidenced repeated failure cannot be handled by the two existing Razor skills. This ticket stays dependency-blocked and its selection question is explicitly deferred until a prototype exists.

## Open questions

The exact selected prototype and approved delta are deferred below because [[UIIMP-002]] has not produced candidates yet.
