---
id: KANMER-011
type: ticket
title: Fix broken greenfield.md link in kanmer-setup SKILL.md
status: done
area: kanmer-meta
order: 590
assignee: claude-code
profile: fix
stageEntered:
  preparing: '2026-09-03T19:24:12.022Z'
  review: '2026-09-03T19:26:04.843Z'
  verifying: '2026-09-03T19:28:10.765Z'
  done: '2026-09-03T19:31:13.441Z'
taken_at: '2026-09-03T19:24:13.971Z'
branch: task/kanmer-011-skill-link
worktree: ../pegasus-worktrees/kanmer-011-skill-link
claim_expires_at: '2026-09-03T19:56:47.471Z'
claim_controller: claude-code
lease_id: 9a5af218-c9d3-4a92-ab82-cf94b11f4c5e
lease_revision: 2
lease_workspace: >-
  worktree:c:\users\alex\documents\github\pegasus-worktrees\kanmer-011-skill-link
lease_phase: review
lease_heartbeat_at: '2026-09-03T19:26:47.471Z'
labels:
  - documentation
  - ci-red
  - review-follow-up
links:
  - ENG-035
commits:
  - f89330698a1fb18b7d82031a8198e2f1b6b3ec33
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/652'
archived: false
created: '2026-09-03T15:15:14.652Z'
updated: '2026-09-03T19:31:13.441Z'
---

## What

`.opencode/skills/kanmer-setup/SKILL.md` line 169 links
`[`docs/manual/greenfield.md`](../../../../docs/manual/greenfield.md)`, which
does not resolve to any file in the repository. The `documentation` CI check
fails on this broken relative link.

Repair the link (point it at the correct existing path, or remove/replace the
reference if no such manual page exists) so the `documentation` check passes.

## Why

Flagged during ENG-035's PR #648 review (2026-09-03). Pre-existing on `dev`:
introduced by commit `c5c7a874` ("chore(kanmer): add OpenCode skills and
localize provider config"), already an ancestor of `origin/dev`. Not touched
or introduced by ENG-035 (`git log --oneline origin/dev..HEAD -- .opencode/skills/kanmer-setup/SKILL.md`
is empty on ENG-035's branch) and outside ENG-035's owned paths, so ENG-035
does not fix it — see ENG-035's plan review-disposition section.

This is a one-line documentation link repair; it blocks nothing else and
should be quick to land on `dev` directly.

## Verification

- [ ] The `documentation` CI check passes on a branch carrying only this fix.
- [ ] The corrected link resolves to a real file (or the dead reference is
      removed).
