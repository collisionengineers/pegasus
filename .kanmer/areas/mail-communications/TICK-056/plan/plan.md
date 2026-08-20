# Plan — TICK-056: UI-10 full email-management workspace

## Approach

Complete only the missing quick-preview composition around the landed mailbox/folder/queue/search table. Reuse `RetainedMailSummary` for list evidence, delegate the one missing attachment-name read to the existing authorized `GetRetainedMail`, and progressively enhance the unchanged full-detail link through the existing `site.js`/`site.css` conventions. This is smaller than widening the SQL projection or adding a preview abstraction and keeps every mutation on the exact message-detail page.

## Governing docs

- **Meets `docs/frd/frd-08-email-mailbox-and-background-processing.md`:** the handler returns sender, subject, timestamp, excerpt, classification, association and attachment names for one authorized retained message; preview is GET-only, contains no controls, preserves full-detail navigation and changes no mail/Case state.
- **Uses `docs/design/README.md` as the existing UI convention:** dense table plus adjacent preview at desktop width; an ordered stacked section at constrained width/200% zoom; pointer and keyboard intent, visible selected/focus state, screen-reader status and focus-departure dismissal.
- **No governing document modification or ADR:** the accepted Web/Core boundary and behavior already cover the change. `docs/capabilities.md` receives evidence wording only.

## Steps

1. Add the thin authenticated `IndexModel.OnGetPreviewAsync` using `GetRetainedMail`, returning only canonical display facts and fail-closed not-found/forbidden results.
2. Enhance the existing list markup, `site.js` and `site.css` so rows select on pointer/keyboard intent, the adjacent evidence-only preview is announced and dismissed on focus departure, responsive stacking works, and the subject remains the no-JS detail link.
3. Add focused authenticated Web and Browser tests for exact payload/markup, no mutation controls or state changes, pointer/keyboard/focus behavior, no-JS navigation, axe and constrained/200%-equivalent layout.
4. Run locked restore/Release build and proportional focused/full tests; apply and record reuse, simplification, efficiency and altitude lenses; update only the UI-10 capability evidence and write the PIR/traceability/PR.

## Verification

Run `dotnet restore --locked-mode`, `dotnet build --configuration Release --no-restore`, focused `MailWorkspaceWebTests`, focused `MailWorkspaceBrowserTests`, then the relevant IntegrationTests project (or the repository runbook's proportional split if SQL/browser profiles are separate). Inspect the rendered DOM at desktop, constrained viewport and browser zoom; prove no horizontal document overflow, axe violations or state mutation from preview GET. Record exact commands/results in the PIR; merged-main proof remains for `kanmer-verify`.

## Risks / open questions

- Fetch races could show the wrong row: abort the prior request and update only if the same row remains selected.
- Pointer dismissal must not break keyboard use: treat focus as authoritative and dismiss only after focus leaves the row/preview relationship.
- The generated UX image is a preview-only constraint, not an asset or authority; implementation follows FRD/design and existing CSS tokens.
- No open product question remains.
