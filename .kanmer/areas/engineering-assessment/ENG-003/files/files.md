## Files touched

- `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml` — the two duplicate
  warning surfaces (readiness aside `:139-147`, report-draft "Not ready" card
  `:250-264`), plus a small hover-disclosure script in `@section Scripts`.
- `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml.cs` — add a
  `CombinedReadiness` computed property and an `IssueSummaryText` helper.
- `src/Pegasus.Web/wwwroot/css/site.css` — new `.readiness-summary` rules for
  the `<details>` disclosure (reuses `.status-chip`/`.blocker-list`, no new
  chip component).
- New test: `tests/Pegasus.IntegrationTests/Browser/AssessmentReadinessSummaryBrowserTests.cs`
  (asserts the summary count, the disclosure content, hover/focus reveal, and
  no axe violations on the route).

## Data/policy read (no changes)

- `src/Pegasus.Core/Assessment/AssessmentPolicy.cs:135` `EvaluateReadiness` —
  emits one `AssessmentReadinessItem` per missing/invalid field.
- `src/Pegasus.Core/Reports/AssessmentReportProjection.cs:84-153` `Project` —
  builds its `Reasons` list by first copying `EvaluateReadiness(assessment)`
  wholesale, then appending report-specific requirements (claimant name, your
  reference, report addressee, report photographs, accepted source evidence,
  assessment method, accepted engineer signature, repair cost figures). It
  never removes an item, so `AssessmentReportDraftPreparation.Reasons` is
  always a superset of `Assessment.Readiness` for the same case/request.

## Root cause of the "flood" (confirmed against prod-diagnostics.md §4)

QDOS26002 has only 4 suggestion rows and zero `CaseAssessmentFields`/
`CaseEstimateLines` — i.e. almost nothing confirmed. Two independent surfaces
on the same page both call the same readiness policy and both render one
`<li>` per item:

1. The Readiness aside (`Index.cshtml:139-147`) renders
   `Model.Assessment.Readiness` — for a near-empty case this is ~20 items
   (every `RequireField` in `AssessmentPolicy.EvaluateReadiness` fires:
   registration/make/model/instruction date from `CaseOwned`, plus vehicle
   type/year/mileage-source/condition, assessed date, impact
   severity/location, retail/trade/engineer value, VAT answer, outcome,
   roadworthiness, history check, engineer name/qualifications/signature,
   agreed fee, odometer reading).
2. The "Report draft → Not ready" card (`:250-264`) renders
   `Model.ReportDraftPreparation.Reasons` — which, per the projection code
   above, is the *same* ~20 items **plus** ~7-8 report-specific ones.

Net effect: every core readiness gap is shown twice (once per surface),
plus the report-specific extras once more — around 45+ individually
rendered `<li>` warning lines for a case that actually has only 4 persisted
suggestion rows in the database. The flood is entirely rendering-side
duplication of the same policy output, not a data-volume problem — this
matches prod-diagnostics.md §4's note verbatim.

## Fix shape

- `IndexModel.CombinedReadiness = ReportDraftPreparation?.Reasons ?? Assessment?.Readiness ?? []`
  — since `ReportDraftPreparation.Reasons` is already a strict superset of
  `Assessment.Readiness` (same policy call, only additions), this single list
  is authoritative and loses nothing either surface previously showed.
- Readiness panel header renders ONE combined indicator: a `<details>`
  disclosure whose `<summary>` is the existing `.status-chip status-chip--amber`
  component reading "N issue(s) detected", containing the full itemised
  `.blocker-list` (same markup/content each `<li>` already had). This is the
  one summary element that owns the list, per the ticket's constraint.
- The report-draft "Not ready" card keeps its heading and `status-card--attention`
  tone (state still not colour-only) but no longer repeats the itemised list —
  it shows the same count and points back to the readiness panel ("N issues
  detected — see Readiness above for the list").
- Hover-to-open is a small progressive-enhancement script (native `<details>`
  already gives click/Enter/Space and screen-reader semantics for free); a
  no-JS operator still reaches every item via the existing native disclosure
  interaction. `<details>`/`<summary>` is the codebase's existing disclosure
  convention (`Pages/Cases/Index.cshtml` "More filters", `Pages/Cases/Create.cshtml`
  "Change a value", `Pages/Operations/Index.cshtml` "Withdraw link") — reused,
  not invented.
