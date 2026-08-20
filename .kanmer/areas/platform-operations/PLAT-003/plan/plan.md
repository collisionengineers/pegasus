# Plan — PLAT-003

## Scope decision

Wire the **Queues** rail badge only (`NotReady + Review + Held`, reusing
`IDashboardQueries.GetCaseStageCountsAsync` — UI-02's already-deployed
query). `Inbox` and `Cases` have no genuine already-queried figure to reuse
(detailed in `research`) and are left to render nothing, which is the
explicitly correct behaviour per `docs/design/README.md:81-83` and the
ticket's own "Why" text. Adding a badge slot for `Operations` (mentioned in
the ticket's "Approach" as an idea, not a requirement) is out of scope: no
markup for it exists today and no established "retryable" count query
exists to back it without inventing one.

## Steps

1. **`src/Pegasus.Web/Presentation/RailCountsPageFilter.cs`** (new): an
   `IAsyncPageFilter` taking `IDashboardQueries` by constructor injection.
   In `OnPageHandlerExecutionAsync`, if
   `context.HttpContext.User.Identity?.IsAuthenticated == true` and
   `context.HandlerInstance is PageModel pageModel`, await
   `GetCaseStageCountsAsync(context.HttpContext.RequestAborted)` and set
   `pageModel.ViewData["RailCounts"]` to a one-entry
   `Dictionary<string, int> { ["Queues"] = counts.NotReady + counts.Review + counts.Held }`,
   then call `next()`. `OnPageHandlerSelectionAsync` is a no-op (nothing to
   decide before the handler is selected).
2. **`src/Pegasus.Web/Program.cs`**: change
   `builder.Services.AddRazorPages();` to
   `builder.Services.AddRazorPages(options => options.Filters.Add<RailCountsPageFilter>());`.
   No other DI registration — `IDashboardQueries` is already scoped.
3. **Test** — `tests/Pegasus.IntegrationTests/RailCountsWebTests.cs` (new
   file, same fixture style as `TriageQueuesWebTests`): seed one
   instruction-initiated NotReady case via the existing
   `SeedNotReadyCaseAsync`-shaped raw-SQL fixture (reused/copied — it is
   `private` to `TriageQueuesWebTests`, so this file gets its own minimal
   copy, matching the documented precedent that
   `ImageIntakePersistenceTests.SeedCaseAsync` already duplicates the same
   shape rather than sharing a static helper across test classes). Assert:
   - `GET /` (any authenticated page) renders the Queues rail link's count
     span with the real total.
   - The Inbox and Cases rail links render no `rail-link__count` span.
4. Build + focused test run (below). Manually confirm
   `AccessibilityTests` still passes (rail markup is additive only).

## Verification commands

- `dotnet restore ./Pegasus.slnx`
- `dotnet build ./Pegasus.slnx -c Release --no-restore`
- `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --filter "FullyQualifiedName~RailCountsWebTests"`
- `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --filter "FullyQualifiedName~AccessibilityTests"` (regression; rail markup unaffected in shape)

## Performance

`GetCaseStageCountsAsync` is a single grouped aggregate query
(no row projection, per the class's own documented invariant) run once per
authenticated request from the filter — no caching layer needed at this
volume (the office is ~8 concurrent users per `docs/design/README.md`), and
there is no existing per-request cache mechanism in this codebase to follow
instead (confirmed in `research`).

## Simplification pass

To be recorded after implementation, before PR, under a dated heading.

## Correction during implementation

`RazorPagesOptions` has no `Filters` collection of its own (build error
CS1061). The global filter is registered through the underlying
`MvcOptions` instead:
`builder.Services.AddRazorPages().AddMvcOptions(options => options.Filters.Add<RailCountsPageFilter>());`
— same effect (a global filter applied to every Razor Page request), just
the correct API surface. `files`/step 2 above described `RazorPagesOptions.
Filters`, which does not exist; this is the as-built correction.
