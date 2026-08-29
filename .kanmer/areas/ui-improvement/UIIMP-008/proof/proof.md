# Proof — UIIMP-008: Port the Work Centre (/) to the Integrated Operations Workspace

## What was verified, and where

Merged `dev` at `b92cb9a7`. PR #610 merged as `c87e8d5d` at
2026-08-29T09:14:17Z from branch head `682668dd`;
`git merge-base --is-ancestor c87e8d5d dev` exits 0, so the ticket's
recorded merge is reachable on the merge target. `git diff --stat
c87e8d5d b92cb9a7` over the twelve files that merge touched returns
nothing, so this lane's shipped code has not moved on `dev` since. Build
and test tiers are cited from the orchestrator's canonical gate evidence
for `b92cb9a7`; nothing was re-run here.

Most of the ticket is proven below. **One claim is not: the External
work kind's record link names a Razor page that does not exist, so one
of the five kinds does not link to a real route.** The ticket is held in
Verifying for it.

## Evidence

### The page is the production home route, wired to one query

Tier: registration.

`src/Pegasus.Web/Pages/Index.cshtml:1` is `@page` with no template, so
the page is `/`. `src/Pegasus.Web/Program.cs:298` calls
`AddRazorPages()` and line 1025 `MapRazorPages()`.
`src/Pegasus.Web/Pages/Shared/_Layout.cshtml:63` makes it the rail's
first entry (`<a class="nav-link" asp-page="/Index" …>` labelled
`OperatorLabels.Nav.WorkCentre`), and `Account/AccessDenied.cshtml:14`,
`Error.cshtml:19` and `StatusCode.cshtml:16` all return to it.

`Index.cshtml.cs:16` takes exactly one dependency,
`IGetOperationsSnapshot`, registered at
`src/Pegasus.Infrastructure/DependencyInjection.cs:256`:

```
services.AddScoped<IDashboardQueries, EfDashboardQueries>();          // 255
services.AddScoped<IGetOperationsSnapshot, GetOperationsSnapshot>();  // 256
```

### Every metric figure is a queried count

Tier: registration + build/test.

Each of the five figures traces from the markup to an EF query that
counts rows. No literal, no fixture, no placeholder string.

| Metric | Markup | Model | Query behind it |
| --- | --- | --- | --- |
| Not ready | `Index.cshtml:39` `@Model.CaseStages.NotReady` | `Index.cshtml.cs:44` | `EfDashboardQueries.GetCaseStageCountsAsync` (`EfDashboardQueries.cs:23`) — `context.CaseWorkflows … GroupBy(State).Select(… group.Count())` |
| Review | `Index.cshtml:43` | same | same |
| Held | `Index.cshtml:47` | same | same |
| Unidentified | `Index.cshtml:51` `@Model.MailActivity.Unidentified` | `Index.cshtml.cs:45` | `EfDashboardQueries.cs:140` — `context.Set<UnidentifiedItemEntity>().AsNoTracking().CountAsync(item => item.State == "Open", …)` |
| Blocked | `Index.cshtml:55` `@Model.Counts.BlockedIntake` | `Index.cshtml.cs:43` | `EfIntakeReceiptStore.GetCountsAsync` (`EfIntakeReceiptStore.cs:170`) — receipts with no `CaseIntakeLinks` row, counted by decision `BlockedIntake` |

The three call sites are `OperationsSnapshot.cs:123`
(`intakeQueries.GetCountsAsync`), `:139` (`GetCaseStageCountsAsync`) and
`:144` (`GetMailActivityCountsAsync`), all inside
`GetOperationsSnapshot.ExecuteAsync`.

Guarded on merged `dev` by
`DashboardCountersWebTests.BlockedMetricCountsBlockedIntakeReceipts`
(`tests/Pegasus.IntegrationTests/DashboardCountersWebTests.cs:46`),
which stores one `IntakeDecision.BlockedIntake` receipt, GETs `/`, and
scrapes the rendered figure:

```
Assert.Equal(1, int.Parse(match.Groups[1].Value, …));
```

The class is `[Trait("Category", "SqlServer")]`, so it is inside the
gate run's `Category!=Corpus&Category!=Browser` filter —
`Pegasus.IntegrationTests Failed: 0, Passed: 1022, Skipped: 2`.

### Every metric links to a real route, and Blocked honours D14

Tier: build/test.

`Index.cshtml:37–56` renders five
`<a … asp-page="/Cases/Index" asp-route-tab="…">`.
`src/Pegasus.Web/Pages/Cases/Index.cshtml` exists, and its `Tabs` list
(`Cases/Index.cshtml.cs:88`) declares the keys `not_ready`, `review`,
`with_engineer`, `complete`, `triage`, `held`, `unidentified` — every
key the strip uses is a real tab.

D14 holds on both sides. `Index.cshtml:53` routes Blocked to
`asp-route-tab="unidentified"`, and `Cases/Index.cshtml.cs:160` records
the other half — "Open Unidentified items only — Blocked intake rows are
listed but never counted (D14)" — with those rows listed under
`RowKind.BlockedIntake` and chipped "Blocked intake"
(`Cases/Index.cshtml.cs:517`). The Work Centre's Unidentified metric and
that tab's count apply the same predicate, `State == Open` on
`UnidentifiedItemEntity` (`EfDashboardQueries.cs:142` and
`EfUnidentifiedStore.cs:258`), so the two figures cannot disagree.

`DashboardCountersWebTests.EveryMetricLinksToItsCasesTab`
(`DashboardCountersWebTests.cs:21`) GETs `/` and asserts the exact
rendered href for all five, including the D14 pair:

```
("not_ready","not_ready"), ("review","review"), ("held","held"),
("unidentified","unidentified"), ("blocked","unidentified")
…
Assert.Contains($"data-value=\"{key}\" href=\"/Cases?tab={tab}\"", html);
```

Passed in the gate run.

### The five kinds are the five kinds, each from an existing query

Tier: build/test.

`NeedsAttentionKind`
(`src/Pegasus.Core/Operations/DashboardCounts.cs:89`) declares exactly
`Case, HeldDecision, Mail, Triage, ExternalWork`, and
`OperatorLabels.NeedsAttentionKind` (`OperatorLabels.cs:307`) is the one
map to the contract words Case / Held decision / Mail / Triage /
External work. Each row is composed in
`GetOperationsSnapshot.ComposeNeedsAttentionAsync`
(`OperationsSnapshot.cs:180`) from a query that already backs another
surface — no new table, no new store:

| Kind | Source query | Line |
| --- | --- | --- |
| Case | `ICaseDueWorkQueries.GetDueAsync` | `OperationsSnapshot.cs:135` |
| HeldDecision | `ISearchCases` filtered to `CaseLifecycleState.Held` | `:148` |
| Mail | `IUnidentifiedStore.ListQueueAsync` | `:151` |
| Triage | `IListTriage` for `Open` and `AwaitingInformation` | `:128`, `:131` |
| ExternalWork | `GetRequestOperations`, kept only when `Kind == ExternalWork && CanRetry` | `:152`, `:276` |

`DashboardBoundaryTests.NeedsAttentionListsOneRowPerKindFromItsFiveQueries`
(`tests/Pegasus.Core.Tests/Operations/DashboardBoundaryTests.cs:66`)
feeds one stub row per source and asserts the composed kinds are exactly
those five. Its siblings pin the boundaries:
`…SkipsTriageWithAFindingAndExternalWorkThatCannotRetry` (`:133`),
`…StillListsOpenTriageBehindFiftySettledRecords` (`:165`),
`…OrdersByPriorityThenDueThenReference` (`:182`),
`…IsBoundedAtFiftyRows` (`:218`). `Pegasus.Core.Tests Failed: 0,
Passed: 1133` in the gate run.

### Work items: four kinds link to a real route, one does not

Tier: build/test for the row link; code plus a rendered artifact for the
record link.

The row itself always resolves — `Index.cshtml:71` is
`asp-page="/Index" asp-route-selected="@item.Id"`, and the selection is
re-resolved server-side at `Index.cshtml.cs:47–50`.

The record link is `IndexModel.RecordPage(kind)`
(`Index.cshtml.cs:58`), bound at `Index.cshtml:106` ("Open full record")
and `:148` (the "Next permitted action" button):

```
NeedsAttentionKind.Case or NeedsAttentionKind.HeldDecision => "/Cases/Details",
NeedsAttentionKind.Mail => "/Unidentified/Details",
NeedsAttentionKind.Triage => "/Triage/Details",
_ => "/Operations"
```

Four resolve. `Pages/Cases/Details.cshtml` (`@page "/Cases/{id:guid}"`),
`Pages/Unidentified/Details.cshtml` (`@page
"/Unidentified/{id:guid}"`) and `Pages/Triage/Details.cshtml` (`@page
"/Triage/{id:guid}"`) all exist and all take the `{id:guid}` that
`asp-route-id="@IndexModel.RecordRouteId(item)"` supplies
(`Index.cshtml.cs:70`).

**The fifth does not** — see the finding below.

### The needs-attention row carries no raw or fabricated data

Tier: build/test.

`NeedsAttentionItem`'s doc comment (`DashboardCounts.cs:120`) commits to
"every field is a recorded fact or a Core enum name; the Web layer
labels them", and the page keeps that. `TitleLabel`
(`Index.cshtml.cs:88`) routes only the ExternalWork title through
`OperatorLabels.Humanise` — the same helper
`Pages/Operations/Index.cshtml:120` already uses for `ExternalKind` —
and `DetailLabel` (`:98`) composes `$"{attempts} attempts"` in Web from
the `int? Attempts` Core carries. `WorkCentreLabelTests`
(`tests/Pegasus.IntegrationTests/WorkCentreLabelTests.cs`) pins all
three claims in 3 tests; it carries no category trait, so it ran in the
gate run.

The Copy-reference control is rendered `hidden` (`Index.cshtml:156`) and
revealed only by `wwwroot/js/site.js:54–60`, which returns early when
there is no clipboard — so it is never a dead control. `data-row-list`
(`Index.cshtml:68`) is handled at `site.js:1385`.

### The build and test gate

Tier: build/test, cited not re-run.

From the canonical gate evidence for `b92cb9a7`: `dotnet restore
--locked-mode` exit 0; `dotnet build --configuration Release
--no-restore` "Build succeeded. 0 Warning(s), 0 Error(s)"; `dotnet test
--filter 'Category!=Corpus&Category!=Browser'` → ArchitectureTests
100/100, Core.Tests 1133/1133, IntegrationTests 1022 passed with 2
pre-existing unrelated skips.

CI on the PR head `682668dd`
(`gh api repos/collisionengineers/pegasus/commits/682668dd/check-runs`):
`unit`, `browser`, `sql-integration` 1/2/3,
`sql-integration-coverage`, `changes`, `documentation`,
`local-development-scripts` and `reference-data` all **success**;
`infrastructure` skipped. That closes the post-implementation report's
open worry that round 2's commits had never been covered by a CI run —
they were. The dev merge commits `c87e8d5d` and `b92cb9a7` carry no
check runs of their own.

## Finding — the External work record link is inert

`IndexModel.RecordPage` returns the Razor page name `"/Operations"` for
`NeedsAttentionKind.ExternalWork` (`Index.cshtml.cs:63`). There is no
`src/Pegasus.Web/Pages/Operations.cshtml`; the page is
`src/Pegasus.Web/Pages/Operations/Index.cshtml`, whose Razor page name
is `/Operations/Index` — which is the spelling the shell itself uses
(`Pages/Shared/_Layout.cshtml`, `asp-page="/Operations/Index"`).

A sweep of every literal `asp-page` value in the Web project,
cross-checked against the page files, finds exactly one spelling that
names no page file, and this is it:

```
NO PAGE FILE: /Operations  (dir index exists? yes)
```

Its one other occurrence is
`src/Pegasus.Web/Pages/Intake/Details.cshtml:36` — pre-existing, not
this lane's — and it is what makes the consequence checkable without
running the app, because the committed Test UI corpus holds a real Razor
render of that page. At the commit that captured it (`35292cff`) the
source line was:

```
Pages/Intake/Details.cshtml:35
<a class="secondary-action" asp-page="/Operations">Back to Operations</a>
```

and the captured render is:

```
docs/design/test-ui/pages/received-details--default.html:78
<a class="secondary-action" href="">Back to Operations</a>
```

An empty `href`. It is the only empty-href anchor across
`received-details--default`, `dashboard--default`, `operations--default`
and `cases--default`, and the capture's rewriter cannot have produced
it: every rewrite regex in
`tests/Pegasus.IntegrationTests/TestUiSnapshotTests.cs:332–341` requires
the value to begin with `/` (`(?:href|src)="(/[^"]*)"`), so it can
neither match nor blank `href=""`. The empty href came from the anchor
tag helper failing to generate a URL for a page name that matches no
endpoint.

Consequence on the shipped Work Centre: when a needs-attention row is
**External work** — a genuine production state, since
`GetRequestOperations` yields `RequestOperationKind.ExternalWork` rows
and `Pages/Operations/Index.cshtml.cs:102` exposes `RetryExternalWork`
for exactly those — both "Open full record" (`Index.cshtml:106`) and the
"Open Operations" next-permitted-action button (`Index.cshtml:148`)
render `href=""` and re-request `/` instead of opening Operations. That
contradicts the ticket's own verification item ("every work-item links
to a real route") for one of the five kinds, and EPIC-011 `context.md`
D7 ("Never render an inert control").

Nothing covers it: `git grep -rn "RecordPage\|Open Operations\|Open full
record" -- tests/` returns nothing, and no integration test renders `/`
with an ExternalWork item. The four resolving kinds are unaffected.

The fix is one word — `"/Operations/Index"` — but it is a source change,
and this proof is read-only.

## The ticket's own verification items

| Item | Status | Evidence |
| --- | --- | --- |
| Every metric links to a real route | Proven | `DashboardCountersWebTests.EveryMetricLinksToItsCasesTab`; `Cases/Index.cshtml.cs:88` declares every tab key used |
| Every figure is a queried count | Proven | The five-row table above; `BlockedMetricCountsBlockedIntakeReceipts` scrapes the rendered figure |
| Blocked → `/Cases?tab=unidentified` (D14) | Proven | `Index.cshtml:53`; asserted as the `("blocked","unidentified")` pair |
| The five kinds, each from an existing query | Proven | `DashboardCounts.cs:89`; `NeedsAttentionListsOneRowPerKindFromItsFiveQueries` |
| Every work-item links to a real route | **Fails** | External work resolves to the non-existent page name `/Operations` — see the finding |
| No clipped text/overflow at 1580/1100/760 | Unproven | See Outstanding |
| Snapshot regenerated by the orchestrator | Unproven, and demonstrably not done | See Outstanding |

## Outstanding

- **The External work record link.** The finding above.
  `Pages/Index.cshtml.cs` is in this ticket's own Owns list, so it is not
  deferrable to a neighbour lane. The ticket stays in Verifying until it
  is fixed or explicitly dispositioned.

- **The 1580/1100/760 layout walk is not proven on merged `dev`.**
  `Browser/LayoutIntegrityTests` asserts exactly this claim, and `/` is
  in the walked route list (`Browser/AccessibilityTests.cs:16`, consumed
  by `LayoutIntegrityTests.cs:20–26`). But the class carries
  `[Trait("Category","Browser")]`, which the gate run excluded, and the
  green `browser` CI job ran on the PR head `682668dd`, whose content is
  not the merged result — `git diff --stat 682668dd c87e8d5d` shows 16
  files and ~2,138 insertions arriving from the other lanes in that
  merge, and three further merges landed on `dev` afterwards. Green at
  PR-head tier; unproven at merged-`dev` tier. **UIIMP-010** owns the
  merged walk with screenshots and axe results.

- **The Test UI snapshot for `/` is stale on merged `dev`.**
  `docs/design/test-ui/catalogue.json:412` still maps
  `src/Pegasus.Web/Pages/Index.cshtml` to `pages/dashboard--default.html`,
  described as "Current loaded dashboard with ordinary metrics", and that
  file still renders `<h1>Dashboard</h1>` with zero occurrences of
  `work-centre-metrics` or "Work Centre". This is the epic's
  regenerate-once-on-the-merging-branch rule (EPIC-011 decisions, "Two
  shared files"), and the gate it feeds is **UIIMP-005**, still in
  `review` on PR #588 — so nothing is red today, but the checklist item
  "snapshot regenerated by the orchestrator" is not satisfied at
  `b92cb9a7`.

- **Carried forward from the ticket's own report, re-checked and still
  true on merged `dev`:** `MailActivityCounts.ReceivedToday` is queried
  on every Work Centre load and rendered nowhere
  (`EfDashboardQueries.cs:123`; guarded by
  `ReceivedTodayCountsMailboxChannelOnlyNotManualUploads`) — **PLAT-058**
  decides its fate. `IUnidentifiedStore.ListQueueAsync` is unbounded
  (`EfUnidentifiedStore.cs:245`) while the composition caps at 50 only
  after composing (`OperationsSnapshot.cs:151`, `:300`) — the
  Cases/Unidentified lanes own that interface. The two empty
  `ci: retrigger` commits `812e3516` and `57a51500` remain in the merged
  history, carrying no content.

- **The lane's own review status.** The post-implementation report
  records that no `APPROVED` review exists on #610 or #598 and that the
  independent review required by CLAUDE.md workflow step 5 was never
  performed on the whole branch. This proof does not change that record,
  and the finding above is the kind of defect that review would have been
  the place to catch.

## Scope of this proof

Written against merged `dev` at `b92cb9a7` per decision D15. `main` has
not been promoted; the exact-SHA `dev` → `main` promotion happens at
wave 5.
