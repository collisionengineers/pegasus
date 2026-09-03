# Open questions — AUTO-018

- [ ] Does a successful MarketResearch completion become `Completed`
  immediately after retaining its findings document and valuation row, or stay
  `DraftReady` until a named staff review action? D35 says "proposal only";
  today `Completed` is reserved for a staff confirmation
  (`AiJobOperations.cs:151-172`). Evidence added 2026-09-02 by the plan: the
  FRD-11 MarketResearch row (DELIV-041, `frd-11:270`) gives staff confirmation
  "None on the job — the entry is a proposal on the Case", while the states
  section (`frd-11:275-285`) lists only Query response and Unidentified-queue
  pass as hand-completed kinds. The plan's default is `DraftReady` (existing
  actor rules untouched), which leaves the job without a closure surface in
  AUTO-018's owned paths; the alternative narrows the Completed-is-staff rule
  for `MarketResearch` alone. Either is one Core line.
