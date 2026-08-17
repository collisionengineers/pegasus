# Checklist — TICK-026

- [ ] Create worktree `../pegasus-worktrees/tick-026-mcp-04-document-evidence` on `task/tick-026-mcp-04-document-evidence` from `origin/dev` and take the ticket
- [ ] Add HTTP success path: lease begin, document add, operation-key replay, download inline, download oversize notice
- [ ] Add HTTP export success after moving the seeded case to Review; assert ActionHistory Succeeded
- [ ] Add validation refusals (bad role, missing lease, empty export selections) with Failed history and no leaked token
- [ ] Add `automation.documents` scope denial on `pegasus_document_add`
- [ ] Run focused `AutomationMcpIngressTests` Release and record the output
- [ ] Write post-implementation-report, push, open PR to `dev`, move ticket to Review

## Progress notes

Worktree created at `../pegasus-worktrees/tick-026-mcp-04-document-evidence` on `task/tick-026-mcp-04-document-evidence`; ticket taken. Adding HTTP caller tests.
