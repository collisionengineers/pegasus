### 2026-08-28 review-fix pass

Branch head after the pass: 56ce7898 (on top of 48baaa75, the clean
`origin/dev` merge). Three fix/test commits: 17930a17 page, 0f80c363
image state, 56ce7898 tests. Pushed; PR #606 body rewritten with the
findings table and the dispositions.

Stage left at `review` — the ticket was already there and every gate for
that boundary is satisfied; `proof` stays for after the merge.

Notes for whoever picks this up:

- `gh pr edit` fails here with "authentication token is missing required
  scopes [read:project]"; `gh api -X PATCH repos/.../pulls/606` works and
  is what updated the body.
- One `dotnet test` run over `Pegasus.slnx` aborted with "Test host
  process crashed" before any test reported. The same command re-run
  passed 5/5, and every per-project run passed. Not reproduced.
- Snapshot gate will fail until `TestUiSnapshotTests.cs:29`'s
  `cases--unavailable` state match stops requiring the pre-port
  `<h2>Cases are unavailable</h2>` (UIIMP-005's file).

## 2026-08-29 — proof written; HELD in Verifying, not moved to Done

`proof/proof.md` is written against merged `dev` at `b92cb9a7` (D15). The
`enter-done` gate is satisfied (proof exists, no open questions), so the hold
is a judgement call, not a missing document.

**Why it is held — finding F1.** `Pages/Search/Index.cshtml:272-317` is an
inline `@section Scripts` block, and `git grep -ln "@section Scripts" --
src/Pegasus.Web/Pages/` returns that file and no other in the application.
`Program.cs:811-828` sets `default-src 'self'` (no `unsafe-inline`, no nonce,
no hash) outside Development, and `site.js:4-7`, `site.js:766-767` and
`docs/operations.md:655-658` all state the consequence in the repository's own
words: an inline script is silently discarded in Production. Every web and
browser test runs under `"Development"`
(`IntakeWebTestSupport.cs:59`), where the header is never set, so nothing in
the suite can catch it — the same trap DOCS-011 hit.

Effect: the plan records review findings **P1** ("Copy Case/PO copies the
previous selection") and **P2** ("refresh's hidden `selected` goes stale") as
*Fixed*. They are fixed in Development only. Deployed, the shell's
row-selection module still swaps the preview (it lives in `site.js`, a file),
but Copy Case/PO keeps copying the previously loaded row's reference and
Refresh keeps reopening the previous row. Server-rendered first load and any
`?selected=` round trip are correct.

`CasesIndexWebTests.cs:144-151` asserts the markup only, so it stays green.

The durable fix named in the plan is right and is not this lane's file:
delegate `[data-copy-target]` binding in `wwwroot/js/site.js` (PLAT-029's),
which removes the need for a page script entirely. Needs an operator/
orchestrator disposition — fix here, or ticket it and accept the Production
gap — before Done.

No source file was changed by this proof pass (read-only).

Also confirmed still open at `b92cb9a7`, as the ticket says: [[UIIMP-011]]
(`TestUiSnapshotTests.cs:28-29` both still match pre-port markup) and
[[PLAT-059]] (`_ShellDialogs.cshtml:64`, `site.js:1364`).
