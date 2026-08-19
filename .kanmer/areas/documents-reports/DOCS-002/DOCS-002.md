---
id: DOCS-002
type: ticket
title: Record the Web Container App as the integrated renderer execution boundary
status: preparing
area: documents-reports
assignee: ''
profile: chore
stageEntered:
  preparing: '2026-08-19T09:13:28.723Z'
labels:
  - now
  - renderer-integration
groups:
  - EPIC-004
links:
  - SIMPLI-014
  - PLAT-007
blocks:
  - TICK-215
docs_todo: true
archived: false
created: '2026-08-19T09:13:24.531Z'
updated: '2026-08-19T09:13:28.723Z'
---

## What

Write and link the thin ADR selecting the existing Pegasus Web Container App as the production Chromium/report-rendering execution boundary, with the existing Flex Consumption Worker unchanged and no separate renderer service/job.

## Why

TICK-215 research established a durable technical choice not fully decided by ADR-0015 or ADR-0025. Repository governance requires the choice to be recorded before implementation planning.

## Approach

- Allocate the next stable ADR id after verifying the index/frontmatter set.
- Record one decision only: in-process rendering in the existing Web Container App because it is the existing custom-container boundary capable of carrying pinned Chromium/native/font dependencies.
- Link ADR-0025 and FRD-11; record consequences, including synchronous/durable operation constraints and the separately approval-gated Azure proof in PLAT-007.
- Update the ADR index and link the new ADR to TICK-215/SIMPLI-014/PLAT-007 as appropriate.

## Verification

- [ ] ADR frontmatter/index are valid and use the next permanent id.
- [ ] The decision creates no new project, runtime, service, queue consumer, or deployment unit.
- [ ] TICK-215 can resume planning against the linked ADR.

## Outcome
