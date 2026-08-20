## What changed

- `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml.cs` — added
  `IndexModel.CombinedReadiness` (`ReportDraftPreparation?.Reasons ??
  Assessment?.Readiness ?? []`) and a static `IssueSummaryText(int)` helper.
- `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml` — the readiness aside
  now renders one `<details class="readiness-summary">` whose `<summary>` is
  the existing `.status-chip status-chip--amber` component reading "N issues
  detected", containing the same `<ul class="blocker-list">` markup as before
  (same per-item text, nothing dropped). The report-draft "Not ready" card no
  longer repeats the list — it names the same count and says "see Readiness
  above for the list". Added a small guarded hover/focus-open script in
  `@section Scripts`.
- `src/Pegasus.Web/wwwroot/css/site.css` — `.readiness-summary` rules
  (marker hidden, spacing, focus ring via the existing `--focus-ring` token).
  No new chip/colour language.
- New test: `tests/Pegasus.IntegrationTests/Browser/AssessmentReadinessSummaryBrowserTests.cs`.

## Root cause (recorded in files.md, confirmed against prod-diagnostics.md §4)

Two independent surfaces both rendered
`AssessmentPolicy.EvaluateReadiness` output as one `<li>` per item: the
readiness aside used `Assessment.Readiness` directly, and the report-draft
card used `ReportDraftPreparation.Reasons`, which
`AssessmentReportProjection.Project` builds by copying `EvaluateReadiness`
wholesale and only appending report-specific extras — never removing. For a
near-empty case (QDOS26002: only 4 persisted suggestion rows, zero confirmed
assessment fields), this meant every core readiness gap was shown twice
(once per surface) plus the report-specific extras once more — around 45+
individually rendered warning lines from a case whose actual persisted data
is a handful of rows. The "flood" was rendering-side duplication, not a data
problem, matching prod-diagnostics.md §4's note.

## Fix

`CombinedReadiness` uses `ReportDraftPreparation.Reasons` when available
(a guaranteed superset of `Assessment.Readiness` for the same request) so one
list is authoritative. The readiness panel owns the one itemised disclosure;
the report-draft card only references its count. No warning text was
dropped — every item that used to render in either list now renders once, in
the one disclosure.

## Tests

- `dotnet build ./Pegasus.slnx -c Release --no-restore` — 0 warnings, 0 errors.
- `dotnet test tests/Pegasus.IntegrationTests -c Release --no-build --filter "FullyQualifiedName~AssessmentReadinessSummaryBrowserTests|FullyQualifiedName~AssessmentReportDraftWebTests"` — 3/3 passed (the 2 pre-existing report-draft web tests plus the new browser test). The new test asserts: exactly one `.blocker-list` renders on the page; the chip's parsed count equals the number of `.blocker` items in it; the "Not ready" card names the same count and points back to Readiness without its own list; the list is hidden by default, revealed on hover, hidden again on mouse-away, and revealed by keyboard focus alone; and `FindAccessibilityViolationIdsAsync()` is empty (run at 1920x1080, doubling as the visual/Playwright check).
- `dotnet test tests/Pegasus.IntegrationTests -c Release --no-build --filter "FullyQualifiedName~AccessibilityTests"` — 24/24 passed (unaffected; `/Cases/{id}/Assessment` needs a seeded id so it was never in `AuthenticatedRoutes`, but the generic suite stays green).
- `dotnet test tests/Pegasus.Core.Tests -c Release --no-build --filter "FullyQualifiedName~Assessment"` — 57/57 passed (Core readiness/report-projection policy untouched).

## Verification checklist (from the ticket)

- [x] A case with many assessment issues shows one summary element with the correct count — proven by the new test's count-equality assertion.
- [x] Hover/focus reveals the list; content matches the previous individual warnings — same `<li>` markup/text moved inside the disclosure, nothing rewritten; hover and bare-focus reveal both proven.
- [x] No information is lost; accessibility suite green — every prior warning's text is still reachable (now once, not twice); axe clean on the route; existing `AccessibilityTests` suite (24/24) and `AssessmentReportDraftWebTests` (2/2) both still pass.

## Parked

None. No operator question arose during implementation.
