Read from the live source on 18 September 2026.

## What this page does not show

- No breadcrumb beyond the "Back to {reference}" action; it is reached only
  from the Case record's Actions menu, never from navigation.
- No submission history list — only the latest submission
  (`Model.LastSubmission`), success or failure, is shown.

## Source table

| File | What it owns |
| --- | --- |
| `Eva/Send.cshtml` | The standalone page header, the Last attempt/Sent-to-EVA summary block, the outcome status chip tone mapping. |
| `Eva/Send.cshtml.cs` | `OnGetAsync` (loads `Model.Handoff`, `Model.LastSubmission`), `OnPostSubmitAsync`. |
| `Shared/_EvaHandoff.cshtml` | Sign-off Engineer read/select, the automatic-failure notice, Export EVA ZIP (`PrincipalReportGenerationPolicy.EvaZip`), Send via API (`EvaManualApi` or a retryable automatic failure, gated further by `ApiEnabled`/`ApiComposed`). |
| `Presentation/OperatorLabels.CaseWorkspace` | `EvaHandoff`, `SignOffEngineer`, `SendViaApi`, `EvaApiNotEnabled`. |

## Behaviours found

### Outcome tone mapping
`Eva/Send.cshtml`'s `OutcomeTone` follows `docs/design/README.md`'s status-chip
rule directly in its own comment: red is blocked/failed/denied, amber is
incomplete/pending. `EvaSubmissionOutcome.Partial` (sent, no reference
returned) is the one amber case; every other non-success outcome is red.

### Two routes, one partial
`ReportGenerationPolicy == EvaZip` offers Export EVA ZIP (a `Documents/Export`
Bundle post); `EvaManualApi` (or a retryable automatic failure) offers Send
via API, itself gated by `ApiComposed` (the seam exists) and `ApiEnabled`
(drawn disabled with `OperatorLabels.CaseWorkspace.EvaApiNotEnabled` when the
Principal has not enabled EVA API submission). Both routes can show at once
when a Principal is configured with a policy this page's two conditions
both satisfy.

## Things the FRD does not settle here

- The exact business rule for `CanRetryAutomaticFailure` (which automatic EVA
  failures are retryable via the manual API versus requiring a different
  path) lives in Core and was not traced beyond its use as an `||` condition
  here.
