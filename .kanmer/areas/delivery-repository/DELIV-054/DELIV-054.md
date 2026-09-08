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
labels:
  - release
  - corrective
links: []
refs:
  - .agents/skills/pegasus-release/SKILL.md
  - docs/adr/0039-windows-and-linux-release-workstations.md
commits:
  - 6509746913eda16d2c4440add20e7f6793500f0b
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/703'
deployment: not-deployed
delivery_state: integrated
delivery_branch: dev
delivery_sha: 6509746913eda16d2c4440add20e7f6793500f0b
delivery_recorded_at: '2026-09-08T14:27:34.008Z'
archived: false
created: '2026-09-08T13:25:29.904Z'
updated: '2026-09-08T14:29:27.266Z'
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

- [x] One host verifier checked ZIP entries and existing platform/script contracts.
- [x] Independent review and exact-merge verification passed.

## Outcome

PR #703 squash-merged into `dev` as
`6509746913eda16d2c4440add20e7f6793500f0b`; schema-2 proof records three
authoritative PASS attempts. The author commit
`ca6ecb0253b0b5ed9884320e4883ceb9621829fe` remains provenance, while the
merged SHA is the reachable integration record. This ticket is the bounded
successor to [[DELIV-048]] and reuses only PR #676's unique ZIP correction
(`80acaf56e65d45c53f46bda75924f1a5f0dd3ed2`); DELIV-048 and PR #676 were not
altered.

No deployment or actual release package occurred. D6 remains the boundary for
immutable release packaging and any release evidence; this Done result proves
the merged three-script correction only.
