# Proof — ENG-025: Port the Assessment workspace shell (assessment-v3, evidence rail, D11 access)

## What was verified, and where

Verified on merged `dev` at `b92cb9a7`, in the primary checkout
`C:/Users/PC/Documents/GitHub/pegasus`, on 2026-08-29. ENG-025 reached `dev`
as merge commit `21b35398` ("Merge pull request #616 from
collisionengineers/task/eng-025-assessment-shell", 2026-08-29 10:14:50 +0100,
first parent `c87e8d5d`, second parent `7cb9acbc`), from PR
[#616](https://github.com/collisionengineers/pegasus/pull/616) (`MERGED`,
base `dev`, head `task/eng-025-assessment-shell`). All eleven recorded
commits are reachable from `b92cb9a7` — `git merge-base --is-ancestor <sha>
b92cb9a7` returns 0 for `7b919b69`, `36655f26`, `065c18ef`, `8315b2f7`,
`bc16d8fa`, `93766579`, `c9e90360`, `d5dd2c3f`, `22dd1870`, `5d3b658c` and
`7cb9acbc`. The merge touched 14 files (+1406 / −2296), rewriting 1795 lines
of `Index.cshtml` and 639 of `Index.cshtml.cs`.

Build and test evidence is the orchestrator's canonical gate run for merged
`dev`, cited below rather than re-run.

## Evidence

### D11 access: With Engineer or onwards, read-only once Complete

Tier: build/test (the Core rule and its tests), with five named production
callers.

Core owns the rule once, at
`src/Pegasus.Core/Assessment/AssessmentWorkspace.cs:43-60`:

```csharp
public static bool CanOpen(AssessmentAccessState access)
{
    ArgumentNullException.ThrowIfNull(access);
    return access.State
            is CaseLifecycleState.ReportPreparation
                or CaseLifecycleState.PostReport
                or CaseLifecycleState.PostReportComplete
        && access.LatestExportVersion is { } exportedVersion
        && exportedVersion >= access.LatestReviewVersion;
}

public static bool IsReadOnly(AssessmentAccessState access)
{
    ArgumentNullException.ThrowIfNull(access);
    return access.State == CaseLifecycleState.PostReportComplete;
}
```

Under EPIC-011 D3, `ReportPreparation` + `PostReport` display as "With
Engineer" and `PostReportComplete` as "Complete", so the opening set is
exactly D11's "With Engineer or onwards" and the read-only state is exactly
"Complete". `Review` is not in the set.

Proved by eleven theory rows at
`tests/Pegasus.Core.Tests/Assessment/AssessmentPolicyTests.cs:15-39`, which
include `[InlineData(CaseLifecycleState.Review, 4L, 4L, false)]` beside
`NotReady`, `Held` and `CreatedInError` false rows, `ReportPreparation` with
a null export and with a stale export both false, and `ReportPreparation` /
`PostReport` / `PostReportComplete` true with a current export. Read-only is
proved by three rows at `:46-57` — `ReportPreparation` false, `PostReport`
false, `PostReportComplete` true.

Production callers of `CanOpen` on merged `dev`
(`git grep -n CanOpen -- 'src/**'`):

| Call site | What it gates |
| --- | --- |
| `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml.cs:285` | `OnGetAsync` — renders the gate surface instead of the workspace |
| `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml.cs:829` | `CanAccessAsync`, called by every mutating handler on the page |
| `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs:210-212` | the Case workspace's `CanOpenAssessment` |
| `src/Pegasus.Core/Reports/AssessmentReportProjection.cs:356` | the report-draft projection |
| `src/Pegasus.Infrastructure/Persistence/EfAssessmentWorkspaceSource.cs:52` | the workspace query itself |

The rendered production entry point is the Case workspace's "Open
Assessment" control, `src/Pegasus.Web/Pages/Cases/Details.cshtml:269-273`:

```cshtml
<span class="gated" data-condition="@(Model.CanOpenAssessment ? null : "Available after the current Review export")">
    <a class="btn btn--dark @(Model.CanOpenAssessment ? string.Empty : "is-disabled")"
       asp-page="@(Model.CanOpenAssessment ? "/Cases/Assessment/Index" : null)"
       asp-route-id="@(Model.CanOpenAssessment ? summary.CaseId : null)"
       aria-disabled="@(Model.CanOpenAssessment ? "false" : "true")">Open Assessment</a>
```

The route it reaches is declared on line 1 of the ported page:
`@page "/Cases/{id:guid}/Assessment"`. Composition is registered at
`src/Pegasus.Infrastructure/DependencyInjection.cs:328-329`
(`IAssessmentAccessSource` → `EfAssessmentAccessSource`,
`IGetAssessmentAccess` → `GetAssessmentAccess`).

When access is refused the page does not 404 — it renders the contract's
gate (`Index.cshtml:21-42`): eyebrow "REF · reg", h1 "Assessment
unavailable", the warning "A current Review-cycle EVA export is required
before the assessment opens.", and Back to Case. `NotFound()` is returned
only for an unknown case (`Index.cshtml.cs:282`, `:291`, `:301`).

### Read-only once Complete is enforced below the view, not only in it

Tier: build/test.

The view withholds every lease control when read-only (`Index.cshtml:157`,
`@if (!Model.CaseIsArchived && !Model.IsReadOnly)`), `ImportCondition`
returns "Read-only once Complete" (`Index.cshtml.cs:178-181`), and
`SendToClaudeCondition` adds the same sentence at `:799-802`. Those are
render-time gates. The page's POST handlers themselves check only
`CanAccessAsync` (= `CanOpen`), which is *true* in `PostReportComplete`, so
the render gate alone would not be sufficient. It does not have to be: both
write paths reachable from this page fail closed in the store.

```
git grep -n "RequireMutable" -- 'src/**'
  src/Pegasus.Infrastructure/Persistence/EfCaseAssessmentStore.cs:91
  src/Pegasus.Infrastructure/Persistence/EfRepairSpecificationStore.cs:549
```

`ArchivedCaseGuard.RequireMutable`
(`src/Pegasus.Infrastructure/Persistence/ArchivedCaseGuard.cs:19-23`) calls
`RequireOpenState`, which throws `CaseTerminalMutationException` when
`CaseLifecycleRules.IsTerminal(state)` — and that list, at
`src/Pegasus.Core/Lifecycle/CaseLifecycle.cs:393-398`, begins
`CaseLifecycleState.PostReportComplete`. So an assessment save or a repair
specification write on a Complete case is refused in the store regardless of
the page.

Recorded as a narrowing rather than a defect: `EfCaseWorkflowStore.ClaimAsync`
(`:114-168`) guards archived, expected version, held lease and the
`PerformCasework` right, but **not** the terminal state — the edit *lease* is
claimable on a Complete case even though every mutation behind it is refused.
The page never draws the control that would claim it.

### No inert control remains

Tier: build/test (browser, axe-clean) over a complete static inventory.

The interactive inventory of
`src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml` on merged `dev` is 13
`type="button"`, 8 `type="submit"`, 5 `<a>`, 0 `<select>`. Every one
resolves:

| Line(s) | Control | Resolves to |
| --- | --- | --- |
| 196, 218 | Import estimate, Send to Claude | `data-dialog-open` → `site.js:98` |
| 204, 226 | the same two when refused | `disabled aria-disabled="true"` inside `<span class="gated" data-condition=…>` |
| 211, 214 | Glass's, Audatex | D7 disabled seams (below) |
| 251 | Generate report draft, not ready | `disabled` + `data-condition="Not ready"` |
| 269 | Evidence rail toggle | `data-rail-toggle` → `site.js:1548` |
| 489 | Choose a file | `data-dropzone-browse` → `site.js:154` |
| 476, 498, 512, 545 | dialog dismiss | `data-dialog-close` → `site.js:115`, `:863` |

The eight submits sit in eight forms, and every handler they name exists on
the page model:

```
grep -oE 'asp-page-handler="[A-Za-z]+"' Index.cshtml | sort -u
  AcceptSpecification  ClaimLease  GenerateReportDraft  ImportEstimate
  PreviewReportDraft   ReleaseLease  SendToClaude
```

against `OnPostAcceptSpecificationAsync:640`, `OnPostClaimLeaseAsync:229`,
`OnPostGenerateReportDraftAsync:334`, `OnPostImportEstimateAsync:477`,
`OnGetPreviewReportDraftAsync:384`, `OnPostReleaseLeaseAsync:260` and
`OnPostSendToClaudeAsync:409` (plus `OnPostHeartbeatLeaseAsync:254`, driven
by the `Shared/_EditHeartbeat` partial). The five anchors are two
Back-to-Case links, the `PreviewReportDraft` GET handler link, and two
evidence-item download links.

The old machinery is gone, not hidden:

```
git grep -n "SaveDamage\|OnPostSendAsync\|OnPostReconcileAsync\|assessment-v2\|readiness-summary\|section-tab" \
  -- 'src/Pegasus.Web/Pages/Cases/Assessment/**'
  → no matches

git grep -n "SaveDamage" -- 'src/**'
  → no matches
```

CSP holds: `grep -n "<script\|<style\|onclick=\|onchange=\|onsubmit="` over
the view returns nothing, and the merge changed no `.css` or `.js` file.
`Suggestions.cshtml` carries no `@page` directive (`grep -n "^@page"` → no
match; its single "@page" hit is inside the opening `@* … *@` comment), so
no route activates for it.

Runtime confirmation:
`tests/Pegasus.IntegrationTests/Browser/AssessmentReadinessSummaryBrowserTests.cs:26`
loads `/Cases/{id}/Assessment`, asserts status 200, `h1` = "Assessment",
`.record-ribbon .ribbon-item` count 7, the `assessment-v3` Evidence and
Estimates pane heads, `.readiness-summary` count 0, `.estimate-tabs` count 0,
and closes with
`Assert.Empty(await support.FindAccessibilityViolationIdsAsync())`. PR #616's
`browser` CI job passed (17m58s, run 33242740186), as did `unit`, all three
`sql-integration` shards, `documentation`, `changes`,
`local-development-scripts` and `reference-data`.

### Glass's and Audatex are approved disabled seams per D7

Tier: code.

`src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml:210-215`:

```cshtml
<span class="gated" data-condition="@Model.EstimatingServiceCondition">
    <button type="button" class="btn" disabled aria-disabled="true">Glass's</button>
</span>
<span class="gated" data-condition="@Model.EstimatingServiceCondition">
    <button type="button" class="btn" disabled aria-disabled="true">Audatex</button>
</span>
```

Neither carries a form, an `href`, nor a `data-dialog-open` — they are drawn
disabled exactly as D7 requires ("Uncomposed integrations … render disabled
as drawn"), state their condition, and name no handler. The condition is a
single page-model property, `Index.cshtml.cs:193-194`
(`EstimatingServiceCondition => "Available once the estimating-service link
is agreed"`), read by both controls — one list per concept, and the fix for
the review's "hardcoded twice" finding.

D7's second half — "a disabled control is permitted only for a named,
ticketed integration seam" — is satisfied by name in the view comment
(`Index.cshtml:155`: "Glass's and Audatex are the D7 disabled seams
(EXT-09)") and by record: **EXT-09** is a registered capability in
`docs/capabilities.md:252`, canonical owner
`docs/frd/frd-06-vehicle-and-engineering-evidence.md`, whose entry states
that "rate-card and paint-materials formula authority remains an open
decision". The Glass's ingestion route is separately ticketed as **TICK-085**
("Complete Glass's repair-estimate import from a representative export",
status `backlog`), and the delivered Audatex *PDF* path is EXT-12/ENG-002 —
reached here by the live "Import estimate" control, which is why the disabled
seam is the direct estimating-service link, not the import.

### Send to Claude runs on the AUTO-011 job ledger

Tier: build/test.

`OnPostSendToClaudeAsync` (`Index.cshtml.cs:409`) calls
`createAiJob.ExecuteAsync(… AiJobKind.Estimate …)` at `:442-444`, injected as
`ICreateAiJob` at `:40`; the old `OnPostSendAsync` / `OnPostReconcileAsync`
panel-state machinery is gone. The outbound-payload guard the review found
deleted is back on merged `dev` at
`tests/Pegasus.IntegrationTests/SendToAiConnectorAdministrationTests.cs:95-97`:

```csharp
Assert.Contains("\"schema_version\":1", request.Body, StringComparison.Ordinal);
Assert.Contains("\"case_reference\":", request.Body, StringComparison.Ordinal);
Assert.DoesNotContain("claimant", request.Body, StringComparison.OrdinalIgnoreCase);
```

### Build and test gate

Tier: build/test. Cited from the orchestrator's canonical gate evidence for
merged `dev` at `b92cb9a7` (not re-run here):

```
dotnet restore ./Pegasus.slnx --locked-mode                       → exit 0
dotnet build ./Pegasus.slnx -c Release --no-restore
  → Build succeeded. 0 Warning(s), 0 Error(s).
dotnet test ./Pegasus.slnx -c Release --no-build
        --filter 'Category!=Corpus&Category!=Browser'
  → ArchitectureTests   Passed  100 / 100
  → Core.Tests          Passed 1133 / 1133
  → IntegrationTests    Passed 1022, Skipped 2 / 1024
```

The two skips are pre-existing and unrelated to this ticket. Category
`Browser` was excluded from that run and is covered instead by PR #616's
green `browser` job above.

## The ticket's own verification items

| Item | Status | Evidence |
| --- | --- | --- |
| No inert control remains | Proven | Full inventory above: 13 buttons / 8 submits / 5 anchors all resolve; `site.js:98,115,154,863,1548` carry every hook; removed-machinery greps empty; browser test axe-clean |
| Access matches FRD-11 (D11) | Proven | `AssessmentWorkspace.cs:43-60`; 14 theory rows at `AssessmentPolicyTests.cs:15-57`; five production callers; store-level terminal guard at `EfCaseAssessmentStore.cs:91` and `EfRepairSpecificationStore.cs:549` |
| No clipped text/overflow at 1580/1100/760 | **NOT proven** | The only browser test on this page runs at `width: 1920, height: 1080` (`AssessmentReadinessSummaryBrowserTests.cs:30`). `site.css` carries breakpoints at 1360/1180/1100/980/900/760 (`:751-799`) and 7→3→1 column ribbon rules (`:389`, `:768`, `:821`), but nothing walks them |

## Outstanding

1. **The three-width layout walk (1580/1100/760) is unproven** and is not
   claimed. **UIIMP-010** owns that walk; per the epic's D-decisions its
   tooling exists on `dev`
   (`tests/Pegasus.IntegrationTests/Browser/LayoutIntegrityTests.cs`). This is
   the ticket's one unticked checklist line, carried forward rather than
   ticked.

2. **The generated Test UI snapshot for this page is stale on merged `dev`.**
   `docs/design/test-ui/catalogue.json:261` declares
   `"source": "src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml"`, but
   `docs/design/test-ui/pages/case-assessment--default.html` still carries the
   pre-port render — `readiness-summary` (line 137), "Open in Glass's" /
   "Open in Audatex" (100, 106), an inline `<script>` (272), and
   `<button type="button" class="primary-action">Save vehicle</button>` (248),
   which is one of the inert controls this ticket removed. Its last commit is
   `35292cff`, well before this port. **This is not an ENG-025 defect**: the
   epic's D-decisions forbid a lane from regenerating snapshots in its own
   worktree and put regeneration "once per merge, on the merging branch only".
   The snapshot CI gate is not yet on `dev` — `.github/workflows/` holds only
   `ci.yml`, with no `test-ui` reference. Owned by the orchestrator's per-merge
   regeneration and by **UIIMP-005**, which merges last among the UI lanes.

3. **Impact location has no operator writer — a real, deferred regression.**
   Confirmed independently on merged `dev`:

   ```
   git grep -n "ISaveAssessment" -- 'src/**'
     src/Pegasus.Core/Assessment/AssessmentContracts.cs:296     (interface)
     src/Pegasus.Core/Assessment/AssessmentOperations.cs:24     (implementation)
     src/Pegasus.Infrastructure/DependencyInjection.cs:332      (registration)
     src/Pegasus.Web/Mcp/AssessmentMcpTools.cs:154              (MCP tool)

   git grep -n "ImpactLocation\|impactLocation" -- 'src/Pegasus.Web/**'
     → no matches
   ```

   `AssessmentReportRendering.cs:232` makes `ImpactLocation` a required report
   source, so a case worked purely through the operator UI cannot satisfy the
   report draft on that field. The one surviving writer is the MCP seam, and
   it is a genuine deployed caller rather than a bare registration: registered
   at `src/Pegasus.Web/Mcp/AutomationMcpExtensions.cs:120`
   (`.WithTools<AssessmentMcpTools>()`), composed at
   `src/Pegasus.Web/Program.cs:684` and mapped at `:1029` behind
   `automationMcpOptions`, and `docs/operations.md:122` records that gate as
   "**enabled in production by release 9** (ADR-0026) … live token/inventory/
   denial/history/kill-switch evidence recorded on 2026-08-18". **ENG-029**
   (backlog, EPIC-011 wave 4, linked from this ticket) owns restoring an
   operator editor for the report-required fields, and must first agree the
   contract extension, since §1.9 draws no home for them. The other twelve
   fields are not a capability loss: on `origin/dev` they sat outside any
   `<form>` behind `<button type="button">Save vehicle</button>` /
   `"Save incident and impact"`, i.e. the inert controls this ticket was
   ordered to delete.

4. **Cross-lane file share, disclosed not fixed.**
   `tests/Pegasus.IntegrationTests/AssessmentWorkspaceTestData.cs` is edited by
   this lane and by `origin/task/tick-058-provider-submission-api` (TICK-058,
   wave 3), against waves.md's whole-file ownership rule. The hunks are
   disjoint so git auto-merges; whichever merges second should re-run this
   file's suite.

5. **Reported to other lanes, not fixed here.**
   `RepairSpecificationSourceRoute.Json` and `.AiDraft` (added by ENG-026) have
   no arm in `OperatorLabels.RepairSpecificationRoute`, so they render as
   "recorded before source tracking". ENG-026 or ENG-028 owns it.

## Scope of this proof

Written against merged `dev` at `b92cb9a7` per decision D15. `main` has not
been promoted; the exact-SHA `dev` → `main` promotion happens at wave 5 and
needs explicit `MERGE AUTH GRANTED` immediately before the `main` update.

## 2026-08-29 — Reversed out of Done under the strict rule 14 (D20/D21)

The operator settled rule 14 in favour of the strict reading after this proof was
written, and separately ruled that a disabled control or a closed feature gate is
never a delivered capability (D21). An independent GPT-5.6 audit, adjudicated
against this ticket's own What/Owns/Verification scope, found the following named
capabilities are not delivered on merged `dev` at `b92cb9a7`:

| Capability | Why it does not qualify | Wired by |
| --- | --- | --- |
| Glass's direct estimating-service link — named in this ticket's own What ("record bar … Glass's/Audatex disabled seams per D7") in a file this ticket Owns | Rendered permanently inert at `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml:211` — `<button type="button" class="btn" disabled aria-disabled="true">Glass's</button>` — inside `<span class="gated" data-condition="@Model.EstimatingServiceCondition">`, where that property is a hard-coded literal at `Index.cshtml.cs:193-194`: `public string EstimatingServiceCondition => "Available once the estimating-service link is agreed";`. There is no `@if` enabling branch and no runtime input, so no state of the deployed system renders it enabled. D21: "Control permanently inert (a D7 integration seam) — **No**". | [[TICK-085]] — "Complete Glass's repair-estimate import from a representative export"; blocked on obtaining a representative Glass's export |
| Audatex direct estimating-service link — same clause of this ticket's What, same owned file | Permanently inert at `Index.cshtml:214` behind the same hard-coded condition. Distinct from the live Audatex PDF import (`Index.cshtml:480` → `OnPostImportEstimateAsync`, EXT-12/[[ENG-002]]), which is genuinely delivered and is not the failing capability. | **no ticket supplied this** at the time of the audit — raised as [[ENG-030]] |

The ticket also fails its own acceptance on the literal wording. Its What requires
"Remove the seven old section tabs and every inert `type="button"` control", and its
Verification box reads "No inert control remains." This proof marks that item
"Proven" at `proof/proof.md:273` while its own inventory table at `proof.md:145`
records "| 211, 214 | Glass's, Audatex | D7 disabled seams (below) |", and
`proof.md:196-228` argues the exemption. That argument was the earlier instruction
D21 explicitly corrected — "we aren't meant to be shipping features disabled." D7
licensed *drawing* them; it never made them delivered. D22 keeps the rendering as a
real disabled button; it does not make the capability claimable.

Nothing in the proof above is withdrawn — it remains accurate at the tier it claims.
What changed is the bar, not the evidence.

Checked and cleared, not findings: the three controls that pass D21's legitimate
"conditionally disabled with a named condition" row each have a live enabled branch
above the disabled one — `Import estimate` (`:196` enabled / `:204` gated),
`Send to Claude` (`:218` / `:226`) and `Generate report draft` (`:235` / `:251`).
`SendToClaudeCondition` is computed per render from `sendToAiControl.IsEnabledAsync`
plus a confirmed Engineer's Value (`Index.cshtml.cs:797-811`) — an Administrator DB
switch, not the closed `Features:SendToAi` composition gate — so the Send-to-Claude
path is genuinely delivered (posts to `OnPostSendToClaudeAsync` →
`createAiJob.ExecuteAsync`, `Index.cshtml.cs:442`).

### Findings that were NOT counted against this ticket

This ticket's What closes the multi-estimate editor out in one sentence — "The
multi-estimate editor is wave 4." — and its Estimates-pane clause is deliberately
narrow ("the Estimates pane carrying the current single estimate/import +
accept-specification handlers"). Its Owns contains exactly one Core file,
`src/Pegasus.Core/Assessment/AssessmentWorkspace.cs`; it never owns `Estimates.cs`.

- `IDuplicateEstimate` registered with no reachable consumer
  (`DependencyInjection.cs:323`) — owned and named by [[ENG-026]]
  (`Core/Assessment/Estimates.cs`, sole commit `bcee2ae2` "ENG-026: named
  estimates…"); consumer owed by [[ENG-028]].
- `IDiscardEstimate` registered with no reachable consumer — named in
  [[ENG-026]]'s What, consumer owed by [[ENG-028]].
- `ISetCurrentEstimate` registered with no reachable consumer — named in
  [[ENG-026]]'s What, consumer owed by [[ENG-028]].
- `JsonEstimateParser` registered with no page caller
  (`DependencyInjection.cs:320`) — [[ENG-026]]'s What names "JSON estimate parser
  beside the Audatex parser" and its Owns lists the file; the JSON/Other import
  source selector is [[ENG-028]]'s "Import estimate dialog".
- Estimate tabs / tablist / keyboard interaction absent — [[ENG-028]]'s What names
  it verbatim. This ticket's What names only the removal of "the seven old section
  tabs".
- New estimate, Save estimate, staff editor fields/lines/totals absent —
  [[ENG-028]]'s What.
- Delete estimate control and confirmation dialog absent — [[ENG-028]]'s What.
- Duplicate estimate control absent — [[ENG-028]]'s What.
- Use estimate / set Current control absent — [[ENG-028]]'s What. The Current chip
  this ticket does draw (`Index.cshtml:359`) is a display of the accepted
  specification and is rendered.
- Import estimate name/source selection absent — [[ENG-028]]'s What. This ticket
  names only "Import estimate = existing handler", wired at `Index.cshtml:480`.
- 1580/1100/760 three-width visual walk not performed — a Verification-box gap this
  proof discloses honestly, but a verification-evidence question, not a rule-14
  caller failure.
