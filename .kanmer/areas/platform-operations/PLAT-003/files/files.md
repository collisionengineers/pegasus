# Files — PLAT-003

## New file

- `src/Pegasus.Web/Presentation/RailCountsPageFilter.cs` — a global
  `IAsyncPageFilter` (the standard ASP.NET Core mechanism for shared
  per-request `ViewData`, since none exists in this codebase to reuse — see
  `research`). Queries `IDashboardQueries.GetCaseStageCountsAsync` once, only
  when the request is authenticated, and sets
  `ViewData["RailCounts"] = new Dictionary<string, int> { ["Queues"] = NotReady + Review + Held }`.
  Placed under `Presentation/` alongside the existing
  `OperatorLabels.cs`/`InstructionDraftFieldsView.cs` — the folder already
  used for view-layer helpers that are not full pages.

## Changed files

- `src/Pegasus.Web/Program.cs` — register the filter:
  `builder.Services.AddRazorPages(options => options.Filters.Add<RailCountsPageFilter>());`
  next to the existing `builder.Services.AddRazorPages();` (line 231). No DI
  registration needed for the filter type itself — `FilterCollection.Add<T>`
  activates it via `TypeFilterAttribute`, which resolves constructor
  dependencies from the container automatically (`IDashboardQueries` is
  already `AddScoped` in `Pegasus.Infrastructure/DependencyInjection.cs:226`).

## Files read, not changed

- `src/Pegasus.Web/Pages/Shared/_Layout.cshtml` — the existing `CountFor`
  mechanism (lines 24-27, 64, 75, 82) is reused exactly as-is; only `Queues`
  ever receives a value in the dictionary, so `Inbox`/`Cases` continue to
  render nothing.
- `src/Pegasus.Infrastructure/Persistence/EfDashboardQueries.cs` — reused
  unmodified (`GetCaseStageCountsAsync`).

## Tests

- `tests/Pegasus.IntegrationTests/RailCountsWebTests.cs` (new) — an
  HTTP-level integration test (same style as
  `TriageQueuesWebTests`/`DashboardCountersWebTests`, not a full browser
  test): seeds one NotReady case, requests any authenticated page, and
  asserts the rendered `Queues <span class="rail-link__count ...">N</span>`
  equals the real `CaseStageCounts` total. A second assertion confirms the
  `Inbox`/`Cases` rail links carry no `rail-link__count` span at all
  (absent count renders nothing, not a `0`).
- `tests/Pegasus.IntegrationTests/Browser/AccessibilityTests.cs` — run
  unmodified as a regression check (it queries `.app-rail, .app-nav`
  generically; the new badge is additive markup inside an existing link, not
  a new landmark or interactive control, so no accessibility contract
  changes).
