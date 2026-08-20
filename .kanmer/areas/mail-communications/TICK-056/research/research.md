# Research — UI-10

## Question

How should Pegasus compose the full accessible email-management workspace from the canonical retained-mail queries and Core actions owned by the MAIL tickets?

## Verified findings

- FRD-08 is the governing behavioural owner and EPIC-006 requires UI, infrastructure and Automation callers to reuse one Core implementation.
- Current repository state: /Inbox list and exact-message pages already exist with paging, mailbox/folder filters, freshness and retained detail/thread; this ticket should integrate filters, preview, navigation and action surfaces rather than duplicate domain policies.
- The previous-implementation material added to MAIL-01–04 is useful reference evidence for durable identity, fail-closed routing and append-only history, but its taxonomy/folder tree is not Pegasus authority.
- Repository implementation and local verification are activated by the operator's EPIC-006 instruction. Real Outlook, Graph or cloud mutation remains separately approval-gated.

## Implications

Reuse `src/Pegasus.Core/Intake/RetainedMail.cs` and the existing caller/store conventions. Keep exact-message identity, classification, operational routing, folder recommendation, Case association and transport mutation as separate facts and commands. Fail closed on missing identity, ambiguity, stale versions, unauthorized actors or unsupported mailbox state.

## Acceptance direction

Focused Core tests prove policy and validation; integration tests prove persistence/concurrency and the real Web caller; no deployment or external write is claimed by local evidence.

# Research refresh — 2026-08-20

## Question

Against current `origin/dev` and the EPIC-006 programme, what remains genuinely owned by UI-10, which tickets must land first, and which separately owned controls may appear progressively without turning UI-10 into a second mail-policy implementation?

## Verified findings

- **Current ref inspected:** `origin/dev` at `a3c88a7bbdb43cf4cbd9303022397f6e028d7bf9`. The existing `/Inbox` and `/Inbox/{id}` callers already provide the default newest-first all-Inboxes list, mailbox/folder tabs, SQL paging, retained read state and excerpt, manual refresh/freshness states, exact-message body, attachment metadata, mailbox/folder-scoped thread, processing/Case facts, live Core operational destination, and a real Core-owned classification-correction action. Sources: `RetainedMail.cs`, `EfRetainedMailboxMessageStore.cs`, both Mail Razor pages, and `MailWorkspaceWebTests.cs`.
- **The UI-10 capability note is partly stale.** It still says classification is undelivered and the quick preview needs client script that production CSP discards. Classification correction is present on the real message-detail caller. More importantly, `Pages/Shared/_Layout.cshtml` loads the external `wwwroot/js/site.js` with cache busting, and that file explicitly owns CSP-safe progressive enhancement under production's self-only policy. Quick preview can use that existing file; no CSP strategy, inline script, new JavaScript bundle, framework, or TICK-170 revival is required.
- **MAIL-11 is a direct structural blocker.** [[TICK-053]] owns the final mailbox/folder/search/Deleted Items request and result shape, match disclosure, paging, list/detail return context, and the same `RetainedMail.cs`, EF store, Mail pages and Web tests that UI-10 would otherwise race. UI-10 must consume its landed shape rather than implement search or Deleted Items.
- **UI-14 is the other direct structural blocker.** [[TICK-057]] owns queue/detailed-classification refinement, Unidentified/Triage separation, SQL-before-paging filtering and filter preservation through detail/refresh. UI-10 must consume that navigation; it must not reproduce `MailOperationalDestinationPolicy` or add a stored queue.
- **MAIL-23 is not a direct UI-10 blocker.** [[TICK-064]] is an accepted earlier programme/coordination predecessor and is a semantic prerequisite for folder recommendation/move via MAIL-05/07. Its administrator-approved logical-folder binding is not required to assemble the read workspace or accessible preview. The structured UI-10 dependency graph therefore records only MAIL-11 and UI-14; MAIL-23 remains upstream through its owning consumers and sequencing.
- **The remaining UI-10-owned behavior is final accessible assembly.** After MAIL-11/UI-14 land, compose their mailbox, folder, queue, search, paging, freshness and exact-detail context into one coherent journey and add the FRD-08 quick preview. The preview is evidence-only: sender, subject, timestamp, excerpt, current classification, Case association and attachment names; it contains no actions and changes no state.
- **The smallest current preview seam is the existing detail use case.** `GetRetainedMail` already returns the exact authorized message, body/attachment names, classification dossier and Case association. A Razor Page preview handler can consume it and `site.js` can progressively fetch/show the result on pointer or keyboard intent, dismiss it on focus departure, and leave the existing subject-detail link as the no-script path. This avoids widening the SQL list projection, loading every attachment/classification dossier for every row, or introducing a preview service/framework. Re-check the merged MAIL-11/UI-14 result before planning: if it already carries every preview fact efficiently, render that shape directly instead of adding even the handler.
- **Actions are progressively available controls, not UI-10 policy or blockers.** The delivered classification correction may be shown now. MAIL-05 recommendation ([[TICK-047]]), MAIL-09/10 association ([[TICK-051]], [[TICK-052]]), MAIL-07 move ([[TICK-049]]), MAIL-13 Outlook state actions ([[TICK-054]]) and MAIL-08 advice ([[TICK-050]]) remain owned, authorized and tested by their tickets; UI-10 only presents a control after its Core use case has landed and otherwise renders no speculative disabled placeholder. MAIL-12 ([[TICK-088]]) is Later/0.5.0 and cannot block Next/0.3.0 UI-10. TICK-062/AUTO-003 are Automation callers and do not block the staff workspace.
- **No generic action registry is justified.** Each action has one concrete caller/boundary, and every action ticket already converges on `Mail/Message.cshtml(.cs)` and `MailWorkspaceWebTests.cs`. Final assembly should preserve exact-message-only placement and accessible confirmation/error conventions while consuming those owners, not invent an optional-action framework to coordinate them.

## Implications

UI-10 should be replanned as a small Web/accessibility integration ticket after [[TICK-053]] and [[TICK-057]], not as another Core/persistence feature. Its expected owned diff is Mail list/detail composition, a CSP-safe quick-preview enhancement, CSS and real browser/Web acceptance. Core and EF changes are a stop condition unless the merged prerequisites provably lack one preview fact and the existing `GetRetainedMail` seam cannot provide it.

Action branches may land before or after this assembly. They need serialization because they share message-detail markup/tests, but their availability does not change UI-10's dependency graph or authorize mailbox writes. Production acceptance remains a read-only full journey; any action execution still requires the exact separate authority of its owning MAIL capability.

## Acceptance direction

Prove one authenticated journey through default and refined list states, accessible pagination/refresh, pointer and keyboard preview opening, focus-away dismissal, no preview mutation controls, exact detail/back-context preservation, attachment/thread evidence, and axe/constrained-desktop/200%-zoom behavior. Verify only controls whose owning capability has landed, with no fabricated messages or external mailbox mutation.

# Post-merge research refresh — 2026-08-21

## Question

After MAIL-11, MAIL-10 and UI-14 landed on `origin/dev`, what is the smallest UI-10-owned slice that completes the accessible mail workspace without duplicating policy or persistence?

## Findings

- **Exact baseline verified:** `origin/dev` is `ee88c70c42e38a8e18d57f73afffabfd81ac0f95`, the merge of UI-14 PR #491. `TICK-053` and `TICK-057` therefore no longer overlap an unmerged implementation branch.
- **The landed list already owns the workspace state:** `Mail/Index.cshtml.cs` parses mailbox, folder, queue, search and page context before calling `ListRetainedMail`; `Index.cshtml` preserves that context through folder tabs, queue selection, search, paging and full-detail links. No Core, EF, schema, query or message-action change is needed.
- **The list projection already carries six of seven preview facts:** sender, subject, timestamp, excerpt, current classification and Case association are on `RetainedMailSummary`. Only attachment names require the exact authorized detail read. The smallest seam is one `OnGetPreviewAsync` handler on `IndexModel` delegating to landed `GetRetainedMail`; a new store, projection, service, partial or preview framework would be larger.
- **Existing Web conventions carry enhancement:** `_Layout.cshtml` already loads same-origin `wwwroot/js/site.js`; `site.js` contains DOM-ready progressive enhancements; `site.css` owns the existing tokens, focus and responsive rules. The full-detail subject link remains the no-JavaScript path.
- **The refined imagegen preview was used only as a transient UX constraint:** it kept the dense table primary, made the selected row explicit, placed evidence preview adjacent at desktop width and stacked it after the result at constrained/200% layouts. No generated bitmap is a repository asset.
- **Governing behavior is already canonical:** FRD-08 requires pointer/keyboard intent, screen-reader access, focus-departure dismissal, no clipping/obscuring, exact preview facts and no mutation; `docs/design/README.md` requires dense panes at 1280+, ordered sections at constrained desktop/200%, visible focus and no bulk actions.
- **Test seams already exist:** `MailWorkspaceWebTests` supplies authenticated retained-message fixtures and exact no-mutation evidence; `BrowserTestSupport` supplies authenticated Playwright, axe, constrained viewport and JavaScript-disabled execution. A focused `MailWorkspaceBrowserTests` file avoids broad harness changes.

## Implications

Implement only `Mail/Index.cshtml.cs`, `Mail/Index.cshtml`, the existing `site.js` and `site.css`, focused Web/browser tests, and the UI-10 capability evidence row. The handler returns evidence-only JSON for an authorized exact retained message. JavaScript selects/fetches on pointer or keyboard focus, updates accessible state, and dismisses on focus departure; CSS uses a two-column table/preview relationship that collapses to document order. No action appears in the preview and no external write is exercised.

## Open questions

None. The parent instruction supplies the approved user-visible layout constraint and forbids the speculative modes, toolbars, cards, page-size control and actions.
