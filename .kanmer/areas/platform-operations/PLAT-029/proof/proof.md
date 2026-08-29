# Proof — PLAT-029: Integrated Operations Workspace shell, design system and route structure

## What was verified, and where

Verified on merged `dev` at `b92cb9a7`, in the primary checkout
`C:/Users/PC/Documents/GitHub/pegasus`, working tree clean. PR #589
("PLAT-029: Integrated Operations Workspace shell, design system and routes")
merged 2026-08-28T11:24:38Z as `5ca2572c`, confirmed an ancestor of `b92cb9a7`
by `git merge-base --is-ancestor 5ca2572c b92cb9a7` (exit 0). The branch
carried nine commits (`c73331de` fonts/sprite · `9b8a1de8` site.css ·
`865b4c0c` shell/partials/JS/labels · `b5ff6590` merge · `71277763` routes,
admin layout, catalogue · `646b6763` tests · `32f4d189` review fixes ·
`af597799` and `d5fe3dd0` CI fixes).

Build and test tiers are cited from the orchestrator's canonical gate evidence
for `b92cb9a7`; nothing was re-run here. Every code claim below was read on
the merged tree.

## Evidence

### The `.app-shell` grid: 220px rail plus content column

Tier: build/test (rendered on every authenticated route in the green
browser lane) plus source.

`src/Pegasus.Web/Pages/_ViewStart.cshtml:2` makes `_Layout` the layout for
every page under `Pages/`, so the shell has the broadest possible production
caller.

`src/Pegasus.Web/Pages/Shared/_Layout.cshtml:55`,`:56`,`:129`:

```
<div class="app-shell" data-app-shell>
    <header class="app-rail">
    ...
    <div class="app-column">
```

`src/Pegasus.Web/wwwroot/css/site.css:34` and `:77`:

```
--focus:#d3232a;--rail:220px;--content-max:1580px;--gap:12px;--page-pad:18px;
.app-shell{min-height:100vh;display:grid;grid-template-columns:var(--rail) minmax(0,1fr)}
```

`main.app-main > .content` at `_Layout.cshtml:160`,`:164`, capped by
`site.css:139` — `.content{width:min(100%,var(--content-max));margin-inline:auto;min-width:0}`.
The two contract breakpoints exist: `site.css:770` `@media(max-width:980px)`
with `:771` `:root{--rail:100%;--content-max:none}`, and `site.css:799`
`@media(max-width:760px)`.

### The rail, and its counts

Tier: build/test (a SQL-backed integration test asserts the rendered figure).

Rail order and glyphs at `_Layout.cshtml:60-113` — Work Centre, Inbox,
Upload, Cases, Search, Operations under the "Work" label; Administration
under "Manage", inside `@if (isAdministrator)`. Health line at `:118` and
user block at `:121-127`.

The count producer is a globally registered page filter, not a per-page
call. `src/Pegasus.Web/Program.cs:299`:

```
builder.Services.AddRazorPages()
    .AddMvcOptions(options => options.Filters.Add<Pegasus.Web.Presentation.RailCountsPageFilter>());
```

`src/Pegasus.Web/Presentation/RailCountsPageFilter.cs:61-77` writes exactly
one key, `Cases`, as the §1.1 contract sum — not_ready + review +
with_engineer + held + open Triage + Unidentified — from
`IDashboardQueries.GetCaseStageCountsAsync`, `IListTriage` and
`IUnidentifiedStore.ListQueueAsync` in one `Task.WhenAll`. `_Layout.cshtml:29`
renders nothing for a missing key:

```
int? CountFor(string key) =>
    railCounts is not null && railCounts.TryGetValue(key, out var value) ? value : null;
```

`tests/Pegasus.IntegrationTests/RailCountsWebTests.cs:22` seeds one Not-ready
Case, GETs `/`, asserts the `nav-count` next to `Cases` reads `1`, and
asserts Inbox and Operations render no `nav-count` span at all. In the gate
evidence's `Pegasus.IntegrationTests` run: Failed 0, Passed 1022.

### The utility bar

Tier: build/test plus source.

`_Layout.cshtml:130-147`: `<section class="utility-bar" aria-label="Utility bar">`
carrying the freshness line, a `role="search"` GET form to `/Search` with
`[data-command-input]` and the "Ctrl K" hint, the Add primary button
(`data-dialog-open="add-dialog"`) and the bell
(`data-dialog-open="notifications-dialog"`). The landmark wrapper is not
cosmetic: it was added because axe's `region` rule failed CI run 33157629752
on the bare `div`.

Every drawn control resolves to a real destination (EPIC-011 D7). The Add
dialog's items link to `/Upload`, `/Cases/Create` and `/Inbox`
(`_ShellDialogs.cshtml:47-78`); the account dialog's Sign out posts to
`/Account/SignOut` (`:38-44`), the handler that
`ShellAndStatusPageWebTests.cs:55` proves redirects with `signedOut=True`.

### The workspace-tab strip

Tier: registration (see the finding below) for the per-record tab; build/test
for the strip itself.

`_Layout.cshtml:148-158` renders `<nav class="workspace-tabs" ... data-workspace-tabs>`
with the Work Centre tab and an "Open" button bound to the command palette.
`wwwroot/js/site.js:1239-1333` implements the record tabs: read/write of
`localStorage` key `pegasus.workspaceTabs`, `MAX = 4`, least-recently-used
evicted by `while (tabs.length > MAX) { tabs.shift(); }`, per-tab close
button, and a `try/catch` that degrades rather than throwing when storage is
refused. A page enrols itself by setting `ViewData["WorkspaceRecord"]`, read
at `_Layout.cshtml:43`.

**Finding — no production producer.** `git grep -n "WorkspaceRecord" -- src tests`
returns exactly one hit, `_Layout.cshtml:43` (the consumer). No page on
`b92cb9a7` sets it, so on merged `dev` the strip renders only the Work Centre
tab and the Open button; the record-tab mechanism is present and
correct-by-reading but has no caller. This is deliberate deferral, stated in
the ticket's own post-implementation report ("What wave 2 must know"), not a
silent gap — but by the repository's own "done means wired" rule the record
tab is tier 1, not tier 3, and is recorded as outstanding below.

### The command palette

Tier: build/test for its render; source for its behaviour (see Outstanding).

`_ShellDialogs.cshtml:102-158` pre-renders the dialog: a
`[data-command-palette-input]`, a `role="listbox"` of `.command-result`
buttons each carrying a `data-route` (`/`, `/Inbox`, `/Upload`, `/Cases`,
`/Search`, `/Operations`, `/Administration`, each behind the same composition
and role guards as the rail), and a `[data-command-fallback]` result routing
to `/Search?query=`.

`wwwroot/js/site.js:1130-1234` binds it: `input` filters, ArrowUp/ArrowDown
move `aria-selected`, Enter follows the selection or falls back to
`'/Search?query=' + encodeURIComponent(...)`, Enter in the utility bar's
`[data-command-input]` opens it seeded, and it opens through the shared
dialog binding so focus, `inert` and Escape behave as for every other dialog.
`site.js:1341-1348` binds Ctrl/Cmd K globally — the one shortcut that also
fires inside a field.

The dialog is opened via the generalised `[data-dialog]` block, whose
`inertOutside(dialog)` at `site.js:773` walks from the dialog to `body`
marking siblings `inert` and releasing exactly those on close. That shape
replaced an earlier `inert`-on-`[data-app-shell]` version as the blocking
finding of the PR #589 review.

### Route moves and their 301 stubs

Tier: build/test for two of the three; route registration for the third.

| Move | Implementation | Evidence |
| --- | --- | --- |
| `/Triage` → `/Cases[?tab=]` | `Pages/Triage/Index.cshtml.cs:21-22` `RedirectPermanent("/Cases" + …"?tab=" + Uri.EscapeDataString(Queue))`, routed by `@page "/Triage"` | route registration only — **no test** |
| `/Unidentified` → `/Cases?tab=unidentified` | `Pages/Unidentified/Index.cshtml.cs:17-18` | `TriageQueuesWebTests.cs:186` asserts `MovedPermanently` and the exact `Location` |
| `/Cases?<search parameter>` → `/Search` | `Pages/Cases/Index.cshtml.cs:286-288` | `AdministrationSearchAccountWebTests.cs:114` asserts `MovedPermanently` for a single-parameter link and for a twelve-parameter bookmark, byte-for-byte, then GETs the target and asserts every value survived |

The destinations themselves moved as claimed: `Pages/Cases/Index.cshtml:1` is
`@page` (the workflow tabs, at `/Cases`) and `Pages/Search/Index.cshtml:1` is
`@page` under `Pages/Search/` (the case search, at `/Search`).
`docs/design/test-ui/catalogue.json` classifies both `/Triage` and
`/Unidentified` as `redirect`.

### The deleted page

Tier: build/test (the route no longer exists to be served).

`src/Pegasus.Web/Pages/ImageIntake/` contains only `Details.cshtml` and
`Details.cshtml.cs` on `b92cb9a7`; `Index.cshtml` and `Index.cshtml.cs` were
deleted in `71277763`, exactly D1's scope (list removed, image record kept).
The merge diff also removed `docs/design/test-ui/pages/vehicle-images--default.html`
and `…--empty.html`, and `catalogue.json` retains only the
`/VehicleImages/{id:guid}` detail entry.

### Design-system assets

Tier: build/test.

`wwwroot/fonts/inter/` carries `InterVariable.woff2` (352,240 bytes),
`InterVariable-Italic.woff2` (387,976 bytes) and `LICENSE.txt` (SIL OFL 1.1,
4,472 bytes) — D13. `wwwroot/images/lucide-sprite.svg` and
`Pages/Shared/_LucideSprite.cshtml` each define 60 `id="icon-…"` symbols.
`site.css` is 2,482 lines; `site.js` is 1,582.

`OperatorLabels.CaseStage` at `Presentation/OperatorLabels.cs:134-146` is the
single D3 mapping (`ReportPreparation or PostReport => "With Engineer"`,
`PostReportComplete => "Complete"`, terminals as `"Closed · …"`), and
`OperatorLabels.Nav` at `:181-192` is the single nav label list.

### Build and test gate

Tier: build/test. Cited, not re-run, from the orchestrator's gate evidence
for `b92cb9a7`:

```
dotnet restore ./Pegasus.slnx --locked-mode                        -> exit 0
dotnet build ./Pegasus.slnx --configuration Release --no-restore   -> 0 Warning(s), 0 Error(s)
dotnet test  --filter 'Category!=Corpus&Category!=Browser'
  Pegasus.ArchitectureTests   Failed: 0, Passed:  100
  Pegasus.Core.Tests          Failed: 0, Passed: 1133
  Pegasus.IntegrationTests    Failed: 0, Passed: 1022, Skipped: 2
```

The `Browser` category is excluded from that run, so the browser lane is
cited separately from CI (below).

## The ticket's own verification items

| Item | Status | Evidence |
| --- | --- | --- |
| Shell matches §1.1/§1.13 at 1580/1100/760 with no clipped text or overflow | Partly proven | `tests/Pegasus.IntegrationTests/Browser/LayoutIntegrityTests.cs:40` walks all 22 routes of `AccessibilityTests.AuthenticatedRouteList` × {1580, 1100, 760} asserting HTTP 200, no `documentElement.scrollWidth > innerWidth`, no clipped text, one `main`, one `h1`, no `[style]`. The `browser` CI check is `success` on `d5fe3dd0` (PR #589's head) and on `2d67cefa` (PR #612's head, which merged as `b92cb9a7`), read via `gh api …/check-runs`. **Not proven:** conformance to §1.1/§1.13 as a design contract is a human judgement no test makes, and "no clipped text" holds only outside the test's own allow-list (`.brand, .vh, .pane-scroll, .table-wrap, .primary-nav, .workspace-tabs, .tabs, .estimate-table, .command-results, .report-preview, .row-excerpt, .ribbon-value, .rail-user strong, .workspace-tab span, textarea, select, [data-allow-clip]`, `LayoutIntegrityTests.cs:34-38`). |
| All routed pages remain reachable; old URLs 301 to the new ones; `scripts/Test-UiCatalogue.ps1` passes | **Not proven — one clause is false** | Reachability: proven for the 22 routes in `AuthenticatedRouteList`, not for every routed page. 301s: two of three test-proven (table above); `/Triage` is implemented and routed but untested. `scripts/Test-UiCatalogue.ps1` **fails** on `b92cb9a7`, exit 1, one error: `Routed Razor source is not classified: src/Pegasus.Web/Pages/Administration/Principals/EvaSubmission.cshtml`. See Outstanding. |
| Rail counts only where a real figure exists; none invented | Proven | `RailCountsPageFilter.cs:69-77` populates only `Cases`; `_Layout.cshtml:29` renders nothing for a missing key; `RailCountsWebTests.cs:22` asserts the real figure on `Cases` and the absence of any `nav-count` on Inbox and Operations. |
| Operator eyeballs the shell before wave 2 starts | **Unproven** | No record of an operator review exists in the ticket folder or on the board. This is a human gate; it cannot be proven from the repository, and it is not ticked here. |

## Outstanding

- **`scripts/Test-UiCatalogue.ps1` fails on `dev`** — exit 1, one error, the
  unclassified `Administration/Principals/EvaSubmission.cshtml`. Verified not
  to be PLAT-029's doing: the page was added by `09beefef` (TICK-077, PR
  #574) and `git show 690ca579:docs/design/test-ui/catalogue.json` (the
  branch's merge base) already contains zero `EvaSubmission` entries. The
  page is [[PLAT-052]]'s (in review — its route is being de-doubled); the
  catalogue entry needs a rendered snapshot, which is an orchestrator step.
  Until one of those lands, this gate stays red for reasons outside this
  ticket.
- **The workspace-tab record mechanism has no production caller.** Nothing in
  `src` sets `ViewData["WorkspaceRecord"]`. The Case workspace is the
  intended producer ([[CASE-012]], merged as `210727dd` in this same wave)
  and has not wired it. Tier 1, not tier 3.
- **No behavioural test covers the command palette or the workspace tabs.**
  `git grep` finds `command-result`, `command-dialog` and `workspace-tab`
  in `tests/` only inside `LayoutIntegrityTests`' clip allow-list. Their
  render is proven; their open/filter/Enter and localStorage/LRU paths are
  proven by reading the source alone.
- **The 1580/1100/760 walk and the operator eyeball belong to [[UIIMP-010]]**
  ("Final browser walk and layout-integrity proof for the Integrated
  Operations Workspace", backlog, wave 5). `LayoutIntegrityTests` is its
  tooling and is green in CI; the design-conformance judgement is its work,
  not evidence this proof can supply.
- **`docs/current-architecture.md` is stale against this ticket** — line 107
  still describes `GET /VehicleImages` as a live list caller, and line 108
  still calls `/Triage` a "physical list/detail owner". Owned by
  [[DELIV-030]] (backlog, wave 5), which the post-implementation report
  already named.
- The legacy CSS block at the end of `site.css`, and the
  `Administration/{Roles,Access,Organizations}` and Automation Activity pages
  the shell still links, are wave-5 removals owned by [[UIIMP-009]], not
  defects here.

## Scope of this proof

Written against merged `dev` at `b92cb9a7` per decision D15. `main` has not
been promoted; the exact-SHA `dev` → `main` promotion happens at wave 5 and
needs explicit `MERGE AUTH GRANTED`.

## 2026-08-29 — Reversed out of Done under the strict rule 14 (D20/D21)

The operator settled rule 14 in favour of the strict reading after this proof was
written, and separately ruled that a disabled control or a closed feature gate is
never a delivered capability (D21). An independent GPT-5.6 audit, adjudicated
against this ticket's own What/Owns/Verification scope, found the following named
capabilities are not delivered on merged `dev` at `b92cb9a7`:

| Capability | Why it does not qualify | Wired by |
| --- | --- | --- |
| Workspace tabs in localStorage, max 4 LRU — named verbatim in the What's module enumeration | `git grep WorkspaceRecord -- src/` returns exactly four hits, all consumers: `Pages/Shared/_Layout.cshtml:43` (`ViewData["WorkspaceRecord"] as (string Href, string Label)?`), `:161-163` (the `data-workspace-*` attributes), and `wwwroot/js/site.js:1243` (a comment) and `:1321` (`document.querySelector('main[data-workspace-record]')`). No page or PageModel writes `ViewData["WorkspaceRecord"]`, and Razor omits the null attribute, so `main[data-workspace-record]` never exists, `write(tabs)` at `site.js:1329` is unreachable, and localStorage `pegasus.workspaceTabs` can only ever be empty. Zero references in `tests/` either — not even test-only. | [[CASE-012]] — the Case record page that must set `ViewData["WorkspaceRecord"] = (Href, Label)`; its own `proof/proof.md:278` already records "`ViewData["WorkspaceRecord"]` has no writer", so it needs a corrective follow-up |
| Sort toggles — named verbatim in the What | `git grep data-sort-toggle -- src/` returns only the binder at `wwwroot/js/site.js:1413`. `Pages/Mail/Index.cshtml:112` renders a real `<a class="btn btn--small sort-toggle" …>` server-side link but carries neither `data-sort-toggle` nor `data-sort-arrow`, so the module this ticket shipped binds to nothing. | [[MAIL-025]] — its What names "messages with sort toggle"; the link ships without the data hooks, so it needs a corrective follow-up |
| Estimate tabs — named verbatim in the What | `git grep tablist -- src/` returns only `wwwroot/js/site.js:1486`. No `[role="tablist"]` is rendered anywhere in the product. | [[ENG-028]] — its What names "estimate tabs (tablist, keyboard)"; backlog, so this cannot clear until ENG-028 lands |
| `_MetricCard` shared partial — covered by the What's "rewrite the shared partials" and Owns `Pages/Shared/**` | `src/Pegasus.Web/Pages/Shared/_MetricCard.cshtml` was rewritten by this ticket's own commit `865b4c0c`, but `git grep MetricCard -- src/ tests/` returns no invocation at all. Weakest of the four — it was equally uninvoked at `865b4c0c^`, so this ticket restyled an already-orphaned partial rather than orphaning it; the reversal does not depend on it. | [[UIIMP-008]] — its What names the "five-metric strip"; verifying, rendered inline rather than through `_MetricCard`, so it must either consume the partial or this ticket must drop the superseded partial |

Nothing in the proof above is withdrawn — it remains accurate at the tier it claims.
What changed is the bar, not the evidence. This proof already concedes the fatal
point at `proof/proof.md:118` ("Finding — no production producer") and `:231` ("The
workspace-tab record mechanism has no production caller"), while `:98` claims "Every
drawn control resolves to a real destination". Under D20 honest disclosure does not
permit Done.

No gate blocks this ticket, and `disabledOrGated` is empty — the reversal is a pure
rule-14 unwired finding. Both gates its surfaces sit behind are OPEN in the deployed
estate: `_Layout.cshtml:8` `Environment.IsProduction()` for the Inbox/Upload/
Operations rail entries (`docs/operations.md:290-293` records the deployed Razor
Pages Web on Container Apps in `rg-pegasus-prod`), and `Features:AutomationMcp` for
`/authorize` and the Automation admin link (`docs/operations.md:122` and `:131-141`,
enabled in production since release 9, 2026-08-18).

### Findings that were NOT counted against this ticket

- Account dialog "Session started" / `auth_time` value — a §1.1 content detail
  imported from the design contract; this ticket's own text names only "Account
  dialogs", and that dialog is wired (`_Layout.cshtml:123` opener,
  `_ShellDialogs.cshtml:37` sign-out form). It is also a missing rendered value, not
  unwired code. No owning ticket exists — it needs a new §1.1 shell-completeness
  ticket, not a reversal of this one.
- "Create upload request" missing from the Add dialog — same shape: a §1.1 draw
  detail, not named in this ticket's own text, which names only "Add/Notifications/
  Account dialogs". The Add dialog is wired with three real destinations
  (`_ShellDialogs.cshtml:59`, `:64`, `:70`), and the capability itself is already
  wired elsewhere via `Cases/Details.cshtml:189-190` to
  `OnPostCreateRequestUploadLinkAsync` at `Cases/Custody.cshtml.cs:119`. A missing
  menu entry, not unwired code.
- Permanently inert Glass's and Audatex D7 seams at
  `Pages/Cases/Assessment/Index.cshtml:211` and `:214` — in
  `Pages/Cases/Assessment/**`, which this ticket does not own; charged to
  [[ENG-025]] and supplied by [[TICK-085]] / [[ENG-030]].
- The closed per-Principal EVA submission path — not touched by this ticket; owned
  in the EVA/estimates lane.
