# Files — TICK-056

*Surveyed after UI-14 landed on `origin/dev` at `ee88c70c`.*

## Where the change lands

| Path | Why |
|---|---|
| `src/Pegasus.Web/Pages/Mail/Index.cshtml.cs` | Inject landed `GetRetainedMail` and expose one thin authenticated exact-message preview GET returning only the seven FRD-08 facts. Risk: authorization/not-found semantics must remain fail-closed. |
| `src/Pegasus.Web/Pages/Mail/Index.cshtml` | Keep the current folder/queue/search/table surface, annotate exact rows/links for progressive enhancement, and add one adjacent accessible preview region with the full-detail link unchanged. Risk: semantic table and no-JS navigation must remain intact. |
| `src/Pegasus.Web/wwwroot/js/site.js` | Reuse the existing CSP-safe enhancement convention for pointer/keyboard selection, bounded fetch, accessible loading/error state and focus-departure dismissal. Risk: enhancement must not steal focus or create a mutation surface. |
| `src/Pegasus.Web/wwwroot/css/site.css` | Reuse existing tokens for selected-row/focus state, desktop table+preview columns and constrained/200% stacked fallback. Risk: no overflow or adjacent-control obstruction. |
| `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` | Add authenticated handler/markup evidence, exact-message/not-found authorization behavior, no mutation controls and no read/classification/association state change. |
| `tests/Pegasus.IntegrationTests/Browser/MailWorkspaceBrowserTests.cs` | Focused real-JavaScript proof for keyboard/pointer opening, selected-row state, focus-away dismissal, no-JS detail fallback, axe and constrained/200%-style layout. |
| `docs/capabilities.md` | Replace the remaining UI-10 “final assembly allocated” note with exact local caller/test evidence; retain deployment/live-mailbox qualifications. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `src/Pegasus.Core/Intake/RetainedMail.cs` | `RetainedMailSummary` already contains all preview evidence except attachment names; `GetRetainedMail` is the authorized exact-message detail seam. Do not modify it. |
| `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs` | Landed MAIL-11/UI-14 query/filter/paging owner. UI-10 must not add another projection or persistence path. |
| `src/Pegasus.Web/Pages/Mail/Message.cshtml(.cs)` | Existing full-detail/no-JS destination and sole home of classification, Case and folder-move actions. Do not edit its commands. |
| `tests/Pegasus.IntegrationTests/Browser/BrowserTestSupport.cs` | Existing authenticated Playwright/axe/viewport/no-JS harness to reuse. |
| `docs/frd/frd-08-email-mailbox-and-background-processing.md` | Exact preview content, evidence-only/no-side-effect contract and workspace navigation behavior. |
| `docs/design/README.md` | Dense desktop panes, constrained/200% ordered fallback, keyboard/focus and no-obscuring rules. |
| EPIC-003 and EPIC-006 `context.md` | UI remains functionally mail-owned; one Core implementation and no local-alpha Outlook mutation. |

## Ripple effects

The `IndexModel` constructor change uses already-registered `GetRetainedMail`. Browser coverage requires the repository's installed pinned Chromium but no new package or harness. `site.css` changes may require design-system sync only if the repository's existing build/check says its committed copy is an owned artifact; verify before adding any generated output.

## Out of scope

No Core/EF/schema/migration/store/query-policy change; no message-detail command edit; no generic preview/action framework; no inline script or new bundle; no Mail/Notifications mode, Refresh redesign, Mark-all, New-message, page-size control, dashboard card, bulk action or new outcome; no bitmap asset; no Graph/Outlook/Box/cloud/deployment/external write.
