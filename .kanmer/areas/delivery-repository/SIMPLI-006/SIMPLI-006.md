---
id: SIMPLI-006
type: ticket
title: >-
  Adopt PRD/FRD/ADR taxonomy; move doc+ADR governance to AGENTS.md; modernize
  ADRs
status: done
area: delivery-repository
order: 260
assignee: claude-code
profile: custom
requires: {}
labels: []
groups:
  - EPIC-002
links:
  - SIMPLI-001
blocks: []
deployment: n/a
archived: false
created: '2026-08-13T12:12:48.821Z'
updated: '2026-08-26T14:34:42.972Z'
---

## What

Adopt a **PRD → FRD → ADR** documentation taxonomy, move all repository
documentation/ADR **governance** into `AGENTS.md`, retire `requirements.md`, and
modernize ADR practice (stable IDs, YAML frontmatter, one-decision-per-ADR,
supersede-not-renumber).

## Why

`requirements.md` conflates product/functional/roadmap content; several ADRs mix
technical decisions with feature behaviour; and documentation governance is
wrongly filed as ADRs (0010/0023). **Governance belongs in AGENTS.md; ADRs record
durable technical/architectural product decisions only.**

## Approach

Full decomposition (approved plan): 1 PRD + 12 FRDs; split the 8 mixed ADRs; move
0012/0020 → FRD; retire the 0010/0023 governance rules into AGENTS.md/index;
delete `requirements.md`. **Supersedes the renumber-to-9 approach in
`docs/temp-plans/simplify/adr-consolidation.md`.** Coordinated with [[SIMPLI-002]]
(AGENTS.md), [[SIMPLI-004]] (NOW.md), [[SIMPLI-005]] (cleanup). Sequenced as 5 PRs.

## Verification

- [ ] Taxonomy live; `requirements.md` retired with no lost normative rule or broken link
- [ ] No ADR encodes documentation/process rules; every ADR carries frontmatter + status
