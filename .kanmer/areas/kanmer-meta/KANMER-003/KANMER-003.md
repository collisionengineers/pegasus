---
id: KANMER-003
type: ticket
title: Reconcile Pegasus setup with packaged Kanmer 0.3.3
status: done
area: kanmer-meta
order: 530
assignee: codex
profile: custom
requires: {}
stageEntered:
  review: '2026-08-17T05:45:24.646Z'
  verifying: '2026-08-17T05:47:59.138Z'
  done: '2026-08-17T05:48:38.793Z'
labels: []
links: []
commits:
  - ed5370da
  - 7af202c7
  - 746401435892a76e4efb532ef2f3c41d26270590
prs:
  - '#382'
deployment: n/a
archived: false
created: '2026-08-17T05:41:23.840Z'
updated: '2026-09-01T14:44:32.137Z'
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

Merged PR #382 into `dev` at `746401435892a76e4efb532ef2f3c41d26270590`. Refreshed the managed AGENTS instructions and synchronized the tracked `.agents` and `.grok` Kanmer skill trees with packaged 0.3.3, removing retired Kanmer-owned files. Existing board work was not re-ingested. Post-merge proof records exact blob comparison and the remaining local-checkout divergence.
