---
id: TICK-001
type: ticket
title: Complete the QDOS alpha production release
status: backlog
area: platform-operations
assignee: ''
profile: feature
labels:
  - capability
  - OPS-10
  - now
  - requires-live-approval
groups:
  - HZN-003
links: []
archived: false
created: '2026-08-12T15:03:52.764Z'
updated: '2026-08-20T03:21:51.297Z'
---

## What

Complete the QDOS alpha production release — **re-scoped 2026-08-20**: the release-execution limbs of this ticket are satisfied (13 numbered releases have shipped with immutable manifests, digests, revisions, and migration transcripts — see `docs/operations.md` release table). What remains is acceptance, which is not agent-executable:

- [ ] Designated-operator acceptance of the QDOS production workflow against real end-to-end work.
- [ ] Explicit Collision Engineers management approval of production use (OPS-25; `docs/capabilities.md` records OPS-10 as "operator acceptance outstanding").

## Why

The capability inventory allocates OPS-10 to Now. The recover-the-manifest / assign-a-numbered-release limbs became moot once the numbered release process (release 8 onwards) was operating; keeping them open misstates the estate.

## Verification

- [ ] Operator acceptance recorded in `docs/operator-notes.md` or a linked decision.
- [ ] Management approval recorded with date and scope.
