# Plan — CASE-003 (retrospective record)

The fix was delivered inside [[INTK-010]]'s upload-confirmation work (PR #433,
release 13) rather than as its own branch, because the create-from-upload offer
made `/Cases/Create?receiptId=` a production path and this 500 stood directly
in its way — the repository's rule that a supporting fix does not require its
own worktree applied. This document records the plan that was executed there
so the ticket's own gates reflect reality, not to re-plan finished work.

1. Guard `OnGetAsync` in `Cases/Create.cshtml.cs`: an empty `receiptId`
   returns `NotFound()` before any load — the approach this ticket's body
   specified. Reuses the page's existing not-found handling; no new pattern.
2. Cover it: `CaseCreateWebTests` asserts empty-receipt → 404.
3. Verification is the deployed behaviour, recorded in `proof`.

Simplification pass: n/a — two-line guard plus one test, delivered inside
PR #433's own reviewed and simplification-passed diff.
