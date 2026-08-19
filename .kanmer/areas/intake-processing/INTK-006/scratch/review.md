## INTK-006 PR review — 2026-08-19

Reviewer/author disclosure: the implementation and this first-pass review are by Codex; this is disclosed for independent human review.

Review set:
- PR #417, commits `70d7c89c`, `866d305e`, `599bfe6d`.
- Diff checked against the INTK-005 grouped-upload base.
- Ticket files.md, plan.md, checklist.md, open-questions.md, and post-implementation-report.md read.
- The plan's narrowed boundary is honoured: grouped recognition, detector/recognizer diagnostics, stable aggregation, unique existing-Case association, and ImageIntake hand-off only. Principal-less formal Case creation is not implemented.
- The explicit `conflicting_vrms` routing decision now routes to Unidentified; one usable VRM without a unique eligible Case hands off to the existing ImageIntake registration path.
- The single-file upload regression found after the initial PR was fixed in `866d305e`; the policy terminology/reason fix is in `599bfe6d`.

Evidence:
- Release build passed before the final policy-only change; targeted Core policy tests pass (3).
- Earlier targeted SQL/web/group verification passed (5 tests) after `866d305e`.
- PR #417 is currently merge-conflicting because its INTK-005 parent is still an open PR; PR #416 is mergeable and has green unit/browser/documentation/reference checks but historical SQL integration failures. Rebase/merge coordination remains required before PR #417 can merge.

Disposition:
- Fixed in PR: single-file preservation, migration expectation, explicit conflicting-VRM reason, ImageIntake hand-off naming.
- Follow-on tickets: [[INTK-007]] owns durable Unidentified U<n> persistence; [[INTK-008]] owns Image-initiated lifecycle/search/history/Box/merge/staff closure.
- No unowned blocking scope remains for this narrowed ticket, but merge must wait for the INTK-005 base review/rebase and a fresh green CI run.

Verdict: review evidence is conditionally pass for the narrowed implementation; merge is pending dependency rebase and green CI.
