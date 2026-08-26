---
id: UIIMP-002
type: ticket
title: Create throwaway HTML replicas of every Pegasus page
status: review
area: ui-improvement
assignee: codex-mcp-client
profile: chore
stageEntered:
  preparing: '2026-08-26T12:10:20.448Z'
  review: '2026-08-26T12:43:27.260Z'
taken_at: '2026-08-26T12:17:30.378Z'
branch: task/uiimp-002-test-ui
worktree: ../pegasus-worktrees/uiimp-002-test-ui
labels:
  - ui
  - design
  - prototype
links: []
blocks:
  - UIIMP-003
  - UIIMP-001
commits:
  - 63ce6901e9979cf5922be2ce4b361310230e62ef
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/556'
deployment: n/a
archived: false
created: '2026-08-26T12:09:14.815Z'
updated: '2026-08-26T12:43:27.260Z'
---

## What

Create double-clickable HTML/CSS replicas of every current routed Pegasus Razor page, with the meaningful visual states needed for throwaway UI experiments.

## Why

UI changes need a fast disposable surface that looks like Pegasus without requiring the application, database, authentication, or services to run. The catalogue is design material, not a second application implementation.

## Approach

- Reuse the real page structure, class names, `site.css`, assets, and approved design-system patterns.
- Cover populated, empty, validation, unavailable, and failure states where each is meaningful.
- Use only existing approved repository fixtures or evidence-safe values already established by tests; do not invent domain emails, images, documents, or work instructions.
- Keep the catalogue isolated from `Pegasus.Web`, application policy, and deployment.

## Verification

- [ ] Every current routed page is represented and indexed.
- [ ] Each page opens locally and its links and assets resolve.
- [ ] Representative visual states are available without application dependencies.
- [ ] No prototype is referenced by or published with the Web application.

## Outcome
