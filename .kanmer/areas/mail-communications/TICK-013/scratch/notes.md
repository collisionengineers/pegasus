## Additional live evidence (orchestrator relay, 2026-08-20 ~05:45Z, read-only SQL on release-13 production)

- `ApprovedSentPollStates.LastCompletedAtUtc = 2026-08-20 05:44:15Z`, `LastFailureCode = NULL` — Sent-folder polling completes successfully against the approved mailbox on the currently deployed release (the approval comparison passes post-2026-08-19 approval; MAIL-003's exception-noise fix rides release 14).
- `ApprovedSentPollOutcomes`: 1 historical row (2026-08-01, OutcomeKind=Unmatched) — detection has run against a real Sent item.
- `CaseReportSentEvidence`: 0 rows — no report has ever been sent, so live matched-report evidence cannot exist yet; that residual is inherent, not a defect.
