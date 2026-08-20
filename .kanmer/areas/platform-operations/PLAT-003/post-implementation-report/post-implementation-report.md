# Post-implementation report — PLAT-003

## What changed

- `src/Pegasus.Web/Presentation/RailCountsPageFilter.cs` (new) — a global
  `IAsyncPageFilter` that, on every authenticated request, sets
  `ViewData["RailCounts"] = { ["Queues"] = NotReady + Review + Held }` from
  `IDashboardQueries.GetCaseStageCountsAsync` (UI-02's already-deployed
  query, the same one the Dashboard and the Queues page's own tab badges
  use).
- `src/Pegasus.Web/Program.cs` — registers the filter via
  `builder.Services.AddRazorPages().AddMvcOptions(options =>
  options.Filters.Add<RailCountsPageFilter>());` (corrected during
  implementation: `RazorPagesOptions` has no `Filters` of its own — the
  filter goes through the underlying `MvcOptions`).
- `tests/Pegasus.IntegrationTests/RailCountsWebTests.cs` (new) —
  `QueuesBadgeShowsTheRealStageTotalAndOtherRoutesRenderNoBadge`: seeds one
  NotReady case, requests `/`, asserts the Queues rail badge shows the real
  total and that Inbox/Cases render no badge span at all.

## Scope decision (recorded in `research`/`plan`)

Only the **Queues** badge is wired. `Inbox` and `Cases` — the other two rail
links that already carry badge markup — have no genuine already-established
figure to reuse: `Pages/Mail/Index.cshtml.cs` has no "unread"/"needs action"
concept, and no "active cases" count exists anywhere in the codebase outside
a section heading. `docs/design/README.md:81-83` explicitly forbids the
shell inventing a count ("a rail count is a figure a page already
queried"), and the ticket's own "Why" confirms partial wiring — rendering
nothing where no figure exists — is the correct outcome, not a shortfall.
Adding a badge slot for `Operations` ("retryable", mentioned only in the
ticket's "Approach" as an idea) was also left out: no markup exists for it
today and no established "retryable" query exists to back it.

## Test evidence

- `dotnet build ./Pegasus.slnx -c Release --no-restore` — Build succeeded, 0
  warnings, 0 errors.
- `dotnet test tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj
  -c Release --no-build --filter "FullyQualifiedName~RailCountsWebTests"` —
  Passed: 1, Failed: 0.
- `dotnet test tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj
  -c Release --no-build --filter "FullyQualifiedName~AccessibilityTests"` —
  Passed: 24, Failed: 0 (full browser + a11y suite; regression clean — the
  new badge is additive markup inside an existing link, no new landmark or
  control).
- `dotnet test tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj
  -c Release --no-build --filter
  "FullyQualifiedName~TriageQueuesWebTests|FullyQualifiedName~OperatorJourneyTests"`
  — Passed: 7, Failed: 0 (regression; the filter runs on every page these
  tests exercise).

## Performance

`GetCaseStageCountsAsync` is a single grouped aggregate query with no row
projection (the class's own documented invariant), run once per
authenticated request. No caching layer was added — none exists elsewhere in
this codebase for per-request cross-cutting data to follow instead
(confirmed in `research`), and at the ~8-concurrent-user office scale
`docs/design/README.md` describes, one extra cheap aggregate query per
request is not a measurable cost.

## Simplification pass

Recorded in the ticket `plan` doc under "Simplification pass — 2026-08-20":
reuse, simplification, efficiency and altitude lenses reviewed; no
findings.

## Left out / parked

Nothing parked — no operator question arose. Inbox/Cases/Operations badges
are a documented scope decision (see above), not an open question.
