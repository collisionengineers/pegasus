# Research — PLAT-003

## The mechanism as it exists today

- `src/Pegasus.Web/Pages/Shared/_Layout.cshtml` (lines 24-27) already reads
  `ViewData["RailCounts"] as IReadOnlyDictionary<string, int>` and a
  `CountFor(route)` helper that returns `null` when the dictionary is absent
  or lacks the key — the "absent count renders nothing" behaviour is already
  correct and untouched by this ticket.
- Exactly **three** rail links already carry badge markup wired to
  `CountFor`: `Inbox` (line 64), `Queues` (line 75), `Cases` (line 82).
  `Upload`, `Operations`, and `Administration` links carry no badge markup
  at all — adding one for them is out of scope (no existing count concept
  and the ticket's verification only requires "each rail route" to show a
  real count *when a figure exists*, not that every route gets a new badge
  slot invented for this ticket).
- Nothing currently sets `ViewData["RailCounts"]` anywhere — confirmed by
  `grep -rn "RailCounts"` returning only the `_Layout.cshtml` read site.
  There is no existing per-request cross-cutting mechanism in this codebase
  (`src/Pegasus.Web/Program.cs` registers `AddRazorPages()` with no global
  page filter, no `ViewComponent` is used anywhere in the app) — this ticket
  is the first caller of that shape.

## Design authority constraint (`docs/design/README.md:81-83`)

> A rail count is a figure a page already queried, never one the shell
> invents. An absent count renders nothing at all — a shell-level `0` would
> be exactly the stale zero placeholder the operator-experience requirements
> forbid.

Read together with the ticket's own "Why": *"Rendering nothing is correct
until a real figure exists, but the figures should be wired."* — partial
wiring, where only routes with a genuine pre-existing figure get a count and
the rest correctly render nothing, is the intended outcome, not a shortfall.

## Which of the three existing badge slots have a genuine, already-established figure

- **Queues** — exact match. `docs/capabilities.md` UI-02 ("Case queues for
  Not ready, Review, and Held... Real Core count queries deployed in release
  6") is precisely `IDashboardQueries.GetCaseStageCountsAsync` /
  `CaseStageCounts(NotReady, Review, Held)`
  (`src/Pegasus.Infrastructure/Persistence/EfDashboardQueries.cs`), already
  queried by the Dashboard (`Pages/Index.cshtml.cs`) and by the Queues page
  itself (`Pages/Triage/Index.cshtml.cs` — its own three tab badges are this
  exact triple). The Queues rail link opens `/Triage/Index`, whose own
  badges sum to the same figure, satisfying FRD-12's "clear counts that link
  to their exact filtered work."
- **Cases** — no established figure. The Dashboard's "Active cases" heading
  (`Pages/Index.cshtml` line 15) is a section label grouping the three
  Queues tiles, not a distinct case count. `Pages/Cases/Index.cshtml.cs` has
  no default "active" filter — its unfiltered view returns every Case
  regardless of lifecycle state. No `ActiveCase`/`ActiveCount` concept
  exists anywhere in `src/Pegasus.Core` or `src/Pegasus.Web` (verified:
  `grep -rn "ActiveCase\|active cases\|ActiveCount"` returns only that one
  heading). Inventing a definition here (e.g. "not Held/Review/NotReady") is
  exactly what the design rule forbids — a shell-invented figure with no page
  that already queries it that way. Left unwired: renders nothing, which is
  correct per the design rule and the ticket's own text.
- **Inbox** — no established figure. `Pages/Mail/Index.cshtml.cs` is a pure
  viewer of retained mail with no "unread"/"needs action" filter or count
  anywhere in its query (`ListRetainedMail`); the codebase has no concept of
  read/unread mail at all (verified: no "unread" hits outside unrelated
  string literals). `MailActivityCounts.Unidentified`/`IntakeQueueCounts.
  BlockedIntake` (UI-03) are the closest "mail needing attention" figures,
  but they surface on the *Queues* tab's Unidentified sub-tab
  (`Pages/Triage/Index.cshtml`, INTK-009), not on `/Mail/Index` — the Inbox
  rail link's actual destination. Badging "Inbox" with a figure that opens a
  different page on click would recreate exactly the badge-disagrees-with-
  destination defect INTK-013 just fixed. Left unwired.

## No existing cross-cutting "runs before every page" mechanism to reuse

Searched for `IAsyncPageFilter`, `IPageFilter`, `ViewComponent`, and any
`RazorPagesOptions.Filters` registration: none exist. The standard ASP.NET
Core mechanism for "set shared `ViewData` before every Razor Page renders"
is a global `IAsyncPageFilter` registered via `RazorPagesOptions.Filters`
(`services.AddRazorPages(options => options.Filters.Add<T>())`) — this is
the direct, minimal solution to the cross-cutting need the ticket describes
("a shell-level query... on each authenticated request"), not an invented
abstraction: there is exactly one caller (the MVC pipeline itself, for every
authenticated page) and no existing narrower mechanism it could reuse
instead.

## Performance

`GetCaseStageCountsAsync` is already documented as a single grouped
aggregate query with no row projection
(`EfDashboardQueries.cs` class remarks) — cheap by the class's own existing
design, and it is exactly the query already paid for by the Dashboard and
Queues pages, just invoked a second time (once per request) from the shell.
The filter only queries when `HttpContext.User.Identity.IsAuthenticated` is
true, which already excludes sign-in, error, and `/Uploads/{token}` pages
(none of which render `_Layout`/the rail in the first place).
