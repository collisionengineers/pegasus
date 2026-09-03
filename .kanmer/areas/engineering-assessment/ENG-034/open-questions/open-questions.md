# Open questions — ENG-034

## Awaiting an answer

- [ ] **Who moves the Assessment POST handlers onto the Case page?** Raised by
  the 2026-09-03 cross-model plan review (finding 2). The plan's adopted
  option A has [[CASE-038]] add the whole handler surface
  (`SaveEstimate`, `EditLine`, `DuplicateEstimate`, `DiscardEstimate`,
  `SetCurrentEstimate`, `ImportEstimate`, `SendToClaude`,
  `GenerateReportDraft`, `PreviewReportDraft`, lease claim/heartbeat/release)
  to `Details.cshtml.cs` while its own section shells are heading-only, so
  CASE-038's PR merges handlers with **no production caller** — which the
  repository's "Done means wired" rule forbids — and `dev` carries two handler
  surfaces until ENG-034 merges. Option B has ENG-034 take the
  `Details.cshtml.cs` capacity-one lease after CASE-038 merges and move the
  handlers itself in the same PR as its partials and the 301, making the
  cutover atomic with no duplicate and no unreachable code; the cost is that
  ENG-034 touches a file the EPIC-012 whole-file ownership rule assigns to
  CASE-038, joining the lease queue behind CASE-038 and [[CASE-039]].
  Recommendation: **option B**. The choice re-scopes two tickets and is the
  epic owner's, not ENG-034's. Everything else in the plan is written for
  option A and switches to option B by moving contract items 3–4 and the
  step-3 handler removal into ENG-034 under that lease.

## Parked (explicitly deferred)
