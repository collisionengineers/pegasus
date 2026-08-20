# Checklist — TICK-056

- [x] Add the thin authenticated exact-message preview handler using `GetRetainedMail`.
- [x] Add progressive table-row/adjacent-preview markup, JavaScript and responsive CSS while preserving the no-JS detail link.
- [x] Add exact authenticated Web and Browser evidence for payload, no mutation, keyboard/pointer/focus, no-JS, axe and constrained layout.
- [x] Run locked restore/build and proportional focused/full verification; complete the four simplification lenses.
- [ ] Update narrow UI-10 capability evidence, write the PIR/traceability, push and open the PR to `dev` in Review.

## Progress notes

- 2026-08-21: Plan refreshed after UI-14 merged at `ee88c70c`. Parent-approved UX constraint is table-primary, selected-row, adjacent preview with responsive stacking; no speculative modes/toolbars/cards/actions and no bitmap asset.
- 2026-08-21: Implemented one authenticated GET handler delegating to `GetRetainedMail`; table/detail links remain unchanged without JavaScript. Existing `site.js` owns abortable/cached pointer+focus enhancement and `site.css` owns adjacent/stacked presentation.
- 2026-08-21: Four lenses — reuse: retained summary/detail labels, `GetRetainedMail`, site assets and browser harness reused; simplification: removed redundant row message-id attribute and unused preview-wide marker; efficiency: only the selected exact message is read, prior fetch aborts and successful evidence is cached; altitude: no Core/EF/schema/query/action/framework change. All findings applied; no deferred finding or new ticket.
- 2026-08-21 verification: locked restore green; Release solution build green (0 warnings/errors); focused Web preview 1/1; focused Browser 2/2; full Mail Web 39/39. Shared Browser lane 47/48 with only `net::ERR_NO_BUFFER_SPACE` on unchanged `/Administration/Mailboxes`; exact isolated rerun 1/1. Final post-simplification focused Web+Browser rerun 3/3. `docs/design/system npm run build` green after local `npm ci`; generated dist/node_modules remain ignored.
