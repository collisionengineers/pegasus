# Checklist — MAIL-033

Adoption of PR #641. Expected repository diff: none. Plan: `plan`@`e289d8dd1ebe93c1`.

- [ ] Step 1 — Assert the worktree `C:\Users\PGUSER\Documents\github\pegasus-worktrees\mail-029-graph-received-datetime`: `--show-toplevel`, both `--git-common-dir` values, `branch --show-current` = `task/mail-029-graph-received-datetime`.
- [ ] Step 1 — `git fetch origin dev` then `git rev-list --left-right --count origin/dev...HEAD` shows 0 behind; if not, `git merge --no-edit origin/dev`, push, and record a fresh green check run.
- [ ] Step 2 — Confirm from `gh pr diff 641` that a sparse entry is skipped before `client.ReadMimeAsync`, so no MIME fetch happens (ticket Verification box 1).
- [ ] Step 2 — Confirm the delta link remains the only cursor owner: `pageCursor` derives from `consumed`, and `MailboxIntake.PollOneAsync` persists `page.NextCursor` in `pollStore.CompleteAsync` after the page loop (box 2).
- [ ] Step 2 — Confirm the `Removed` skip, the exact-folder assertion, the Deleted Items throw and `GraphApprovedSentSource` are unchanged (box 3).
- [ ] Step 2 — Confirm no new failure-classification path: no quarantine, exception type or health-surface addition; `InvalidDataException` is only narrowed (box 4).
- [ ] Step 3 — `dotnet build ./Pegasus.slnx --configuration Release --no-restore` in the worktree exits 0 (compiler feedback only).
- [ ] Step 4 — No repository file was edited; if one was, it is inside the two Expected files and is reported as a deviation with its command and exit code.
- [ ] Step 5 — PR #641 title is exactly `Advance the Graph delta cursor when sparse messages omit receivedDateTime (MAIL-033)`.
- [ ] Step 5 — The PR body's trailing footer reads `Kanmer: MAIL-033` and every other body line is byte-identical to before.
- [ ] Step 6 — `update_item` MAIL-033 records both commit SHAs (`712bfcf3…`, `c6842a8c…`) and `prs: ["641"]`, with `expected_updated` and `expected_project`.
- [ ] Step 7 — The simplification pass ran over `gh pr diff 641` (reuse, simplification, efficiency, altitude) and its findings and dispositions are appended to the plan under a dated `## Simplification pass` heading; unapplied findings are named with a reason or a ticket.
- [ ] Step 8 — The `post-implementation-report` is written from the kanmer-execute template, records the head SHA and every check result, states why the commit trailers still read `Kanmer: MAIL-029`, and names the two parked risks.
- [ ] Step 9 — `get_doc_gates` shows `enter-review` passable, then `move_item` MAIL-033 → `review` with `expected_updated`. Stop there: no merge, no second boundary, no other ticket.

## Progress notes
