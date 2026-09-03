# Open questions — ENG-034

## Resolved

- [x] **Who moves the Assessment POST handlers onto the Case page?** Resolved
  2026-09-03 by the epic owner (the operator delegated the sequencing choice
  to the controller): **option B**. [[ENG-034]] takes the capacity-one
  `Pages/Cases/Details.cshtml.cs` lease after [[CASE-038]] merges and moves
  the handler surface (`SaveEstimate`, `EditLine`, `DuplicateEstimate`,
  `DiscardEstimate`, `SetCurrentEstimate`, `ImportEstimate`, `SendToClaude`,
  `GenerateReportDraft`, `PreviewReportDraft`, lease claim/heartbeat/release)
  in the same PR as its section partials and the `/Assessment` 301, so the
  cutover is atomic. Reason: option A would merge a handler surface with no
  production caller, which the repository's "Done means wired" rule forbids,
  and would leave two handler surfaces on `dev` until ENG-034 merged.
  Consequences recorded in the plan: contract items 3–4 and the step-3
  handler removal move into ENG-034; ENG-034's owned paths gain
  `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs` (handler surface) and
  `Details.cshtml`; ENG-034 runs **serial** in wave 3, after CASE-038 and
  [[CASE-039]] have released the lease. CASE-038's section shells stay
  heading-only and its PR carries no Assessment handler.

## Parked (explicitly deferred)
