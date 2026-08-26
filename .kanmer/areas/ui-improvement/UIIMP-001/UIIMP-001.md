---
id: UIIMP-001
type: ticket
title: Add Live UI and Test UI local-development modes
status: implementing
area: ui-improvement
assignee: codex-mcp-client
profile: chore
stageEntered:
  preparing: '2026-08-26T12:10:20.425Z'
taken_at: '2026-08-26T13:34:55.762Z'
branch: task/uiimp-001-live-test-ui-modes
worktree: ../pegasus-worktrees/uiimp-001-live-test-ui-modes
labels:
  - ui
  - design
  - local-development
links: []
blocks: []
archived: false
created: '2026-08-26T12:09:14.790Z'
updated: '2026-08-26T13:34:55.762Z'
---

## What

Add explicit Live UI and Test UI choices to the supported local-development launcher.

## Why

Developers need a safe distinction between the real Razor Pages interface that can be deployed and a disposable visual-experiment surface. Live UI must remain the default and Test UI must never enter the Web runtime or deployment output.

## Approach

- Add an explicit `-UiMode Live|Test` launcher option, defaulting to `Live`.
- Keep Live on the existing `DevelopmentOffline` Razor startup path.
- Make Test open the isolated static catalogue without starting Pegasus, SQL, authentication, migrations, or external services.
- Fail clearly for an unsupported mode or missing catalogue.

## Verification

- [ ] The default and `Live` selections start the existing Razor UI unchanged.
- [ ] `Test` opens only the local prototype catalogue.
- [ ] Build and publish outputs contain no Test UI files or routes.

## Outcome
