## Independent review — PR #439 (orchestrator, 2026-08-20)

Verdict: **pass**, with one verify-stage obligation.

- The lane corrected the ticket's premise with evidence: the production rejections were *correct* — the ApprovedMailboxes row (49f47eb9…) had AllowSentEvidence=false for the SentEvidence route scope from 2026-08-10 until the 2026-08-19 approval; the diagnostics read the current (approved) row and inferred a comparison bug that wasn't there. The real defect: an expected administrative state threw an unhandled exception every poll tick (~2,080/48 h).
- Fix follows the file's own "not due yet" idiom: release the lease with the standard 30 s failure-retry backoff and the existing `sent_mailbox_not_approved` failure code, return an empty tick. Constant extracted so the switch and the release can't drift. Regression test mirrors the production row shape and pins release timing + code, and that the source is never called.
- **Verify-stage obligation**: after release 14, confirm PollSentEvidence now completes real polls (poll store completions/cursor advancing in SQL — App Insights is capped until the window rolls), proving the post-approval path works end to end. If completions do not advance, there is a second, real bug to chase.
