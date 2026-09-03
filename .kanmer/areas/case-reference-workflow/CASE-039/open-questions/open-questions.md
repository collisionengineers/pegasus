# Open questions — CASE-039 (2026-09-02)

- [x] Should adding an Engineer note also record a one-line event in the case Notes history? Operator answer 2026-09-03: no — nothing about an Engineer note appears in the Notes history; Engineer notes stay entirely in their own section (D32).
- [x] Should Add note be refused for every terminal lifecycle state, or only once the case is Complete (D30)? Settled by the governing docs and recorded in the plan (2026-09-02): Add note is offered in editing only, with no additional lifecycle-state gate — the design README's read-only-once-Complete list (Damage, Valuation, Estimate, Settlement, Report) excludes Engineer notes, FRD-01 §Engineer notes states no state rule, and the edit lease is claimable on a terminal case today (`Details.cshtml` lines 227-238). The store uses `CaseMutationGuard.RequireLease`, not `Require` (which adds a terminal gate).

## Parked (explicitly deferred)

None.
