---
id: UIIMP-006
type: ticket
title: Rewrite the design authority to the Integrated Operations Workspace
status: done
area: ui-improvement
assignee: claude-code
profile: chore
stageEntered:
  preparing: '2026-08-28T08:08:14.136Z'
  review: '2026-08-28T08:21:12.901Z'
  verifying: '2026-08-28T08:31:39.331Z'
  done: '2026-08-29T10:08:49.927Z'
taken_at: '2026-08-28T08:12:15.816Z'
branch: task/uiimp-006-design-authority
worktree: ../pegasus-worktrees/uiimp-006-design-authority
labels:
  - ui
  - design
  - docs
groups:
  - EPIC-011
links:
  - UIIMP-003
  - UIIMP-004
refs:
  - docs/frd/frd-12-operator-experience.md
commits:
  - b5cb2edd
  - 932e0e64
  - 9be74a48
  - 3614d63a
prs:
  - '587'
archived: false
created: '2026-08-28T08:05:30.031Z'
updated: '2026-08-29T10:08:49.927Z'
---

## What

Rewrite `docs/design/README.md` so it describes the approved Integrated Operations Workspace as the design system: shell anatomy (220px rail, dark utility bar, workspace-tab strip, 1580px centred content), nav order and labels (Work Centre / Inbox / Upload / Cases / Search / Operations / Administration), count sources and the absent-never-zero rule, the token table, the vendored Inter font (licence + SHA-256), the class vocabulary and state-chip tones, the Lucide icon set incl. the five added glyphs, the route map and 301 stubs, breakpoints (1360/1180/1100/980/900/760), the CSP rule with the utility classes that replace inline styles, the keyboard/dialog contract, the amended disabled-versus-absent rule (D7), the removed surfaces, and the prototype defects recorded as reviewed divergences.

Keep verbatim: Evidence discipline, Test UI, Voice/banned words, No explanatory copy and page economy, Accessibility, Change and verification rule. Fix the logo mapping (`logo_no_margin.png` is used by `_LayoutExternal`, not `_Layout`) and the four unplaced marks that are not on disk.

## Why

`docs/design/README.md` is the design authority; no UI ticket may leave backlog against a document that still describes the 236px rail and 1280px content.

## Owns

`docs/design/README.md` only. Docs-only PR to `dev`.

## Verification

- [x] Every section of the new contract (group `context.md` §1) has an owning heading in the README.
- [x] `scripts/Test-DocumentationLinks.ps1` passes.
