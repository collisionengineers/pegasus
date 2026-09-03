# Open questions — CASE-041

- [ ] **Repairer location has no recorded value anywhere, so "Choosing Repairer
  fills the address" cannot be met.** Verified: the only repairer concept in
  production is the Assessment flag `costs.repairer_vat_registered`; no
  repairer name or address is persisted in Core, Infrastructure or Web, and no
  EPIC-012 ticket adds one — repairer reference data is TICK-034 (backlog,
  post-alpha, not designated). Under D33's "options without a value are
  disabled", Repairer location is therefore permanently disabled
  (` · not recorded`), which contradicts this ticket's own Verification line.
  Which does the operator want?
  1. Amend the ticket Verification line so Repairer location is accepted as
     disabled in this programme (the plan's recorded working default), or
  2. Add a persisted per-Case repairer address in a new ticket, which CASE-041
     would then consume.
  Raised by the 2026-09-03 cross-model plan review; a plan cannot amend its own
  acceptance line.

## Parked (explicitly deferred)
