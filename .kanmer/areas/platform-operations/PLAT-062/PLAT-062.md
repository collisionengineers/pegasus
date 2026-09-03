---
id: PLAT-062
type: ticket
title: >-
  Add administrator-configurable instruction/image completeness and
  chase-interval settings
status: backlog
area: platform-operations
order: 830
assignee: ''
profile: feature
labels:
  - backend
  - administration
  - workflow
  - follow-up
groups:
  - EPIC-011
links:
  - PLAT-025
refs:
  - docs/frd/frd-01-case-identity-and-lifecycle.md
  - docs/frd/frd-12-operator-experience.md
docs_todo: true
archived: false
created: '2026-08-29T10:23:02.768Z'
updated: '2026-09-03T15:15:28.579Z'
---

## What

Add the remaining real Workflow configuration controls shown by EPIC-011: `Instruction document required`, `Eligible images required`, and the chase interval, alongside the already-backed review settings.

## Why

[[PLAT-025]] could port only the two existing review requirements. The other controls need a Core-owned configuration contract, persistence and migration rather than inert UI.

## Settled boundary

- The two completeness switches govern readiness for Engineer assignment/Review only where a route does not impose a stricter mandatory requirement.
- They never waive identity, Principal/reference allocation, Audit-original-report, processing-limit, custody or route-specific fail-closed requirements.
- The chase interval replaces the existing seven-calendar-day scheduling constant; default remains seven calendar days and Held work preserves the remaining interval.
- Existing Case evidence remains factual. Changing policy does not rewrite whether a Case actually has instructions or eligible images.

## Approach

- Update FRD-01/12 and capabilities before implementation leaves Backlog.
- Extend the existing workflow-configuration Core owner rather than creating a second settings service.
- Persist one versioned global policy and apply optimistic concurrency, Administrator authorization and permanent history.
- Extend `Pages/Administration/Configuration.*` through that port; add no explanatory copy.
- Reuse existing London-calendar and Held-interval behavior.

## Verification

- [ ] All five displayed controls read and write real versioned policy.
- [ ] Disabling a readiness requirement cannot bypass a stricter route/product invariant.
- [ ] Existing Cases are re-evaluated from their retained facts without facts being rewritten.
- [ ] Chase scheduling uses the configured calendar-day interval and preserves Held remainder.
- [ ] Unauthorized, stale and replayed updates fail safely.

## Outcome
