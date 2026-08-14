---
id: TICK-201
type: ticket
title: Correct canonical documentation claims against source evidence
status: todo
area: delivery-build-ci-documentation-governance
priority: medium
assignee: ''
labels:
  - now
  - source-now
links: []
archived: false
created: '2026-08-12T15:08:05.001Z'
updated: '2026-08-13T14:40:07.690Z'
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

- Source: `NOW.md` canonical-documentation accuracy audit.
- Documentation correctness does not itself prove application callers or live behavior.
