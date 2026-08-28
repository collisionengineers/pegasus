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
