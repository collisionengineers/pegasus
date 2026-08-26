---
id: UIIMP-002
type: ticket
title: Create throwaway HTML replicas of every Pegasus page
status: done
area: ui-improvement
assignee: codex-mcp-client
profile: chore
stageEntered:
  preparing: '2026-08-26T12:10:20.448Z'
  review: '2026-08-26T12:43:27.260Z'
  verifying: '2026-08-26T13:29:37.042Z'
  done: '2026-08-26T13:33:09.570Z'
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
  - 1cd0c4c12dbd8ba67d617612c036466b4f0b3070
  - b8d2ac45f454cd868b91065580094df96bf521ed
  - 6474c7fe487e130c2d66fbef01a288b4665ba251
  - 05e9e1e5cdb4daf4b18bca4e43d787c6405e8d69
  - 0140e236c9156cff16086f6a9e61311fe20f2463
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/556'
  - 'https://github.com/collisionengineers/pegasus/pull/557'
  - 'https://github.com/collisionengineers/pegasus/pull/558'
deployment: n/a
archived: false
created: '2026-08-26T12:09:14.815Z'
updated: '2026-08-26T13:34:17.224Z'
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

- [x] Every current routed page is represented and indexed.
- [x] Each page opens locally and its links and assets resolve.
- [x] Representative visual states are available without application dependencies.
- [x] No prototype is referenced by or published with the Web application.

## Outcome

Shipped an offline Test UI catalogue on `dev` through PR #556. It classifies all 52 current routed Razor sources and supplies 60 locally viewable, page-specific HTML states for the 39 visual routes. PRs #557 and #558 supplied the final fidelity corrections before the ticket PR merged. Exact merged verification at `0140e236` passed catalogue, documentation, Markdown placement, PowerShell parse, whitespace, runtime-isolation, locked restore, Release build, and representative browser-render checks. No deployment was required or performed.

Follow-ups remain [[UIIMP-001]] for the Live/Test local launcher and [[UIIMP-003]] for any explicitly approved reintegration into Live Razor pages.
