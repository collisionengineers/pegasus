---
id: KANMER-010
type: ticket
title: Reconcile Kanmer setup drift after KANMER-006
status: backlog
area: kanmer-meta
assignee: ''
profile: chore
labels:
  - kanmer
  - setup
  - phase-0
links:
  - KANMER-006
  - KANMER-009
deployment: n/a
archived: false
created: '2026-09-01T14:40:45.085Z'
updated: '2026-09-01T21:54:48.842Z'
---

## What

Run the current Kanmer setup reconciliation for the drift reported after KANMER-006 against the active 0.3.12 server (`639df4cf`).

## Why

`get_status` on 2026-09-01 (repo root at `fb3f07ac`) reports four `behind` artefacts and one compensated one:

- `agents-block`: the managed `AGENTS.md` block differs from the one 0.3.12 ships.
- `skills` `.agents/skills`: 15 files differ from the bundled skills and 10 are missing.
- `skills` `.grok/skills`: 15 files differ.
- `mcp-registration`: `opencode.json` registers Kanmer against `C:\Users\PC\Documents\GitHub\pegasus\.worktrees\kanmer`, another workstation's board.
- `board-config`: compensated, informational, no manual fix.

KANMER-006 truthfully closed an earlier slice (PR #638) and must not be reopened or rewritten. The direct dev commit `9b8f78a3` ("carry the board branch by env, not by cwd") is the accepted baseline this ticket reconciles from.

## Approach

- Use the `kanmer-setup` skill (bundled 0.3.12 text) in the ticket worktree `../pegasus-worktrees/kanmer-010-setup-drift` on `task/kanmer-010-setup-drift` from `origin/dev`; PR to `dev`, precedent PR #638.
- Refresh the managed block with `node <plugin-root>/scripts/agents-block.mjs <repo>`; preserve every repository-owned line outside the block, including the Repository task workflow section that overrides the block's worktree text.
- Refresh `.agents/skills` and `.grok/skills` from the bundled tree so `get_status` compares clean; keep `.kanmer-skills-version` stamps consistent with what the app writes.
- Report the machine-specific `opencode.json` registration rather than committing another workstation's path; the fix is "reconnect this project in the Kanmer app" on the host that uses it.
- Do not edit the board branch or `.worktrees/kanmer` manually; no product code changes.

## Verification

- [ ] `get_status.repo.stale` has no `behind` entry except any the operator explicitly defers (the `opencode.json` registration if it belongs to another host).
- [ ] Repository instructions outside the managed block are unchanged (`git diff` shows the block and skill trees only).
- [ ] The board worktree remains healthy and no ticket data is lost.
- [ ] Merge SHA recorded and reachable from `origin/dev`.

## Outcome
