---
id: KANMER-003
type: ticket
title: Reconcile Pegasus setup with packaged Kanmer 0.3.3
status: review
area: kanmer-meta
assignee: codex
profile: custom
requires: {}
stageEntered:
  review: '2026-08-17T05:45:24.646Z'
taken_at: '2026-08-17T05:41:33.106Z'
branch: task/kanmer-003-setup-reconcile
worktree: ../pegasus-worktrees/kanmer-003-setup-reconcile
labels: []
links: []
commits:
  - ed5370da
prs:
  - '#382'
archived: false
created: '2026-08-17T05:41:23.840Z'
updated: '2026-08-17T05:45:24.646Z'
---

## What

Re-run `kanmer-setup` against the existing format-3 Pegasus board after the packaged Kanmer 0.3.3 update: refresh the managed AGENTS block and provider skill installations without re-ingesting established work.

## Why

`get_status` reports the managed block and installed provider skills behind, with `.claude/skills` unstamped and `.grok/skills` retaining retired files.

## Verification

- Migration dry-run remains a no-op.
- Managed AGENTS content matches packaged setup instructions.
- Provider skill trees match the packaged skill bundle and retired Kanmer-owned files are absent.
- Final `get_status` is reported, including anything that remains local-only or needs GUI reconnect.

## Outcome
