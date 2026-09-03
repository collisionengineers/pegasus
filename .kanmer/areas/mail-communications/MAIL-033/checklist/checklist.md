# Checklist — MAIL-033

Adoption of PR #641. Expected repository diff: none. Plan: `plan`@`e289d8dd1ebe93c1`.

- [x] Step 1 — Assert the worktree `C:\Users\PGUSER\Documents\github\pegasus-worktrees\mail-029-graph-received-datetime`: `--show-toplevel`, both `--git-common-dir` values, `branch --show-current` = `task/mail-029-graph-received-datetime`.
- [x] Step 1 — `git fetch origin dev` then `git rev-list --left-right --count origin/dev...HEAD` shows 0 behind; if not, `git merge --no-edit origin/dev`, push, and record a fresh green check run.
- [x] Step 2 — Confirm from `gh pr diff 641` that a sparse entry is skipped before `client.ReadMimeAsync`, so no MIME fetch happens (ticket Verification box 1).
- [x] Step 2 — Confirm the delta link remains the only cursor owner: `pageCursor` derives from `consumed`, and `MailboxIntake.PollOneAsync` persists `page.NextCursor` in `pollStore.CompleteAsync` after the page loop (box 2).
- [x] Step 2 — Confirm the `Removed` skip, the exact-folder assertion, the Deleted Items throw and `GraphApprovedSentSource` are unchanged (box 3).
- [x] Step 2 — Confirm no new failure-classification path: no quarantine, exception type or health-surface addition; `InvalidDataException` is only narrowed (box 4).
- [x] Step 3 — `dotnet build ./Pegasus.slnx --configuration Release --no-restore` in the worktree exits 0 (compiler feedback only).
- [x] Step 4 — No repository file was edited; if one was, it is inside the two Expected files and is reported as a deviation with its command and exit code.
- [x] Step 5 — PR #641 title is exactly `Advance the Graph delta cursor when sparse messages omit receivedDateTime (MAIL-033)`.
- [x] Step 5 — The PR body's trailing footer reads `Kanmer: MAIL-033` and every other body line is byte-identical to before.
- [x] Step 6 — `update_item` MAIL-033 records both commit SHAs (`712bfcf3…`, `c6842a8c…`) and `prs: ["641"]`, with `expected_updated` and `expected_project`.
- [x] Step 7 — The simplification pass ran over `gh pr diff 641` (reuse, simplification, efficiency, altitude) and its findings and dispositions are appended to the plan under a dated `## Simplification pass` heading; unapplied findings are named with a reason or a ticket.
- [x] Step 8 — The `post-implementation-report` is written from the kanmer-execute template, records the head SHA and every check result, states why the commit trailers still read `Kanmer: MAIL-029`, and names the two parked risks.
- [x] Step 9 — `get_doc_gates` shows `enter-review` passable, then `move_item` MAIL-033 → `review` with `expected_updated`. Stop there: no merge, no second boundary, no other ticket.

## Progress notes

- 2026-09-02 (implementer a1, attempt 1): worktree assertions passed — `--show-toplevel` is
  the recorded worktree, both `--git-common-dir` values name the one primary repository, the
  branch is `task/mail-029-graph-received-datetime`, the tree is clean, HEAD `c6842a8c`.
- `git fetch origin` then `git rev-list --left-right --count origin/dev...HEAD` gave `0` / `2`:
  0 behind `origin/dev` (`9b8f78a3`), so **no merge was needed** and the head is unchanged.
- Read-only verification against `gh pr diff 641` (+72 / -3 across the 2 planned files):
  - box 1 — the `if (item.ReceivedAtUtc is null)` `continue` sits at line 637 and
    `client.ReadMimeAsync` at line 651, so the skip precedes every MIME fetch;
  - box 2 — `consumed = cursor.SkipCount + available.Length` (line 668) is independent of how
    many items were skipped, and `MailboxIntake.PollOneAsync` persists `page.NextCursor` in
    `pollStore.CompleteAsync` at line 489, after the page loop, while `ValidatePage` (line 661)
    accepts an empty `Messages` list with a non-empty cursor;
  - box 3 — the `Removed` skip, the exact-folder `UnauthorizedAccessException` (lines 288-292),
    the Deleted Items `InvalidDataException` (lines 431-434) and `GraphApprovedSentSource`
    (which never reads `ReceivedAtUtc`) are all untouched by the diff;
  - box 4 — `ReceivedDateTimePresent` appears at only 2 sites, both in the changed file; no
    quarantine, exception type or health surface was added, and `InvalidDataException` is the
    pre-existing type, narrowed to the present-but-unparseable case.
- FRD-08 lines 284-345 read directly: the per-mailbox durable cursor, "one mailbox's failure
  or backlog never affects another", the Worker as sole owner of "the mailbox lease,
  cursor/delta read", "maintain a durable cursor/checkpoint and idempotent occurrence
  processing" and mail before activation that "advances the cursor but is not retained" are all
  present as the plan quotes them. No FRD sentence is contradicted by the skip.
- Build: `dotnet build ./Pegasus.slnx --configuration Release --no-restore` first exited **1**
  (NETSDK1004, 7 cold `project.assets.json` files); `dotnet restore ./Pegasus.slnx
  --locked-mode` exited 0; the re-run exited **0** with 0 warnings and 0 errors. The first
  failure is kept, not replaced.
- **No repository file was edited.** `git status --porcelain` is empty at `c6842a8c`.
- `gh pr checks 641` at this head: unit, browser, sql-integration (1..3),
  sql-integration-coverage, test-ui, changes, documentation, local-development-scripts and
  reference-data all **pass**; infrastructure **skipping**. `mergeStateStatus: CLEAN`, base `dev`.
- Steps 5-9 (PR retitle and re-footer, `update_item`, the simplification pass, the report and
  the move) are held until the controller returns a PASS from the test runner.
- 2026-09-02 (implementer a1, after the controller's PASS): local rail 1-restore / 2-build /
  3-core-tests (1185 passed) / 4-architecture-tests (100 passed) all PASS; 5-sql-integration
  INCONCLUSIVE (LocalDB absent, error 52) with CI run `33525322197` as the recorded evidence for
  that lane. Neither new test appears among the local failures.
- Simplification pass appended to `plan`@`74a5ddf0a5ef2ece`: F1 reuse (duplicate
  `receivedDateTime` probe in `ParseItem`), F2 simplification (nested throw, judged a wash),
  efficiency no finding (net-negative work: a skipped entry no longer fetches MIME), altitude no
  finding, tests no finding. Nothing applied — a code change is a deviation on this adoption.
- F3 (an explicit JSON `null` for `receivedDateTime` throws rather than skipping) and
  ASSUMPTION 1 (the `gh pr edit` scope failure and the REST substitution) recorded in
  `open-questions`@`568172e0a56fb947`; `questions-resolved` re-checked and still satisfied.
- PR #641: title now `... (MAIL-033)`; body footer `Kanmer: MAIL-033` with exactly one line
  differing from the pre-edit original. `gh pr edit` exited 1 (missing `read:project` scope), so
  the same two fields went through `gh api -X PATCH .../pulls/641` (exit 0). Base `dev`, head
  `c6842a8c`, `CLEAN`, unchanged.
- Board: `commits` = `712bfcf3…`, `c6842a8c…`; `prs` = `["641"]`; the ticket body's four
  Verification boxes ticked against the evidence in the report.
- `post-implementation-report`@`e04eede6877ed0f9` written whole-file. Nothing was pushed and no
  commit was made: the head is the same `c6842a8c` the tests ran against.

## Closeout — MAIL-033

- [x] PR merge verified ([#641](https://github.com/collisionengineers/pegasus/pull/641), merged 2026-09-02T02:52:43Z)
- [x] proof.md finalised with PR URL and merge date
- [x] Moved to final stage
- [x] Outcome recorded in ticket body
- [ ] cd out of worktree; remove recorded ticket worktree
- [ ] delete local branch (and merged remote branch)
- [ ] prune stale worktree metadata
- [ ] release ticket claim

### Closeout completion — 2026-09-03

- [x] Recorded implementation and verification worktrees removed cleanly.
- [x] Local ticket branch deleted.
- [x] Merged remote ticket branch deleted and remote refs pruned.
- [x] Ticket claim released after Git cleanup.
