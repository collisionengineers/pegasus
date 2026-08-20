## Plan

1. **`IndexModel` (Index.cshtml.cs)** — add
   `public IReadOnlyList<AssessmentReadinessItem> CombinedReadiness { get; private set; } = [];`
   set once after `ReportDraftPreparation` is computed in `OnGetAsync`:
   `CombinedReadiness = ReportDraftPreparation?.Reasons ?? Assessment?.Readiness ?? [];`
   Reuses `AssessmentReportProjection.Project`'s existing superset guarantee
   (see files.md) instead of writing a new merge/dedupe routine — one list,
   not two. Also add a small static helper
   `IssueSummaryText(int count) => $"{count} {(count == 1 ? "issue" : "issues")} detected";`
   so the pluralisation rule lives in one place, used by both surfaces.

2. **Readiness aside (`Index.cshtml:134-153`)** — replace the per-item `<ul
   class="blocker-list">` with a `<details class="readiness-summary"
   data-hover-disclosure>` whose `<summary>` wraps the existing
   `_StatusChip`-style markup (`<span class="status-chip status-chip--amber">`,
   reusing the same `icon-alert-triangle` glyph `_StatusChip.cshtml` already
   picks for "not ready") reading `@IndexModel.IssueSummaryText(count)`. The
   `<ul class="blocker-list">` moves inside the `<details>`, iterating
   `Model.CombinedReadiness` — same `<li class="blocker" data-state="not-ready">`
   markup as today, so no warning text is lost. Zero-count keeps today's
   `<p class="empty-state">No outstanding requirements are listed.</p>`.

3. **Report-draft "Not ready" card (`:250-264`)** — keep the `status-card
   status-card--attention` wrapper and `<h3>Not ready</h3>` (state stays
   dual-signalled, not colour-only) but replace the repeated
   `<ul class="blocker-list">` with one line:
   `<p>@IndexModel.IssueSummaryText(Model.ReportDraftPreparation.Reasons.Count) — see Readiness above for the list.</p>`.
   This is the "one summary element owns it" constraint: only the readiness
   panel renders the itemised list.

4. **CSS (`site.css`)** — add a `.readiness-summary` block near the existing
   `.blocker-list`/`.blocker` rules: `<summary>` marker hidden (reuses
   `.status-chip` for all colour/border/padding, so no new chip look is
   invented), `.blocker-list` inside gets `margin-top` only when `[open]`.

5. **Hover/focus script (`Index.cshtml` `@section Scripts`)** — a guarded IIFE
   matching the file's existing script style (see the PAV slider/dialog
   scripts already in this file): for each `[data-hover-disclosure]`, open on
   `mouseenter` and on the `<summary>`'s `focus`, close on `mouseleave`/`blur`
   unless the other condition still holds (`:hover`/`:focus-within`). Native
   `<details>` click/Enter/Space toggling and screen-reader semantics are
   untouched — this only adds hover and bare-focus as two more ways in, so a
   no-JS operator still reaches every item.

6. **Test** — new
   `tests/Pegasus.IntegrationTests/Browser/AssessmentReadinessSummaryBrowserTests.cs`,
   modelled on `tests/Pegasus.IntegrationTests/Reports/AssessmentReportDraftWebTests.cs`'s
   fake-composition pattern (`FakeGetCase`/`FakeGetCaseAssessment`/
   `FakeProjectionSource`) but driven through `BrowserTestSupport` (reused,
   not a new harness) so it also exercises axe. Fixture is a near-empty
   assessment (mirrors QDOS26002: no confirmed fields, no `CaseOwned` values)
   so several readiness items exist. Assertions:
   - the chip text matches `\d+ issues? detected` and the parsed count equals
     the number of `.blocker` items inside `.readiness-summary` (self-consistent,
     not a hardcoded magic number — survives future readiness-rule changes);
   - the list is not visible before interaction, becomes visible on hover, and
     becomes visible on keyboard focus of the summary (three separate checks);
   - the "Not ready" card no longer renders its own `.blocker-list` (only the
     readiness panel's does) — proves the one-summary-owns-it constraint;
   - `FindAccessibilityViolationIdsAsync()` is empty for the route.
   Existing coverage that must stay green: `AssessmentReportDraftWebTests`
   (`IncompleteCaseFailsClosedNamingWhatIsMissingInsteadOfThrowing` still
   substring-matches "Not ready" and `RepairCostRequirement` — both remain
   true, the requirement text just now lives inside the readiness disclosure
   instead of the blocker card) and the generic `AccessibilityTests` suite
   (unaffected — `/Cases/{id}/Assessment` needs a seeded id so it was never in
   `AuthenticatedRoutes`).

## Simplification pass

Recorded after implementation, dated, before the PR (per lane instructions).
