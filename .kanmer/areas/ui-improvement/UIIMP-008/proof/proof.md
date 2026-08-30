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

---

# HELD — independently re-verified 2026-08-29, closeout board walk

## Verdict: **this ticket does NOT reach Done.** It stays in Verifying.

Re-verified against **merged `dev` at
`450b9234a6f5626f21adea3c4da244550a3bdace`** (2026-08-29 18:03:20 +0100).
`b92cb9a7`, the SHA the body above was written at, is an ancestor of it.

This remains **dev-merged evidence, pending the single wave-5 `dev` → `main`
promotion**. `main` serves release 36; nothing here is deployed.

The finding in the body above was re-derived from scratch by a reviewer who did
not write it, and then confirmed a second time by an independent audit. **All
three walks agree: the External work record link is inert, and the defect is
still live at `450b9234`.**

## The defect, confirmed at the newer SHA

`git show 450b9234:src/Pegasus.Web/Pages/Index.cshtml.cs`, lines 58–64:

```csharp
public static string RecordPage(NeedsAttentionKind kind) => kind switch
{
    NeedsAttentionKind.Case or NeedsAttentionKind.HeldDecision => "/Cases/Details",
    NeedsAttentionKind.Mail => "/Unidentified/Details",
    NeedsAttentionKind.Triage => "/Triage/Details",
    _ => "/Operations"
};
```

`"/Operations"` is a Razor **page name**, and no page has it:

```
git ls-tree -r --name-only 450b9234 -- src/Pegasus.Web/Pages/Operations
  src/Pegasus.Web/Pages/Operations/Index.cshtml
  src/Pegasus.Web/Pages/Operations/Index.cshtml.cs
```

Its page name is `/Operations/Index` — which is the spelling the shell itself
uses at `src/Pegasus.Web/Pages/Shared/_Layout.cshtml:96`
`<a class="nav-link" asp-page="/Operations/Index" …>`. (The page's own
`@page "/Operations"` sets the *URL template*, not the page name; `asp-page`
resolves page names.)

Two rendered controls bind to it:

```
src/Pegasus.Web/Pages/Index.cshtml:106
  <a class="btn btn--small" asp-page="@IndexModel.RecordPage(head.Kind)" …>  ← "Open full record"
src/Pegasus.Web/Pages/Index.cshtml:148
  <a class="btn btn--dark" asp-page="@IndexModel.RecordPage(selected.Kind)" …> ← "Next permitted action"
```

### The empirical confirmation still holds

The same bad spelling occurs once more, at
`src/Pegasus.Web/Pages/Intake/Details.cshtml:36`
`<a class="btn" asp-page="/Operations">`, and the committed Test UI corpus
holds a **real Razor render** of that page:

```
docs/design/test-ui/pages/received-details--default.html:78
  <a class="secondary-action" href="">Back to Operations</a>
```

An empty `href` — the only one in that file. The anchor tag helper generates no
URL for a page name that matches no endpoint. This is measured output, not
inference.

## Why that bars Done

`NeedsAttentionKind.ExternalWork` is a genuine production state:
`src/Pegasus.Core/Operations/OperationsSnapshot.cs:264`–`:283` emits a row
whenever `GetRequestOperations` yields a retryable external-work item. When the
Work Centre shows one, both its "Open full record" link and its "Open
Operations" next-permitted-action button render `href=""` and re-request `/`.

That breaks three separate bindings at once:

- **The ticket's own verification item** — *"Every metric and work-item links
  to a real route"* — fails for one of the five named kinds.
- **EPIC-011 `context.md`** — *"Every drawn control maps to a named handler or
  an approved disabled seam (D7). **Never render an inert control.**"* This is
  an inert control, and it is not an approved seam.
- **D21** — *"Control permanently inert (a D7 integration seam) → **No**"*. An
  unticketed inert control is worse than a seam: D22 requires a seam to be a
  *named, ticketed* integration rendered as a real `disabled` button with a
  `data-condition`. This is none of those; it is a broken link.

No test covers it: `git grep -n "RecordPage\|Open Operations\|Open full record"
450b9234 -- tests/` returns nothing, and no integration test renders `/` with
an ExternalWork row.

## The ticket that supplies the fix: **UIIMP-008 itself**

`src/Pegasus.Web/Pages/Index.cshtml.cs` is in this ticket's **own Owns list**
(*"`src/Pegasus.Web/Pages/Index.cshtml(.cs)`, …"*). It is not another lane's
file and is not deferrable to a neighbour under rule 2. The fix is one word —
`"/Operations"` → `"/Operations/Index"` at `Index.cshtml.cs:63` — plus a
regression test that renders `/` with an ExternalWork row, which is the gap
that let this ship.

**No source change was made during this walk**; the closeout brief for this
pass is board work only.

### Reported, not fixed — a neighbour's copy of the same defect

`src/Pegasus.Web/Pages/Intake/Details.cshtml:36` carries the identical bad
spelling and is pre-existing, not this lane's. Per D19 it is reported loudly
rather than silently fixed: it belongs to the Received/Intake page lane
(**INTK-046**, Done) and should be swept with the same one-word change.

## Two further findings from this walk (recorded, not blocking)

Both are the same defect class as **PLAT-058** — queried on every Work Centre
load, read by nothing — and neither is a capability this ticket names, so
neither adds to the hold:

| Finding | Detail |
| --- | --- |
| **PLAT-058 confirmed still live** | `MailActivityCounts.ReceivedToday` (`DashboardCounts.cs:60`) is queried every load at `EfDashboardQueries.cs:123`. `git grep "ReceivedToday" 450b9234` returns five hits: three in `DashboardCounts.cs` (declaration + doc comment) and two in `DashboardCountersWebTests.cs:78,100`. **Zero render sites.** PLAT-058 owns its fate |
| **Three more unread snapshot members — unticketed** | `OperationsSnapshot.CaseActivity` (composed at `:131` via `GetCaseActivityCountsAsync`, which runs **three** EF queries at `EfDashboardQueries.cs:82,89,97`), `OperationsSnapshot.TriageCount` (`:157`) and `OperationsSnapshot.DueWork` (`:158`) have no reader in `Pegasus.Web`. `IGetOperationsSnapshot` has exactly one consumer (`Index.cshtml.cs:41`) which reads 5 of 8 members. This is PLAT-058's defect roughly 4× wider, and **no ticket owns it** |

## Citation drift in the body above (accuracy note, not a defect)

The body was written at `b92cb9a7`; PLAT-054's `LondonCalendar` extraction has
since removed ~48 lines from `OperationsSnapshot.cs`. Line numbers that have
moved, for anyone re-walking it: `DependencyInjection.cs:256` → **:267**;
`OperationsSnapshot.cs` `:123/:135/:139/:144/:148/:151/:152/:180/:276/:300` →
**:114/:126/:130/:135/:139/:142/:143/:171/:266/:290**; `site.js:54–60` →
**:77–84**; `site.js:1385` → **:1408**. The refactor was behaviour-preserving
and all five needs-attention kinds remain intact. Every `Index.cshtml`,
`Index.cshtml.cs`, `DashboardCounts.cs`, `OperatorLabels.cs` and test citation
in the body still lands exactly.

## Commands run, with exit codes

```
dotnet build ./Pegasus.slnx --configuration Release -nodeReuse:false
  -> Build succeeded. 0 Warning(s), 0 Error(s).   exit 0
```

No `MSB3027`/`MSB3021` file lock and no `SqlException` occurred; this is a
clean PASS, not INCONCLUSIVE. **A green build is not evidence against this
finding** — a page name that matches no endpoint is a runtime link-generation
failure, not a compile error. That is precisely why it shipped.

## What this evidence does NOT prove

- **Nothing here is deployed.** Tier-2 evidence only.
- **The other four kinds are unaffected** — `/Cases/Details`,
  `/Unidentified/Details` and `/Triage/Details` all resolve, and the metric
  strip, the five queried figures and the D14 Blocked mapping are all proven in
  the body above and were re-confirmed in this walk.
- **The 1580/1100/760 layout walk and the Test UI snapshot for `/` remain
  unproven**, exactly as the body records. **UIIMP-010** owns the walk;
  **UIIMP-005** owns the snapshot gate. Those are *additional* to the hold, not
  the reason for it.
- **No browser was driven.** The inert-link consequence is proven from a
  committed Razor render plus source reading, not from a live page load.

---

# CLEARED — re-audited 2026-08-30 against deployed `main`

## Verdict: the hold above is **stale**. This ticket reaches Done.

Re-audited against **`origin/main` at `fb3f07acc8cca8d9d8b57db8a431b607772436dc`**,
which is what release 37 deployed to production on 2026-08-30. The two SHAs the
sections above were written at (`b92cb9a7`, `450b9234`) are both ancestors of it.

This is no longer dev-merged evidence: the promotion happened, so the code
audited here is the code production serves.

## The defect the hold was raised on is fixed

The `# HELD` section held this ticket because `RecordPage` returned
`"/Operations"`, which is not a Razor page name, leaving "Open full record" and
"Next permitted action" inert. On `main`:

```
git show origin/main:src/Pegasus.Web/Pages/Index.cshtml.cs
58:    public static string RecordPage(NeedsAttentionKind kind) => kind switch
60:        NeedsAttentionKind.Case or NeedsAttentionKind.HeldDecision => "/Cases/Details",
61:        NeedsAttentionKind.Mail => "/Unidentified/Details",
62:        NeedsAttentionKind.Triage => "/Triage/Details",
63:        _ => "/Operations/Index"
```

Fixed by **PR #628, "fix(shell): name the Operations page so its links
resolve (UIIMP-008)"** — this ticket's own follow-up lane, merged into the
release.

Every `asp-page` value the Work Centre emits resolves to a real page file:

| `asp-page` | Backing file on `main` |
| --- | --- |
| `/Cases/Details` | `src/Pegasus.Web/Pages/Cases/Details.cshtml` |
| `/Unidentified/Details` | `src/Pegasus.Web/Pages/Unidentified/Details.cshtml` |
| `/Triage/Details` | `src/Pegasus.Web/Pages/Triage/Details.cshtml` |
| `/Operations/Index` | `src/Pegasus.Web/Pages/Operations/Index.cshtml` |
| `/Cases/Index`, `/Cases/Create`, `/Index` | present |

`Pages/Intake/Details.cshtml:36`, the second bad spelling the hold named, now
reads `asp-page="/Operations/Index"`.

## The rest of the ticket's named capabilities, censused

Rule 14 asks for every capability the ticket names, not just the one it was
held on.

| Named capability | Evidence on `main` |
| --- | --- |
| Five needs-attention kinds | All five produced in `Pages/Index.cshtml.cs`: `Case` (:60), `HeldDecision` (:60), `Mail` (:61), `Triage` (:62), `ExternalWork` (:71) |
| Five-metric strip → `/Cases?tab=…` | `asp-route-tab` values rendered: `not_ready`, `review`, `held`, `unidentified` ×2 — the second being Blocked, which routes to `?tab=unidentified` exactly as decision **D14** requires |
| Selected-work detail: Next permitted action, Copy reference | present in `Pages/Index.cshtml` |
| No inert "Filter" control | no `Filter` control in the rendered markup — the ticket's explicit prohibition holds |
| `DashboardCounts` / `IGetOperationsSnapshot` | consumed by `src/Pegasus.Web/Pages/Index.cshtml.cs`, the routed `/` page — a real caller, not a registration |

No capability this ticket names is registered-but-unreachable. The D21 table's
failing row does not apply.

## A defect found while auditing — NOT this ticket's, and filed separately

The audit swept every `asp-page` in the application for a value with no backing
page, and found **one survivor of the same defect class**:

```
src/Pegasus.Web/Pages/Cases/Shared/_CaseDocuments.cshtml:40
  <a class="btn" asp-page="/Operations">
```

Reached from `Pages/Cases/Details.cshtml:318` → `_CaseFiles` →
`_CaseDocuments`, rendered when `Model.Section == "case-files"` — a live
operator route, deployed. It is **CASE-027's owned file**, not this ticket's
(`Cases/Shared/_CaseDocuments.cshtml` is named in CASE-027's *Owns*), so under
rule 2 it is filed as its own ticket rather than absorbed here or used to hold
this one.

Worth recording why it survived: **no snapshot state captures
`?section=case-files`** — the corpus holds `case-details--default`,
`--conflict` and `--unavailable` only — so neither UIIMP-008's fix nor
UIIMP-005's new CI gate could have caught it.

## What this evidence does NOT prove

- **No browser or layout walk.** Nothing here claims the Work Centre is free of
  clipped text or overflow at 1580/1100/760 — **UIIMP-010** owns that, and it is
  still in backlog. The ticket's second verification line remains unproven.
- **No live click-through.** The links are proven to *resolve to a page* by
  static census; nobody has clicked them in the deployed estate.
- **Single-model audit.** Unlike the earlier walks on this ticket, this pass was
  not independently refuted by a second model family. The evidence is
  mechanical — file existence and string census — rather than judgement, which
  is why that was judged acceptable here; it would not be for a behavioural claim.
- `MailActivityCounts.ReceivedToday` is still queried and rendered nowhere
  (**PLAT-058**, backlog). It is linked from this ticket and is not part of its
  named contract.

## Commands run

```
git show origin/main:src/Pegasus.Web/Pages/Index.cshtml.cs        -> exit 0
git ls-tree -r --name-only origin/main -- src/Pegasus.Web/Pages   -> exit 0
git grep -n 'Operations' origin/main -- src/Pegasus.Web           -> exit 0
per-asp-page existence sweep (git cat-file -e)                    -> 1 miss, named above
```
