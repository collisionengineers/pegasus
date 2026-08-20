# Proof — MAIL-14 (VERIFY2, 2026-08-20) — written against merged origin/main, production release 13 = 2325ed4a

- **File presence on origin/main (2325ed4a ancestor path):** `src/Pegasus.Core/Workflow/PollSentEvidence.cs`, `src/Pegasus.Infrastructure/Persistence/EfSentEvidencePollStore.cs`, `EfSentEvidencePollOutcomeQueries.cs`, migrations `20260729160000_CaseWorkflowRuntime.cs` (`CaseReportSentEvidence`) and `20260729183000_SentEvidencePolling.cs`, Worker caller in `src/Pegasus.Worker/EmailEvidenceFunctions.cs`, tests `PollSentEvidenceTests.cs` + `SentEvidencePollPersistenceTests.cs` — all confirmed via `git ls-tree origin/main`.
- **Live production readback (read-only SQL, pegasus-prod-sql-252ow37gij/pegasus, 2026-08-20):**
  - `ApprovedSentPollStates`: 1 row, `instructions@collisionengineers.co.uk`, `LastCompletedAtUtc = 2026-08-20 05:39:15Z`, `LastFailureCode = NULL` — the Sent-evidence poll is running and completing cleanly in production today.
  - `ApprovedMailboxes` for that address: `AllowSentEvidence = True`, `State = Approved` (operator approval effective 2026-08-19).
  - `ApprovedSentPollOutcomes`: 1 row (`Unmatched`, 2026-08-01 23:56Z) — the poll has processed a real Sent item and correctly recorded a non-match.
- **Worker:** `SentEvidencePollFunction` confirmed enabled in production (prod-diagnostics §6), schedule `15 * * * * *`.

**Residuals (named):**
1. No exact report-sent item has ever been detected live because no report has ever been sent through the approved mailbox (`CaseReportSentEvidence` = 0 rows). The detection capability is deployed and actively polling; live detection awaits the first genuine report send. Expected business state, not a defect.
2. MAIL-003's hardening (commit `c432bc9a`, no-throw on unapproved mailbox) is merged to dev but **not** on origin/main — ships next release; currently moot since the mailbox is approved.
3. The 2026-08-19 Sent-evidence approval is evidenced by live `ApprovedMailboxes` state, not by a runbook/operations entry — a one-line docs note is recommended in the next docs pass.
