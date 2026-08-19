---
id: TICK-093
type: ticket
title: >-
  ENG-01 — One canonical repair specification with route provenance for Glass's,
  Audatex PDF, or an approved AI proposal
status: implementing
area: engineering-assessment
order: 50
assignee: codex-mcp-client
profile: feature
stageEntered:
  preparing: '2026-08-19T09:05:03.087Z'
  review: '2026-08-19T11:05:25.746Z'
  implementing: '2026-08-19T11:17:31.308Z'
taken_at: '2026-08-19T09:48:52.496Z'
branch: task/tick-093-versioned-repair-spec
worktree: ../pegasus-worktrees/tick-093-versioned-repair-spec
labels:
  - capability
  - ENG-01
  - later
  - post-alpha
groups:
  - EPIC-003
  - EPIC-004
links:
  - SIMPLI-014
  - TICK-205
  - TICK-098
blocks:
  - TICK-096
  - TICK-097
  - TICK-098
  - TICK-100
  - TICK-081
  - TICK-092
refs:
  - docs/frd/frd-06-vehicle-and-engineering-evidence.md
commits:
  - 7c238f52
  - 3b44bd67
  - 405dc0e4
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/420'
deployment: not-deployed
archived: false
created: '2026-08-12T15:06:02.568Z'
updated: '2026-08-19T11:17:31.308Z'
---

## Outcome

Implemented the activated ENG-01 contract as one Core-owned immutable repair-specification aggregate. Ordinary assessment has one accepted ordinary role; Audit can retain independent conservative and maximised accepted roles. Each version records ordered existing estimate lines, source route/artifact/version/hash, accepted calculation inputs, Engineer acceptance, correction lineage, and exact-version identity.

Legacy estimate lines migrate to explicit `LegacyUnresolved` drafts without fabricated source or acceptance. Accepted versions are corrected by reasoned supersession, never overwritten. The current assessment surface remains compatible through the ordinary draft.

PR: https://github.com/collisionengineers/pegasus/pull/420

## Boundaries

- [[TICK-092]] owns exact accepted-version projection into the render snapshot.
- [[TICK-098]] remains Audit-presentation deferred pending an approved representative template.
- [[SIMPLI-014]] owns report rendering.
- No Audit wording/uplift, provider integration, cloud write, or `main` update is included.

## Verification

Locked restore and Release build pass. Core Assessment is 41/41, final focused SQL schema/lifecycle/migration is 8/8, architecture is 97/97, and merged-state Core is 639/639. The local full integration run produced no failures before its proportional 25-minute ceiling; isolated PR CI is the authoritative completion gate.

## Governing document

`docs/frd/frd-06-vehicle-and-engineering-evidence.md`
