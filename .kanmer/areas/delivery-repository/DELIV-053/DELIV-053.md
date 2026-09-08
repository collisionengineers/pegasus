---
id: DELIV-053
type: ticket
title: Pin reusable Codex subagents and serialize host verification
status: done
area: delivery-repository
assignee: codex-mcp-client
profile: fix
stageEntered:
  preparing: '2026-09-08T13:17:57.073Z'
  review: '2026-09-08T14:17:44.269Z'
  verifying: '2026-09-08T14:38:09.858Z'
  done: '2026-09-08T15:26:09.281Z'
taken_at: '2026-09-08T13:21:11.661Z'
branch: DELIV-053-codex-agents
worktree: .worktrees/deliv-053
claim_expires_at: '2026-09-08T15:46:17.818Z'
claim_controller: codex-mcp-client
lease_id: 820e4f1a-1e6e-4d12-82be-f2082dffa481
lease_revision: 11
lease_workspace: 'worktree:c:\users\alex\documents\github\pegasus\.worktrees\deliv-053'
lease_phase: verifying
lease_heartbeat_at: '2026-09-08T15:16:17.818Z'
labels:
  - codex
  - agent-configuration
  - verification
links: []
refs:
  - docs/engineering.md
commits:
  - ed20af4275d0312c963a6fc86c330f563141c98c
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/704'
deployment: n/a
delivery_state: integrated
delivery_branch: dev
delivery_sha: ed20af4275d0312c963a6fc86c330f563141c98c
delivery_recorded_at: '2026-09-08T15:27:37.692Z'
archived: false
created: '2026-09-08T13:17:44.781Z'
updated: '2026-09-08T15:27:37.692Z'
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

- [x] Strict Codex configuration diagnostics and named-agent discovery/model evidence.
- [x] Confirm tracked configuration preserves Kanmer and contains no secrets.
- [x] Independent semantic/configuration review; no parallel tests/builds.

## Outcome

PR #704 squash-merged into `dev` as
`ed20af4275d0312c963a6fc86c330f563141c98c`. The author commit
`f9f9cc0a9a66da15306b49ffa34f1d5b253c524d` remains provenance; the merged
SHA is the reachable integration record. The schema-2 proof records PASS for
strict configuration diagnostics (20 ok, 0 fail), actual fresh acceptance of
all five named role profiles, a fresh Kanmer connection, and 140 resolved
documentation links. Earlier harness failures remain retained in proof-linked
scratch evidence rather than being erased.

This integrated configuration change is non-deployable (`n/a`): no application
build/test, source edit, cloud write, promotion, or deployment occurred. The
canonical verifier host ledger in `scratch/verify` remains in place for the
separate D56 lane and was not altered.
