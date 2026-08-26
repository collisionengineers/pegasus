---
id: UIIMP-001
type: ticket
title: Add Live UI and Test UI local-development modes
status: done
area: ui-improvement
assignee: codex-mcp-client
profile: chore
stageEntered:
  preparing: '2026-08-26T12:10:20.425Z'
  review: '2026-08-26T13:47:35.462Z'
  verifying: '2026-08-26T13:52:17.421Z'
  done: '2026-08-26T13:54:24.773Z'
labels:
  - ui
  - design
  - local-development
links: []
blocks: []
commits:
  - 4a157be28df58d96694583a44fa3f6099570e18f
  - 93060b619ca92c2f6b3675ddba025abb724c0aa1
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/559'
deployment: n/a
archived: false
created: '2026-08-26T12:09:14.790Z'
updated: '2026-08-26T13:56:25.307Z'
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

- [x] The default and `Live` selections start the existing Razor UI unchanged.
- [x] `Test` opens only the local prototype catalogue.
- [x] Build and publish outputs contain no Test UI files or routes.

## Outcome

Shipped through PR #559. The supported local launcher now exposes Live UI (the unchanged default Razor path) and an isolated Test UI catalogue mode. Focused validators and the Release build pass; Web/Worker publish output contains no Test UI files or catalogue markers. No deployment was required or performed.
