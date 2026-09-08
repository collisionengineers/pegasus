# Checklist — CASE-031

- [ ] Extend the existing Core API payload/mapping and one submission policy for accepted canonical claimant address, with focused status/precedence/value tests and API-only mapping version 2.
- [ ] Wire exact ClmAdd and the actual EvaSubmissionStore guard after known replay, before image/transport work; prove no calls/mutations for invalid values and replay while retaining manual outcome/version/lease tests.
- [x] After root clears FRD-07 ownership, document only the API prerequisite and complete the bounded source/consumer/ZIP-unchanged review.
- [ ] Freeze for root's exact focused commands; retain every failure/pass, complete the post-implementation report and stop for independent review.

## Progress notes

2026-09-08: Preparing-only refresh replaces the obsolete seven-step plan.
Extraction, receipt/Case persistence and UI already exist. ADR-0038 supersedes
the old automatic API promise. No claim, source edit, build, test or live call
was performed; root must sequence FRD-07 with TICK-085 before execution.

2026-09-08: Ten-file implementation and focused tests are written; source is frozen for root. Runtime-proof boxes remain unchecked until actual root results. No author build/test/push/PR or live call.
