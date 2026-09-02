---
id: TICK-053
type: ticket
title: >-
  MAIL-11 — Browse, search, and view mailbox messages and conversation threads
  in the app, including read-only search of accepted D…
status: done
area: mail-communications
order: 500
assignee: codex-mcp-client
profile: feature
stageEntered:
  preparing: '2026-08-19T08:10:45.571Z'
  review: '2026-08-20T11:11:50.033Z'
  verifying: '2026-08-20T14:15:45.734Z'
  done: '2026-08-21T15:12:28.788Z'
labels:
  - capability
  - MAIL-11
  - next
  - requires-live-approval
groups:
  - EPIC-003
  - EPIC-006
links: []
blocks:
  - TICK-056
  - TICK-057
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
commits:
  - 2d7c1421fcc4ff864cd9e114c904a3181c3fb4b9
  - 72f55b8a551336ffd46b8536aafdc334c1854f26
  - 93c069579ca3437e7560336e6c5d53b59402790c
  - 347f5ce741e19e6973a31655cd433f5c452005b0
  - 8b300043182ab14e8716323f6fa6f800bc2ba782
  - c0fa9a9905f2808ec1e2eb03e42dbe29cfde7ae4
  - 6e52935e9065f0769c2629015202909186f5625c
  - 7932d683782669e112f3d996c6914323e8ba72d4
  - fc6840361c1c19ece9a75d7ea68c713c75d01b75
  - eaf2f9f4eac577242ed301dd917f0682d4a77729
  - 6aaf2418c30defc1fb21111a10b954e70f74eea3
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/469'
deployment: production
archived: false
created: '2026-08-12T15:05:19.296Z'
updated: '2026-09-01T14:44:32.110Z'
---

## What

Implement **MAIL-11**: Browse, search, and view mailbox messages and conversation threads in the app, including read-only search of accepted Deleted Items.

## Why

This remains allocated to **Next / 0.3.0** in `docs/capabilities.md`. On 2026-08-20 the operator instructed “Implement the plan”, activating the planned local browse/search UI. That instruction does not authorize deployment, Graph permission changes, live mailbox writes, or claim live-mailbox/manual visual acceptance.

## Approach

- Reuse the existing retained-mail query/read model and canonical intake reader projection.
- Keep Deleted Items as an explicit bounded GET-only read of exact approved mailbox scopes, with no persistence, backfill, reconstruction, or mailbox mutation.
- Keep production composition, failure behavior, exact attachment identity, and acceptance evidence explicit.

## Verification

- [x] A task-level plan covers the capability's exact contract and tests.
- [x] The local implementation activation is recorded in canonical design/capability owners.
- [x] Original review blockers [[PR-015]] through [[PR-022]] have implementations and PIRs in Review.
- [x] Follow-up blockers [[PR-024]], [[PR-025]], and [[PR-029]] through [[PR-037]] have implementations and PIRs in Review; [[PR-018]]'s exact attachment identity is completed by [[PR-034]], and [[PR-037]] completes [[PR-033]]'s malformed envelope cases.

## Notes

- Source: `docs/capabilities.md` — MAIL-11.
- Deployment and live-mailbox evidence remain separately allocated.
