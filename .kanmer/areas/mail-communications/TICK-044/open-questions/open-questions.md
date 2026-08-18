# Open questions — TICK-044

- [ ] **Product-owner decision required:** Should MAIL-02 use this exact exhaustive operational mapping: `new-instruction-received` → **Receiving work**; `post-report-emails` plus Received `billing/billing-query` → **Queries**; accepted `pre-instruction-emails` Triage requests → the separate **Triage** workflow only when its existing registration and route predicates pass (otherwise **Needs sorting**); every remaining classified Received/Sent category, including reasoned `Other`, → **Other**; and every `Ambiguous` or `Unclassified` result → **Needs sorting**? Recommendation: accept this conservative matrix only if those category meanings match operations, retain Ambiguous/Unclassified as Needs sorting, and keep Outlook-folder mapping in MAIL-23. Also confirm whether a classified `pre-instruction-emails` item that is not a Triage request belongs in Receiving work or Other.

## Parked (explicitly deferred)

None.
