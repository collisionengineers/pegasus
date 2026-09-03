# Research — PLAT-069 (2026-09-02, gpt-5.6-terra medium, wrapper-checked)

Wrapper notes: Codex ran read-only in `.worktrees/research` at origin/dev
`cad00be9`; the checkout was clean afterwards. The wrapper spot-checked the
DELIV-041 D37 commit, `EfServiceHealthQueries`, the design README notice
rule, and the browser tests; one Codex claim ("no Operations browser test")
was wrong and is corrected below, and one line citation was moved from
README:789 to README:781.

## Scope and revision

VERIFIED — `git rev-parse --verify HEAD`, `git branch --show-current`, and
`git status --short` show this is a clean detached checkout at the inspected
`origin/dev` revision. No files were changed and no build or test ran.

## Current behaviour

VERIFIED — `Get-Content src/Pegasus.Web/Pages/Operations/Index.cshtml`:

- `/Operations` renders a Service health panel only when
  `Model.ServiceHealth` is non-null. The table shows Area, Service, State,
  Latest evidence, and Dependency; it has no action column.
- Independently, `Model.Operations.LimitReached` renders a warning:
  `Partial data — Showing recent operational results; refresh for the latest
  activity.`
- The page uses `.notice`, `.notice--warning`, and
  `#icon-alert-triangle`; its header uses `Shared/_FreshnessBanner`.

VERIFIED — `Get-Content src/Pegasus.Web/Pages/Operations/Index.cshtml.cs`:

- `IndexModel` accepts optional `GetServiceHealth`; `OnGetAsync` assigns
  `ServiceHealth` only when that dependency is composed. A null dependency
  means the panel is absent.
- The page authorizes Administrator, Engineer, and User staff roles. It does
  not presently distinguish Administrator in its page model.

VERIFIED — `Get-Content src/Pegasus.Core/Operations/ServiceHealth.cs`:

- Core owns `ServiceHealthState`: `Current`, `Partial`, `Failed`, `Running`,
  `Configured`, and `ReviewRequired`.
- `ServiceHealthSnapshot` contains `AsOfUtc`, `Rows`, and
  `ExternalWorkLimitReached`; it has no predicate for non-current, partial, or
  failed rows.
- `GetServiceHealth` composes mailbox polls, `IServiceHealthQueries`,
  `GetRequestOperations`, EVA submissions, AI-job data, Send-to-AI control,
  Automation ingress status, Automation activity, and `TimeProvider`.
- The use case returns the Operations projection's limit flag separately from
  the health-row states.

VERIFIED — `Get-Content
src/Pegasus.Infrastructure/Persistence/EfServiceHealthQueries.cs`
(wrapper re-checked):

- `EfServiceHealthQueries` implements `IServiceHealthQueries`.
- It reads existing `ApprovedSentPollStates` and `IntakeWorkItems` with
  `AsNoTracking`; it creates no new persistence shape and contacts no service.

VERIFIED — `Get-Content src/Pegasus.Web/Mcp/AutomationMcpExtensions.cs`:

- `GetServiceHealth` is composed by the Automation MCP extension
  (`services.AddScoped<GetServiceHealth>()`, line 34), not by an
  Infrastructure registration.

VERIFIED — `Get-Content src/Pegasus.Web/Presentation/OperatorLabels.cs`:

- Existing helpers translate health areas, states, dependencies, and selected
  internal service names:
  `ServiceHealthAreaName`, `ServiceHealthStateName`,
  `ServiceHealthDependencyName`, and `ServiceHealthServiceName`.
- There is no existing `Partial data` or Service health panel/title label
  member; the Operations page carries both as literals today.

VERIFIED — `rg -n -C 4
'IsInRole\(StaffRoleNames\.Administrator|User\.IsInRole'
src/Pegasus.Web/Pages`:

- The established Razor convention is
  `User.IsInRole(Pegasus.Core.Identity.StaffRoleNames.Administrator)`, used by
  `Pages/Shared/_Layout.cshtml` and `_ShellDialogs.cshtml`.
- `StaffPageModel` supplies actor construction, not an administrator boolean.

VERIFIED — `Get-Content
tests/Pegasus.IntegrationTests/OperationsWebTests.cs`:

- `OperationsPageIsStaffWorkspaceWithNoReceiptLedgerOrBoxSurface` asserts
  Service health is absent when the test host does not compose the query
  (line 57).
- `ComposedServiceHealthRenamesInternalVocabularyAndRetriesThroughTheCanonicalCommand`
  (line 70 on) asserts the table is present when it does compose the query,
  including health-name translations and absence of a health action column.
- Wrapper correction (Codex claimed no browser test touches Operations):
  `tests/Pegasus.IntegrationTests/Browser/OperatorJourneyTests.cs` visits
  `/Operations` at lines 90, 112, 121 and 216 (Attention required / retry
  journey) and `Browser/AccessibilityTests.cs` line 18 lists `/Operations`
  in its route sweep. Neither asserts on "Service health" or "Partial data",
  so they need no change but will exercise the new markup.

VERIFIED — `rg -n -C 5 'operations--(default|empty)'
docs/design/test-ui/catalogue.json
tests/Pegasus.IntegrationTests/TestUiSnapshotTests.cs`:

- `operations--default` and `operations--empty` are the current Operations
  snapshot scenarios (catalogue.json lines 532–547).
- `TestUiSnapshotTests` has an Operations empty-state assertion (line 42,
  `>No retryable external work<`).
- Neither scenario names an administrator partial-health state.

VERIFIED — `rg -n -i -C 4 'Service health|Partial data'
docs/design/test-ui/pages/operations--default.html
docs/design/test-ui/pages/operations--empty.html`:

- The committed Operations snapshots contain neither "Service health" nor
  "Partial data" (the snapshot host does not compose `GetServiceHealth`), so
  removing the table does not by itself change them; a notice state is new.

VERIFIED — `rg -n 'ServiceHealth|Health'
src/Pegasus.Infrastructure/Persistence/Migrations --glob '*.cs'`:

- No Service-health-specific migration exists. This change consumes existing
  projections and requires no migration.

## Mockup

VERIFIED — `rg -n -C 4
'DATA\.health|serviceHealthTable|renderOperations|Partial data|admin/service-health'
C:/Users/PC/Downloads/Pegasus_UI_v2_src/src/16-operations.js
C:/Users/PC/Downloads/Pegasus_UI_v2_src/src/04-fixtures.js
C:/Users/PC/Downloads/Pegasus_UI_v2_notes.md`:

- `renderOperations()` shows a warning only where at least one health row is
  `partial` or `failed` and the viewer role is `administrator`.
- The mockup notice is headed `Partial data`, includes the sentence
  "Some figures may be behind.", and links to `/admin/service-health`.
- Operations does not render `serviceHealthTable()`. That helper remains for
  the Administration page.
- Fixtures include `DATA.health` rows with a `partial` state.
- The notes identify missing Administration Service health, Action Logs, and
  Reports pages as backend gap 7.

## Gap list

1. VERIFIED — `Get-Content src/Pegasus.Web/Pages/Operations/Index.cshtml`:
   Operations currently owns and renders the Service health table when the
   snapshot is composed. D37 requires removing it.

2. VERIFIED — `Get-Content src/Pegasus.Core/Operations/ServiceHealth.cs`:
   there is no Core-owned predicate for the D37 notice condition. Add one
   only after resolving the meaning of "not current".

3. VERIFIED — the mockup source tests only `Partial` and `Failed`, whereas
   Core also has `Running`, `Configured`, and `ReviewRequired`. Therefore
   `row.State != Current` would broaden the mockup condition.

4. VERIFIED — `Get-Content src/Pegasus.Web/Pages/Operations/Index.cshtml`:
   the existing limit warning is a different condition from health state.
   Its explanatory sentence conflicts with the design rule below.

5. VERIFIED (wrapper: citation corrected to `docs/design/README.md:781`,
   component table row `notice … | Inline notice: label plus value only`, and
   section "No explanatory copy and page economy" at line 648): the existing
   limit-warning sentence and the mockup's "Some figures may be behind"
   sentence are not compliant copy to carry forward.

6. VERIFIED — `Get-ChildItem
   src/Pegasus.Web/Pages/Administration/ServiceHealth` reports the directory
   is missing (wrapper: `ls src/Pegasus.Web/Pages/Administration/` shows
   Access, Accounts, Automation, Configuration, MailCategories, Mailboxes,
   Organizations, Principals, Roles, Shared only). The D37 link has no
   destination yet; `_AdminNav.cshtml` header comment says Service health
   joins "when their pages land (wave 4), never as dead links".

7. ASSUMED — an `asp-page` pointing to a page that is absent cannot meet the
   ticket's "no dead link" requirement (Razor's anchor tag helper renders an
   empty `href` or throws for an unknown page depending on configuration; not
   executed in this read-only research).

8. VERIFIED — `git log --format='%h %s' -S 'D37' -- docs` shows `632ec0c4`
   (DELIV-041: record D29–D43) and `2944cbf1` (DELIV-041: apply review
   dispositions, PR #647) already updated `docs/frd/frd-12-operator-experience.md`
   (lines 102, 355–372), `docs/design/README.md` (745, 1089, 1129, 1166,
   1431) and `docs/capabilities.md` (UI-19, "allocated to `PLAT-069`, not
   delivered"). PLAT-069 must not reopen those shared documents; DELIV-030
   refreshes current-state docs.

## Existing helpers and conventions to reuse

- Markup: `.notice.notice--warning` with `<svg class="icon"><use
  href="#icon-alert-triangle" /></svg>` exactly as the existing LimitReached
  notice in `Pages/Operations/Index.cshtml` (lines 43–48).
- Administrator check: `User.IsInRole(Pegasus.Core.Identity.StaffRoleNames.Administrator)`
  (`_Layout.cshtml:12`, `_ShellDialogs.cshtml:11`).
- Link to an Administration area: `asp-page="/Administration/<Area>/Index"`
  (`Pages/Intake/Details.cshtml:91`, `_Layout.cshtml:108`).
- Core policy home: `ServiceHealthPolicy` (static class, ServiceHealth.cs
  line 127) or a member on `ServiceHealthSnapshot` for the notice predicate.
- Labels: `OperatorLabels` nested static classes (e.g. `OperatorLabels.AiJobs`,
  `OperatorLabels.EvaHandoffs`) for panel/notice strings.
- Tests: `OperationsWebTests.Configure(baseFactory, store, withServiceHealth:
  true)` already hand-composes `GetServiceHealth` with recording ports.

## Recommended shape

ASSUMED — subject to the open state-definition question:

- Keep `GetServiceHealth` optional on Operations, but remove the table.
- Add a small Core predicate on `ServiceHealthSnapshot` or
  `ServiceHealthPolicy` to express the approved notice condition, rather than
  duplicating health-state policy in Razor.
- In `Index.cshtml`, render the one D37 warning only for
  `User.IsInRole(StaffRoleNames.Administrator)` and that Core predicate.
- Reuse the existing warning markup and the existing Administrator role
  convention.
- Use label-only content, for example `Partial data` plus the Service health
  link; do not copy either explanatory sentence.
- Retain the Operations limit condition as a separate data-boundary concern.
  If it remains visible, its existing explanatory sentence is a pre-existing
  design-rule violation and needs an explicit disposition; do not silently
  merge it with health state because the predicates describe different facts.
- Sequence PLAT-069 after PLAT-051 supplies
  `Pages/Administration/ServiceHealth`, or land the removal first with the
  link following PLAT-051 (two PRs under one ticket is not the convention; a
  plan must pick one). A route constant does not create the missing
  destination; deferring the link would fail the stated acceptance criterion.

## Risks

- VERIFIED — `Get-Content src/Pegasus.Core/Operations/ServiceHealth.cs`:
  treating all non-`Current` states as partial data would include deliberately
  disabled/configured services, in-flight work, and review-required outcomes.
- VERIFIED — `Get-Content tests/Pegasus.IntegrationTests/OperationsWebTests.cs`:
  the existing composed-health test is coupled to the Operations table and
  must be rewritten, not deleted.
- VERIFIED — `docs/design/test-ui/**` is a capacity-one shared lock; the
  ticket's own Verification line ("Snapshot states updated") and CLAUDE.md
  ("After changing a routed Razor page, regenerate the Test UI snapshots …
  commit `docs/design/test-ui/` with the page change") mean PLAT-069 takes
  that lease for its Operations states; UIIMP-014 (which PLAT-069 blocks)
  owns the Case-record states, not Operations.
- VERIFIED — the EPIC-012 constraints identify
  `Presentation/OperatorLabels.cs` as capacity-one shared-lock work. A label
  change needs the lane lease or coordination.
- The link destination does not exist on origin/dev (PLAT-051 is in backlog);
  merging PLAT-069 first ships a dead link or no link.

## Open questions (operator only)

- Does "any query is not current" mean only `Partial` or `Failed` as the D37
  mockup implements, or every `ServiceHealthState` other than `Current`?
- When the independent Operations result limit and the D37 health condition
  both hold, should the page show two distinct label-only notices, or should
  the existing limit warning be removed or otherwise redesigned?
- Sequencing: may PLAT-069 merge before PLAT-051 (table removed, notice
  without link until the page exists), or must it wait for PLAT-051 so the
  link is live in the same PR?
