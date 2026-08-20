# Research — MAIL-16 auto-match Sent item to case (retrospective backfill, VERIFY2 lane, 2026-08-20)

**Read-only verification backfill** — implemented and shipped before this ticket was worked. Shares the detection pipeline verified in [[TICK-013]] (MAIL-14); this document records only the auto-match half. Verdict: **implemented + deployed; zero live auto-links yet (no report ever sent) — residual named.**

## Capability vs code

Capability row: "MAIL-16 | Automatically match the exact report Sent item to its case | Now | 0.1.0-alpha.1 | outbound-correspondence-evidence | Allocated but non-blocking for 0.1.0-alpha.1 acceptance; post-report tracking starts manual via MAIL-15."

FRD contract (same section as MAIL-14): "When automatic matching is absent, ambiguous, late, duplicated, or conflicting, the item remains unconfirmed until any authorised staff member reasonedly links the exact item."

Auto-match logic (`src/Pegasus.Core/Workflow/PollSentEvidence.cs`, on origin/main):
- `HandleItemAsync` ~L479: when there are no exact Triage reply candidates and `provenance.AuthoritativeCaseIdentities.Distinct()` yields case identities, report evidence is retained via `retainReportEvidence.ExecuteAsync` (L481–497).
- **Exactly one** case identity → `autoLinkReportEvidence.ExecuteAsync` (L502–509) → outcome `ReportEvidenceAutoLinked`; link persisted to `CaseReportSentEvidence` with matcher identity and separate discovery/link times.
- Ambiguous (>1 identity) → outcome `Ambiguous`, retained **unlinked** and visible — matching the FRD's stay-unconfirmed rule; staff link is the fallback (MAIL-15 manual path).

## Tests

- `PollSentEvidenceTests.ExactCaseIdentityAutoLinksRetainedReportEvidence` — single `ReportEvidenceAutoLinked` outcome, `AutoLinkPort` invoked with the correct CaseId/EvidenceId.
- `PollSentEvidenceTests.IneligibleExactCaseRetainsReportEvidenceVisibleAndUnlinked`, `AmbiguousCaseIdentitiesRetainOneVisibleUnlinkedReportItem` — non-link branches.
- `AutoLinkReportEvidenceTests` — link operation itself.
- Note: the case auto-link path is unit-tested only; the integration suite (`SentEvidencePollPersistenceTests`) exercises the Triage reply path and lease/cursor durability, not case auto-link end-to-end.

## Live production evidence (read-only SQL, 2026-08-20)

Same poll instance as [[TICK-013]]: `ApprovedSentPollStates.LastCompletedAtUtc = 2026-08-20 05:39:15Z`, no failure code, mailbox Approved with `AllowSentEvidence = True`. `CaseReportSentEvidence` = **0 rows**; `ApprovedSentPollOutcomes` = 1 `Unmatched` row ever (2026-08-01).

## Residuals (named, not defects)

1. **No live auto-link has ever occurred** — no report has ever been sent through the approved mailbox, so the auto-match branch has never fired against real traffic. Unit-tested; live exercise awaits the first genuine report send.
2. Case auto-link lacks an end-to-end integration test (unit coverage only) — candidate for a future test ticket, not a delivery blocker.
3. Shares [[TICK-013]]'s residuals: MAIL-003 hardening (`c432bc9a`) on dev not yet on main; 2026-08-19 mailbox approval not recorded in the runbook.

Premises verified read-only as in [[TICK-013]].
