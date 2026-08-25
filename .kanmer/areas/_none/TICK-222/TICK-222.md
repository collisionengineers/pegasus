---
id: TICK-222
type: ticket
title: Reconcile the two MCP path files blocking the ENG-016 release
status: implementing
area: ''
assignee: codex-mcp-client
profile: chore
stageEntered:
  preparing: '2026-08-25T00:55:21.078Z'
taken_at: '2026-08-25T00:55:33.099Z'
branch: task/tick-222-reconcile-mcp-config
worktree: 'C:\Users\PC\Documents\GitHub\pegasus-worktrees\tick-222-reconcile-mcp-config'
labels:
  - release-blocker
links:
  - ENG-016
docs_todo: true
commits:
  - 5e8ceff0
  - c56f00f8
prs:
  - '540'
deployment: not-deployed
archived: false
created: '2026-08-25T00:55:12.141Z'
updated: '2026-08-25T00:56:55.636Z'
---

`main` contains commit `c9005efb`, which changes only `.codex/config.toml` and `.mcp.json` from the obsolete `C:\Users\Alex` paths to this workstation's actual `C:\Users\PC` Kanmer executable, board worktree, and repository paths. Bring that commit's ancestry into `dev` through one non-force merge commit and a normal reviewed green PR, so the exact-SHA release can fast-forward. No product, schema, infrastructure, or Kanmer board-root change.
