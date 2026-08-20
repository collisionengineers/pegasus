# Plan — MAIL-005

Branch `task/mail-005-inbox-case-links` from origin/dev (09b42a57), worktree `../pegasus-worktrees/mail-tiles`.

1. **Projection** (reuse: the `allocationStates` dictionary the summary query already builds): in `EfRetainedMailboxMessageStore`'s summary mapping, `caseId = linkedCase?.CaseId ?? allocationState?.CaseId` (and reference likewise) — `IntakeAllocationState.CaseId` is set only on a succeeded attempt, so failed attempts still read "Case not created". No new query, no schema change. `OutcomeLabel` needs no change: `{ CaseId: not null }` now matches.
2. **Tile layout**: the outcome `.stack` becomes a chip row — chip and reference link on one line, "No longer polled" beneath; small CSS rule.
3. **Test**: extend the retained-mail web tests — seed a mailbox receipt with decision `case_created`, a succeeded allocation attempt carrying a case, and **no** `CaseIntakeLinks` row; assert the Inbox row shows "Case created" and the reference link, not "Ready for case allocation".
4. Suites: retained-mail web tests + Release build 0/0.

The three zero-attempt pre-release-14 rows are data, not code: eliminated by the T9 wipe; the pipeline that could strand a decided receipt without an attempt was closed by the durable-intake reconciliation and CASE-005.

Deviation: subagents barred — self-review recorded.
