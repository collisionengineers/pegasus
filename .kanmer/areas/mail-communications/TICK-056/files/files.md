# Files — TICK-056 / UI-10 final workspace assembly

*Surveyed on current `origin/dev` (`a3c88a7b`) and against the refreshed EPIC-006 ticket maps. Refresh once [[TICK-053]] and [[TICK-057]] merge; they are the two direct structural blockers.*

## Where the change lands

| Path | Why |
|---|---|
| `src/Pegasus.Web/Pages/Mail/Index.cshtml.cs` | Compose the final landed mailbox/folder/queue/search/page context. If the merged list result does not already carry every preview fact efficiently, add one thin authorized preview handler that calls the existing `GetRetainedMail`; no policy, EF query or action logic. |
| `src/Pegasus.Web/Pages/Mail/Index.cshtml` | Assemble the landed filters and results into the final workspace list and add an accessible evidence-only preview trigger/region per exact message. Preserve the subject link as the no-script/detail path and expose no mutation control in the preview. |
| `src/Pegasus.Web/wwwroot/js/site.js` | Extend the existing CSP-safe progressive-enhancement file for pointer/keyboard intent, `aria-expanded`/focus behavior, bounded positioning, focus-away dismissal and honest load failure. No inline script, new bundle or client framework. |
| `src/Pegasus.Web/wwwroot/css/site.css` | Add the existing-design-system preview placement and responsive/forced-colour/reduced-motion rules so it neither clips nor obscures adjacent controls at constrained desktop and 200% zoom. |
| `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` | Prove the authenticated final list/detail journey, preserved filters/return context, preview payload/markup, exact-message scoping, no preview actions and no read/classification/association/mailbox mutation from viewing. |
| New focused test under `tests/Pegasus.IntegrationTests/Browser/` (prefer `MailWorkspaceBrowserTests.cs`) | Exercise the real loaded `site.js`: pointer and keyboard opening, focus-away dismissal, detail fallback, axe, CSP-safe execution, constrained desktop and 200%-zoom/no-overflow behavior with a retained-message fixture. |
| `docs/capabilities.md` | At delivery only, replace UI-10's stale classification/CSP note with the exact landed caller and evidence tier. Do not claim separate MAIL actions, deployment or live mutation. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `src/Pegasus.Core/Intake/RetainedMail.cs` | Existing `ListRetainedMail`/`GetRetainedMail` authorization and exact-message DTOs. Consume the merged MAIL-11/UI-14 shape; do not add workspace policy or a preview abstraction unless the real merged shape proves the thin handler insufficient. |
| `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs` | Sole SQL list/detail/thread owner. UI-10 should not modify it after MAIL-11/UI-14; a new UI-owned query/store is a stop condition. |
| `src/Pegasus.Web/Pages/Mail/Message.cshtml(.cs)` | Existing exact-detail, correction and return-context caller, and the integration point used by separately owned action tickets. UI-10 verifies coherent placement/context but does not recreate their commands or eligibility. |
| `src/Pegasus.Web/Pages/Shared/_Layout.cshtml` | Already loads `site.js` as a same-origin external script with cache busting; this disproves the old CSP blocker without a layout change. |
| `tests/Pegasus.IntegrationTests/Browser/BrowserTestSupport.cs` and `AccessibilityTests.cs` | Existing authenticated pinned-Chromium, axe, forced-colour, reduced-motion and viewport conventions. Reuse rather than create a browser harness. |
| `docs/frd/frd-08-email-mailbox-and-background-processing.md` | Settled default/refined workspace, preview content/no-side-effect rule, exact-message-only actions and state preservation. |
| `docs/design/README.md` | Preview keyboard/pointer/focus behavior and desktop/zoom/accessibility conventions. |
| EPIC-006 `context.md` | One Core implementation and no local-alpha Outlook mutation. |
| [[TICK-053]] and [[TICK-057]] research/files | Final read/search/detail and queue/filter shapes that UI-10 must assemble rather than duplicate. |
| [[TICK-047]], [[TICK-049]], [[TICK-050]], [[TICK-051]], [[TICK-052]], and [[TICK-054]] files | Separate recommendation, move, advice, association and Outlook-state owners. Their controls are progressively available from detail only. |

## Direct blockers and progressive controls

| Relationship | Tickets | Consequence |
|---|---|---|
| Direct structural blockers | [[TICK-053]], then [[TICK-057]] | They settle the list/query/filter/detail-return surface. Both now structurally block UI-10. |
| Earlier programme/coordination predecessor, not direct UI-10 behavior | [[TICK-064]] | Land first under the accepted wave and for MAIL-05/07 folder binding; UI-10 adds no direct edge or folder policy. |
| Progressive read/advisory controls | [[TICK-047]], [[TICK-050]] | Render only after their Core result exists; absence does not block the workspace. |
| Progressive Case controls | [[TICK-051]], then [[TICK-052]] | Preserve their exact-message search/confirmation policy; absence does not block list/preview assembly. |
| Progressive Outlook mutation controls | [[TICK-049]], then [[TICK-054]] | Their tickets own authorization, confirmation, retry/history and live approval. UI-10 grants no write authority. |
| Later capability | [[TICK-088]] | Later/0.5.0 compose/reply/forward/send is explicitly not a UI-10 blocker. |
| Separate caller | TICK-062 / [[AUTO-003]] | Automation exposure follows landed Core use cases and has no staff-workspace dependency. |

## Exact overlaps and serialization

- [[TICK-053]] overlaps `RetainedMail.cs`, `EfRetainedMailboxMessageStore.cs`, both Mail pages and `MailWorkspaceWebTests.cs`; it must land before UI-10.
- [[TICK-057]] overlaps `RetainedMail.cs`, the EF store, `Index.cshtml(.cs)`, `Message.cshtml(.cs)` and `MailWorkspaceWebTests.cs`; it must land after MAIL-11 and before UI-10.
- [[TICK-047]], [[TICK-049]], [[TICK-050]], [[TICK-051]], [[TICK-052]], and [[TICK-054]] all overlap `Mail/Message.cshtml(.cs)` and/or `MailWorkspaceWebTests.cs`. Serialize/rebase their detail changes; UI-10 should avoid editing message-detail command code and consume what landed.
- [[TICK-052]] may reuse/edit `wwwroot/js/site.js` for Case search, while MAIL-07/13 may reuse the dialog behavior it contains. Refresh the single shared script after those branches; do not create mail-specific JavaScript files to avoid coordination.
- [[TICK-064]] deliberately has no UI-10 source overlap when this reduced scope is respected; only `docs/capabilities.md` delivery notes are shared.
- The new focused browser test is UI-10-owned and avoids contention in the broad `AccessibilityTests.cs`; add `/Inbox` coverage there only if the existing route matrix itself must change.

## Out of scope

No search/Deleted Items implementation, queue mapping/filter policy, taxonomy, folder catalogue/binding/recommendation, Case matching/link policy, message move/state/send action, Automation tool, Core action registry, generic preview service, new persistence/store/migration, new JavaScript framework/bundle, inline script, bulk/list/preview mutation, arbitrary destination, deployment, live Outlook/Graph/cloud write, or production-delivery claim.
