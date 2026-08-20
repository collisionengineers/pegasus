---
id: TICK-201
type: ticket
title: Correct canonical documentation claims against source evidence
status: backlog
area: delivery-repository
assignee: ''
profile: feature
labels:
  - now
  - source-now
links: []
docs_todo: true
archived: false
created: '2026-08-12T15:08:05.001Z'
updated: '2026-08-20T04:00:18.278Z'
---

## What

Audit canonical documentation claims against their authoritative sources and correct statements that are unsupported, stale, contradictory, or assigned to the wrong owner.

## Why

Repository documentation distinguishes allocation, implementation, caller proof, deployment, live verification, and acceptance. Claim-level drift can overstate delivery or create competing policy owners even when links and formatting remain valid.

## Approach

- Inventory material claims in each canonical document and identify its authority owner from `docs/index.md`.
- Check claims against code callers, tests, ADRs, current operations evidence, and operator truth at the required evidence tier.
- Correct only the owning canonical document and replace duplicated policy with links where appropriate.
- Preserve every material statement in `docs/operator-notes.md`; stop for user resolution before changing its meaning.

## Verification

- [ ] Each reviewed material claim has an identified authority and supporting evidence tier.
- [ ] Unsupported or contradictory claims are corrected without inventing stronger evidence.
- [ ] Documentation links and repository documentation checks pass.
- [ ] The review records any unresolved operator decision rather than guessing.

## Notes

- Source: the retired pre-Kanmer tracker canonical-documentation accuracy audit.
- Documentation correctness does not itself prove application callers or live behavior.


## Tracker migration

Authority references were retargeted by [[KANMER-001]] after the legacy tracker was retired.
