---
id: DELIV-051
type: ticket
title: Revamp repository documentation and agent context
status: review
area: delivery-repository
assignee: codex-mcp-client
profile: fix
stageEntered:
  preparing: '2026-09-08T08:16:37.944Z'
  review: '2026-09-08T08:26:45.656Z'
  implementing: '2026-09-08T08:53:07.562Z'
taken_at: '2026-09-08T08:16:57.989Z'
branch: DELIV-051-instructions
worktree: .worktrees/deliv-051
claim_expires_at: '2026-09-08T12:45:14.731Z'
claim_controller: codex-mcp-client
review_round: 1
lease_id: 4f76801a-7137-4e01-aa89-a8454709c1fc
lease_revision: 7
lease_workspace: 'worktree:c:\users\alex\documents\github\pegasus\.worktrees\deliv-051'
lease_phase: review
lease_heartbeat_at: '2026-09-08T12:15:14.731Z'
labels:
  - documentation
  - kanmer
  - governance
links:
  - DELIV-052
commits:
  - d1854b4730615fae51fdc0fd2f1b8233eba5d4fa
  - af1625fae8ac8018054c95e988907f6c44fa4639
  - d90820295be68b7632012568879555f21fed5dcc
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/702'
deployment: n/a
archived: false
created: '2026-09-08T08:15:33.723Z'
updated: '2026-09-08T12:15:14.731Z'
---

## What

Revamp repository documentation and agent context so any working agent receives concise, current, coherent instructions with one clear owner per concern.

1. Continue streamlining AGENTS.md from PR #702 / commit d1854b4730615fae51fdc0fd2f1b8233eba5d4fa.
2. Audit the full repository documentation, reconcile contradictions and competing ownership, remove stale or unnecessary instructions, and introduce documentation where required.
3. Ensure tests still pass and record exact commands, results and failures.
4. Confirm the resulting context neither competes with Kanmer nor duplicates its procedures, and introduces no instruction or behavioral regressions.

## Why

The operator explicitly expanded this ticket beyond AGENTS.md cleanup to improve context for all working agents. The earlier narrow scope, mirror-preservation requirement and completed-review handoff are superseded.

## Scope

AGENTS.md, docs/**, CONTEXT.md and README.md. New documentation is permitted where necessary; use the repository's governing-document placement rules, and surface a concrete conflict rather than inventing a parallel authority. Preserve protected operator business meaning and real release/platform requirements.

Include the operator's worktree edits: remove CHANGELOG.md (Git history is sufficient), remove duplicated Kanmer skills from .agents/skills, remove the redundant .codex/skills/pegasus-release entrypoint, and preserve supplied skill additions copied from Claude. Retain unique project skills in .agents/skills. Installed Kanmer plugin is the Codex source for Kanmer procedures; do not restore the removed mirror merely to satisfy drift reporting.

Observed additions are .agents/skills/pegasus-release/SKILL (2).md and .agents/skills/pegasus-wipe-intake-data/SKILL (2).md. They coexist with canonical SKILL.md files and need reconciliation during the expanded audit; they are not proof of newly discoverable skill entrypoints. Retained pegasus-release/SKILL.md explicitly supports Windows x64 and Linux x64 PowerShell 7 and platform-specific migration bundles.

## Acceptance

- Inventory documentation/context owners and disposition contradictions, duplicate instructions, obsolete text and references to removed files.
- Make navigation and authority consistent across AGENTS.md, docs/**, CONTEXT.md and README.md.
- Preserve unique current project/product/safety rules; no speculative compatibility or duplicate Kanmer lifecycle.
- Reconcile copied skills to discoverable canonical entrypoints without losing needed capabilities, including Linux release support.
- Run relevant documentation/skill checks and required regression tests for the completed scope; retain earlier cancelled-build evidence and do not claim stale results cover the new head.
- Independently review the completed current head for instruction conflicts and regressions before integration.

## Current state

Implemented the amended full documentation plan and all fourteen operator answers at d90820295be68b7632012568879555f21fed5dcc, pushed to PR #702 on DELIV-051-instructions. All new skills vetoed; useful procedures remain in documentation and existing owners. All supplied worktree changes, including vendor relocations, are committed. Documentation placement/link/catalogue/classifier regressions passed; no compiled code or renderer asset changed. Current-head independent review and mergeability comment are the final requested actions. [[DELIV-052]] remains separate. No merge, deployment, data wipe or Done transition is authorized by this task.

## Outcome
