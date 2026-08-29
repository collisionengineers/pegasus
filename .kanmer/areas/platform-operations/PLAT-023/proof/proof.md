# Proof — PLAT-023: Redesign the Operations workspace

## What was verified, and where

Verified by reading merged `dev` at `b92cb9a7` in the primary checkout
`C:/Users/PC/Documents/GitHub/pegasus`, plus the recorded CI run for the
ticket's own PR. The work reached `dev` as PR
[#602](https://github.com/collisionengineers/pegasus/pull/602), merged
2026-08-28T19:22:53Z as `9868cf58` (`gh pr view 602 --json` reports
`state: MERGED`, `mergeCommit.oid 9868cf583a0f…`). All three recorded
commits are reachable from the `dev` head — `git merge-base --is-ancestor`
returns 0 for `a0c28af8`, `6bf5f789`, `2e7ea751` and for `9868cf58` itself.
The merge touched exactly five files:

```
git diff --stat 9868cf58^1 9868cf58
 src/Pegasus.Web/Pages/Operations/Index.cshtml      | 274 +++++++++--------
 src/Pegasus.Web/Pages/Operations/Index.cshtml.cs   |  29 ++-
 src/Pegasus.Web/Pages/Shared/_StatusChip.cshtml    |   5 +
 src/Pegasus.Web/Presentation/OperatorLabels.cs     |  84 +++++++
 .../Pegasus.IntegrationTests/OperationsWebTests.cs | 157 +++++++++-
 5 files changed, 435 insertions(+), 114 deletions(-)
```

That set matches the ticket's `files` document exactly — no neighbouring
lane's file was touched, and `tests/Pegasus.IntegrationTests/Browser/`
`OperatorJourneyTests.cs` is absent from the diff as the plan promised.

The build and test tiers are cited from the orchestrator's canonical gate
evidence for merged `dev` at `b92cb9a7`; nothing was re-run here.

## Evidence

### The page has a real production caller

Tier: **registration** (route + shipped navigation), corroborated by test.

`src/Pegasus.Web/Pages/Operations/Index.cshtml:1` declares the route:

```
@page "/Operations"
```

It is reached from two shipped controls, not from a test only:

- `src/Pegasus.Web/Pages/Shared/_Layout.cshtml:96` — the rail entry
  `<a class="nav-link" asp-page="/Operations/Index" aria-current="@CurrentWhen("/Operations")">`
- `src/Pegasus.Web/Pages/Shared/_ShellDialogs.cshtml:140` — the command
  palette result `data-route="/Operations"`

`OperationsWebTests.OperationsCockpitLinksBothExactWorkspaces`
(`tests/Pegasus.IntegrationTests/OperationsWebTests.cs:26`) asserts
`href="/Operations"` is present in the rendered `/` response.

### Header, freshness and Refresh

Tier: **build/test** (code read plus the CI-green browser walk).

`Index.cshtml:18-25` replaces the old header with the design-system shape,
and the header's action slot is the shared partial, not a new control:

```
<header class="page-header">
    <div class="page-title">
        <h1>Operations</h1>
    </div>
    <div class="page-actions">
        <partial name="Shared/_FreshnessBanner" model="Model.LoadedAtUtc" />
    </div>
</header>
```

`grep -c "<h1>"` on the page returns 1, and `_Layout.cshtml` renders no
`<h1>` of its own and exactly one `<main id="main-content">`
(`_Layout.cshtml:160`) — so the contract's one-h1/one-main rule holds.
The Refresh control is not inert: `_FreshnessBanner.cshtml:48-63` is a
`<form method="get">` whose submit re-enters `OnGetAsync`.

### Attention required — retryable external work

Tier: **build/test**, with the production handler proven.

Markup: `Index.cshtml:99-140`. Rows post to the page's own handler:

```
Index.cshtml:124   <form method="post" asp-page-handler="RetryExternal">
Index.cshtml:130       <span>Retry this work</span>
```

Handler: `Index.cshtml.cs:84` `OnPostRetryExternalAsync(Guid workItemId,`
`int expectedAttemptCount, string operationKey, …)`, which calls
`retryExternalWork.ExecuteAsync(new(workItemId, expectedAttemptCount,`
`actor, operationKey), …)` at `Index.cshtml.cs:102`. The class carries
`[ValidateAntiForgeryToken]` (`Index.cshtml.cs:15`).

The panel's heading is pinned by
`OperationsPageIsStaffWorkspaceWithNoReceiptLedgerOrBoxSurface`
(`OperationsWebTests.cs:39`, assertion at line 49), and the retry
round-trip through this handler is proven end to end by
`ComposedServiceHealthRenamesInternalVocabularyAndRetriesThroughTheCanonicalCommand`
(`OperationsWebTests.cs:69`), which posts to
`/Operations?handler=RetryExternal`, asserts the PRG redirect, and asserts
the recorded `RetryExternalWorkCommand` carries the expected work id,
attempt count and `ActorKind.Staff`.

The empty state is `div.empty` with `No retryable external work`
(`Index.cshtml:105-107`) — a label, not explanatory prose.

### Active upload links — withdraw

Tier: **build/test**, with the production handler proven.

Markup: `Index.cshtml:142-203`. The state chip now reads through the
shared label map (`Index.cshtml:175`), and the withdraw control is a
`details` / `row-confirm` reason form posting to `RevokeLink`
(`Index.cshtml:184-193`), preserving a rejected reason via
`Model.PreservedRequestId == item.Id ? Model.PreservedReason : null`
(`Index.cshtml:191`).

Handler: `Index.cshtml.cs:125` `OnPostRevokeLinkAsync`, which acquires an
edit lease (`Index.cshtml.cs:149`), calls
`revokeRequestUploadLink.ExecuteAsync` (`Index.cshtml.cs:167`) and
releases the lease on every path (`Index.cshtml.cs:181`, `:185`).
`OperationsWithdrawalUsesTheCanonicalAntiforgeryAndLeaseGuardedCommand`
(`OperationsWebTests.cs:105`) asserts the recorded
`RevokeRequestUploadLinkCommand` carries the case id, request id, expected
case version, the lease token and the operator's reason.

The empty state is `div.empty` with `No active upload links`
(`Index.cshtml:148-150`).

### Service health table

Tier: **registration** for the production wiring; **build/test** for the
rendering, and that test composes the query by hand rather than through the
production graph. This is *not* deployed-and-exercised evidence.

Markup: `Index.cshtml:46-97`, rendered only when the snapshot exists:

```
Index.cshtml:46   @if (serviceHealth is not null)
```

Page model: the dependency is optional, so an uncomposed deployment renders
the section absent rather than failing —

```
Index.cshtml.cs:24   GetServiceHealth? getServiceHealth = null) : StaffPageModel
Index.cshtml.cs:76   if (getServiceHealth is not null)
Index.cshtml.cs:78       ServiceHealth = await getServiceHealth.ExecuteAsync(actor, cancellationToken);
```

Production registration chain, found with `grep -rn "GetServiceHealth"`:

- `src/Pegasus.Web/Mcp/AutomationMcpExtensions.cs:34` —
  `services.AddScoped<Pegasus.Core.Operations.GetServiceHealth>();`
- `src/Pegasus.Web/Program.cs:682-685` — that extension is called only
  when `automationMcpOptions is not null`
- `src/Pegasus.Web/Mcp/AutomationMcp.cs:12` — the gate is
  `public const string FeatureFlag = "Features:AutomationMcp";`
- `infra/modules/platform.bicep:467` — the deployed web app sets
  `{ name: 'Features__AutomationMcp', value: 'true' }`

So the composed path exists in the shipped artifact and the deployed
configuration turns it on. What has **not** been done here is opening the
deployed `/Operations` and reading the table — no tier-3 claim is made.

Rendering is proven at tier 2 by
`ComposedServiceHealthRenamesInternalVocabularyAndRetriesThroughTheCanonicalCommand`
(`OperationsWebTests.cs:69`), which asserts `Service health`,
`Receiving dispatch`, `Automation clients` and `Vehicle lookup` render, and
asserts the two internal names never reach the operator. Note the caveat at
`OperationsWebTests.cs:177-193`: that test hand-composes
`new GetServiceHealth(new NoMailboxPolls(), new NoServiceHealthFacts(), …)`
over the recording store. The production DI graph for this query is
registered but exercised by no test in this diff.

The row Retry reuses the same handler and `IndexModel.NewOperationKey()`
(`Index.cshtml:80-88`); there is no `View` control, because none has a
handler.

### Labels live in one place

Tier: **build/test**.

`src/Pegasus.Web/Presentation/OperatorLabels.cs` gained five maps and no
second copy:

```
:505  RequestOperationState(RequestOperationState state)
:525  ServiceHealthAreaName(ServiceHealthArea area)
:537  ServiceHealthStateName(ServiceHealthState state)
:549  ServiceHealthDependencyName(ServiceHealthDependency dependency)
:571  ServiceHealthServiceName(string? service)
```

The page model's former eight-arm `StateLabel` switch was deleted and is
now a one-line forwarder to the shared map:

```
Index.cshtml.cs:189-190
    public static string StateLabel(RequestOperationState state) =>
        Presentation.OperatorLabels.RequestOperationState(state);
```

The two banned Core service names are renamed at the Web boundary and only
there — `ServiceHealthPolicy.IntakeDispatchService` (`"Intake dispatch"`,
`src/Pegasus.Core/Operations/ServiceHealth.cs:139`) renders "Receiving
dispatch", and `ServiceHealthPolicy.AutomationService`
(`"Automation ingress"`, `ServiceHealth.cs:143`) renders "Automation
clients". Core was not edited.

Tone keys: `_StatusChip.cshtml:90-92` adds `"running" => "blue"`,
`"review required" => "amber"`, `"active" => "navy"`.

Every class and icon the new markup names resolves in the shipped assets:
`.notice--warning`, `.section-gap`, `.table-wrap`, `.no-border`,
`.btn--small`, `.empty`, `.panel-head`, `.row-confirm` and `.tabular` all
match in `src/Pegasus.Web/wwwroot/css/site.css`, and `#icon-info`,
`#icon-alert-triangle`, `#icon-refresh-cw` and `#icon-x` are all defined in
`src/Pegasus.Web/Pages/Shared/_LucideSprite.cshtml`.

### The AI placeholder is gone

Tier: **build/test**.

`grep -rn "AI operations" --include=*.cshtml src/` returns nothing. The
removal is pinned negatively by `OperationsWebTests.cs:51-54`:

```
// The AI Job List is PLAT-049's to add; until it is composed it is
// absent, not announced by a placeholder section.
Assert.DoesNotContain("AI operations", html, StringComparison.Ordinal);
```

### Build and test tiers

Tier: **build/test**, cited from the canonical gate evidence for merged
`dev` at `b92cb9a7` (not re-run here):

```
dotnet restore ./Pegasus.slnx --locked-mode                 -> exit 0
dotnet build ./Pegasus.slnx --configuration Release --no-restore
  -> Build succeeded. 0 Warning(s), 0 Error(s).
dotnet test … --filter 'Category!=Corpus&Category!=Browser'
  -> Pegasus.IntegrationTests  Failed: 0, Passed: 1022, Skipped: 2
```

`OperationsWebTests` is `[Trait("Category", "SqlServer")]`
(`OperationsWebTests.cs:19`) and carries neither excluded category, so its
five facts and theories are inside that 1022.

The browser tier was excluded from that local run, so it is cited from the
ticket's own CI instead. `gh pr checks 602` on run `33201040220` reports
every job green:

```
unit                     pass  3m56s
sql-integration (1..3)   pass
sql-integration-coverage pass
browser                  pass  15m48s
changes / documentation / local-development-scripts / reference-data  pass
infrastructure           skipping
```

That `browser` job covers `/Operations`: `AccessibilityTests.cs:18` lists
`"/Operations"` in `AuthenticatedRouteList` (present at `9868cf58`, checked
with `git show 9868cf58:…`), and both `OperatorJourneyTests`
(`Browser/OperatorJourneyTests.cs:90`, `:216`) and `LayoutIntegrityTests`
(`Browser/LayoutIntegrityTests.cs:20-26`) iterate that list.

## The ticket's own verification items

The ticket body carries one item; the plan's Acceptance section carries
six. Both are tabulated.

| Item | Status | Evidence |
| --- | --- | --- |
| Body: the approved redesign meets the resulting acceptance criteria | Partly proven | The six plan rows below; two of them are not fully proven |
| Header "Operations" (one H1; shell's single `main`) | Proven | `Index.cshtml:18-25`; one `<h1>` on the page, one `<main>` at `_Layout.cshtml:160`, no `<h1>` in the layout |
| Partial-data notice present iff `LimitReached` | Code-read only | `Index.cshtml:35-41` guards on `Model.Operations.LimitReached`; `grep -rn "Partial data"` across `tests/` finds **no** assertion, so no test pins it |
| Attention required + Active upload links restyled, handlers and pinned strings byte-compatible | Proven | `Index.cshtml:99-203`; handlers at `Index.cshtml.cs:84` and `:125`; round-trips asserted at `OperationsWebTests.cs:69` and `:105`; "Retry this work" at `Index.cshtml:130`; reason preserved at `Index.cshtml:191` |
| Service health renders from the merged PLAT-048 query when composed, absent when not; banned words absent | Proven at tiers 1–2 only | Registration chain `AutomationMcpExtensions.cs:34` → `Program.cs:684` → `AutomationMcp.cs:12` → `platform.bicep:467`; absence asserted at `OperationsWebTests.cs:57`; presence and renames at `:78-84`. The **production** DI graph for this query is never exercised — the test hand-composes it (`OperationsWebTests.cs:182-192`) |
| AI placeholder gone; EVA handoffs not fabricated | Proven | `grep` finds no "AI operations" in `src/`; `OperationsWebTests.cs:53`. No EVA handoffs panel exists anywhere under `Pages/Operations/` |
| `OperatorJourneyTests` surfaces untouched semantically (no test edit) | Proven | `git diff --name-only 9868cf58^1 9868cf58` contains no `Browser/OperatorJourneyTests.cs`; the `browser` CI job passed on PR #602 |

The checklist's fourteen ticked items were all corroborated except one
wording detail: "Page model: … dead StateLabel removed" is more precisely
*the dead map moved* — `IndexModel.StateLabel` survives at
`Index.cshtml.cs:189` as a two-line forwarder to the shared map. The "one
list per concept" rule holds (there is only one table), so this is recorded
as wording drift, not a defect.

## Outstanding

- **The layout walk at 1580 / 1100 / 760 is only partly proven.**
  `LayoutIntegrityTests` does cover `/Operations` at all three widths and
  passed in PR #602's `browser` job — but `BrowserTestSupport` sets no
  `Features:AutomationMcp`, so that walk rendered the page **without** the
  Service health table. The new table's behaviour at 1100 and 760 is
  unproven. Owned by **UIIMP-010**.
- **The Test UI snapshot corpus does not depict the shipped page.**
  `docs/design/test-ui/pages/operations--default.html` was last written by
  `35292cff` (2026-08-26), which predates this merge; it still contains
  "AI operations" and contains no `page-header`, no `Service health` and
  none of the new empty states. No CI job references `test-ui` on `dev`
  today, so nothing is red — regeneration is the merging branch's job under
  the EPIC-011 decisions, and the gate itself arrives with **UIIMP-005**
  (PR #609), which merges last among the UI lanes.
- **No tier-3 evidence exists for this ticket.** Nothing in this proof
  opened the deployed `/Operations`. The Service health table is registered
  and configured-on in `platform.bicep`, which is a registration claim, not
  a deployed-and-exercised one.
- **The partial-data notice has no test.** It is guarded correctly in the
  markup, but nothing pins the string or the condition.
- **Panels from EPIC-011 §1.11 that PLAT-023 did not ship**, all of them
  deliberate and all now owned by **PLAT-049** ("Operations: AI Job List,
  Service health and Send Unidentified to AI", currently in Review on PR
  [#617](https://github.com/collisionengineers/pegasus/pull/617)):
  - **AI Job List** panel and its `Send Unidentified to AI` action —
    removed here as a placeholder rather than shipped inert; `grep` confirms
    neither string exists under `src/Pegasus.Web/Pages/Operations/`.
  - **EVA handoffs** panel (Case, Route, Engineer, State, Result) — never
    shipped by this ticket; no listing query exists in Core, so it was not
    fabricated. `grep -rni "EVA handoff"` finds it only on the Case
    workspace (`Pages/Cases/Details.cshtml:653`) and the EVA send page
    (`Pages/Cases/Eva/Send.cshtml:11`), never on Operations.
  - The Service health **View** control, and the **Item** and **Recipient**
    columns — omitted for want of a handler and of projection data
    respectively, as recorded in the post-implementation report.

None of these contradicts what PLAT-023 claims to have shipped; the ticket's
post-implementation report names each one as a deliberate divergence.

## Scope of this proof

Written against merged `dev` at `b92cb9a7` per decision D15. `main` has not
been promoted; the exact-SHA `dev` → `main` promotion happens at wave 5.
