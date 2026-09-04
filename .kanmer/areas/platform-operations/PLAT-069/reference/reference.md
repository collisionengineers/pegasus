# Review record — PLAT-069 (PR https://github.com/collisionengineers/pegasus/pull/657)

Reviewed head: `74124b7fbec5f7bfc267c292b77925b9de41a9fa` (confirmed equal to
the head named in the review brief; the branch did not move).
Base: `origin/dev` at `80f0ca262b0fe2ca354a5dfb18933dc3f105b917`.
Review worktree: `.worktrees/plat-069-review` (detached, read-only).

Reviewer models: independent read by `gpt-5.6-terra` at `xhigh`
(built by `gpt-5.6-sol`); dispositions, independent verification and gate by
Claude Opus. Date: 2026-09-04.

## Verdict

**APPROVED.** The independent reviewer returned REQUEST CHANGES with one
finding marked blocker, two should-fix and one nit. Every finding was checked
against the code in the review checkout; the blocker rests on a false premise
and is rejected, both should-fix findings are rejected with evidence, and the
nit is a wording inaccuracy in a ticket document, corrected here. No blocker
survives. Nothing in the diff falls outside the ticket's owned paths, Core
owns the policy, no label is left as a Razor literal, no explanatory copy
remains, no migration is involved, and no pre-existing assertion was weakened.

## Change under review

Eleven files, four commits, all owned paths:

| Path | Change |
| --- | --- |
| `src/Pegasus.Core/Operations/ServiceHealth.cs` | `ServiceHealthPolicy.HasPartialData` — true only for a `Partial` or `Failed` row |
| `src/Pegasus.Web/Pages/Operations/Index.cshtml` | Service health panel and table deleted; administrator-only anchorless health notice added; limit notice's explanatory sentence removed |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` | `OperationsNotices` nested class in a `// PLAT-069`-delimited block |
| `tests/Pegasus.Core.Tests/Operations/ServiceHealthTests.cs` | Predicate theory over the health states and the limit flag |
| `tests/Pegasus.IntegrationTests/OperationsWebTests.cs` | Table-absence, administrator notice, non-administrator theory, no-empty-href, configurable `LimitReached`, combined-notice test |
| `tests/Pegasus.IntegrationTests/TestUiSnapshotTests.cs` | One `StateMatches` entry for the new catalogue state |
| `docs/design/test-ui/catalogue.json`, `index.html`, `pages/operations--*.html` | New `operations--partial-data` state and regenerated Operations pages |

`src/Pegasus.Web/Pages/Operations/Index.cshtml.cs` is absent from
`git diff --name-only origin/dev...HEAD` — unchanged, as the plan required.
No Administration page, migration, governing document, shell partial,
`site.css`, `site.js`, `ci.yml` or `scripts/*.ps1` is touched.

## Findings and dispositions

| # | Severity (reviewer) | Location | Finding | Disposition |
| --- | --- | --- | --- | --- |
| 1 | blocker | post-implementation report | The report records an unscoped full snapshot capture (120 browser + 300 non-browser); EPIC-012 forbids running the whole capture and requires the scoped capture. | **Rejected — false premise.** The scoped capture is UIIMP-015's deliverable and had not landed at this head. Verified in the review checkout: `grep -c 'Scope' scripts/Update-TestUiSnapshots.ps1` returns `0` at both `HEAD` and `origin/dev`, so no `-Scope` parameter existed for this lane to use. The build policy's scoped-capture clause names `[[UIIMP-015]]` as its source; until that merges the full capture is the only capture there is, and the lane correctly reverted every page outside Operations before committing (`git diff --name-only origin/dev...HEAD` lists only the three Operations pages plus `index.html`). No re-capture is required. |
| 2 | should-fix | `tests/Pegasus.Core.Tests/Operations/ServiceHealthTests.cs:18` | The new theory omits `ServiceHealthState.Current`. | **Rejected.** The plan's step 2 enumerates the states to cover — `Partial`, `Failed`, the excluded `Running`/`Configured`/`ReviewRequired`, and the ignored limit flag — and `Current` is not among them; the report's "all states above" refers to that same list, so no claim is overstated. `HasPartialData` is a two-value whitelist (`row.State is Partial or Failed`), so `Current` traverses the identical code path as the three excluded states that *are* tested. Adding it would raise no coverage the existing cases do not already give. |
| 3 | should-fix | `tests/Pegasus.IntegrationTests/OperationsWebTests.cs:79` | Only three former Service health column headers (`Area`, `Latest evidence`, `Dependency`) are asserted absent; `Service` and `State` are not. | **Rejected — the suggested assertion would be false.** Verified against the regenerated snapshot: `<th scope="col">State</th>` still occurs once in `operations--partial-data.html`, emitted by a different Operations table. Asserting its absence would fail on markup this ticket does not own. The three headers the test does assert are the ones unique to the deleted panel, and the test additionally asserts the absence of `service-health-title` — the panel's own `aria-labelledby` anchor, which is a stronger and unambiguous proof of removal than either generic header. |
| 4 | nit | post-implementation report | The report describes `operations--default.html` and `operations--empty.html` as having a "content diff: 2 lines each, the removed table and rewritten limit notice"; the actual diff for each is a whitespace-only blank-line shift. | **Accepted; corrected here.** Confirmed with `git diff ... \| cat -A`: both pages change by one blank line only. This is expected and not a defect — neither snapshot host composes `GetServiceHealth` and neither sets `LimitReached`, so neither page ever rendered the table or the limit notice (recorded in `research/research.md`). The report's wording is inaccurate; the underlying artifact is correct. This review record is the correction. |

## Independent verification (review checkout at the reviewed head)

| Command | Exit | Result |
| --- | ---: | --- |
| `dotnet restore ./Pegasus.slnx --locked-mode` | 0 | restored |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | 0 | 0 warnings, 0 errors |
| `dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build` | 0 | 1225 passed |
| `dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build` | 0 | 100 passed |
| `dotnet test ./tests/Pegasus.IntegrationTests/... --filter "FullyQualifiedName~OperationsWebTests\|FullyQualifiedName~TestUiSnapshotTests"` | 0 | 27 passed |
| `pwsh -NoProfile -File ./scripts/Test-UiCatalogue.ps1` | 0 | 54 routed sources, 59 prototypes, 0 broken local references |

Why that scope covers the change: the diff touches exactly three compiled
types (`ServiceHealthPolicy`, `OperatorLabels`, the Operations Razor page) and
three test files. `Pegasus.Core.Tests` covers the new predicate,
`Pegasus.ArchitectureTests` covers the Core/Infrastructure boundary the new
Core member sits on, `OperationsWebTests` is the only class that renders
`/Operations`, and `TestUiSnapshotTests` is the only class that reads the
catalogue and the committed snapshot pages. No migration exists in the diff,
so `Test-MigrationGrants.ps1` does not apply. The full suite is GitHub CI's
gate, not a reviewer's, per the EPIC-012 build policy.

## Artifact facts (files opened, not inferred from a green gate)

Committed blob sizes (`git cat-file -s`, LF as stored) exactly match the
post-implementation report:

| File | Bytes (LF blob) | Report | Begins `<!DOCTYPE html>` | ` href=""` | `>Service health</strong>` | Removed sentence |
| --- | ---: | ---: | --- | --- | ---: | --- |
| `operations--default.html` | 33099 | 33099 | yes | none | 0 | absent |
| `operations--empty.html` | 26675 | 26675 | yes | none | 0 | absent |
| `operations--partial-data.html` | 33305 | 33305 | yes | none | 1 | absent |

The working checkout materialises CRLF (33559 / 27040 / 33769 bytes), which
accounts for the only apparent discrepancy; the stored bytes are the reported
ones. `service-health-title` occurs zero times in all three pages, so the
panel is gone from every captured state. The `>Service health</strong>`
marker is unique to `operations--partial-data.html`, so the `StateMatches`
entry cannot mis-bind to `operations--default`. `operations--partial-data.html`
carries exactly one `notice notice--warning` and no anchor on it.
`docs/design/test-ui/catalogue.json` parses as valid JSON.

## Correctness and rule checks performed by the dispositioning reviewer

- **Core owns the policy.** The `Partial`/`Failed` decision lives only in
  `ServiceHealthPolicy.HasPartialData`; the Razor page calls it and does not
  restate it, and the tests assert through it. `ArgumentNullException.ThrowIfNull`
  guards the argument. The limit flag is not read by the predicate, matching
  resolution 1.
- **Two separate conditions, two separate notices.** `Model.Operations.LimitReached`
  and the health predicate render independent `notice notice--warning` blocks;
  `LimitAndHealthNoticesRenderSeparatelyWithoutExplanatoryCopy` proves both
  render together and counts exactly two.
- **No explanatory copy.** The sentence "— Showing recent operational results;
  refresh for the latest activity." is deleted from `Index.cshtml` and asserted
  absent by the combined test and by the committed snapshots. Both notices are
  a single `<strong>` label with no trailing prose.
- **Labels only in `OperatorLabels`.** Both visible strings resolve through
  `OperatorLabels.OperationsNotices`; `grep -rn "Service health" src/Pegasus.Web/`
  finds the literal only in that one constant and in code comments, never in
  rendered markup.
- **Absent, not disabled.** The notice carries no anchor. Every Operations web
  test asserts the response contains no ` href=""`, so the dead-link class
  cannot regress before PLAT-051 supplies the destination.
- **Role gate is real, not vacuous.** `ComposedServiceHealthDoesNotRenderTheNoticeForNonAdministrators`
  runs against the same `withServiceHealth: true` composition and the same
  `Failed`-row fixture as the positive test; the default test identity is
  Administrator, so had `X-Test-Roles` been ignored the notice would render and
  the assertion would fail. `GetHtmlAsync` asserts `HttpStatusCode.OK`
  (`IntakeWebTestSupport.cs:543-545`), so the negative cannot pass through a
  403 or a redirect. The role spelling matches the `_Layout.cshtml` convention.
- **No assertion weakened.** The rewritten composed-health test replaces the
  removed table-vocabulary assertions with strictly stronger absence
  assertions, retains the `Vehicle lookup` / `Retry this work` / retry-command
  coverage unchanged, and adds the no-empty-href assertion. The dropped
  `<span class="sr-only">Action</span>` assertion is subsumed: the whole panel
  it guarded is now asserted absent by `service-health-title`.
- **No controls added.** The change draws two static notices and no
  interactive control, so there is no handler to name; the retry control it
  leaves in place keeps its existing `OnPostRetryExternal` handler, exercised
  by the retained test.
- **Deliberate retention accepted.** `ServiceHealthAreaName`,
  `ServiceHealthServiceName`, `ServiceHealthStateName` and
  `ServiceHealthDependencyName` remain in `OperatorLabels` with no current
  caller. This was recorded in the plan (step 3, plan-review finding 5) with
  PLAT-051's Administration table as the named next caller, and the
  simplification pass was told not to flag it. Accepted as scoped; if PLAT-051
  is dropped, those four helpers become dead code and should be removed by
  that ticket's closeout.
- **Simplification pass dispositions are honest.** One finding was raised
  (inline the two `OperationsNotices` labels as Razor literals) and rejected
  with a reason that cites the binding repository rule rather than a
  preference. The rejection is correct: inlining would create a second label
  source in the view layer, which is the exact duplication the rule forbids.
- **Report and checklist match the diff** on every claim except finding 4's
  wording, corrected above. The checklist's annotation that the focused
  project/class test commands replaced the plan's solution-wide filter is
  accurate and is what the EPIC-012 build policy requires.

## Open items carried forward

- PLAT-051 must add the Administration Service health anchor to the notice in
  `src/Pegasus.Web/Pages/Operations/Index.cshtml` together with its
  destination page, and give the four retained `ServiceHealth*Name` helpers
  their caller. Until then FRD-12's "links to Administration Service health"
  wording describes the epic's end state, not this head.
