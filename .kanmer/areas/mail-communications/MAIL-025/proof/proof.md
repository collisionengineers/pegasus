# Proof — MAIL-025: Port the Inbox list and message pages to the Integrated Operations Workspace

## What was verified, and where

Verified on merged `dev` at `b92cb9a7` (`Merge pull request #612 …`), the
canonical gate SHA for this wave. MAIL-025 landed as merge commit
`420a96fc` — `Merge pull request #597 from
collisionengineers/task/mail-025-inbox-port`, 2026-08-28 18:37:30 +0000, PR
head `979fc771`, 9 files, +1343/-795. `git merge-base --is-ancestor 420a96fc
b92cb9a7` exits 0, so the recorded commit is reachable from the merge
target. `git log 420a96fc..b92cb9a7 --` over the ticket's owned files
(`src/Pegasus.Web/Pages/Mail`, `src/Pegasus.Core/Intake/RetainedMail.cs`,
`src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs`,
`tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs`,
`Browser/MailWorkspaceBrowserTests.cs`) returns nothing: the state read below
*is* the state that merged. Every file:line is read at `b92cb9a7`.

## Evidence

### The production caller: the Inbox is reachable, not merely registered

Tier: registration, plus the route and the shell link that reach it.

`src/Pegasus.Web/Pages/Mail/Index.cshtml:1` — `@page "/Inbox"`; the message
record is `src/Pegasus.Web/Pages/Mail/Message.cshtml:1` —
`@page "/Inbox/{id:guid}"`.

The page model takes its Core use cases by constructor injection
(`Index.cshtml.cs:18`):

```csharp
public sealed class IndexModel(
    ListRetainedMail listRetainedMail,
    GetRetainedMail getRetainedMail,
    GetRetainedMailFreshness getFreshness,
    SearchDeletedMail searchDeletedMail) : StaffPageModel
```

and those are composed in
`src/Pegasus.Infrastructure/DependencyInjection.cs:76`:

```csharp
services.AddScoped<IRetainedMailQueries>(
    provider => provider.GetRequiredService<EfRetainedMailboxMessageStore>());
…
services.AddScoped<ListRetainedMail>();
services.AddScoped<GetRetainedMail>();
```

The shell rail links the route at
`src/Pegasus.Web/Pages/Shared/_Layout.cshtml:69`
(`<a class="nav-link" asp-page="/Mail/Index" …>`), inside the `inboxEnabled`
condition defined at `_Layout.cshtml:8`:

```csharp
var inboxEnabled = Environment.IsProduction()
    || (Environment.IsDevelopment()
        && Configuration.GetValue<bool>("Features:LocalIntake"));
```

That is unconditionally true in Production, so this is not a closed
composition gate — the link renders for every production operator. The
command palette also routes to it (`_ShellDialogs.cshtml:121`,
`data-route="/Inbox"`).

The message page has exactly one in-application caller. `grep -rn
'asp-page="/Mail/Message"' src/Pegasus.Web/Pages/` returns a single hit
outside the page itself: `Index.cshtml:416`, the preview pane's "Open full
message" button (`Index.cshtml:414-425`). The message row's subject anchor
(`Index.cshtml:237`) is `asp-page="/Mail/Index"` with
`asp-route-selected="@item.Id"` — it selects the preview, it does not open
the record. That is as drawn, and the browser test names it ("The pane, not
the row, is the full-detail entry",
`Browser/MailWorkspaceBrowserTests.cs:108`), but it is worth stating
plainly: one caller, not many.

### Claim 1 — the three panes

Tier: build/test.

`src/Pegasus.Web/Pages/Mail/Index.cshtml:79` opens the pane container and
switches its modifier on whether a preview is resolved:

```html
<section class="pane-layout @(selectedDetail is null ? "pane-layout--2" : "pane-layout--3")"
         data-mail-preview-workspace aria-label="Retained mail">
```

- Pane 1, Scope: `Index.cshtml:81-84` — `<aside class="pane">` /
  `<div class="pane-head"><h2>Scope</h2></div>`.
- Pane 2, Messages: `Index.cshtml:105-109` — `<div class="pane">` /
  `<h2>Messages</h2>`.
- Pane 3, Message preview: `Index.cshtml:354-356` — `<div class="pane">` /
  `<h2>Message preview</h2>`.

The third pane is not conditional on a query string. `LoadSelectedDetailAsync`
(`Index.cshtml.cs:253`) falls back to the first row of the page:

```csharp
var row = Results.Items.FirstOrDefault(item => item.Id == selectedId)
    ?? (Results.Items.Count > 0 ? Results.Items[0] : null);
```

so a bare `/Inbox` with any retained mail renders three panes;
`pane-layout--2` is reached only by an empty list.

The preview pane's contents match `context.md` §1.3 field for field
(`Index.cshtml:359-436`): subject, route line (From · time · mailbox),
`_StatusChip`, excerpt, attachment list, and the four-fact grid
`Classification` / `Case association` / `Folder` / `Search match`, then
`Open full message` (dark) and a conditional `Open linked Case`.

Test: `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs:551`
`TheScopeRailCountsEachScopeUnreadFiltersAndTheSortToggleFlipsOrder` asserts
`data-mail-preview-facts` and `Open full message` on a bare `/Inbox` render.
Browser test
`tests/Pegasus.IntegrationTests/Browser/MailWorkspaceBrowserTests.cs:17`
`QuickPreviewWorksByKeyboardAndPointerAndStacksWithoutOverflow` measures the
real geometry of the second and third panes:

```js
const messages = document.querySelector('[data-mail-preview-workspace] > .pane:nth-child(2)').getBoundingClientRect();
const previewPane = document.querySelector('[data-mail-preview-workspace] > .pane:nth-child(3)').getBoundingClientRect();
```

### Claim 2 — the scope counts

Tier: registration, plus build/test.

Seven scopes in the drawn order, `Index.cshtml.cs:449`:

```csharp
public static readonly IReadOnlyList<MailScopeDefinition> ScopeDefinitions =
[
    new("All incoming", "inbox", MailFolderScope.Inbox),
    new("Unread", "mail", MailFolderScope.Inbox, UnreadOnly: true),
    new("Receiving work", "download", MailFolderScope.Inbox, MailOperationalDestination.ReceivingWork),
    new("Case updates", "reply", MailFolderScope.Inbox, MailOperationalDestination.Queries),
    new("Pre-instructions", "clock", MailFolderScope.Inbox, MailOperationalDestination.Triage),
    new("Unidentified", "search", MailFolderScope.Inbox, MailOperationalDestination.Unidentified),
    new("Sent Items", "send", MailFolderScope.Sent)
];
```

Each renders as a real submit button with an icon well and its count
(`Index.cshtml:92-99`), inside its own GET form, so the rail works without
JavaScript.

The counts are queried, not derived client-side. `LoadScopeCountsAsync`
(`Index.cshtml.cs:272`) calls the new Core use case once per scope, carrying
the current mailbox and search term:

```csharp
var count = await listRetainedMail.CountAsync(
    actor,
    new(mailbox, definition.Folder, SearchTerm, definition.Destination, null, definition.UnreadOnly),
    cancellationToken);
```

`ListRetainedMail.CountAsync` (`src/Pegasus.Core/Intake/RetainedMail.cs:424`)
applies the same authorization and normalization as the list:

```csharp
ArgumentNullException.ThrowIfNull(scope);
StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
return await queries.CountAsync(Normalize(scope), cancellationToken);
```

The adapter counts on the *shared* filter builder, so a scope's count is the
number of rows that scope pages through — one list per concept, not a second
filter encoding (`EfRetainedMailboxMessageStore.cs:95`):

```csharp
return await BuildMatches(context, scope).CountAsync(cancellationToken);
```

`BuildMatches` (`EfRetainedMailboxMessageStore.cs:780`) is the single filter
pipeline `ListAsync` also uses (`:115`), including the new
`if (scope.UnreadOnly) matches = matches.Where(item => !item.IsRead);`
(`:797`).

Tests: `MailWorkspaceWebTests.cs:551` asserts all seven scope labels, exactly
seven `class="scope-button"` occurrences, the rendered count
(`<span class="tabular">3</span>` for three seeded messages), that
`/Inbox?unread=true` is a real server query, and that
`/Inbox?folder=sent&unread=true` 404s. Core-level authorization and argument
tests for `CountAsync` are in
`tests/Pegasus.Core.Tests/Intake/RetainedMailTests.cs:184-214`, with the fake
implementing the new port member at `:751`.

### Claim 3 — the sort toggle

Tier: build/test.

`Index.cshtml:112` renders the toggle as a two-state link in the Messages
pane head, carrying the full list context and flipping the `sort` value:

```html
<a class="btn btn--small sort-toggle"
   …
   asp-route-sort="@(Model.OldestFirst ? null : "oldest")"
   aria-label="@(Model.OldestFirst
       ? "Received order: oldest first; activate for newest first"
       : "Received order: newest first; activate for oldest first")">
    Received @(Model.OldestFirst ? "↑" : "↓")
</a>
```

Parsing is strict — only the two drawn addresses exist, anything else fails
closed (`Index.cshtml.cs:623` `TryParseSort`: `null or ""` → newest,
`"oldest"` → oldest, `default: return false`, and `:107` turns that into a
`NotFound`).

The flip is server-side, in SQL (`EfRetainedMailboxMessageStore.cs:121`):

```csharp
var ordered = scope.OldestFirst
    ? matches.OrderBy(item => item.ReceivedAtUtc).ThenBy(item => item.Id)
    : matches.OrderByDescending(item => item.ReceivedAtUtc).ThenByDescending(item => item.Id);
```

`MailWorkspaceWebTests.cs:551` proves the flip by row order
(`Assert.True(newestIndex > middleIndex, "sort=oldest must list the newest
message last.")`), asserts the rendered arrow bytes in both states
(`Received &#x2191;` with no `&#x2193;` on the flipped page,
`Received &#x2193;` on the default), and asserts `/Inbox?sort=newest-first`
returns 404. `ScopingAndPagingCarryTheMailboxFolderAndPageForward`
(`MailWorkspaceWebTests.cs:605`) proves `unread` and `sort` survive the whole
message round-trip — into `/Inbox/{id}`, back out via Back to Inbox, and
through the section tabs.

### Claim 4 — the record bar: a real, owned gap

Tier: read of the shipped file. **The record bar is not rendered at all.**

`grep -c "record-bar" src/Pegasus.Web/Pages/Mail/Message.cshtml` → `0`.

There is a `record-head` (`Message.cshtml:45`: subject `<h2>`, identity line
sender / mailbox / received time, and the classification `_StatusChip`) and a
`record-accent`, then straight to the tabs nav (`Message.cshtml:66`). The
omission is deliberate and stated in the file (`Message.cshtml:11`):

```
@* The record bar (Reply, Forward, Compose, Flag, Delete) is drawn but has no
   handler yet: it lands with the outbound-mail work, not as dead buttons. *@
```

This is not a missing style. The `.record-bar` / `.record-bar-end`
vocabulary exists (`src/Pegasus.Web/wwwroot/css/site.css:380-381`) and four
other ported pages already use it — `Pages/Cases/Details.cshtml`,
`Pages/Cases/Assessment/Index.cshtml`, `Pages/Triage/Details.cshtml`,
`Pages/ImageIntake/Details.cshtml`. The bar is absent here because no
outbound-mail handler exists to post to, and EPIC-011 `context.md` forbids
rendering an inert control (D7: a disabled control is permitted only for a
named, ticketed integration seam — outbound mail is not one).

The gap is owned. `MAIL-026` ("Mail composer (Reply/Forward/Compose), Flag,
Delete and Case correspondence actions", status `backlog`, label `wave-4`,
groups EPIC-011/EPIC-006) declares `src/Pegasus.Web/Pages/Mail/Message.*` in
its **Owns** section and is blocked by this ticket and the outbound-mail
ticket. So §1.3's record bar remains undelivered on `dev` at `b92cb9a7`, and
the shipped state matches what MAIL-025's own body, plan and checklist say it
would ship.

One further §1.3 element is likewise absent by the same rule: the attachments
table ships five columns — File, Type, Size, Search content, Custody
(`Message.cshtml:330-336`) — and omits the drawn **Preview** column, because
no preview handler exists and it is not a D7 seam.

### Claim 5 — the rest of the ported surface

Tier: build/test.

- List header: eyebrow "Retained mail" + `<h1>Inbox</h1>`
  (`Index.cshtml:16-19`), no lede.
- Filter bar (`Index.cshtml:26-77`): Mailbox, Folder, Queue selects, search
  box, dark Search button, `data-auto-submit`; the Queue select is disabled
  for Deleted Items so a folder switch never submits a queue that view
  refuses.
- Rows (`Index.cshtml:219-302`): unread state as
  `class="row-button @(item.IsRead ? null : "unread")"` with the
  `unread-indicator` dot plus an `sr-only` "Unread" (`Index.cshtml:225`; CSS
  at `site.css:279`), sender, date and clock `<time>` elements, subject link,
  excerpt, outcome `_StatusChip`, and the case-reference · attachments meta
  line. Pagination is bounded (`Index.cshtml:314-340`), asserted by
  `ScopingAndPagingCarryTheMailboxFolderAndPageForward`.
- Message tabs (`Message.cshtml:66-107`): Message / Attachments with a
  `tab-count` when there are attachments / Thread / Case (the Case tab
  renders only when an association receipt exists).
- Decision card (`Message.cshtml:188-290`): Decision, Destination, Filed to,
  "Correct classification", "Move to X" and "Check move status".
- Corrections timeline (`Message.cshtml:295-311`): before → after, meta, and
  the recorded reason.

Gate evidence for the build and test tiers is the canonical run recorded in
`gate-evidence.md` at `b92cb9a7`: restore exit 0; `dotnet build … Release`
succeeded with 0 warnings / 0 errors; `dotnet test … --filter
'Category!=Corpus&Category!=Browser'` → ArchitectureTests 100 passed,
Core.Tests 1133 passed, IntegrationTests 1022 passed / 2 skipped (both skips
pre-existing and unrelated to mail). Not re-run here.

### Browser tier for this ticket

Tier: build/test, at the PR head rather than at `b92cb9a7`.

The canonical gate run excluded `Category=Browser`, so
`MailWorkspaceBrowserTests` — which carries this ticket's pane-geometry and
no-JavaScript select-then-open assertions — is not covered by it. It is
covered by PR #597's CI on head `979fc771`:

```
gh pr view 597 --json statusCheckRollup
  -> "name":"browser","conclusion":"SUCCESS","completedAt":"2026-08-28T18:00:04Z"
     (workflow repository-check, run 33196241696)
  -> unit SUCCESS; sql-integration (1)(2)(3) SUCCESS;
     sql-integration-coverage SUCCESS
```

## The ticket's own verification items

| Item | Status | Evidence |
| --- | --- | --- |
| Existing handlers (classification correction, folder move, association) keep antiforgery, version and reason behaviour | Proven (build/test) | Three strands, below |
| No clipped text/overflow at 1580/1100/760 | **Unproven** | See Outstanding |

On the first item, three independent strands:

1. **No handler body changed.** `git diff 420a96fc^1 420a96fc --
   src/Pegasus.Web/Pages/Mail/Message.cshtml.cs` is 41 lines and adds only
   the `unread` / `sort` list-context binding, its strict parse in
   `TryParseListContext`, and the same pair re-emitted on the four
   `RedirectToPage` targets. `OnPostCorrectClassificationAsync`,
   `OnPostMoveToRecommendedFolderAsync`, `OnPostPrepareLinkCaseAsync`,
   `OnPostPrepareUnlinkCaseAsync`, `OnPostLinkCaseAsync` and
   `OnPostUnlinkCaseAsync` are untouched, so the version checks
   (`Message.cshtml.cs:235`, `:243`, `:296`, `:303` — each throwing
   `IntakeVersionConflictException`) and the reason parameters (`:337`,
   `:402`) are the same code that passed before the port.
2. **Antiforgery survived the markup rewrite.** The one form that posts to a
   raw `action=` URL, where the Razor form tag helper does not emit a token
   automatically, keeps its explicit token: `Message.cshtml:706-707` is
   `<form method="post" action="@moveAction">` followed by
   `@Html.AntiForgeryToken()`. The count of `@Html.AntiForgeryToken()` in the
   file is 1 before the port and 1 after. Every other post form uses
   `asp-page-handler` (`Message.cshtml:267`, `:461`, `:593`, `:626`), where
   the tag helper emits the token.
3. **The tests post through the rendered token**, so a dropped token fails
   them. `MailWorkspaceWebTests.cs:1809` `AntiforgeryToken(html)` regex-reads
   `__RequestVerificationToken` out of the page and asserts
   `match.Success, "The antiforgery token was not rendered."`. It is used by
   `MessageDetailExplainsTheVersionedDecisionAndOffersExactMessageCorrection`
   (`:1145`, which also asserts `name="ExpectedClassificationVersion"` is
   rendered), `AuthenticatedUncertainMoveReusesTheSameConfirmationForExactRecovery`
   (`:1281`), `CraftedOrOversizedCorrectionsFailClosedWithoutHistoryWrites`
   (`:1353`),
   `InvalidSearchContextOnACorrectionReloadReturnsASupportedResponseWithoutWrites`
   (`:1400`) and
   `InvalidMailViewContextStopsEveryExactMessagePostBeforeMutation` (`:937`),
   which drives every exact-message post with a corrupted view context and
   asserts no mutation. Association reason and lease behaviour is covered by
   `ExactMessageCanBeSearchedLinkedUnlinkedAndLinkedToAReplacement` (`:157`)
   and `PreparedAssociationCannotMoveToAnotherMessageOrTheOtherAction`
   (`:331`). All are inside the 1022 passing IntegrationTests of the
   canonical gate run.

## Outstanding

- **§1.3's record bar (Reply / Forward / Compose / Flag / Delete) is not
  shipped**, and neither is the attachments-table Preview column. Owned by
  **MAIL-026** (backlog, wave 4), which names
  `src/Pegasus.Web/Pages/Mail/Message.*` in its Owns section. This is the
  declared scope boundary of MAIL-025, not a defect against it — but §1.3 is
  not fully delivered until MAIL-026 lands.
- **"No clipped text/overflow at 1580/1100/760" is unproven.** No such walk
  was run for this ticket; the checklist item is correctly left unticked. The
  browser test that ships with MAIL-025 measures pane geometry at the
  desktop and mobile widths it sets, not the three EPIC-011 breakpoints.
  Recorded as outstanding against **UIIMP-010**, which owns that walk and
  whose tooling
  (`tests/Pegasus.IntegrationTests/Browser/LayoutIntegrityTests.cs`) exists
  on `dev`.
- **Tier 3 (deployed, exercised) is not available for any claim here.** No
  EPIC-011 work is on `main`; `main` is promoted once, at wave 5. Every claim
  above is tier 1 (registration and route wiring) or tier 2 (green build and
  test).
- **No `repository-check` run exists on `dev` at `b92cb9a7`.**
  `gh run list --branch dev` shows the most recent `dev` run at `9eec6dc2`
  (2026-08-27, `failure`). The build/test evidence at `b92cb9a7` is therefore
  the orchestrator's local run recorded in `gate-evidence.md`, and the
  browser evidence for this ticket is PR #597's CI at head `979fc771`, not a
  run at `b92cb9a7`. Not a MAIL-025 defect; recorded so the tier is not
  overstated.
- **Selected-row highlight lost, accepted by the implementing lane.** CI's
  axe `aria-allowed-attr` check rejected `aria-selected` on the roleless row
  `div`s, so commit `40a1920e` moved selection to `aria-current="true"` on
  the row's subject link (`Index.cshtml:251`). The server-rendered highlight
  in `site.css` keys on `[aria-selected]`, so the visual highlight of the
  selected row is gone; selection is now conveyed by the link's
  `aria-current` and by the preview pane. The lane recorded this as a
  design-system follow-up for the wave-5 pass rather than a page fix
  (`scratch/notes.md`, "CI fixes — 2026-08-28"). I found no ticket raised for
  it; named here so it is not lost.

## Scope of this proof

Written against merged `dev` at `b92cb9a7` per decision D15. `main` has not
been promoted; the exact-SHA `dev` → `main` promotion happens at wave 5.
