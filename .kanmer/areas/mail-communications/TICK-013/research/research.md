# Research — MAIL-14 exact Sent-item detection (retrospective backfill, VERIFY2 lane, 2026-08-20)

**Read-only verification backfill** — the capability was already implemented and shipped before this ticket was worked. Verdict: **implemented + deployed + live poll evidence; live-detection residual named** (no real report has ever been sent).

## Capability vs code

Capability row (`docs/capabilities.md`): "MAIL-14 | Detect an exact Outlook Sent item as report-sent evidence | Now | 0.1.0-alpha.1 | outbound-correspondence-evidence | Allocated but non-blocking for 0.1.0-alpha.1 acceptance; post-report tracking starts manual via MAIL-15."

Owning FRD (`docs/frd/frd-08-email-mailbox-and-background-processing.md#outbound-correspondence-evidence`) requires: one exact immutable Sent item from an allowlisted mailbox associated with exactly one Case; retained mailbox/folder scope, immutable item + conversation/reply-chain identities, authoritative `sentDateTime`, separate discovery/link times, actor/matcher identity; ambiguous/absent matches stay unconfirmed; confirmation proves existence in approved Sent scope only.

Code (all present on origin/main = 2325ed4a ancestor path):
- `src/Pegasus.Core/Workflow/PollSentEvidence.cs` — `ExecuteAsync` (~L175–326): lease claim via `pollStore.ClaimAsync`, approval check `approvedMailboxPolicy.IsApprovedAsync(..., ApprovedMailboxRouteScope.SentEvidence, ...)` (L214), pages `sentSource.ReadAsync`.
- Exact detection: `HandleItemAsync` (~L328–543) matches `InReplyToIdentities` against `responseEvidenceQueries.FindExactCandidatesAsync` filtered by `ReplyChainIdentity` (L407–419); non-exact/ambiguous items are never linked.
- Persistence: `CaseReportSentEvidence` table (migration `20260729160000_CaseWorkflowRuntime.cs`) retains MailboxIdentity, ImmutableItemIdentity, ConversationIdentity, ReplyChainIdentity, SentAtUtc, LinkedAtUtc, LinkedBy* — matching the FRD's retained-identity contract. Poll state in `ApprovedSentPollStates` / outcomes in `ApprovedSentPollOutcomes` (migration `20260729183000_SentEvidencePolling.cs`).
- Worker caller: `SentEvidencePollFunction` in `src/Pegasus.Worker/EmailEvidenceFunctions.cs`, schedule `15 * * * * *`, confirmed enabled in production.

## Tests

- `tests/Pegasus.Core.Tests/Workflow/PollSentEvidenceTests.cs` — detection/link/ambiguity branches.
- `tests/Pegasus.IntegrationTests/SentEvidencePollPersistenceTests.cs` — `ExactReplyPollAtomicallyLinksTriageAndReplayAllowsStaffCompletion`, `LeaseCursorAndOutcomeReplayRemainDurable`.

## Live production evidence (read-only SQL, 2026-08-20, pegasus-prod-sql-252ow37gij/pegasus)

- `ApprovedSentPollStates`: 1 row, `instructions@collisionengineers.co.uk`, **`LastCompletedAtUtc = 2026-08-20 05:39:15Z`, `LastFailureCode = NULL`** — the poll is running and completing now.
- `ApprovedMailboxes` (that address): `AllowSentEvidence = True`, `State = Approved`, Version 4 — Sent-evidence approval is live (granted 2026-08-19; production data was `AllowSentEvidence = false` 2026-08-10 → 2026-08-19 per the MAIL-003 commit narrative).
- `ApprovedSentPollOutcomes`: exactly 1 row ever — `Unmatched`, 2026-08-01 23:56Z. `CaseReportSentEvidence`: **0 rows**.

## Residuals (named, not defects)

1. **No genuine report-sent item has ever been detected** because no report has ever been sent through the approved mailbox — `CaseReportSentEvidence` is empty; the single historical poll outcome is `Unmatched`. Detection capability is live and waiting on real business traffic.
2. **MAIL-003 hardening not yet released**: commit `c432bc9a` ("Stop PollSentEvidence throwing on an unapproved Sent-evidence mailbox", merged to dev 2026-08-20) is NOT an ancestor of origin/main — production still carries the throw-on-unapproved branch. Currently moot (the mailbox is approved and the poll completes cleanly), ships with the next release.
3. **The 2026-08-19 operator approval is not recorded in `docs/runbook.md`** — evidence is the live `ApprovedMailboxes` state and the MAIL-003 commit message narrative only. Worth a one-line runbook/operations note in a future docs pass.

Premises verified read-only: all file/line quotes via `git show origin/dev` and `git ls-tree origin/main`; SQL via Azure AD token, SELECT only. Assumed: none material.
