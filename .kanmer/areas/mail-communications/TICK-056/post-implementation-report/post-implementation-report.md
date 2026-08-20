# Post-implementation report — TICK-056

## Summary

Completed the final accessible UI-10 workspace assembly on the existing retained-mail list. One thin authenticated GET handler delegates to `GetRetainedMail` for the only preview fact absent from list rows (attachment names); the existing subject link, table, filters, Core policies, stores and message-detail actions remain authoritative. Existing `site.js` and `site.css` progressively add selected-row quick preview beside the dense table at desktop width and after it at constrained/200%-equivalent width. The transient imagegen preview influenced only this table/preview relationship; no bitmap entered the repository.

## Changes

| File | Change | Why |
|---|---|---|
| `src/Pegasus.Web/Pages/Mail/Index.cshtml.cs` | Injected existing `GetRetainedMail` and added authenticated `OnGetPreviewAsync` returning sender, subject, timestamp, excerpt, current classification, Case association and attachment names. | Reuses the exact authorized detail use case instead of widening the SQL list projection or adding a preview service/store. |
| `src/Pegasus.Web/Pages/Mail/Index.cshtml` | Kept the current semantic table/detail link, added progressive row/trigger attributes and one evidence-only preview `aside`. | Provides pointer/keyboard intent and screen-reader status without a new action, outcome, mode or no-JS dependency. |
| `src/Pegasus.Web/wwwroot/js/site.js` | Added bounded exact-row selection, abortable/cached preview fetch, safe text rendering, loading/error state and pointer/focus dismissal. | Reuses the CSP-safe application script and prevents stale responses or client-injected markup. |
| `src/Pegasus.Web/wwwroot/css/site.css` | Added existing-token desktop panes, selected row, preview facts, constrained stacking and forced-colour rules. | Keeps the table primary, avoids overlay/clipping, preserves visible state and lets 200% zoom trigger the ordinary ordered fallback. |
| `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` | Added authenticated exact payload/markup/not-found/roleless proof and database before/after assertions. | Proves previewing changes no read state, classification history or association history and exposes no form/button. |
| `tests/Pegasus.IntegrationTests/Browser/MailWorkspaceBrowserTests.cs` | Added real Playwright keyboard, pointer, focus-away, axe, desktop, constrained/no-overflow and JavaScript-disabled detail-link journeys with a retained-message fixture. | Proves the loaded shared script/CSS and fallback rather than only server markup. |
| `docs/capabilities.md` | Reconciled UI-10 with the exact local caller/evidence and retained deployment/live-operation qualifications. | Removes the stale “final assembly allocated” claim without claiming deployment or mailbox mutation. |

## Governing docs

- `docs/frd/frd-08-email-mailbox-and-background-processing.md`: exact authorized message evidence is shown on pointer/keyboard intent; focus departure dismisses it; no preview control mutates classification, association, read state, Case state or custody; full detail remains an exact-message link.
- `docs/design/README.md`: table/preview relationship uses dense desktop panes, ordered constrained/200% fallback, visible selected/focus state, screen-reader announcement, forced-colour support and no overlay. Its required local design-system build completed; generated `dist/` and `node_modules/` remain intentionally ignored.
- No PRD/FRD/ADR behavior was modified and no new ADR was needed.

## Risks / follow-ups

- Deployment and live read-only acceptance remain separate evidence. No Graph, Outlook, Box, cloud, permission, deployment or external write occurred.
- The full shared Browser lane initially passed 47/48 with one `net::ERR_NO_BUFFER_SPACE` navigation failure on unchanged `/Administration/Mailboxes`; that exact existing test passed immediately in isolation (1/1). The focused changed Browser class passed both before and after simplification.
- No Core, EF, schema, migration, policy, message-action, generic preview framework, inline script, new mode/toolbar/card/page-size control/outcome or bitmap asset was added.

## Simplification pass — 2026-08-21

- **Reuse:** used `RetainedMailSummary`, `GetRetainedMail`, existing display helpers, `site.js`, `site.css` and `BrowserTestSupport`; no second policy/query/test harness.
- **Simplification:** removed a redundant row message-id attribute and unused preview layout marker; kept one handler and one markup region.
- **Efficiency:** reads only the selected exact message, aborts superseded requests and caches successful preview evidence for the page lifetime; no list-wide detail/attachment load.
- **Altitude:** all behavior stays in Web presentation and tests; Core/Infrastructure/action owners are untouched.
- All findings were applied. No unapplied finding or follow-up ticket remains.

## Verification hand-off

Run on merged `main`:

- `dotnet restore ./Pegasus.slnx --locked-mode` — expected green.
- `dotnet build ./Pegasus.slnx --configuration Release --no-restore` — expected 0 warnings/errors.
- `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~MailWorkspaceWebTests" -- xUnit.MaxParallelThreads=1` — branch result 39/39.
- `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~MailWorkspaceBrowserTests" -- xUnit.MaxParallelThreads=1` — branch result 2/2.
- `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "Category=Browser&Category!=Corpus" -- xUnit.MaxParallelThreads=2` — branch result 47/48 with the isolated workstation socket failure above; exact rerun green.
- Inspect `/Inbox` at 1280px and a 640px 200%-equivalent viewport: preview sits adjacent then stacks after the table, the document does not overflow, focus departure dismisses it, and the subject link opens full detail with JavaScript disabled.
