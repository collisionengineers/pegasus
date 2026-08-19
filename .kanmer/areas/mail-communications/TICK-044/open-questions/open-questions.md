# Open questions — MAIL-02

- [ ] **Confirm the operational meaning of Other versus Needs sorting.** Proposed distinction: **Other** is a successful, explainable classification whose known business destination is the general Other queue; **Needs sorting** is a fail-closed exception queue for Ambiguous, Unclassified, contradictory, incomplete, or unsupported evidence where Pegasus cannot safely choose a business destination.
- [ ] **Confirm the exhaustive mapping after accepting that distinction:** new instructions → Receiving work; post-report email and billing queries → Queries; accepted pre-instruction Triage requests → Triage; other pre-instruction email → Other; every remaining successfully classified category, including reasoned Other → Other; Ambiguous/Unclassified → Needs sorting.

## Parked (explicitly deferred)

- [ ] Exact automatic predicates/confidence/holdout activation beyond the delivered routes.
- [ ] Real Outlook/Graph/cloud activation and live verification.
