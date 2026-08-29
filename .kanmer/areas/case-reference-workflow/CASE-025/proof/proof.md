# Proof — CASE-025: Port the Cases queues page (/Cases) with workflow rail groups and filters

## What was verified, and where

Verified on merged `dev` at `b92cb9a7` (`b92cb9a7b8bf7727b452aa397d9df04084da1270`).
CASE-025 arrived through PR
[#596](https://github.com/collisionengineers/pegasus/pull/596), merged to `dev`
at `2026-08-28T17:11:52Z` as merge commit `213fc479`, which
`git merge-base --is-ancestor 213fc479 b92cb9a7` confirms is an ancestor of the
proven head. The branch was `task/case-025-cases-queues`, carrying six commits
(`95f69958`, `027cf806`, `4f5f9574`, `c56b5d5b`, `cffdce63`, `5c685460`) and
touching ten files. Between `213fc479` and `b92cb9a7` the two production files
`Pages/Cases/Index.cshtml` and `Pages/Cases/Index.cshtml.cs` are **byte-identical**
(`git diff 213fc479 b92cb9a7 --` on both paths returns nothing); the only later
change under this ticket's ownership is UIIMP-008's retarget of one cross-page
assertion in `TriageQueuesWebTests.cs` from the retired dashboard tile to the Work
Centre metric, which kept the assertion at equal strength. Everything below is
read from `b92cb9a7`.

## Evidence

### The page is the three-pane queue of §1.4

Tier: build/test (the markup renders under integration tests); not deployed.

`src/Pegasus.Web/Pages/Cases/Index.cshtml:72` opens the layout and three `.pane`
children follow — the rail at `:73`, the row list at `:110`, the quick detail at
`:161`:

```
<section class="pane-layout pane-layout--3 queue-layout">
```

The design vocabulary it uses is PLAT-029's, not invented here: every class the
page keys is present in `src/Pegasus.Web/wwwroot/css/site.css` — `queue-layout`,
`pane-layout--3`, `queue-group-label`, `queue-group-divider`, `queue-exception`,
`scope-list`, `scope-button`, `scope-visual-icon`, `workflow-stepper--compact`
and `blocker-list` all match at least once. The rail's pressed state is styled at
`site.css:335` and `site.css:650` (`.scope-button[aria-pressed="true"]`), so the
selected scope is a real visual state and not an unstyled attribute.

### The rail groups are Workflow / Pre-Case work / Exceptions

Tier: build/test for Workflow and Exceptions; **code-only** for the Pre-Case work
label (no test pins that string).

`src/Pegasus.Web/Pages/Cases/Index.cshtml.cs:79-97` declares the three group
constants and the seven rail entries:

```
public const string WorkflowGroup = "Workflow";
public const string PreCaseGroup = "Pre-Case work";
public const string ExceptionsGroup = "Exceptions";

public static readonly IReadOnlyList<Tab> Tabs =
[
    new("not_ready", OperatorLabels.CaseStage(CaseLifecycleState.NotReady), WorkflowGroup, "icon-clock"),
    new("review", OperatorLabels.CaseStage(CaseLifecycleState.Review), WorkflowGroup, "icon-check-circle"),
    new("with_engineer", OperatorLabels.CaseStage(CaseLifecycleState.ReportPreparation), WorkflowGroup, "icon-user"),
    new("complete", OperatorLabels.CaseStage(CaseLifecycleState.PostReportComplete), WorkflowGroup, "icon-check"),
    new("triage", "Triage", PreCaseGroup, "icon-file-text"),
    new("held", OperatorLabels.CaseStage(CaseLifecycleState.Held), ExceptionsGroup, "icon-pause", IsException: true),
    new("unidentified", "Unidentified", ExceptionsGroup, "icon-alert-triangle", IsException: true)
];
```

`Index.cshtml:87` renders them with `Tabs.GroupBy(tab => tab.Group)`, emitting a
`queue-group-label` per group, a `queue-group-divider` between groups, and
`queue-exception` on the two exception scopes (`:91-97`). All eight sprite symbols
the rail and stepper reference (`icon-clock`, `icon-check-circle`, `icon-user`,
`icon-check`, `icon-file-text`, `icon-pause`, `icon-alert-triangle`,
`icon-arrow-right`) are defined in
`src/Pegasus.Web/Pages/Shared/_LucideSprite.cshtml` — §1.15's undefined-icon
defect is not reproduced.

The rendered group headings "Workflow" and "Exceptions" are pinned by
`tests/Pegasus.IntegrationTests/TriageQueuesWebTests.cs:351-353`, together with
the pane heading "Case workflow". **"Pre-Case work" is asserted by no test**; it
is proven only by the constant and the `GroupBy` that renders it.

The four D3 display labels come from one map,
`src/Pegasus.Web/Presentation/OperatorLabels.cs:134-146`, where
`ReportPreparation or PostReport => "With Engineer"` and
`PostReportComplete => "Complete"` — no second label table in the markup.

### The production caller

Tier: registration (a rendered link in the shell that every authenticated page
carries), plus build/test for the deep links.

Strongest: `src/Pegasus.Web/Pages/Shared/_Layout.cshtml:82` — the app rail's
permanent "Cases" nav link:

```
<a class="nav-link" asp-page="/Cases/Index" aria-current="@CurrentWhen("/Cases", "/Triage", "/Unidentified", "/ImageIntake", "/Intake")">
```

Four further production call sites reach the same page:

| Caller | file:line | Target |
| --- | --- | --- |
| Work Centre metric strip (5 tiles) | `src/Pegasus.Web/Pages/Index.cshtml:37,41,45,49,53` | `asp-page="/Cases/Index"` + `asp-route-tab` |
| Command palette | `src/Pegasus.Web/Pages/Shared/_ShellDialogs.cshtml:130` | `data-route="/Cases"` |
| Retired `/Unidentified` list | `src/Pegasus.Web/Pages/Unidentified/Index.cshtml.cs:18` | 301 to `/Cases?tab=unidentified` |
| Retired `/Triage` queues | `src/Pegasus.Web/Pages/Triage/Index.cshtml.cs:21` | 301 to `/Cases?tab=<queue>` |

The `/Unidentified` redirect is pinned by
`TriageQueuesWebTests.UnidentifiedRouteRedirectsPermanentlyToTheQueuesTab`.

### Every rail count is a queried figure

Tier: build/test.

`Index.cshtml:103` renders `@Model.Count(tab)` and nothing else. `Count` is a pure
projection of three awaited query results — `Index.cshtml.cs:163-173` — with no
literal or placeholder in it. The three reads are issued together at
`Index.cshtml.cs:309-315`:

```
var stageCountsTask = _dashboardQueries.GetCaseStageCountsAsync(cancellationToken);
var triageTask = _listTriage.ExecuteAsync(new(actor, State: null, Page: 1, PageSize: 1), cancellationToken);
var openUnidentifiedTask = _unidentifiedStore.ListQueueAsync(null, cancellationToken);
```

Each resolves to real database work:

- **Not ready / Review / With Engineer / Complete / Held** —
  `src/Pegasus.Infrastructure/Persistence/EfDashboardQueries.cs:23-73`: a
  `GroupBy(workflow => workflow.State).Select(… group.Count())` over
  `CaseWorkflows`, plus a second `ImageIntakes.CountAsync` for the
  image-initiated Not-ready origin, returning
  `For(reportPreparation) + For(postReport)` for With Engineer — the D3 grouping
  in the query, matching the display map.
- **Triage** — `src/Pegasus.Core/Triage/TriageQueryUseCases.cs:70-75` returns
  `matches.Count` from `EfTriageStore.ListAsync`
  (`src/Pegasus.Infrastructure/Persistence/EfTriageStore.cs:458-482`), a real
  `context.Triage` read.
- **Unidentified** — `src/Pegasus.Infrastructure/Persistence/EfUnidentifiedStore.cs`
  `ListQueueAsync`, whose query is `where item.State == openState`.

`TriageQueuesWebTests.NotReadyRailCountMatchesRowsAcrossBothOrigins:105-152` pins
the figure end to end: it seeds one instruction-initiated case and one
image-initiated intake, then asserts the rail's Not-ready `scope-button` figure
is `2`, that exactly `2` `row-button` elements rendered, and that the Work Centre
metric for `not_ready` reports the identical number.

### D14 — Blocked intake rows are listed, uncounted, with their own chip

Tier: build/test.

The count and the rows come from different queries and only the rows carry the
blocked receipts. `Index.cshtml.cs:315` sets
`UnidentifiedCount = openUnidentifiedTask.Result.Count` — open Unidentified items
only, per the `where item.State == openState` clause above. The blocked rows are
fetched separately and concatenated into `Rows` alone, at
`Index.cshtml.cs:438-449`:

```
var blocked = await _listIntake.ExecuteAsync(
    new(actor, IntakeDecision.BlockedIntake, Page: 1, PageSize: MergedPageSize),
    cancellationToken);
return openRows.Select(UnidentifiedRow)
    .Concat(blocked.Items.Select(BlockedRow))
    .ToArray();
```

`Count(tab)` never reads `Rows`, so a blocked receipt cannot reach any rail
figure. The distinct chip is the literal at `Index.cshtml.cs:618`, `"Blocked
intake"`, toned red by the one chip owner
(`src/Pegasus.Web/Pages/Shared/_StatusChip.cshtml:48`,
`"blocked intake" or "blocked" => "red"`), against Unidentified's amber at
`:47` — the two meanings are visually distinct as well as arithmetically
distinct.

`TriageQueuesWebTests.UnidentifiedTabListsBlockedIntakeRowsUncounted:268-316`
proves it directly: it stores one `IntakeDecision.BlockedIntake` receipt and zero
Unidentified items, then asserts the row is listed (`blocked-file.msg`), that the
chip text `Blocked intake` is present, that the row links to
`/Received/{id:D}`, and — the load-bearing assertion — that the Unidentified
`scope-button` figure reads `0`, not `1`.

D14's other half, the Work Centre "Blocked" metric landing on this scope, is
shipped by UIIMP-008 at `src/Pegasus.Web/Pages/Index.cshtml:53`
(`data-value="blocked" asp-page="/Cases/Index" asp-route-tab="unidentified"`).

### The Principal and Missing filters

Tier: build/test.

`Index.cshtml:40-69` renders one GET `filter-bar` with `data-auto-submit`: the
Principal select unconditionally on case-listing scopes
(`IndexModel.ListsCases`, `Index.cshtml.cs:153-154`), the Missing select only
when `Model.ShowingNotReady` (`Index.cshtml:52`), a `noscript` Apply button, and
a Clear link rendered only while a filter is active (`Index.cshtml:65`) — no
inert control is drawn. The Missing options are exclusive, applied at
`Index.cshtml.cs:399-405`:

```
"instructions" => item.InstructionComplete == false && item.ImagesComplete == true,
"images" => item.InstructionComplete == true && item.ImagesComplete == false,
"both" => item.InstructionComplete == false && item.ImagesComplete == false,
```

`TriageQueuesWebTests.NotReadyMissingFilterReturnsOnlyTheMatchingRows:36-104`
seeds one case per completeness combination and pins all four options.

### The quick-detail pane

Tier: build/test.

`Index.cshtml:161-253`. A Case renders the compact stepper
(`workflow-stepper--compact`, `:174`, over `IndexModel.Steps`,
`Index.cshtml.cs:232-238`), a `workflow-exception` badge for Held whose text is
`OperatorLabels.CaseStage(CaseLifecycleState.Held)` (`:190`), an Outstanding
requirements `blocker-list` built from the case's recorded completeness facts
(`Index.cshtml.cs:464-467`), a Current work panel of Due / Engineer / Next action
(`Index.cshtml.cs:469-497`) and "Open full Case". Every other row kind renders a
`definition-list` plus its own open button (`Index.cshtml:234-248`).

### Build and test

Tier: build/test. Cited from the orchestrator's canonical gate evidence for
merged `dev` at `b92cb9a7`; not re-run here.

```
dotnet restore ./Pegasus.slnx --locked-mode                        -> exit 0
dotnet build ./Pegasus.slnx --configuration Release --no-restore   -> 0 Warning(s), 0 Error(s)
dotnet test  ./Pegasus.slnx --configuration Release --no-build \
  --filter 'Category!=Corpus&Category!=Browser'
    Pegasus.ArchitectureTests   Failed: 0, Passed:  100, Skipped: 0
    Pegasus.Core.Tests          Failed: 0, Passed: 1133, Skipped: 0
    Pegasus.IntegrationTests    Failed: 0, Passed: 1022, Skipped: 2
```

This ticket's suite is inside that run: `TriageQueuesWebTests` is declared
`[Trait("Category", "SqlServer")]` (`TriageQueuesWebTests.cs:22`), so neither
excluded category applies, and it lives in `Pegasus.IntegrationTests`, which
reported zero failures. The run's two skips are named in the gate evidence and
neither belongs to this class. The merged file carries **eight** `[Fact]`
methods:

| Test | Line |
| --- | --- |
| `NotReadyMissingFilterReturnsOnlyTheMatchingRows` | 37 |
| `NotReadyRailCountMatchesRowsAcrossBothOrigins` | 106 |
| `NotReadyImageRowRendersRetainedImageCountAndChaseState` | 163 |
| `UnidentifiedRouteRedirectsPermanentlyToTheQueuesTab` | 186 |
| `UnidentifiedTabRendersNoBannedVocabularyOrRawIdentifiers` | 198 |
| `UnidentifiedTabListsBlockedIntakeRowsUncounted` | 269 |
| `NotReadyRendersOneMergedRowListAcrossOrigins` | 324 |
| `NotReadyRowsRenderNewestReceivedFirst` | 365 |

PR #596's own CI (run `33182676365`, head `5c685460`) was green on every job:
`unit`, `sql-integration` (3 shards), `sql-integration-coverage`, `browser`,
`changes`, `documentation`, `local-development-scripts`, `reference-data`;
`infrastructure` skipped.

## The ticket's own verification items

| Item | Status | Evidence |
| --- | --- | --- |
| Every tab count is a queried figure | Proven | `Index.cshtml:103` renders only `Model.Count(tab)`; `Index.cshtml.cs:163-173` maps each entry to one of three awaited queries — `EfDashboardQueries.cs:23-73`, `TriageQueryUseCases.cs:70-75` → `EfTriageStore.cs:458`, `EfUnidentifiedStore.ListQueueAsync`. Pinned end to end by `TriageQueuesWebTests.cs:106-152`. |
| Unidentified count excludes Blocked intake | Proven | `Index.cshtml.cs:315` counts open Unidentified only; blocked receipts enter `Rows` alone at `:438-449`. `TriageQueuesWebTests.cs:269-316` asserts the rail figure is `0` with one blocked row listed. |
| No clipped text/overflow at 1580/1100/760 | **Partially proven — see Outstanding** | `/Cases`, `/Cases?tab=triage` and `/Cases?tab=unidentified` are enrolled in `Browser/LayoutIntegrityTests.cs:17-29` × `Browser/AccessibilityTests.cs:20-25` at exactly those widths, and PR #596's `browser` job passed (run `33182676365`, job `98887783905`, 18m40s) on branch head `5c685460`. That job was **not** re-run at `b92cb9a7`, and four of the seven rail scopes are not enrolled at any width. |

The ticket's checklist (10/10) records lane-scoped steps — recovery audit, page
repairs, markup, test rewrite, Release build, simplification pass, PR — and its
own verification note says the three body items "are proven by the wave loop's
browser pass, not here". Two of the three are now proven above; the third is
carried below.

## Outstanding

1. **The 1580/1100/760 walk is not proven at `b92cb9a7`.** The gate run excluded
   `Category=Browser` by design, so the only exit code behind this claim is PR
   #596's own `browser` job at branch head `5c685460`. Six merges landed on `dev`
   afterwards; the two `/Cases` production files are byte-identical across them,
   but the shared shell and `site.css` this page lays out inside are not this
   ticket's files and did change. **Owner: UIIMP-010**, which owns the browser
   walk over the final markup.
2. **Four rail scopes have no viewport coverage at any width.**
   `AccessibilityTests.AuthenticatedRouteList` enrols `/Cases`,
   `/Cases?tab=triage` and `/Cases?tab=unidentified` only; `?tab=review`,
   `?tab=with_engineer`, `?tab=complete` and `?tab=held` are walked by nothing.
   **Owner: UIIMP-010.**
3. **The selected row has no visual highlight on this page.** Review round 1
   moved the rows from `aria-selected` to `aria-current="true"`
   (`Index.cshtml:126`) because `aria-selected` is illegal on a link, and the
   plan disclosed the consequence rather than working around it. Confirmed on
   `dev`: `site.css:270` and `site.css:652` still key
   `.row-button[aria-selected="true"]`, and `grep aria-current site.css` returns
   ten selectors, none of them `.row-button`. Selection is still announced by
   `aria-current` and named by the quick-detail heading, but the background and
   inset bar do not apply. The plan calls this "a PLAT-029/wave-5 follow-up, not
   this lane's file"; **no ticket ID has been recorded for it**, and one should
   be before wave 5 deletes the legacy CSS block.
4. **"Pre-Case work" is unpinned.** The other two group labels are asserted at
   `TriageQueuesWebTests.cs:352-353`; this one is proven by code alone.
5. **The Test UI snapshot corpus is stale for this route and is not evidence.**
   `docs/design/test-ui/pages/cases--default.html` at `b92cb9a7` still renders the
   pre-PLAT-029 shell (a rail with separate "Queues" and "Cases" links) and
   contains none of `queue-layout`, `pane-layout--3`, `scope-button` or
   `Quick detail`. That is the epic's own merge ordering working as decided —
   snapshots regenerate once per merge on the merging branch, and UIIMP-005
   merges its snapshot CI gate last — but no claim in this proof rests on it.

Two disclosures from the post-implementation report remain true on `dev` and are
recorded, not disputed: image rows carry a retained-image count but no custody
line (no persisted custody projection exists), and the Principal select is drawn
on case-listing scopes only.

## Findings against the ticket

Nothing shipped contradicts what the ticket claims. Three observations, none
blocking:

- **The ticket's "Owns" list overstates by one file.** `Pages/Unidentified/Index.cshtml(.cs)`
  is listed as owned, but CASE-025's merge did not touch it: `git log` shows its
  last change is PLAT-029's `71277763`, which already reduced it to the
  `RedirectPermanent("/Cases?tab=unidentified")` this ticket needed. The required
  behaviour is present and tested; only the ownership claim was wrong.
- **The post-implementation report says "7 tests"; the file has 8.** Miscount in
  the report, verified against `c56b5d5b` (already 8 there) and `b92cb9a7`. The
  code is unaffected.
- **The middle pane's "N items" and the rail figure differ on the Unidentified
  scope.** `Index.cshtml:114` renders `Model.Rows.Count`, which for that scope
  includes the uncounted blocked rows, so `UnidentifiedTabListsBlockedIntakeRowsUncounted`'s
  fixture would show "Unidentified / 1 item" beside a rail reading "Unidentified
  0". This is not a D14 breach — D14 governs "the Unidentified count (tab and
  rail sum)", which is the rail figure and is correct — and the two quantities
  differ by design on every scope once a filter or page is applied (rail is
  estate-wide, the pane head is this list). It is recorded because no test pins
  the pane-head figure and an operator sees both numbers at once.

## Scope of this proof

Written against merged `dev` at `b92cb9a7` per decision D15. `main` has not been
promoted; the exact-SHA `dev` → `main` promotion happens at wave 5.
