---
ticket: MAIL-016
merged_main: 1ec65dc894f121f4bb5b31ae82c818a401d08beb
pr: 567
proof_type: command-log
date: 2026-08-27
---

# Proof — MAIL-016

Written on merged `main` (`1ec65dc8`, promoted 2026-08-27 as release 34
source).

- PR #567 (`78c734cc`) merged to `dev` as `cfb03e8a`; `main` fast-forwarded to
  `1ec65dc8`, which contains it.
- `git show origin/main:tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs`
  line 178 asserts `$"mailbox={FirstMailboxFilter}"`.
- PR #567 CI run 33053995316: `sql-integration (1)` **pass** on rerun (first
  attempt failed on an unrelated SQL post-login timeout in
  `CaseWorkflowPersistenceTests`, 307 passed / 1 failed; the mailbox test
  passed in both attempts); shards 2 and 3, unit, browser: pass.
- Subsequent PRs #566 and #562 went green on shard 1 only after merging this
  fix; before it, both failed on `Not found: "mailbox=instructions"`.

Verdict: **PASS**.
