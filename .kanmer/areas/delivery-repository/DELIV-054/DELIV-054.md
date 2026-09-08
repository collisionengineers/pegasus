---
id: DELIV-054
type: ticket
title: Include hidden runtime directories in release ZIPs
status: done
area: delivery-repository
assignee: codex-mcp-client
profile: fix
stageEntered:
  preparing: '2026-09-08T13:27:02.890Z'
  review: '2026-09-08T13:47:12.527Z'
  verifying: '2026-09-08T13:58:49.526Z'
  done: '2026-09-08T14:25:48.558Z'
taken_at: '2026-09-08T13:31:44.019Z'
branch: DELIV-054-hidden-runtime-zips
worktree: .worktrees/deliv-054
claim_expires_at: '2026-09-08T14:47:21.139Z'
claim_controller: codex-mcp-client
lease_id: 2c3e2ccd-235a-47fa-b707-4b718c061e6a
lease_revision: 3
lease_workspace: 'worktree:c:\users\alex\documents\github\pegasus\.worktrees\deliv-054'
lease_phase: verifying
lease_heartbeat_at: '2026-09-08T14:17:21.139Z'
labels:
  - release
  - corrective
links: []
refs:
  - .agents/skills/pegasus-release/SKILL.md
  - docs/adr/0039-windows-and-linux-release-workstations.md
commits:
  - ca6ecb0253b0b5ed9884320e4883ceb9621829fe
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/703'
archived: false
created: '2026-09-08T13:25:29.904Z'
updated: '2026-09-08T14:25:48.558Z'
---

## What

Integrate the unique hidden-runtime-file ZIP correction from PR #676 on current dev, retaining portable release workstations and schema-3 artifacts.

## Why

The corrective-release plan D1 requires .azurefunctions at the Worker ZIP root. PR #676 remains conflicting and unbound; its ZipFile correction is absent from dev. [[DELIV-048]] is already Verifying with a retained historic worktree, so this bounded successor must not reopen or edit that workspace.

## Approach

- Reuse the .NET ZipFile approach from #676/80acaf56 in the existing release artifact builder.
- Preserve relative root layout and native bundle/OCI/manifest behavior; assert actual Worker hidden runtime content and applicable Playwright content.
- No Linux-only policy restoration, deployment, or historical-record rewrite.

## Verification

- [ ] One host verifier checks ZIP entries and existing platform/script contracts; no parallel build/test commands.
- [ ] Independent review, draft PR to dev, exact provenance to #676 and DELIV-048.

## Outcome
