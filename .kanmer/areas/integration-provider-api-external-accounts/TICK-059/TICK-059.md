---
id: TICK-059
type: ticket
title: API-02 — Provider API receipt and processing-status lookup
status: backlog
area: integration-provider-api-external-accounts
assignee: ''
profile: feature
labels:
  - capability
  - API-02
  - next
  - post-alpha
  - blocked
  - requires-live-approval
links:
  - TICK-058
archived: false
created: '2026-08-12T15:05:19.443Z'
updated: '2026-08-12T15:09:19.976Z'
---

## What

Plan and research **API-02**: Provider API receipt and processing-status lookup

## Why

This is allocated to **Next / 0.4.0** in `docs/capabilities.md`. It is **not designated until post-alpha** and is blocked from implementation pending its activation decision and evidence.

## Approach

- At activation, define the Core policy owner, caller, contract, failure behavior, and acceptance evidence.
- Re-check the exact activation boundary in `docs/capabilities.md`; allocation alone is not implementation or deployment.

## Verification

- [ ] A task-level plan covers the capability's exact contract and tests.
- [ ] All activation conditions are accepted before implementation starts.

## Notes

- Source: `docs/capabilities.md` — API-02.
- Blocked by: [[TICK-058]] — Submission-status lookup depends on the principal-scoped submission contract.
