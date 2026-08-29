# Proof — CASE-026: Port the Search page (/Search) with the advanced filter grid and selected-Case pane

## What was verified, and where

Verified on merged `dev` at `b92cb9a7`, the head of the wave-A batch. CASE-026
arrived as PR
[#606](https://github.com/collisionengineers/pegasus/pull/606), merge commit
`d7f6201c` ("Merge pull request #606 from
collisionengineers/task/case-026-search-page", 2026-08-29 10:13:53 +0100). All
seven recorded commits (`882f32ae`, `20843a7e`, `9d739ab9`, `17930a17`,
`0f80c363`, `56ce7898`, `d2ce04fe`) are ancestors of `b92cb9a7`
(`git merge-base --is-ancestor <sha> b92cb9a7` → 0 for each), so rule 17 holds.

The page's three blobs are byte-identical at `d7f6201c` and at `b92cb9a7`:

```
$ git ls-tree b92cb9a7 -- src/Pegasus.Web/Pages/Search/
35c47418  Index.cshtml
6131526f  Index.cshtml.cs
64cabe0c  _CasePreview.cshtml
$ git ls-tree d7f6201c -- src/Pegasus.Web/Pages/Search/   # same three hashes
```

Nothing merged after CASE-026 touched `Pages/Search/**`,
`Core/Cases/CaseQueries.cs`, `EfCaseQueryStore.cs`, `site.js` or
`CasesIndexWebTests.cs`. UIIMP-008 (`c87e8d5d`) touched
`Presentation/OperatorLabels.cs`, but `CaseStage(CaseLifecycleState)` — the D3
mapping this page renders — is identical at both SHAs (`diff` of the extracted
method: no output).

Build and test evidence is cited from the orchestrator's canonical gate run on
`b92cb9a7`, not re-run here: restore exit 0; `Build succeeded. 0 Warning(s), 0
Error(s)`; `Category!=Corpus&Category!=Browser` → ArchitectureTests 100/100,
Core.Tests 1133/1133, IntegrationTests 1022 passed / 2 skipped (both skips
pre-existing and unrelated).

## Evidence

### The page exists and has a real production caller

Tier: **registration + build/test**. Not deployed.

`/Search` is a Razor Page at
`src/Pegasus.Web/Pages/Search/Index.cshtml:1` (`@page`), and the shell links
it from the rail:

```
src/Pegasus.Web/Pages/Shared/_Layout.cshtml:90
    <a class="nav-link" asp-page="/Search/Index" aria-current="@CurrentWhen("/Search")">
src/Pegasus.Web/Pages/Shared/_Layout.cshtml:132
    <form class="utility-search" role="search" method="get" action="/Search">
src/Pegasus.Web/Pages/Shared/_ShellDialogs.cshtml:134
    <button ... data-route="/Search">
src/Pegasus.Web/Pages/Shared/_ShellDialogs.cshtml:154
    <button ... data-route="/Search?query=" data-command-fallback>
```

Four production entry points: rail nav, the utility bar's global search form,
the command palette's Search row, and the palette's Enter fallback. The route
is also walked green by CI (below).

The query chain is registered end to end, not just declared. The page model
injects `ISearchCases` (`Index.cshtml.cs:34`);
`src/Pegasus.Infrastructure/DependencyInjection.cs:305` registers
`ISearchCases → SearchCases`; `SearchCases`
(`src/Pegasus.Core/Cases/CaseQueries.cs:198`) takes `ICaseQueryStore`, which
`DependencyInjection.cs:302-304` resolves to `EfCaseQueryStore`.

### 1. The advanced filter grid

Tier: **build/test**.

`Index.cshtml:42-107` renders one `<form method="get" class="panel"
aria-label="Filter cases">` whose `.advanced-search-grid` body holds the ten
§1.7 fields — `search-query` (Case/PO or image reference), `search-registration`,
`search-claimant`, `search-claim-number`, `search-principal`, `search-state`
(the `CaseLifecycleState` enum select with an "All states" option),
`search-engineer`, `search-from-date`, `search-to-date`, `search-origin` —
plus the `Search` dark submit and the `Clear` link (`:96-105`).

Every value reaches Core intact, and paging keeps it. Proved by
`tests/Pegasus.IntegrationTests/CasesIndexWebTests.cs:19`
`SearchUsesAuthorizedCoreQueryAndPreservesEveryFilterInPagingUrl`: a
fourteen-parameter URL is asserted field by field on the recorded
`SearchCasesQuery` (`:41-53`), and the rendered `Next` href is asserted to
carry all fourteen plus `page=3` (`:74-84`). The four parameters §1.7 does not
draw (`case`, `receivedDate`, `instructionDate`, `kind`) are bound
(`Index.cshtml.cs:42-82`), applied (`:161-173`) and pager-preserved
(`RouteValues`, `:359-392`) — plan decision P4, and the reason the 301 below
loses nothing.

The CSS the grid relies on is present, not assumed:
`src/Pegasus.Web/wwwroot/css/site.css:659-660`
(`.advanced-search-grid{display:grid;grid-template-columns:repeat(5,minmax(150px,1fr))…}`).

### 2. Selectable rows

Tier: **build/test** for the markup contract; **build/test (CI browser lane)**
for the script.

`Index.cshtml:193-218` renders each result as
`<tr data-select-href="…" data-select-id="…" data-copy-reference="…"
aria-selected="true|false">` carrying its own `<template>` with the row's
preview (`:215-217`), inside a `data-row-list` pane body (`:161`).

The consumer is the shell's row-selection module,
`src/Pegasus.Web/wwwroot/js/site.js:1428-1481`: it finds
`[data-preview-target]`, clones the row's `<template>` into it
(`:1440-1448`), rewrites `?selected=` via `history.replaceState`
(`:1449-1451`), and maintains `aria-selected` across rows (`:1445-1447`).
Openings are click (`:1461`), Enter (`:1468`) and focus (`:1474`);
ArrowUp/Down roving focus over `tr[data-select-href]` is the separate
`[data-row-list]` module at `:1382-1406`. Hover and focus affordances are
CSS: `site.css:667-670`.

Server-side assertion:
`CasesIndexWebTests.cs:60-61` pins `data-select-id` for both fixture rows, and
`:129-134` pins that the row named by `?selected=` renders
`aria-selected="true"`.

**Space is not an opening.** `git grep` over `site.js` finds no handler for
`' '`/Spacebar anywhere; the module handles Enter only. The ticket body's
parenthetical "keyboard Enter/Space" is therefore not delivered. The operator
outcome is not lost — arrowing onto a row fires `focus`, which selects — but
the literal claim is unproven. `site.js` is PLAT-029's file.

### 3. The server-rendered Selected Case pane

Tier: **build/test**.

`Index.cshtml:240-268` renders the pane; `Index.cshtml.cs:179-191` resolves
`?selected=` server-side, defaults to the first row when unset, and returns
`NotFound()` when the named id is not on the page. The partial
`Pages/Search/_CasePreview.cshtml` carries every §1.7 element: eyebrow case
type (`:15`), `h2` heading (`:16`), muted claimant · principal (`:17`), the
status chip (`:18`), **Accident circumstances** (`:19-22`), the fact grid
Provider reference / Engineer / Due / Next action (`:23-28`), `Outstanding
(n)` (`:31-45`) and the dark `Open Case` anchor (`:46-51`).

Proved by `CasesIndexWebTests.cs:115`
`SelectedRowPreviewsItsFactsServerSide`, which asserts the heading
(`QDOS3100043 &#xB7; AB12CDE`), the circumstances line (`Rear-end impact at a
roundabout`), `Provider reference`/`CLM43`, `Unassigned`, and
`href="/Cases/{ClosedCaseId}"` (`:136-143`).

The two facts §1.7 needs and the pre-port projection lacked are shipped
through the **real adapter**, not only the fake:
`src/Pegasus.Core/Cases/CaseQueries.cs:73-75` adds `VehicleMake`,
`VehicleModel`, `AccidentCircumstances` as trailing optional parameters, and
`src/Pegasus.Infrastructure/Persistence/EfCaseQueryStore.cs:251-253` projects
them from the joined `InstructionDraftEntity` inside the existing
`SearchRows` query — no new query, no migration.

### 4. The "Closed · <outcome>" chip (D3)

Tier: **build/test**.

One list, one place:
`src/Pegasus.Web/Presentation/OperatorLabels.cs:134-146`.

```
CaseLifecycleState.ReportPreparation or CaseLifecycleState.PostReport => "With Engineer",
CaseLifecycleState.PostReportComplete => "Complete",
CaseLifecycleState.ProviderCancelled => "Closed · Provider cancelled",
CaseLifecycleState.CollisionEngineersRejected => "Closed · Collision Engineers rejected",
CaseLifecycleState.CreatedInError => "Closed · Created in error",
CaseLifecycleState.SourceEmailUnlinked => "Closed · E-mail unlinked",
```

The Core enum is untouched — this is a display mapping, as D3 requires. The
page reads it for the table chip (`Index.cshtml.cs:318`) and for the State
select's option labels (`Index.cshtml:71`), so a terminal outcome is both a
filter choice and a result here.

Rendered-bytes assertion, not a source literal:
`CasesIndexWebTests.cs:66` — `Assert.Contains("Closed &#xB7; Created in
error", html)` — because the framework's encoder writes the separator as a
numeric reference.

### 5. `/Cases?query=` 301 to `/Search`, values preserved

Tier: **build/test**.

`src/Pegasus.Web/Pages/Cases/Index.cshtml.cs:286-289`:

```csharp
if (SearchOnlyParameters.Any(parameter => Request.Query.ContainsKey(parameter)))
{
    return RedirectPermanent("/Search" + Request.QueryString.Value);
}
```

`SearchOnlyParameters` (`:103-107`) is `case, registration, claimant,
claimNumber, engineerId, receivedDate, instructionDate, fromDate, toDate,
query`. The whole query string is passed through verbatim, so no value can be
dropped or re-encoded.

`tests/Pegasus.IntegrationTests/AdministrationSearchAccountWebTests.cs:114`
`OldCasesSearchLinksRedirectToSearchWithTheirValuesIntact` asserts:

- `/Cases?query=<k>` → 301, `Location` exactly `/Search?query=<k>` (`:123-127`);
- a thirteen-parameter bookmark → 301 with `Location` equal to `"/Search" +
  wholeFilterSet` **byte for byte, in original order** (`:143-147`);
- the landed `/Search` renders eight of those values back into their own input
  by id (`:152-163`).

Precision the ticket body overstates: eight of the thirteen are asserted
*re-rendered into a field*. `state` is a `<select>` and its selected option is
not asserted; `case`, `receivedDate` and `instructionDate` are deliberately
not drawn (P4). All thirteen are asserted preserved in the redirect target,
and all are asserted reaching `ISearchCases` by
`CasesIndexWebTests.cs:41-53`. So the values survive and are applied; only
their re-display is partially asserted.

### Ancillary behaviours the review pass claimed

Tier: **build/test**.

- Empty vs unavailable stay distinct and non-leaking:
  `CasesIndexWebTests.cs:89` — empty renders `No cases match these filters.`
  and must *not* contain the superseded `No matching cases`; failure renders
  503 + `Cases are unavailable` and never the exception type name.
- A failed image read no longer answers with an empty list:
  `CasesIndexWebTests.cs:178`.
- The exact-reference image hit carries its real lifecycle state:
  `CasesIndexWebTests.cs:155` (`Merged into Instruction-initiated Case`, and
  not the `AwaitingInstruction` default).

## Findings

### F1 — the page's inline `@section Scripts` is discarded in every deployed environment

**Confirmed.** This is shipped code that does not do what the ticket record
claims.

`Pages/Search/Index.cshtml:272-317` is an inline `<script>` block, and it is
the **only** one in the application:

```
$ git grep -ln "@section Scripts" b92cb9a7 -- src/Pegasus.Web/Pages/
b92cb9a7:src/Pegasus.Web/Pages/Search/Index.cshtml
```

Outside Development the app sets a CSP with no `unsafe-inline`, nonce or hash
allowance — `src/Pegasus.Web/Program.cs:811-828`:

```csharp
if (!app.Environment.IsDevelopment())
{
    …
    context.Response.Headers.ContentSecurityPolicy =
        "default-src 'self'; object-src 'none'; base-uri 'self'; frame-ancestors 'self'";
```

The repository states the consequence in its own words, three times:

- `src/Pegasus.Web/wwwroot/js/site.js:4-7` — "This file exists as a file rather
  than as inline `<script>` blocks because the deployed
  Content-Security-Policy is `default-src 'self'` with no nonce or hash
  allowance, so an inline script is silently discarded in Production."
- `src/Pegasus.Web/wwwroot/js/site.js:766-767` — the dialog module "lives here
  rather than beside the markup because the deployed Content-Security-Policy
  discards inline scripts."
- `docs/operations.md:655-658` — the security headers "are added only outside
  Development and so no test in the suite had ever seen one", which is exactly
  how DOCS-011's `frame-ancestors` defect shipped green.

No test can catch it: every web and browser test runs under `"Development"`
(`tests/Pegasus.IntegrationTests/IntakeWebTestSupport.cs:59`), where the
header is not set at all.

What this costs. The plan's dispositions record two review findings as
**Fixed** by this script — P1 "`Index.cshtml:249` Copy Case/PO copies the
previous selection" and P2 "refresh's hidden `selected` goes stale". In a
deployed environment the script never runs, while the shell's own
row-selection module (in `site.js`, a file) does. So after an operator selects
a different row: the preview swaps correctly, but **Copy Case/PO still copies
the previously loaded row's reference and Refresh still reopens the previous
row** — precisely the reported defect, live. On first load and after any full
round trip (`?selected=`) both are correct, because the server renders them
(`Index.cshtml:251-266`, `Index.cshtml.cs:352-357`).

`CasesIndexWebTests.cs:144-151` asserts the *markup* (`data-copy-target`,
`data-copy-reference` on each row, `name="selected"`), which does render — so
the test stays green while the behaviour it stands for is inert outside
Development. The assertion is not wrong; it simply cannot reach this.

The durable fix the plan itself names is the right one and is not this lane's
file: bind `[data-copy-target]` by delegation in `site.js` (PLAT-029's), which
would also remove the need for the page script at all. Recorded here for
dispositioning; no code was changed by this proof.

## The ticket's own verification items

| Item | Status | Evidence |
| --- | --- | --- |
| Old `/Cases?query=` bookmarks 301 to `/Search` with the same values | **Proved** | `Cases/Index.cshtml.cs:286-289`; `AdministrationSearchAccountWebTests.cs:114-164` (301 + `Location` byte for byte, 13 values, 8 re-rendered by field id) |
| No clipped text/overflow at 1580/1100/760 | **Partly proved** | `Browser/LayoutIntegrityTests.cs:17-42` walks `AccessibilityTests.AuthenticatedRouteList` — which contains `/Search` (`AccessibilityTests.cs:26-27`) — at exactly 1580/1100/760. PR #612's `browser` lane passed (run `33243741194`, job `99080400645`) on the merge ref whose base `210727dd` contains `d7f6201c` and whose Search blobs equal `b92cb9a7`'s. **But** the fixture seeds no cases (`DevelopmentOfflineInitialization` creates an administrator, an organisation and the QDOS principal only), so only the *empty* page was laid out; and `.pane-scroll` and `.table-wrap` are in `LayoutIntegrityTests.AllowedClipSelector:35-38`, which is where this page's table and panes live. The populated seven-column table and the Selected Case pane are unproven. |

The twelve checklist items in `checklist/` all correspond to markup verified
above, with one qualification: item 10 reads "No new CSS/JS file; no inline
styles/scripts". No new file was added and no `style` attribute is present,
but an inline `<script>` **was** added — see F1.

## Outstanding

- **F1** — the inline `@section Scripts` at `Index.cshtml:272-317` is dead in
  every deployed environment, leaving review findings P1 and P2 unfixed in
  Production. Needs a disposition. The durable fix (delegated
  `[data-copy-target]` binding) lives in `wwwroot/js/site.js`, PLAT-029's file.
- **Populated-layout walk** at 1580/1100/760 with rows in the table and a
  rendered preview pane — **UIIMP-010** owns that walk. The empty-state pass
  above does not cover the fixed-layout table (`site.css:665`,
  `min-width:760px`, narrowing to 640px at `:831`).
- **Space as a row-selection opening** is absent from `site.js`; the ticket
  body claims "Enter/Space". PLAT-029's file.
- **[[UIIMP-011]]** — `tests/Pegasus.IntegrationTests/TestUiSnapshotTests.cs:28-29`
  still carries `["cases--empty"] = new("No matching cases.")` and
  `["cases--unavailable"] = new("<h2>Cases are unavailable</h2>")` on
  `b92cb9a7`; the ported page renders `No cases match these filters.` and
  `<strong>Cases are unavailable</strong>`. Both states are known-failing until
  UIIMP-011 lands. Confirmed still open at this SHA.
- **[[PLAT-059]]** — `Pages/Shared/_ShellDialogs.cshtml:64` and
  `wwwroot/js/site.js:1364` still send "Create Case" and Ctrl N to the
  receipt-less `/Cases/Create` 404. Confirmed still open at this SHA;
  this page's own header correctly targets `/Upload` (`Index.cshtml:30`).

No claim in this document is tier 3. `/Search` has not been exercised in a
deployed environment at this SHA.

## Scope of this proof

Written against merged `dev` at `b92cb9a7` per decision D15. `main` has not been
promoted; the exact-SHA `dev` → `main` promotion happens at wave 5.
