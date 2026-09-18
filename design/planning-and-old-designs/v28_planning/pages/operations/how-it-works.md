Read from the live source on 18 September 2026.

## What this page does not show

- No service-health query of its own. Service health is Administration-only
  (FRD-12); this page only links to `/Administration/Health` when
  `Model.Operations.LimitReached` is true (source comment, lines 37-38).
- No status message unless `Model.StatusMessage` is set by a previous action
  (e.g. a completed retry) — the page has no default banner.

## Source table

| File | Owns |
| --- | --- |
| `Pages/Operations/Index.cshtml` | AI Job List, EVA handoffs, Attention required (failed intake + retryable external work) |
| `Pages/Operations/Index.cshtml.cs` | Job listing, `RetryExternal`, `SendUnidentifiedToAi`, `CompleteAiJob`, `CancelAiJob`, `StartedBy()`, `RecordPage()`, `ReviewAction()` |
| `Pages/Administration/Logs.cshtml` handlers | `RetryIntakeAllocation`, `RetryIntakeOcr`, `ReevaluateIntake` — posted to from this page's failed-intake rows |
| `Presentation/OperatorLabels.AiJobs`, `.EvaHandoffs`, `.OperationsNotices`, `.IntakeLog` | Every label on this page |

## Behaviours

### Three panels, independently gated

1. **AI Job List** always renders (with an empty table body when there are no
   jobs); its head carries a `<details>`-based "Send Unidentified to AI" form
   requiring the Unidentified reference by hand.
2. **EVA handoffs** renders only when `hasEvaHandoffs` is true (a submitted
   timestamp exists or there is at least one failure) — otherwise the whole
   section is absent, not an empty state.
3. **Attention required** always renders its heading; "Nothing needs
   attention" shows only when both the failed-intake table and the retryable
   external-work table are empty.

### AI job row actions are mutually exclusive-ish per job

A row can show a Review link (`ReviewAction()` routes to the Estimate section,
a query, or similar depending on job kind), a Complete-by-hand form, and/or a
Cancel form (visible while the job is non-terminal per `AiJobStates.IsTerminal`)
— several can coexist; when none apply the cell shows an em dash.

### Failed intake retry actions are keyed by failure kind

`AllocationFailed` → Retry allocation (only when `CanRetryAllocation` and a
last attempt id exists); `OcrFailed` → Retry OCR; `ProcessingFailed` → Re-
evaluate. Each is a `<details>` disclosure holding a reason-required
confirmation form, matching the Operations page's general pattern of a named
reason for every state-changing action.

### Partial-data notice

`Model.Operations.LimitReached` renders the "Partial data" warning notice
above everything else in `.stack`, linking to Service health — this is the
Operations page's own reading of a composed query limit, not a page-level
freshness state (the freshness banner beside the header handles the ordinary
Current/Stale/Unavailable states via `_FreshnessBanner`).

## Things the FRD does not settle

- Nothing found while reading this page in isolation.
