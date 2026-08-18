# Checklist — TICK-026

- [x] Create worktree `../pegasus-worktrees/tick-026-mcp-04-document-evidence` on `task/tick-026-mcp-04-document-evidence` from `origin/dev` and take the ticket
- [x] Add HTTP success path: lease begin, document add, operation-key replay, download inline, download oversize notice
- [x] Add HTTP export success after moving the seeded case to Review; assert ActionHistory Succeeded
- [x] Add validation refusals (bad role, missing lease, empty export selections) with Failed history and no leaked token
- [x] Add `automation.documents` scope denial on `pegasus_document_add`
- [x] Run focused `AutomationMcpIngressTests` Release and record the output
- [x] Write post-implementation-report, push, open PR to `dev`, move ticket to Review

## Progress notes

Worktree created at `../pegasus-worktrees/tick-026-mcp-04-document-evidence` on `task/tick-026-mcp-04-document-evidence`; ticket taken. Adding HTTP caller tests.

Focused Release run: 9 passed, 0 failed (`dotnet test … --filter FullyQualifiedName~AutomationMcpIngressTests`). Export requires a fresh `pegasus_case_edit_begin` after add because `CaseMutationGuard.Complete` clears the lease.

Pushed `task/tick-026-mcp-04-document-evidence` and opened https://github.com/collisionengineers/pegasus/pull/393 targeting `dev`.

## Closeout — TICK-026 (2026-08-18)

- [x] PR #393 MERGED 2026-08-18T11:12:20Z (`6cf9b166`)
- [x] proof.md rewritten from the merged-main run (15/15); moved to Done; Outcome recorded; deployment = production (release 9)
- [x] Worktree `../pegasus-worktrees/tick-026-mcp-04-document-evidence` removed; local + remote branch deleted; prune
- [x] Released
