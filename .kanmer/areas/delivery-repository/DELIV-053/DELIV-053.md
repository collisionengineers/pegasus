---
id: DELIV-053
type: ticket
title: Pin reusable Codex subagents and serialize host verification
status: implementing
area: delivery-repository
assignee: codex-mcp-client
profile: fix
stageEntered:
  preparing: '2026-09-08T13:17:57.073Z'
taken_at: '2026-09-08T13:21:11.661Z'
branch: DELIV-053-codex-agents
worktree: .worktrees/deliv-053
claim_expires_at: '2026-09-08T14:29:03.610Z'
claim_controller: codex-mcp-client
lease_id: 820e4f1a-1e6e-4d12-82be-f2082dffa481
lease_revision: 6
lease_workspace: 'worktree:c:\users\alex\documents\github\pegasus\.worktrees\deliv-053'
lease_phase: implementing
lease_heartbeat_at: '2026-09-08T13:59:03.610Z'
labels:
  - codex
  - agent-configuration
  - verification
links: []
refs:
  - docs/engineering.md
deployment: n/a
archived: false
created: '2026-09-08T13:17:44.781Z'
updated: '2026-09-08T13:59:03.610Z'
---

## What

Pin five reusable project-local Codex agents (Luna scout, Terra investigator and implementer, Sol reviewer and verifier), an eight-subagent ceiling, and one host-wide owner for every test/build execution.

## Why

The operator approved the corrective-release/subagent plan on 8 September 2026 and requested implementation. Current .codex/config.toml is ignored and contains the existing Kanmer registration, with no reusable agent definitions. Independent work should be faster without concurrent host test/build processes or weakened review independence.

## Approach

- Track project agent configuration while preserving the non-secret Kanmer registration and board-branch convention.
- Add five standalone agent TOML definitions with exact model/reasoning settings and bounded role instructions.
- Update the unmanaged AGENTS.md section with delegation and exclusive verification ownership rules; cite current OpenAI documentation.
- Validate configuration and actual named-agent discovery without application builds or cloud writes.

## Verification

- [ ] Strict Codex configuration diagnostics and named-agent discovery/model evidence.
- [ ] Confirm tracked configuration preserves Kanmer and contains no secrets.
- [ ] Independent semantic/configuration review; no parallel tests/builds.

## Outcome
